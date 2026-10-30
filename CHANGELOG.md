# Registro de cambios (bitácora)

Formato: fecha — cambio — motivo.

## 2026-10-29 — Consolidación de entrega académica v0.2.0
- 95 pruebas superadas, incluidos 30 casos SQL adversariales; ocho comprobaciones funcionales y nueve generaciones actuales documentadas.
- README completo, datos ficticios, demo visual identificada, matriz de entrega y avance del documento final en PDF con anexos.
- SDK y dependencias bloqueados, CI de compilación y pruebas, auditoría NuGet sin vulnerabilidades conocidas en la consulta del corte.
- Actualización de System.Security.Cryptography.Pkcs a 10.0.13; exclusión de secretos y seudonimización de dos identidades históricas.
- Distinción entre resultados históricos, verificaciones actuales y metas de evaluación pendientes; no se declara entrenamiento de pesos.

## 2026-10-29 — Recuperación vectorial de contexto del inventario
- Corrección de plurales confundidos con custodios; filtros de laptops sin cargadores/bases/soportes y criterio confirmado de un mouse por kit teclado-mouse.
- Consultas controladas para listas y totales habituales, con procedencia explícita; el evaluador del LLM conserva su ruta propia.
- Búsqueda vectorial de texto de Acta, limpieza de HTML e identificadores conocidos, versión independiente actas-v1 y filtros previos de tipo/persona. ActaDetalle no se usa para contar el inventario actual.
- 65 pruebas automatizadas superadas tras estas correcciones.
- Verificación real posterior: catálogos inventario-v2 (28 entradas) y actas-v1 (257 actas) indexados; conteos y listas coinciden con SQL de referencia. Consultas habituales comprobadas entre 0,25 y 4,2 s; búsqueda de actas de devolución y por persona comprobada. Esto no es una medición global ni una mejora de latencia del LLM.
- Interfaz corporativa local con logo oficial de SynerGy, diseño adaptable, estados de carga y cancelación, resultados y evidencia desplegable; vista de enmascaramiento visual e impresión para anexos.
- Resolución de custodios por nombre/correo con consultas parametrizadas a VistaColaboradorInv; aclaración ante nombres ambiguos o ausentes y selección en el front.
- Validación sintáctica con ScriptDom, lista de fuentes y funciones permitidas; consultas por persona ejecutadas sobre una fuente temporal restringida al correo confirmado y estado asignado.
- Lectura acotada a 101 filas para indicar truncamiento a 100; 43 pruebas automatizadas superadas (recuperación, nombres, aclaraciones y seguridad).
- Verificación posterior de integración real: catálogo de 28 entradas en SQL Server 2025, recuperación de cinco fragmentos y respuesta coincidente con una consulta de referencia. API y Ollama locales; generación en CPU, aproximadamente 95 s. No equivale a una evaluación de exactitud global.
- Catálogo versionado de 28 entradas, cliente `/api/embed` de Ollama y validación de embeddings de 768 dimensiones.
- Tabla auxiliar y búsqueda exacta por distancia coseno en SQL Server 2025; SQL fijo parametrizado.
- Indexación por comando, con credencial separada y reemplazo transaccional del modelo/versión.
- Contexto recuperado incorporado al prompt y expuesto en JSON y front; activación por configuración.
- Pruebas automatizadas sin servicios externos y configuración `vectorial` en el harness comparativo.
- Guía de despliegue y límites de validación. No se aplicó DDL ni se indexó el catálogo en un servidor real.

## 2026-10-06 — Ajustes del prototipo y evaluación
- **Prompt ajustado** (`Prompts.Ajustado`): reglas de dominio, 5 ejemplos few-shot y diccionario de estados. *Motivo:* el baseline filtraba el tipo de equipo por `NombreModelo` (código del fabricante) en lugar de `NombreCategoria`/`Descripcion`.
- **Recuperación de pistas de dominio** (`ConocimientoDominio`): detecta palabras clave (laptop, celular, monitor…) y añade al prompt el filtro correcto. *Motivo:* las categorías del inventario están fragmentadas (p. ej. `COMPUTADOR/Laptop`, `LAPTOP/LAPTOP INCOOP`).
- **Preprocesamiento de la pregunta**: normalización (minúsculas, sin tildes) antes de recuperar pistas.
- **Hiperparámetro**: temperatura 0.3 (baseline) → 0.1 (final) para respuestas más estables.
- **Harness de evaluación** (`tests/SynerGy.RAG.Evaluacion`) con 30 preguntas, 3 configuraciones y 3 repeticiones; análisis estadístico en `scripts/analizar_resultados.py`.
- `SqlQueryService`: las columnas sin nombre (p. ej. `COUNT(*)`) reciben `columnaN` para mostrarse en el front.
- `RagService` recibe `RagOptions` (estrategia y temperatura) y expone `GenerarSqlAsync`.
- README y CHANGELOG actualizados.

## 2026-10-06 — Prototipo baseline
- Cliente Ollama (`OllamaClient`) y configuración `Ollama`.
- `RagService` con prompt baseline y endpoint `POST /api/preguntas`.
- `SqlValidator`: solo un `SELECT` sobre lista blanca (`VistaEquipo`), sin `;` ni comentarios.
- Front mínimo en `wwwroot/index.html`.
- Cadena de conexión movida a user-secrets (se retiró de `appsettings.json`).

## Anterior
- Estructura de la solución (API, Application, Domain, Infrastructure) y conexión a SQL Server (`DiagnosticoController`).
