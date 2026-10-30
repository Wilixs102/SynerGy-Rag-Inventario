# Correspondencia con el entregable del 30/10/2026

| Criterio | Evidencia | Alcance y pendiente |
|---|---|---|
| Implementación modular | API, Application, Infrastructure; solución .NET 10 | Domain reservado para evolución |
| Modelo final | Qwen2.5-Coder 7B y nomic-embed-text; `resultados/consolidacion/entorno.json` | Modelos preentrenados locales; no hubo entrenamiento de pesos |
| Mejoras | Recuperación vectorial de contexto y actas, resolución de custodios, reglas de laptops y kits de mouse | Cuenta cada kit teclado-mouse como un mouse |
| Pruebas finales | 95 pruebas; 30 escenarios SQL adversariales; ocho comprobaciones funcionales | No equivalen a cobertura exhaustiva ni certificación de seguridad |
| README y ejecución | README, global.json, packages.lock.json, requirements.txt | Requiere configurar SQL Server 2025 y Ollama para el pipeline real |
| Demo | `scripts/demo_visual.py`, `data/demo/` y capturas en `docs/assets/` | Demo visual explícitamente sintética; no evalúa el modelo |
| Comparación | 270 generaciones históricas; nueve generaciones actuales | Acierto histórico por componentes, no exactitud de ejecución final |
| Robustez | Pruebas de parser, ambigüedad, ausencia de persona y rechazo de totales vectoriales | Falta evaluación ciega ampliada y estudio con usuarios |
| Documento final: avance | `output/pdf/Avance_Consolidado_SynerGy_RAG.pdf` | Sigue la estructura del informe facilitado; tutor no informado |
| Ética y organización | SECURITY.md, metodología y riesgos del PDF | Roles/autorización y auditoría requeridos antes de despliegue multiusuario |
| Anexos técnicos | PDF, `docs/informe.pdf`, resultados y scripts | Anexo previo conservado como histórico |

No se presenta como cumplida la meta de 120 preguntas independientes, fidelidad 0,90, recall@5 0,85 o reducción del tiempo humano del 40 %. Estas requieren un protocolo adicional y usuarios autorizados. Los ocho casos reales se publican únicamente como indicadores y latencias, sin respuestas corporativas.
