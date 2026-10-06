| Configuración | Exactitud | IC 95% | DE entre reps | SQL válida | Latencia mediana (ms) |
|---|---|---|---|---|---|
| baseline | 55.6% | [37.8%, 73.3%] | 0.019 | 98.9% | 3070 |
| prompt_enriquecido | 93.3% | [83.3%, 100.0%] | 0.000 | 100.0% | 3514 |
| ajustado | 96.7% | [90.0%, 100.0%] | 0.000 | 100.0% | 3452 |

| Comparación | Δ exactitud | IC 95% Δ | Wilcoxon p | McNemar (+/−) | McNemar p |
|---|---|---|---|---|---|
| baseline vs prompt_enriquecido | +37.8% | [+21.1%, +55.6%] | 0.0006 | 11/0 | 0.0010 |
| baseline vs ajustado | +41.1% | [+24.4%, +57.8%] | 0.0003 | 12/0 | 0.0005 |