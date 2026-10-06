# Registro de cambios (bitácora)

Formato: fecha — cambio — motivo.

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
