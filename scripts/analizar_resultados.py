"""Análisis estadístico de resultados/resultados.csv (generado por el proyecto de evaluación).

Uso: python scripts/analizar_resultados.py [resultados/resultados.csv]
Salidas: resultados/resumen.json, resultados/resumen.md, resultados/grafico.png
Dependencias: numpy, matplotlib
"""
import csv
import json
import sys
from math import comb
from pathlib import Path

import numpy as np

RNG = np.random.default_rng(42)
N_BOOT = 10_000
BASE = "baseline"
FINAL = "ajustado"


def cargar(ruta):
    filas = list(csv.DictReader(open(ruta, encoding="utf-8-sig", newline="")))
    for f in filas:
        f["id_pregunta"] = int(f["id_pregunta"])
        f["rep"] = int(f["rep"])
        for k in ("sql_valida", "componentes_ok", "latencia_ms"):
            f[k] = float(f[k])
    return filas


def matriz(filas, config, campo):
    """Matriz [pregunta, repetición] del campo indicado."""
    sel = [f for f in filas if f["config"] == config]
    ids = sorted({f["id_pregunta"] for f in sel})
    reps = sorted({f["rep"] for f in sel})
    m = np.full((len(ids), len(reps)), np.nan)
    for f in sel:
        m[ids.index(f["id_pregunta"]), reps.index(f["rep"])] = f[campo]
    return m


def ic_bootstrap(por_pregunta):
    """IC 95% percentil, remuestreando preguntas (la unidad independiente)."""
    n = len(por_pregunta)
    medias = np.array([por_pregunta[RNG.integers(0, n, n)].mean() for _ in range(N_BOOT)])
    return float(np.percentile(medias, 2.5)), float(np.percentile(medias, 97.5))


def ic_bootstrap_dif(a, b):
    n = len(a)
    d = a - b
    medias = np.array([d[RNG.integers(0, n, n)].mean() for _ in range(N_BOOT)])
    return float(np.percentile(medias, 2.5)), float(np.percentile(medias, 97.5))


def mcnemar_exacto(b, c):
    """p-valor bilateral exacto (binomial) sobre pares discordantes."""
    n = b + c
    if n == 0:
        return 1.0
    k = min(b, c)
    p = 2 * sum(comb(n, i) for i in range(k + 1)) / 2**n
    return min(1.0, p)


def wilcoxon_permutacion(d, n_perm=100_000):
    """Wilcoxon de rangos con signo (bilateral) por permutación de signos."""
    d = d[d != 0]
    if len(d) == 0:
        return 1.0
    rangos = np.argsort(np.argsort(np.abs(d))) + 1.0
    # promedio de rangos en empates
    for v in np.unique(np.abs(d)):
        idx = np.abs(d) == v
        rangos[idx] = rangos[idx].mean()
    obs = abs(np.sum(rangos * np.sign(d)))
    signos = RNG.choice([-1, 1], size=(n_perm, len(d)))
    sim = np.abs((signos * rangos).sum(axis=1))
    return float((np.sum(sim >= obs - 1e-9) + 1) / (n_perm + 1))


def resumen_config(filas, config):
    ok = matriz(filas, config, "componentes_ok")
    val = matriz(filas, config, "sql_valida")
    lat = matriz(filas, config, "latencia_ms")
    por_q = ok.mean(axis=1)
    por_rep = ok.mean(axis=0)
    ic = ic_bootstrap(por_q)
    consistente = float(np.mean([(r.min() == r.max()) for r in ok]))
    return {
        "n_preguntas": int(ok.shape[0]),
        "n_repeticiones": int(ok.shape[1]),
        "exactitud_media": float(ok.mean()),
        "exactitud_ic95": ic,
        "exactitud_por_repeticion": [float(x) for x in por_rep],
        "desviacion_entre_repeticiones": float(por_rep.std(ddof=1)) if len(por_rep) > 1 else 0.0,
        "sql_valida_media": float(val.mean()),
        "latencia_mediana_ms": float(np.median(lat)),
        "latencia_p25_p75_ms": [float(np.percentile(lat, 25)), float(np.percentile(lat, 75))],
        "consistencia_por_pregunta": consistente,
        "preguntas_siempre_fallidas": int(np.sum(ok.max(axis=1) == 0)),
        "por_pregunta": por_q.tolist(),
        "mayoria": (ok.mean(axis=1) >= 0.5).tolist(),
    }


def comparar(res, a, b):
    qa, qb = np.array(res[a]["por_pregunta"]), np.array(res[b]["por_pregunta"])
    d = qb - qa
    ma, mb = np.array(res[a]["mayoria"]), np.array(res[b]["mayoria"])
    sube = int(np.sum(~ma & mb))
    baja = int(np.sum(ma & ~mb))
    return {
        "dif_media": float(d.mean()),
        "dif_ic95": ic_bootstrap_dif(qb, qa),
        "wilcoxon_p": wilcoxon_permutacion(d),
        "mcnemar_mejoran": sube,
        "mcnemar_empeoran": baja,
        "mcnemar_p": mcnemar_exacto(sube, baja),
    }


def main():
    ruta = Path(sys.argv[1] if len(sys.argv) > 1 else "resultados/resultados.csv")
    filas = cargar(ruta)
    configs = list(dict.fromkeys(f["config"] for f in filas))
    res = {c: resumen_config(filas, c) for c in configs}
    comparaciones = {
        f"{BASE} vs {c}": comparar(res, BASE, c) for c in configs if c != BASE
    }
    salida = ruta.parent
    json.dump({"configs": res, "comparaciones": comparaciones},
              open(salida / "resumen.json", "w", encoding="utf-8"), indent=2, ensure_ascii=False)

    lineas = ["| Configuración | Exactitud | IC 95% | DE entre reps | SQL válida | Latencia mediana (ms) |",
              "|---|---|---|---|---|---|"]
    for c in configs:
        r = res[c]
        lineas.append(
            f"| {c} | {r['exactitud_media']:.1%} | [{r['exactitud_ic95'][0]:.1%}, {r['exactitud_ic95'][1]:.1%}] "
            f"| {r['desviacion_entre_repeticiones']:.3f} | {r['sql_valida_media']:.1%} | {r['latencia_mediana_ms']:.0f} |")
    lineas += ["", "| Comparación | Δ exactitud | IC 95% Δ | Wilcoxon p | McNemar (+/−) | McNemar p |", "|---|---|---|---|---|---|"]
    for k, v in comparaciones.items():
        lineas.append(
            f"| {k} | {v['dif_media']:+.1%} | [{v['dif_ic95'][0]:+.1%}, {v['dif_ic95'][1]:+.1%}] "
            f"| {v['wilcoxon_p']:.4f} | {v['mcnemar_mejoran']}/{v['mcnemar_empeoran']} | {v['mcnemar_p']:.4f} |")
    (salida / "resumen.md").write_text("\n".join(lineas), encoding="utf-8")
    print("\n".join(lineas))

    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    fig, ax = plt.subplots(figsize=(6, 3.4))
    medias = [res[c]["exactitud_media"] * 100 for c in configs]
    err = [[(res[c]["exactitud_media"] - res[c]["exactitud_ic95"][0]) * 100 for c in configs],
           [(res[c]["exactitud_ic95"][1] - res[c]["exactitud_media"]) * 100 for c in configs]]
    ax.bar(configs, medias, yerr=err, capsize=5, color=["#9ca3af", "#60a5fa", "#2563eb"][: len(configs)])
    for i, m in enumerate(medias):
        ax.text(i, m + 3, f"{m:.0f}%", ha="center", fontsize=9)
    ax.set_ylabel("Exactitud (%)")
    ax.set_ylim(0, 115)
    ax.set_title("Exactitud por configuración (IC 95% bootstrap)", fontsize=10)
    fig.tight_layout()
    fig.savefig(salida / "grafico.png", dpi=200)


if __name__ == "__main__":
    main()
