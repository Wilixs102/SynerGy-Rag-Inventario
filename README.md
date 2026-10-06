# SynerGy-Rag-Inventario

Asistente de inteligencia artificial que responde preguntas en español sobre el **inventario de activos informáticos** de SynerGy. Usa un enfoque **RAG de tipo text-to-SQL**: un LLM local (Ollama) genera una consulta `SELECT` a partir de la pregunta, un validador la restringe a una lista blanca y se ejecuta sobre SQL Server.

```
Pregunta -> [recuperación de pistas de dominio] -> prompt -> Ollama (qwen2.5-coder:7b)
         -> SQL -> SqlValidator (solo SELECT sobre VistaEquipo) -> SQL Server -> respuesta
```

## Arquitectura (Clean Architecture, .NET 10)

| Proyecto | Responsabilidad |
|---|---|
| `SynerGy-Rag-Inventario` (API) | Controllers (`POST /api/preguntas`, `GET /api/diagnostico/sql`) y front estático (`wwwroot/index.html`) |
| `SynerGy.RAG.Application` | Interfaces, DTOs y casos de uso (`RagService`, `Prompts`, `ConocimientoDominio`) |
| `SynerGy.RAG.Infrastructure` | `OllamaClient`, `SqlQueryService`, `SqlValidator` |
| `SynerGy.RAG.Domain` | Reservado para entidades |
| `tests/SynerGy.RAG.Evaluacion` | Harness de evaluación (baseline vs ajustado) |
| `scripts/analizar_resultados.py` | Análisis estadístico de los resultados |

## Dependencias y versiones

| Componente | Versión |
|---|---|
| .NET SDK | 10.0.x |
| Microsoft.Data.SqlClient | 7.1.1 |
| Microsoft.Extensions.Options | 10.0.0 |
| Microsoft.AspNetCore.OpenApi | 10.0.12 |
| Ollama | última estable |
| Modelo | `qwen2.5-coder:7b` (~4.7 GB) |
| SQL Server | acceso de **solo lectura** a la base `ActivosInformaticos` |
| Python (solo análisis) | 3.10+ con `numpy` y `matplotlib` |

## Instrucciones de ejecución

1. Instalar .NET 10 SDK y Ollama (`winget install Ollama.Ollama`), luego `ollama pull qwen2.5-coder:7b`.
2. Configurar la cadena de conexión **sin subirla al repositorio**:
   ```powershell
   cd SynerGy-Rag-Inventario
   dotnet user-secrets set "ConnectionStrings:ActivosInformaticos" "Server=<host>\INSTBDD02,<puerto>;Database=ActivosInformaticos;User Id=<usuario>;Password=<clave>;Encrypt=True;TrustServerCertificate=True;"
   ```
   (La máquina debe tener red/VPN hacia el servidor SQL.)
3. Ejecutar la API y abrir `http://localhost:5225`:
   ```powershell
   dotnet run --launch-profile http
   ```
4. Probar por API: `POST /api/preguntas` con `{"pregunta": "¿Cuántas laptops hay asignadas?"}`.

Configuración de Ollama en `appsettings.json` (sección `Ollama`): `BaseUrl`, `Modelo`, `Temperature`, `TimeoutSegundos`.

## Evaluación y reproducibilidad

El harness genera SQL con tres configuraciones (30 preguntas x 3 repeticiones) y **no necesita acceso a la base de datos**, solo Ollama:

```powershell
dotnet run --project tests/SynerGy.RAG.Evaluacion -- --reps 3 --salida resultados/resultados.csv
python scripts/analizar_resultados.py resultados/resultados.csv
```

| Configuración | Prompt | Temperatura |
|---|---|---|
| `baseline` | esquema + estados | 0.3 |
| `prompt_enriquecido` | reglas + few-shot + pistas recuperadas | 0.3 |
| `ajustado` (final) | reglas + few-shot + pistas recuperadas | 0.1 |

Métrica principal: **exactitud por componentes** (el SQL contiene los filtros/agregaciones esperados y ninguno de los prohibidos). Detalles y resultados en `resultados/` y en el informe (`docs/informe.pdf`).

## Seguridad

- El LLM solo puede consultar la vista `VistaEquipo`; `SqlValidator` rechaza todo lo que no sea un único `SELECT`.
- Ningún secreto va en el repositorio: usar user-secrets o variables de entorno (`ConnectionStrings__ActivosInformaticos`).
- La contraseña original de `rag_reader` aparece en el primer commit del historial: **debe rotarse**.

## Bitácora

Ver [CHANGELOG.md](CHANGELOG.md).
