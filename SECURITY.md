# Seguridad y límites de operación

El prototipo se ejecuta en localhost para evaluación académica. Antes de desplegarlo para usuarios se requieren autenticación, autorización por custodio/rol, auditoría y límites de frecuencia. La selección de un custodio en la interfaz es un filtro de consulta, no una autorización.

Las credenciales se configuran mediante User Secrets o variables de entorno. No deben incorporarse a appsettings, capturas, incidencias ni resultados. La cuenta de consulta debe tener únicamente SELECT sobre las vistas aprobadas. La cuenta de indexación debe ser distinta y limitar sus escrituras a la tabla auxiliar vectorial.

El validador analiza el árbol sintáctico SQL y restringe sentencias, fuentes y funciones. Es una defensa adicional: no sustituye los permisos mínimos de SQL Server. Hay 30 casos adversariales explícitos en `tests/SynerGy.RAG.Tests/RobustezEntregaTests.cs`.

La publicación actual seudonimiza dos identidades del conjunto histórico y excluye registros operacionales y secretos locales. El historial inicial del repositorio incluyó una credencial de demostración: debe tratarse como comprometida y rotarse si aún existe o se reutilizó. Limpiar el árbol actual no elimina versiones anteriores. No se reescribió el historial compartido.

El enmascaramiento de campos conocidos de actas reduce exposición, pero el texto libre requiere revisión adicional antes de usarlo fuera del entorno autorizado. Los vectores y las bases corporativas no se publican. Las imágenes de resultados del PDF usan datos ficticios identificados como tales.

Las dependencias tienen archivos de bloqueo y auditoría NuGet. Un resultado sin vulnerabilidades conocidas corresponde al momento de la consulta y debe repetirse al actualizar dependencias.
