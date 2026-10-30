# Interfaz de consultas y capturas para anexos

La pantalla utiliza el logo del sitio oficial de SynerGy, almacenado localmente,
y un diseño adaptable a escritorio y móvil. No carga fuentes, analítica ni recursos
gráficos de terceros al consultar el inventario.

## Uso

1. Abrir la API local en `http://localhost:5225`.
2. Escribir una pregunta o usar uno de los ejemplos. Los ejemplos completan el texto;
   no ejecutan la consulta automáticamente.
3. Pulsar **Consultar inventario** o **Ctrl + Enter**. Se muestra el tiempo transcurrido
   durante la espera. **Cancelar** interrumpe la solicitud del navegador.
4. Revisar la tabla, las aclaraciones de custodio y los indicadores de filas, tiempo y
   fragmentos recuperados. El estado de la consulta refleja la respuesta real de la API.
5. Desplegar **Consulta SQL generada** y **Contexto recuperado por búsqueda vectorial**
   para inspeccionar la evidencia. El SQL puede copiarse; la distancia coseno no se
   presenta como porcentaje de confianza.

## Capturas para la tesis

- Activar **Vista para anexos** después de obtener el resultado. Oculta visualmente la
  pregunta y enmascara nombres de candidatos, correos, series y otros identificadores
  habituales en la tabla. Los literales del SQL también se enmascaran.
- Revisar la captura antes de compartirla: es una presentación visual, no una
  anonimización de la respuesta de la API ni un control de acceso. Campos con alias
  inesperados podrían requerir revisión adicional.
- La vista para anexos deshabilita nuevas consultas y selección de personas hasta
  desactivarla. No cambia los datos ni la consulta ejecutada.
- **Imprimir vista** prepara los resultados y la evidencia para imprimir o guardar
  como PDF mediante el diálogo del navegador, manteniendo el enmascaramiento activo.
- Para mostrar la trazabilidad, abrir los paneles de SQL y contexto antes de capturar.
  No presentar una captura aislada como prueba de exactitud global del sistema.

## Archivos

- `SynerGy-Rag-Inventario/wwwroot/index.html`: estructura y elementos accesibles.
- `SynerGy-Rag-Inventario/wwwroot/styles.css`: identidad visual, adaptación e impresión.
- `SynerGy-Rag-Inventario/wwwroot/app.js`: estados, resultados, aclaraciones y enmascaramiento.
- `SynerGy-Rag-Inventario/wwwroot/assets/README.md`: procedencia del logotipo oficial.
