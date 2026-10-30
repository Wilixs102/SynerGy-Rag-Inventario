# Inventario actual y actas históricas

## Reglas corregidas del inventario

- **Laptops:** categoría LAPTOP, o categoría COMPUTADOR con descripción que contiene
  Laptop. Cargadores, bases y soportes quedan fuera aunque mencionen laptop.
- **Mouses:** categoría MOUSE, o categoría PERIFÉRICO con Mouse en la descripción.
  El usuario confirmó que cada registro de kit teclado-mouse cuenta como un mouse.
- Se cuenta `COUNT(DISTINCT IdEquipo)` sobre VistaEquipo. Las actas y sus detalles no
  se utilizan para contar el stock actual, pues un mismo activo puede aparecer en
  varios documentos históricos.
- Los plurales laptops/mouses/ratones se reconocen como equipos, no como nombres de
  colaboradores. Los filtros de custodio se siguen resolviendo con el directorio.

Las preguntas habituales de listas y cantidades usan consultas controladas y verificadas,
además de la recuperación vectorial del catálogo. La API indica
`metodoGeneracion=consulta_validada` y la interfaz identifica esa procedencia. No se
atribuye esa consulta al modelo de lenguaje. Si hay filtros no soportados por la
plantilla (marca, fechas, negación, agrupaciones), se conserva el generador con las
reglas corregidas y el validador sintáctico. El harness de generación sigue evaluando
el LLM, sin activar automáticamente las consultas controladas.

Ejemplos:

- `Dame el total de las laptops`.
- `¿Cuántos mouses hay?`.
- `Dame los equipos de laptops`.
- `Lista las 5 laptops disponibles`.
- `¿Cuántas laptops tiene Nombre Apellido?`.

Las listas muestran hasta 100 registros e indican truncamiento; no confundir ese
límite visual con el total. Los conteos no se limitan a las filas mostradas.

## Búsqueda vectorial en Acta

Se usa el texto existente en SQL: TextoActaEntrega, TextoIntroductorio y, cuando
tengan contenido, TerminosCondiciones, MotivoDevolucion y Observaciones. No se descargan
ni procesan documentos binarios. El HTML se convierte a texto y se reemplazan los
identificadores conocidos del registro, correos y números largos antes de generar
embeddings locales. Este tratamiento reduce datos personales, pero no constituye una
garantía universal de anonimización de texto libre.

Los vectores se almacenan en dbo.RagContextoInventario dentro de la base auxiliar,
con versión **actas-v1**, independiente de **inventario-v2**. El comando reemplaza
atómicamente esa versión y reutiliza embeddings de textos normalizados idénticos.
No escribe en Acta ni ActaDetalle. No trunca silenciosamente textos: si uno supera
4000 caracteres se requiere segmentarlo antes de indexar.

```powershell
dotnet run --project SynerGy-Rag-Inventario --launch-profile http -- --indexar-catalogo
dotnet run --project SynerGy-Rag-Inventario --launch-profile http -- --indexar-actas
dotnet user-secrets set --project SynerGy-Rag-Inventario "BusquedaVectorial:ActasHabilitadas" "true"
```

Reiniciar la API después de cambiar configuración. La cuenta de indexación de la base
auxiliar se configura en ConnectionStrings:ContextoVectorialIndexacion. La conexión del
inventario requiere lectura de Acta y TipoActa; para filtros de persona también del
directorio autorizado. El modelo y sus pesos deben coincidir al indexar y buscar.

Ejemplos desde la misma pantalla:

- `Busca actas sobre la responsabilidad de custodia y cuidado de equipos`.
- `Busca actas de devolución sobre el cuidado de los equipos`.
- `Busca actas de entrega para Nombre Apellido`.

Las consultas se dirigen al buscador de actas cuando contienen la palabra acta/actas.
Se filtran actas activas por tipo explícito (entrega/devolución) y por custodio resuelto
antes de seleccionar las coincidencias vectoriales. Se devuelven número, tipo y fecha
del acta y los fragmentos relacionados con distancia coseno. No se habilita Acta como
fuente libre para el SQL generado por el LLM.

Un top-K semántico no es un total exacto ni evidencia de asignación actual. Por eso las
preguntas de total de actas solicitan aclaración. El buscador no implementa todavía
filtros exactos por fecha o número de acta. La autenticación y permisos por usuario
siguen siendo necesarios antes de un despliegue productivo.

## Evidencia y evaluación

Separar las métricas del generador, de las consultas controladas y de la recuperación
de actas. Los resultados históricos del modelo no describen automáticamente estas
nuevas rutas. Para actas, etiquetar coincidencias esperadas y medir relevancia/Recall@K,
además de verificar los filtros. Para stock, contrastar resultados contra consultas
SQL revisadas, incluyendo accesorios y kits como casos de regresión.
