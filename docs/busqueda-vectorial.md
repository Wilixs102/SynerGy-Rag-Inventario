# Recuperación vectorial del contexto del inventario

## Qué implementa

Pregunta -> embedding local -> búsqueda exacta por distancia coseno en SQL Server 2025 ->
contexto seleccionado + prompt -> Ollama genera T-SQL -> validador existente -> VistaEquipo.

Se recuperan reglas, significados de columnas, estados, categorías y ejemplos SQL. No se
indexan documentos ni filas de activos. Los conteos y sumas siguen consultando toda la
vista con los filtros generados. La búsqueda usa `VECTOR_DISTANCE`, no necesita índices
vectoriales aproximados ni habilitar funciones preview. Para este catálogo pequeño se
prefiere búsqueda exacta y orden estable por distancia e ID.

`data/catalogo-inventario.json` contiene 28 entradas iniciales derivadas del prototipo.
Es una propuesta a revisar con el responsable del inventario, no un catálogo aprobado.
Los IDs son estables; el contenido de cada entrada se incrusta junto con su tipo.
El catálogo no contiene credenciales ni registros personales de activos.

## 1. Preparar SQL Server y el modelo

1. Confirmar SQL Server 2025 (17.x): `SELECT @@VERSION;`.
2. El DBA ejecuta `docs/sql/crear_contexto_vectorial.sql` en una base auxiliar autorizada.
   También puede usar la base del inventario si la organización lo aprueba.
3. Asignar `SELECT` en `dbo.RagContextoInventario` a la cuenta lectora de contexto.
4. Asignar `SELECT, INSERT, DELETE` en esa tabla a una cuenta de indexación separada.
   La API nunca necesita permisos de escritura. La indexación no crea tablas.
5. Descargar el modelo local: `ollama pull nomic-embed-text`.

La implementación requiere embeddings de **768 dimensiones**, finitos y no nulos.
No se recortan ni rellenan vectores; un modelo incompatible produce error.
Usar el mismo modelo/pesos para indexar y consultar. Registrar `ollama --version` y
`ollama list` con el identificador del modelo como evidencia. Al cambiar los pesos,
cambiar `VersionCatalogo` y reindexar antes de activar esa versión.

## 2. Configurar e indexar

Desde la raíz del repositorio, configurar secretos de desarrollo; sustituir los valores
de ejemplo por cadenas autorizadas. No guardar contraseñas en Git:

```powershell
dotnet user-secrets set --project SynerGy-Rag-Inventario "ConnectionStrings:ActivosInformaticos" "<cadena de lectura del inventario>"
dotnet user-secrets set --project SynerGy-Rag-Inventario "ConnectionStrings:ContextoVectorial" "<cadena de lectura de la base auxiliar>"
dotnet user-secrets set --project SynerGy-Rag-Inventario "ConnectionStrings:ContextoVectorialIndexacion" "<cadena del indexador de la base auxiliar>"
dotnet run --project SynerGy-Rag-Inventario --launch-profile http -- --indexar-catalogo
```

El proceso carga la copia de `data/catalogo-inventario.json` distribuida con la API,
genera todos los embeddings y reemplaza **solo el modelo y versión configurados** en
una transacción. Las otras versiones permanecen. Si falla Ollama no modifica SQL;
si falla la escritura, la transacción se revierte. Repetir el comando no duplica IDs.
No hay un endpoint HTTP de indexación.

En producción usar variables de entorno o el gestor de secretos de la organización.
El indexador debe ejecutarse en un proceso con su credencial propia; no distribuir la
credencial de indexación al proceso habitual de la API.

## 3. Activar y probar

```powershell
dotnet user-secrets set --project SynerGy-Rag-Inventario "BusquedaVectorial:Habilitada" "true"
dotnet run --project SynerGy-Rag-Inventario --launch-profile http
```

La configuración predeterminada es `nomic-embed-text`, `inventario-v2`, `TopK=5` y
`DistanciaMaxima=0.65`. Una distancia menor significa mayor similitud; el umbral es
inicial y debe calibrarse. Solo se incluyen coincidencias bajo el umbral. Si no hay
coincidencias, se conserva el prompt con sus reglas y pistas léxicas. Si la tabla, versión,
modelo o servicio no están disponibles, la petición falla sin ejecutar SQL del inventario;
no se oculta el fallo mediante un cambio silencioso a búsqueda léxica.

`ConnectionStrings:ContextoVectorial` puede apuntar a otro SQL Server 2025. Si está
vacía, se utiliza la cadena de lectura del inventario. La cadena de indexación es
obligatoria y nunca usa esa sustitución.

Abrir `http://localhost:5225` y preguntar: `¿Cuántas compus portátiles están disponibles?`.
La respuesta JSON conserva sus campos anteriores y agrega `contexto`, con ID, tipo,
contenido y distancia. El front permite desplegar los fragmentos usados.

Verificación de la carga con una cuenta autorizada:

```sql
SELECT Modelo, VersionCatalogo, COUNT(*) AS entradas
FROM dbo.RagContextoInventario
GROUP BY Modelo, VersionCatalogo;
```

Para desactivar la recuperación: establecer `BusquedaVectorial:Habilitada=false`.
El flujo anterior sigue disponible. No afirmar que hubo búsqueda vectorial si `contexto`
está vacío sin comprobar la configuración y el umbral.

## 4. Verificación y evaluación comparativa

```powershell
dotnet test SynerGy-Rag-Inventario.slnx
```

Las pruebas automatizadas usan dobles de Ollama y del almacén: verifican el contrato
de embeddings, dimensiones, cancelación, indexación, incorporación del contexto al
prompt, evidencia en la respuesta y que no se ejecute SQL tras fallos o rechazos.
No demuestran que tu instalación real de SQL Server u Ollama esté operativa.

El harness admite `vectorial` manteniendo las tres configuraciones anteriores por
defecto. La comparación mantiene igual modelo generador, prompt y temperatura, y añade
la recuperación únicamente a la configuración vectorial:

```powershell
# Configurar en esta sesión con los valores autorizados; no publicar la cadena.
$env:ConnectionStrings__ContextoVectorial = "<cadena de lectura de la base auxiliar>"
dotnet run --project tests/SynerGy.RAG.Evaluacion -- --configs ajustado,vectorial --reps 3 --salida resultados/comparacion-vectorial.csv
```

Si cambias modelo, versión, K o umbral, exportar también
`BusquedaVectorial__ModeloEmbedding`, `BusquedaVectorial__VersionCatalogo`,
`BusquedaVectorial__TopK` y `BusquedaVectorial__DistanciaMaxima` para que coincidan con
la API. El harness usa variables de entorno, no los user-secrets de la API.

Esta evaluación consulta la tabla auxiliar, pero **no ejecuta el SQL generado sobre
VistaEquipo**. `latencia_ms` incluye recuperación y generación en el caso vectorial;
no es latencia de extremo a extremo. La métrica sigue siendo exactitud por componentes.
Para el análisis estadístico con el script existente, incluir también `baseline`:

```powershell
dotnet run --project tests/SynerGy.RAG.Evaluacion -- --configs baseline,ajustado,vectorial --reps 3 --salida resultados/vectorial/resultados.csv
python scripts/analizar_resultados.py resultados/vectorial/resultados.csv
```

No sobrescribir los resultados históricos ni presentar el 96,7 % anterior como resultado
del nuevo flujo. Reservar preguntas independientes/paráfrasis fuera de las usadas para
escribir el catálogo. Etiquetar qué IDs son relevantes por pregunta y medir Recall@K/MRR,
además de exactitud de ejecución y latencia total con una réplica autorizada.

## Límites pendientes

El validador ahora analiza T-SQL con ScriptDom: exige una sentencia SELECT, valida
todas las fuentes y restringe funciones, variables e indicaciones de tabla. La tabla
auxiliar y el directorio no se agregan a la lista blanca del SQL generado. El lector
se detiene en 101 registros y devuelve hasta 100 con un indicador de truncamiento.
La autenticación y los permisos por usuario siguen pendientes antes de uso productivo.

## Consultas por compañero

Se reconocen expresiones como `equipos asignados a Nombre Apellido`, `equipos de
Nombre Apellido` y `qué equipos tiene Nombre Apellido`, además de correos explícitos.
La resolución usa consultas parametrizadas contra `dbo.VistaColaboradorInv` y únicamente
selecciona `NombreCompleto` y `CorreoInstitucional`. El nombre se busca por palabras,
sin distinguir tildes ni mayúsculas. No se infiere el correo a partir del nombre.

Con una coincidencia se incorpora el correo verificado al prompt. Con varias se muestran
hasta seis candidatos para seleccionar; con ninguna se solicita nombre completo o correo.
Esas aclaraciones no llaman al LLM ni consultan equipos. La identificación sigue siendo
un conjunto de patrones de lenguaje habituales, no un reconocimiento universal de personas.
Si una frase no se entiende, reformular con `equipos asignados a Nombre Apellido`.

La consulta de una persona se ejecuta sobre una tabla temporal por conexión, poblada
exclusivamente con sus activos en estado asignado mediante un parámetro de correo.
Se reescriben las referencias de tabla del AST para que los filtros adicionales del modelo
operen sobre ese subconjunto. No se modifica el inventario ni se permite que el LLM
consulte el directorio directamente. El SQL mostrado es la propuesta del modelo; la
restricción parametrizada de la fuente se aplica internamente al ejecutarlo.

La cuenta de la API necesita lectura de esas dos columnas del directorio, además de
su acceso a VistaEquipo y al catálogo auxiliar. Esta resolución identifica al custodio
de la consulta; no sustituye la autorización del usuario que usa la aplicación.

La creación de la tabla y la carga se ejecutan explícitamente, no durante el arranque
habitual de la API. El 29 de octubre de 2026 se comprobó una instalación real con SQL
Server 2025: 28 entradas indexadas, cinco fragmentos recuperados y una pregunta de conteo
cuyo resultado coincidió con el SQL de referencia. La petición tomó aproximadamente
95 segundos con Ollama en CPU. Es una prueba de integración, no una evaluación global
de exactitud ni evidencia del objetivo de latencia del proyecto. Las conexiones están
en secretos locales; no se incluyen credenciales ni resultados del inventario en esta guía.

## Referencias técnicas

- [Ollama: API de embeddings](https://docs.ollama.com/api/embed).
- [Microsoft: VECTOR_DISTANCE](https://learn.microsoft.com/en-us/sql/t-sql/functions/vector-distance-transact-sql?view=sql-server-ver17).
