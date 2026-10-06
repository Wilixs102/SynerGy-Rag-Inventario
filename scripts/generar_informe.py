"""Genera docs/informe.pdf (2-3 páginas) a partir de resultados/resumen.json.
Dependencias: reportlab. Uso: python scripts/generar_informe.py"""
import json
import subprocess
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_JUSTIFY
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import cm
from reportlab.platypus import Image, Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle

raiz = Path(__file__).resolve().parent.parent
datos = json.load(open(raiz / "resultados" / "resumen.json", encoding="utf-8"))
cfg, comp = datos["configs"], datos["comparaciones"]
b, e, a = cfg["baseline"], cfg["prompt_enriquecido"], cfg["ajustado"]
cb = comp["baseline vs ajustado"]
ce = comp["baseline vs prompt_enriquecido"]


def pct(x):
    return f"{x * 100:.1f}%"


def ic(r):
    return f"[{r['exactitud_ic95'][0] * 100:.0f}%, {r['exactitud_ic95'][1] * 100:.0f}%]"


log = subprocess.run(["git", "log", "--oneline", "--reverse"], cwd=raiz, capture_output=True,
                     text=True, encoding="utf-8").stdout.strip().splitlines()

ss = getSampleStyleSheet()
base = ParagraphStyle("b", parent=ss["BodyText"], fontSize=9, leading=11.5, alignment=TA_JUSTIFY, spaceAfter=3)
h1 = ParagraphStyle("h1", parent=ss["Heading1"], fontSize=14, spaceAfter=4)
h2 = ParagraphStyle("h2", parent=ss["Heading2"], fontSize=10.5, spaceBefore=6, spaceAfter=3,
                    textColor=colors.HexColor("#1d4ed8"))
peq = ParagraphStyle("p", parent=base, fontSize=7.5, leading=9, alignment=0)


def P(t, s=base):
    return Paragraph(t, s)


def tabla(filas, anchos):
    t = Table([[P(str(c), peq) for c in f] for f in filas], colWidths=anchos)
    t.setStyle(TableStyle([("GRID", (0, 0), (-1, -1), 0.4, colors.grey),
                           ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#e5e7eb")),
                           ("VALIGN", (0, 0), (-1, -1), "TOP"),
                           ("TOPPADDING", (0, 0), (-1, -1), 2), ("BOTTOMPADDING", (0, 0), (-1, -1), 2)]))
    return t


s = []
s.append(P("Asistente RAG de Inventario (SynerGy): ajustes, validación estadística y repositorio", h1))
s.append(P("Repositorio: <b>https://github.com/Wilixs102/SynerGy-Rag-Inventario</b> &nbsp;|&nbsp; "
           "Modelo: qwen2.5-coder:7b (Ollama) &nbsp;|&nbsp; .NET 10 / SQL Server", peq))

s.append(P("1. Ajustes realizados al prototipo", h2))
s.append(P(
    "El prototipo convierte preguntas en español sobre el inventario de activos informáticos en consultas T-SQL "
    "(text-to-SQL con un LLM local) y las ejecuta sobre la vista <i>VistaEquipo</i>. El <b>baseline</b> usaba un prompt "
    "simple (esquema + códigos de estado). Su error típico fue filtrar el tipo de equipo con <i>NombreModelo</i> (el código "
    "del fabricante) en lugar de <i>NombreCategoria/Descripcion</i>, por lo que contaba mal o devolvía cero filas. "
    "Además, las categorías están fragmentadas (p. ej. «COMPUTADOR/Laptop» y «LAPTOP/LAPTOP INCOOP»)."))
s.append(tabla([
    ["Ajuste", "Por qué fue necesario", "Mejora esperada"],
    ["Ingeniería del prompt: reglas de dominio y 5 ejemplos few-shot (ninguno coincide con las preguntas de evaluación)",
     "El modelo no sabía qué columna identifica el tipo de equipo ni la semántica de los estados",
     "Mayor exactitud semántica"],
    ["Recuperación de pistas de dominio (palabras clave -> filtro correcto) y normalización de la pregunta "
     "(minúsculas, sin tildes)",
     "Sinónimos (portátil, celular, teléfono) y categorías duplicadas en la base", "Menos errores por vocabulario"],
    ["Hiperparámetro: temperatura 0.3 -> 0.1", "Reducir la aleatoriedad del muestreo",
     "Mayor estabilidad entre repeticiones"],
], [7.2 * cm, 6.2 * cm, 3.6 * cm]))

s.append(P("2. Reporte de análisis estadístico", h2))
s.append(P(
    "<b>Diseño.</b> 30 preguntas (conteos por estado/tipo/marca, sumas, rankings, fechas, factura y custodio), cada una con "
    "criterios verificables: expresiones regulares requeridas y prohibidas sobre el SQL generado. Cada configuración se "
    "ejecutó <b>3 veces</b> por pregunta (270 generaciones) para medir la variabilidad estocástica del LLM; como no hay "
    "entrenamiento, se usa <b>validación por repeticiones</b> en lugar de validación cruzada. La unidad independiente es "
    "la pregunta, no el intento."))
s.append(P(
    "<b>Métricas.</b> Exactitud por componentes (proporción de intentos cuyo SQL cumple todos los criterios), SQL válida "
    "(pasa el validador de seguridad), latencia, desviación estándar entre repeticiones y consistencia por pregunta. "
    "<b>Incertidumbre:</b> IC 95% por bootstrap sobre preguntas (10 000 remuestreos). <b>Comparación pareada</b> "
    "baseline vs ajustado: prueba de Wilcoxon de rangos con signo (permutación de signos, 100 000) sobre la exactitud por "
    "pregunta y prueba de McNemar exacta sobre el resultado por mayoría."))
s.append(tabla([
    ["Configuración", "Exactitud", "IC 95%", "DE entre reps", "SQL válida", "Latencia mediana"],
    ["baseline (prompt simple, T=0.3)", pct(b["exactitud_media"]), ic(b),
     f"{b['desviacion_entre_repeticiones']:.3f}", pct(b["sql_valida_media"]), f"{b['latencia_mediana_ms']:.0f} ms"],
    ["prompt enriquecido (T=0.3)", pct(e["exactitud_media"]), ic(e),
     f"{e['desviacion_entre_repeticiones']:.3f}", pct(e["sql_valida_media"]), f"{e['latencia_mediana_ms']:.0f} ms"],
    ["<b>ajustado final (T=0.1)</b>", f"<b>{pct(a['exactitud_media'])}</b>", ic(a),
     f"{a['desviacion_entre_repeticiones']:.3f}", pct(a["sql_valida_media"]), f"{a['latencia_mediana_ms']:.0f} ms"],
], [5.2 * cm, 2.0 * cm, 2.6 * cm, 2.3 * cm, 2.1 * cm, 2.8 * cm]))
s.append(Spacer(1, 4))


def fila_comp(nombre, c):
    return [nombre, f"{c['dif_media'] * 100:+.1f} pp",
            f"[{c['dif_ic95'][0] * 100:+.1f}, {c['dif_ic95'][1] * 100:+.1f}] pp", f"{c['wilcoxon_p']:.4f}",
            f"{c['mcnemar_mejoran']}/{c['mcnemar_empeoran']}", f"{c['mcnemar_p']:.4f}"]


s.append(tabla([
    ["Comparación con baseline", "Diferencia", "IC 95% de la diferencia", "Wilcoxon p",
     "McNemar (mejoran/empeoran)", "McNemar p"],
    fila_comp("prompt enriquecido", ce),
    fila_comp("ajustado final", cb),
], [3.6 * cm, 2.2 * cm, 3.6 * cm, 2.1 * cm, 3.8 * cm, 1.9 * cm]))
s.append(Spacer(1, 4))
s.append(Image(str(raiz / "resultados" / "grafico.png"), width=9.2 * cm, height=5.2 * cm))

s.append(P(
    f"<b>Interpretación.</b> El ajuste elevó la exactitud de {pct(b['exactitud_media'])} a {pct(a['exactitud_media'])} "
    f"(+{cb['dif_media'] * 100:.1f} puntos porcentuales). La mejora es estadísticamente significativa "
    f"(Wilcoxon p={cb['wilcoxon_p']:.4f}; McNemar: {cb['mcnemar_mejoran']} preguntas mejoran y "
    f"{cb['mcnemar_empeoran']} empeoran) y el IC 95% de la diferencia no incluye 0. "
    f"<b>Variabilidad:</b> el baseline fue inestable (13 preguntas fallaron siempre y una de forma intermitente); el "
    f"prompt enriquecido y el ajustado dieron el mismo resultado en las 3 repeticiones (DE = 0), de modo que su "
    f"incertidumbre proviene del muestreo de preguntas y no del LLM. La diferencia entre <i>prompt enriquecido</i> y "
    f"<i>ajustado</i> (93.3% vs 96.7%) corresponde a una sola pregunta y <b>no es concluyente</b>: el efecto principal "
    f"se debe al prompt, no a la temperatura. La latencia no cambió de forma relevante (~3 s)."))
s.append(P(
    "<b>Limitaciones.</b> (i) 30 preguntas dan ICs amplios; (ii) la exactitud se mide por componentes del SQL (proxy) y "
    "no por igualdad de resultados ejecutados contra la base; (iii) las preguntas y criterios fueron escritos por los "
    "autores. El único fallo del modelo final (¿Qué marca tiene más equipos?) usó <i>LIMIT 1</i>, sintaxis inválida en "
    "SQL Server; el validador no detecta dialectos, por lo que «SQL válida» es una métrica de seguridad y no de "
    "ejecutabilidad."))

s.append(P("3. Consolidación y documentación del repositorio", h2))
s.append(P(
    "El repositorio incluye el código final (API .NET 10 con Clean Architecture, cliente Ollama, <i>RagService</i>, "
    "validador SQL y front mínimo), el harness de evaluación (<i>tests/SynerGy.RAG.Evaluacion</i>), los resultados "
    "crudos y resumidos (<i>resultados/</i>), el script de análisis (<i>scripts/analizar_resultados.py</i>), el "
    "<b>README</b> (descripción, ejecución, dependencias y versiones) y el <b>CHANGELOG</b> (bitácora). Evidencia de "
    "control de versiones (<i>git log</i>):"))
for linea in log:
    s.append(P("• " + linea.replace("&", "&amp;").replace("<", "&lt;"), peq))

s.append(P("4. Síntesis reflexiva", h2))
s.append(P(
    "<b>Decisiones técnicas relevantes.</b> (1) Text-to-SQL sobre una única vista en lugar de embeddings: los datos son "
    "estructurados y el problema principal era mapear lenguaje a columnas. (2) LLM local (Ollama) para no enviar datos "
    "del inventario a terceros. (3) Lista blanca de objetos y validador que solo admite un <i>SELECT</i>. "
    "(4) Evaluación repetida con estadística pareada, porque el modelo es estocástico y una sola corrida podría engañar."))
s.append(tabla([
    ["Riesgo técnico", "Mitigación"],
    ["El SQL generado por el LLM podría modificar datos o leer tablas sensibles (p. ej. Colaborador)",
     "SqlValidator: un único SELECT, sin ; ni comentarios, palabras reservadas bloqueadas y lista blanca "
     "(VistaEquipo); usuario de BD de solo lectura"],
    ["Credenciales expuestas en el repositorio público (cadena de conexión en el primer commit)",
     "Cadena movida a user-secrets/variables de entorno; <b>pendiente rotar la contraseña</b> porque permanece en el "
     "historial"],
    ["Alucinaciones o dialecto SQL incorrecto (p. ej. LIMIT)",
     "Prompt con esquema, reglas y ejemplos; evaluación con criterios; siguiente paso: regla TOP y validación "
     "sintáctica con SET PARSEONLY"],
    ["Conjunto de evaluación pequeño y exactitud medida con un proxy",
     "Se reportan ICs bootstrap; trabajo futuro: 100+ preguntas y comparar resultados ejecutados"],
], [7.6 * cm, 9.4 * cm]))

doc = SimpleDocTemplate(str(raiz / "docs" / "informe.pdf"), pagesize=A4, leftMargin=1.8 * cm, rightMargin=1.8 * cm,
                        topMargin=1.5 * cm, bottomMargin=1.5 * cm, title="Informe RAG Inventario SynerGy")
doc.build(s)
print("OK")
