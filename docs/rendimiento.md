# Optimización local de consultas - 04/11/2026

Las solicitudes habituales de laptops/mouses y las listas o conteos simples de un compañero identificado se resuelven con SQL controlado antes de recuperar contexto vectorial. La resolución del nombre y el ejecutor SQL restringido al correo confirmado siguen activos. Las consultas con filtros adicionales que la plantilla no comprende se remiten al generador completo.

Se reconocen las expresiones «qué equipos tiene», «qué equipos tiene asignado» y «qué equipos tiene asignados». Una identidad ambigua continúa solicitando aclaración. Las preguntas sobre actas mantienen su búsqueda vectorial y las preguntas generales mantienen la recuperación de contexto.

## SQL reutilizado y datos actuales

La API dispone de una caché de SQL validado de cinco minutos, limitada a 128 entradas en memoria. La clave depende del prompt completo (incluido contexto y custodio confirmado) y de la temperatura. No contiene resultados del inventario. Cada solicitud vuelve a validar la sentencia y ejecutarla en SQL Server. El campo `sqlReutilizado` permite distinguir una generación nueva de una reutilización. La caché pertenece a esta instancia y configuración del modelo; reiniciar al cambiar de modelo. El harness de evaluación no utiliza esta caché ni las plantillas de la API.

`Rag:CacheSqlSegundos` controla la vigencia; 0 desactiva la caché y se admite hasta 3600 segundos. No se almacenan SQL rechazados. Un cambio en el contexto recuperado cambia la clave. Las peticiones simultáneas que aún no tienen entrada pueden generar por separado.

`Ollama:KeepAlive` se establece en `15m` para reducir recargas tras pausas cortas. Este parámetro mantiene el modelo cargado y consume memoria durante ese período; no acelera por sí mismo el procesamiento de cada token. Fuente: [API de generación de Ollama](https://docs.ollama.com/api/generate).

## Comprobación local

| Escenario | Antes | Después | Observación |
|---|---:|---:|---|
| Equipos asignados a un compañero concreto | 98,843 s | 2,924 s y 1,020 s | Dos formulaciones equivalentes; mismos identificadores de equipos |
| Total de laptops, segundo intento | 0,207 s | 0,086 s | SQL controlado sin embeddings |
| Total de mouses, segundo intento | 0,213 s | 0,120 s | SQL controlado sin embeddings |
| Total general, segundo intento | 2,920 s | 0,235 s | Reutilización de SQL; nueva ejecución de datos |

Las filas de resultados coincidieron en los casos comparados. La primera generación del total general siguió tardando 17,648 s después del cambio: una pregunta nueva que necesita el modelo de 7B en CPU puede seguir tardando. No se atribuye la diferencia entre primeras generaciones (23,598 s frente a 17,648 s) a una mejora causal porque no se controlaron calentamiento, orden ni carga.

Estas mediciones son secuenciales y pequeñas. No son una prueba de carga, un p95 ni una estimación global. Los valores completos y los indicadores de coincidencia sin datos corporativos están en [la evidencia del 04/11](../resultados/rendimiento-2026-11-04.json). Los resultados académicos del 29/10 se conservan como histórico.

Se superaron 110 pruebas automáticas, incluidas regresiones que comprueban datos frescos, expiración, límites de caché, revalidación, cambios de contexto, aislamiento de la evaluación, filtros adicionales y ejecución restringida por compañero. La compilación final de la API no emitió errores ni advertencias.
