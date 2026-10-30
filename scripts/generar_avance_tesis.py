"""Genera el avance académico desde evidencias verificables, sin conexiones privadas.
Uso: python scripts/generar_avance_tesis.py
"""
from pathlib import Path
import csv
import hashlib
import json
import re
from xml.sax.saxutils import escape

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import cm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import BaseDocTemplate, Frame, PageTemplate, Paragraph, Spacer, PageBreak, Table, TableStyle, Image, KeepTogether, Preformatted
from reportlab.platypus.tableofcontents import TableOfContents
from reportlab.graphics.shapes import Drawing, Rect, String, Line, Polygon

ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'docs/assets'
EVID=ROOT/'resultados/consolidacion'
OUTPUT=ROOT/'output/pdf/Avance_Consolidado_SynerGy_RAG.pdf'
TAG='v0.2.0-capstone-2026-10-29'
W,H=A4
BODY=W-5.2*cm
BURGUNDY=colors.HexColor('#8c1638')
INK=colors.HexColor('#172538')

def load(name):return json.loads((EVID/name).read_text(encoding='utf-8-sig'))
def f(n,d=1):return f'{n:.{d}f}'.replace('.',',')
def pct(n):return f(n*100)+' %'

fonts=Path('C:/Windows/Fonts')
if (fonts/'arial.ttf').exists():
    for name,file in [('Arial','arial.ttf'),('Arial-Bold','arialbd.ttf'),('Arial-Italic','ariali.ttf'),('Arial-BoldItalic','arialbi.ttf')]:
        pdfmetrics.registerFont(TTFont(name,str(fonts/file)))
    pdfmetrics.registerFontFamily('Arial',normal='Arial',bold='Arial-Bold',italic='Arial-Italic',boldItalic='Arial-BoldItalic')
    FONT='Arial';BOLD='Arial-Bold';ITALIC='Arial-Italic'
else:FONT='Helvetica';BOLD='Helvetica-Bold';ITALIC='Helvetica-Oblique'

styles={
 'body':ParagraphStyle('Body',fontName=FONT,fontSize=11,leading=16.1,alignment=TA_JUSTIFY,spaceAfter=10),
 'h1':ParagraphStyle('H1',fontName=BOLD,fontSize=14,leading=19,spaceAfter=15,keepWithNext=True),
 'h2':ParagraphStyle('H2',fontName=BOLD,fontSize=11.5,leading=16,spaceBefore=12,spaceAfter=8,keepWithNext=True),
 'small':ParagraphStyle('Small',fontName=FONT,fontSize=8.6,leading=12,spaceAfter=7,alignment=TA_LEFT),
 'caption':ParagraphStyle('Caption',fontName=ITALIC,fontSize=9.3,leading=13,spaceBefore=7,spaceAfter=10),
 'cell':ParagraphStyle('Cell',fontName=FONT,fontSize=8.6,leading=11.9,spaceAfter=0,alignment=TA_LEFT),
 'headercell':ParagraphStyle('HeaderCell',fontName=BOLD,fontSize=8.6,leading=11.9,textColor=colors.white,spaceAfter=0),
 'cover':ParagraphStyle('Cover',fontName=BOLD,fontSize=13,leading=20,alignment=TA_CENTER,spaceAfter=12),
 'title':ParagraphStyle('Title',fontName=BOLD,fontSize=15.5,leading=23.2,alignment=TA_CENTER,spaceAfter=14),
 'center':ParagraphStyle('Center',fontName=FONT,fontSize=11,leading=17,alignment=TA_CENTER,spaceAfter=10),
 'code':ParagraphStyle('Code',fontName='Courier',fontSize=8.2,leading=12,spaceAfter=12),
}

def P(text,style='body'):return Paragraph(text,styles[style])
story=[]
def para(text):story.append(P(text))
def h2(text):story.append(P(text,'h2'))
def page(title):story.extend([PageBreak(),P(title,'h1')])
def note(text):story.append(P(text,'small'))
def table(headers,rows,widths=None,compact=False):
    widths=widths or [BODY/len(headers)]*len(headers)
    data=[[P(escape(str(v)),'headercell') for v in headers]]+[[P(escape(str(v)),'cell') for v in row] for row in rows]
    t=Table(data,colWidths=widths,repeatRows=1,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),INK),('BACKGROUND',(0,1),(-1,-1),colors.HexColor('#f6f7f9')),
        ('GRID',(0,0),(-1,-1),.35,colors.HexColor('#d8dde3')),('VALIGN',(0,0),(-1,-1),'TOP'),
        ('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),3 if compact else 7),('BOTTOMPADDING',(0,0),(-1,-1),3 if compact else 7)]))
    story.extend([t,Spacer(1,10)])
def figure(path,caption,maxh=12*cm):
    if not path.exists():raise FileNotFoundError(path)
    from PIL import Image as PILImage
    with PILImage.open(path) as im:w,h=im.size
    factor=min(BODY/w,maxh/h)
    story.extend([Image(str(path),width=w*factor,height=h*factor),P(caption,'caption')])

def footer(canvas,doc):
    if doc.page==1:return
    canvas.saveState()
    logo=ASSETS/'referencia_Image9.jpg'
    if logo.exists():canvas.drawImage(str(logo),W-4.5*cm,H-1.8*cm,width=2.0*cm,height=.9*cm,preserveAspectRatio=True,mask='auto')
    canvas.setFont(FONT,8)
    canvas.setFillColor(colors.HexColor('#66717c'))
    canvas.drawString(2.7*cm,1.25*cm,'SynerGy RAG Inventario | Avance consolidado | 29 octubre 2026')
    canvas.drawRightString(W-2.5*cm,1.25*cm,'i' if doc.page==2 else str(doc.page-2))
    canvas.restoreState()

class Doc(BaseDocTemplate):
    def afterFlowable(self,flowable):
        if isinstance(flowable,Paragraph) and flowable.style.name in ('H1','H2'):
            text=flowable.getPlainText();level=0 if flowable.style.name=='H1' else 1
            key='h'+hashlib.sha1(text.encode()).hexdigest()[:16]
            self.canv.bookmarkPage(key)
            if level == 0 and text != 'Contenido' and not re.match(r'^\d+\.\d+', text):
                self.notify('TOCEntry',(level,text,max(1,self.page-2),key))
            if level==0:self.canv.addOutlineEntry(text,key,level=0,closed=False)

def pipeline():
    d=Drawing(BODY,235)
    boxes=[(0,176,110,43,'Pregunta en español'),(143,176,145,43,'Identidad e intención'),(322,176,125,43,'Contexto en SQL'),
           (0,92,138,45,'SQL controlado'),(155,92,138,45,'Ollama y ScriptDom'),(310,92,137,45,'Actas por similitud'),
           (98,9,250,42,'Resultados y evidencia verificable')]
    for x,y,w,h,text in boxes:
        d.add(Rect(x,y,w,h,rx=6,ry=6,fillColor=colors.HexColor('#f2f5f7'),strokeColor=colors.HexColor('#8b9ba9'),strokeWidth=.7))
        d.add(String(x+w/2,y+h/2-3,text,fontName=FONT,fontSize=9,textAnchor='middle',fillColor=INK))
    def arrow(x1,y1,x2,y2):
        d.add(Line(x1,y1,x2,y2,strokeColor=BURGUNDY,strokeWidth=1))
        if y1==y2:d.add(Polygon([x2,y2,x2-5,y2+3,x2-5,y2-3],fillColor=BURGUNDY,strokeColor=BURGUNDY))
        else:d.add(Polygon([x2,y2,x2-3,y2+5,x2+3,y2+5],fillColor=BURGUNDY,strokeColor=BURGUNDY))
    arrow(110,197,143,197);arrow(288,197,322,197)
    arrow(216,176,69,137);arrow(216,176,224,137);arrow(384,176,379,137)
    arrow(69,92,160,51);arrow(224,92,224,51);arrow(379,92,290,51)
    return d

def main():
    pruebas=load('pruebas_automaticas.json');integracion=load('integracion.json');entorno=load('entorno.json');smoke=load('generador_smoke_resumen.json')
    historico=json.loads((ROOT/'resultados/resumen.json').read_text(encoding='utf-8-sig'))
    total=pruebas['total'];ok=pruebas['superadas'];casos=integracion['casos'];n_ok=sum(c['resultado']=='superado' for c in casos)
    logo=ASSETS/'referencia_Image9.jpg'
    story.extend([Spacer(1,2*cm),Image(str(logo),width=3.5*cm,height=1.58*cm),Spacer(1,.35*cm),
        P('Universidad de Las Américas','cover'),P('Facultad de Ingeniería y Ciencias Aplicadas','cover'),P('Maestría en Inteligencia Artificial Aplicada','cover'),Spacer(1,1.6*cm),
        P('Asistente inteligente local para la consulta del inventario tecnológico de SynerGy mediante lenguaje natural, arquitectura de generación aumentada por recuperación RAG, modelos ejecutados en Ollama y búsqueda vectorial en SQL Server','title'),
        Spacer(1,1.0*cm),P('Galo Andrés Estrella<br/>Wilian Pepinos','cover'),Spacer(1,.7*cm),
        P('Avance consolidado del proyecto y documento final','center'),P('Corte técnico 29 de octubre de 2026<br/>Entrega 30 de octubre de 2026','center'),Spacer(1,.7*cm),P('Quito, Ecuador<br/>Octubre de 2026','center')])
    story.extend([PageBreak(),P('Contenido','h1')])
    toc=TableOfContents();toc.levelStyles=[ParagraphStyle('TOC0',fontName=FONT,fontSize=9,leading=12,spaceBefore=1),ParagraphStyle('TOC1',fontName=FONT,fontSize=8.6,leading=12,leftIndent=14,spaceBefore=1)]
    story.append(toc)

    page('Resumen')
    para('El inventario tecnológico de SynerGy requiere respuestas oportunas sobre equipos, disponibilidad y custodios, junto con la consulta de información histórica registrada en actas. El proyecto desarrolla un asistente local en español que combina consultas estructuradas y recuperación de contexto mediante embeddings almacenados en SQL Server 2025. El artefacto utiliza ASP.NET Core, Ollama, qwen2.5-coder:7b y nomic-embed-text, con validación externa de T-SQL mediante ScriptDom.')
    para('La versión consolidada distingue tres rutas: consultas controladas para listas y cantidades habituales; generación de SQL con el modelo, apoyada por un catálogo semántico; y búsqueda vectorial del texto de actas. El directorio autorizado permite resolver nombres sin inventar correos y solicita aclaración ante ambigüedad. Los conteos se ejecutan sobre el inventario actual; las actas no se utilizan como sustituto del stock vigente.')
    para(f'La evidencia histórica comprende 270 generaciones: la exactitud por componentes pasó de 55,6 % en el baseline a 96,7 % en la configuración ajustada. Es un indicador aproximado del texto SQL y no una medida de exactitud de ejecución. En la consolidación se superaron {ok} de {total} pruebas automáticas, incluidos 30 escenarios SQL adversariales, y {n_ok} de {len(casos)} casos funcionales seleccionados. Se efectuó además una regresión de nueve generaciones sobre tres preguntas del conjunto de desarrollo. Estos resultados no sustituyen una evaluación independiente de 120 preguntas ni pruebas con usuarios.')
    para('Se entrega código modular, dependencias bloqueadas, instrucciones de ejecución, una demo visual explícitamente sintética, datos ficticios para un despliegue SQL y un repositorio versionado. El modelo se utiliza con pesos preentrenados; no se realizó entrenamiento ni ajuste fino. Continúan pendientes la evaluación ampliada, la relevancia del recuperador, la medición del proceso humano y los controles de identidad para un despliegue institucional.')
    note('Palabras clave: RAG, text-to-SQL, SQL Server, embeddings, Ollama, inventario tecnológico, validación de software.')

    page('Abstract')
    para('SynerGy needs timely answers about IT equipment, availability and custodians, as well as access to historical information recorded in handover records. This project develops a local Spanish-language assistant that combines structured queries and embedding-based retrieval in SQL Server 2025. The artifact uses ASP.NET Core, Ollama, qwen2.5-coder:7b and nomic-embed-text, with external T-SQL validation through ScriptDom.')
    para('The consolidated prototype separates controlled queries for common lists and counts, model-generated SQL supported by a semantic catalog, and vector search over handover record text. An authorized directory resolves names to institutional addresses and requests clarification when multiple matches exist. Inventory counts are computed from the current inventory view; historical records are not treated as current stock.')
    para(f'The historical experiment includes 270 generations, with component-based accuracy increasing from 55.6% to 96.7%. This proxy measures expected SQL patterns rather than execution correctness. The consolidated version passed {ok} automated tests, including 30 adversarial SQL scenarios, and {n_ok} selected functional checks. A nine-generation regression test covered three development questions. These results do not establish performance on an independent 120-question set or prove improvements in the human workflow.')
    para('The delivery includes modular code, locked dependencies, execution instructions, an explicitly synthetic visual demo, fictitious SQL data and a versioned repository. The language model retains its pretrained weights; no fine-tuning was performed. Remaining work includes broader evaluation, retrieval relevance assessment, user studies and institutional identity and authorization controls.')
    note('Keywords: retrieval-augmented generation, text-to-SQL, SQL Server, embeddings, local language models, IT inventory.')

    page('1 Introducción')
    para('La información del inventario es útil cuando una persona puede formular una necesidad concreta y recibir una respuesta verificable. Preguntas como cuántas computadoras portátiles están disponibles o qué equipos tiene un colaborador combinan lenguaje de negocio, códigos de estado y conocimiento del esquema. Los reportes estáticos cubren solo parte de esas necesidades y obligan a escalar otras consultas al personal técnico.')
    para('Los modelos de lenguaje facilitan la interpretación de preguntas, pero sus respuestas deben vincularse a fuentes autorizadas. En text-to-SQL, una sentencia plausible puede tener errores de dialecto, filtros incompletos o agregaciones equivocadas. La propuesta incorpora conocimiento del dominio, recuperación de contexto y controles externos al modelo, de modo que la ejecución no dependa únicamente de instrucciones en el prompt.')
    para('El proyecto se ubica en el área de Tecnología de SynerGy y aborda el inventario de activos informáticos. Su unidad de aplicación son las consultas sobre equipos y la búsqueda de texto histórico en actas. No evalúa las actividades financieras de la organización ni incorpora decisiones sobre personas. La información personal del custodio se utiliza únicamente para identificar la relación con los activos solicitados.')
    para('Este avance consolida el artefacto técnico y distingue lo implementado de las metas de investigación aún abiertas. Se conserva la estructura del documento final: problema, alternativas, objetivos, alcance, planificación, estudios, desarrollo, resultados, ética, conclusiones y anexos. Las cifras históricas, las verificaciones actuales y las demostraciones sintéticas se presentan por separado para evitar conclusiones que excedan la evidencia.')

    page('1.1 Problema y línea base')
    para('La dificultad principal es la traducción entre expresiones del usuario y el significado real de los datos. NombreModelo identifica el modelo del fabricante y no el tipo de equipo. Las categorías están fragmentadas: una laptop puede estar clasificada como LAPTOP o como COMPUTADOR con descripción Laptop. Un filtro que busque la palabra laptop en cualquier descripción incluye cargadores, bases y soportes. Asimismo, los kits teclado-mouse requieren una regla explícita de conteo.')
    para('Se confirmó con el usuario que cada registro de kit teclado-mouse cuenta como un mouse. Esta decisión se incorporó a las reglas controladas y al contexto del generador. La corrección resuelve un problema de calidad semántica: una consulta válida en SQL no garantiza que la categoría seleccionada represente el concepto del negocio.')
    table(['Indicador','Instrumento previsto','Meta del proyecto','Estado al corte'],[
        ['Tiempo de atención','Registro de 30 solicitudes reales','Reducción de al menos 40 %','No medido en el proceso humano'],
        ['Resolución inicial','Consulta resuelta sin escalamiento','70 % del catálogo','Pendiente de usuarios'],
        ['Exactitud de respuesta','120 preguntas con referencia aprobada','Al menos 85 %','Solo evidencia preliminar'],
        ['Trazabilidad','Fuente y ejecución identificables','100 % de respuestas evaluadas','Evidencia visible; auditoría por usuario pendiente'],
        ['Seguridad SQL','30 cargas adversariales declaradas','Bloquear las 30','Conjunto automatizado superado']],[80,145,103,BODY-328])
    note('Tabla 1. Indicadores y estado. La exactitud del LLM no es una línea base del tiempo de atención humano. Las metas organizacionales requieren medición propia.')
    para('La concentración del conocimiento en el equipo técnico y la ausencia de tiempos registrados motivan el estudio, pero no permiten estimar ahorros reales. Se propone registrar categoría, inicio, fin, respuesta validada y necesidad de escalamiento, con identificadores ficticios y sin copiar información personal al instrumento de evaluación.')

    page('2 Análisis de posibles soluciones')
    para('Se compararon un asistente local con Ollama y SQL Server y una alternativa administrada con Microsoft Copilot Studio. La primera favorece control del procesamiento y reproducción del experimento; la segunda representa una integración posible con el ecosistema corporativo. La matriz utiliza una escala de 1 a 5 y ponderaciones del diseño inicial, inspiradas en atributos de calidad de sistemas de IA. No constituye una certificación de conformidad ni una prueba empírica de ambas plataformas.')
    table(['Criterio','Peso','Local','Administrada'],[
        ['Robustez y corrección funcional','20 %','4,3','4,1'],['Privacidad y control del dato','20 %','5,0','4,0'],
        ['Explicabilidad y trazabilidad','15 %','4,4','4,2'],['Integración con el ecosistema','15 %','3,8','5,0'],
        ['Mantenibilidad','10 %','3,8','4,6'],['Reproducibilidad','10 %','4,8','3,6'],
        ['Viabilidad de implementación','10 %','3,9','4,7'],['Puntaje ponderado','100 %','4,34','4,29']],[BODY-155,55,50,50])
    note('Tabla 2. Matriz de selección del diseño inicial. Fuente: valoración del equipo en la propuesta. La diferencia estrecha no demuestra superioridad estadística.')
    para('Se implementó la alternativa local. La arquitectura permite inspeccionar el esquema, el contexto recuperado, el SQL propuesto y su validación. La alternativa administrada permanece como comparación de diseño; no se presenta como un prototipo construido ni se atribuyen resultados experimentales que no fueron medidos.')
    para('El beneficio esperado es facilitar el acceso a información verificable y reducir consultas repetitivas al personal técnico. El impacto depende de la calidad de los datos y de un uso limitado a la gestión de activos. La posibilidad de consultar custodios exige controles de identidad, minimización y supervisión, incluso cuando la inferencia ocurre localmente.')

    page('3 Objetivos')
    h2('3.1 Objetivo general')
    para('Desarrollar un asistente inteligente local para consultar el inventario tecnológico de SynerGy en lenguaje natural, mediante recuperación de contexto, modelos ejecutados en Ollama, consultas controladas y búsqueda vectorial en SQL Server, con respuestas verificables y controles que permitan evaluar exactitud, trazabilidad, seguridad y tiempo de respuesta.')
    h2('3.2 Objetivos específicos')
    for texto in [
        '<b>OE1.</b> Analizar el proceso de consulta, consolidar un catálogo de necesidades y medir una línea base de al menos 30 solicitudes reales antes de estimar beneficios organizacionales.',
        '<b>OE2.</b> Preparar las fuentes autorizadas, el diccionario semántico y un conjunto de 120 preguntas de referencia, con controles de calidad y seudonimización.',
        '<b>OE3.</b> Implementar recuperación de contexto e indexación vectorial en SQL Server y evaluar relevancia y cobertura de los fragmentos recuperados.',
        '<b>OE4.</b> Implementar la traducción a SQL y los controles externos, restringir fuentes y comprobar el bloqueo de los 30 escenarios adversariales declarados.',
        '<b>OE5.</b> Evaluar el asistente con referencias aprobadas y usuarios, contrastando exactitud, fidelidad, trazabilidad y latencia; documentar resultados y limitaciones.']:
        para(texto)
    para('Las metas finales de exactitud de al menos 85 %, fidelidad de al menos 0,90, trazabilidad completa y p95 de hasta ocho segundos se mantienen como criterios de evaluación. El corte actual no demuestra su cumplimiento global. La implementación técnica está consolidada para pruebas locales; la evaluación ampliada y del proceso humano permanece abierta.')

    page('4 Alcance y restricciones')
    table(['Fuente','Uso implementado','Límite'],[
        ['VistaEquipo','Listas, filtros y agregaciones de activos','Única fuente libre permitida al SQL generado'],
        ['VistaColaboradorInv','Resolución parametrizada de nombre y correo','Solo proyección necesaria; no se consulta desde el LLM'],
        ['Acta y TipoActa','Texto, tipo, fecha y referencia de actas activas','Búsqueda histórica, separada del inventario vigente'],
        ['ActaDetalle','Contexto del esquema histórico','No se usa para contar el stock actual'],
        ['Catálogo auxiliar','Embeddings de contexto y actas','Versiones independientes y credencial de indexación']],[100,170,BODY-270])
    note('Tabla 3. Fuentes de la versión consolidada. No se incluye recuperación de archivos PDF/DOCX externos ni búsqueda en veinte manuales.')
    para('La aplicación ejecuta consultas del inventario y búsquedas del texto que ya reside en SQL. El cuerpo de las actas es una fuente textual estructurada por registro; no requiere descargar documentos binarios. Los resultados del buscador semántico son coincidencias top-K y no un censo de documentos.')
    para('Las operaciones implementadas no modifican activos, asignaciones ni actas. La indexación escribe únicamente en la tabla auxiliar; la restricción por custodio utiliza una tabla temporal por conexión. Los permisos mínimos deben ser aplicados y comprobados por el administrador. La aplicación local aún no implementa autenticación ni autorización por usuario.')
    para('El prototipo depende de SQL Server 2025, conectividad a la fuente autorizada y modelos locales disponibles. La búsqueda de actas admite tema, tipo explícito y custodio resuelto; no implementa filtros exactos por número o fecha. Las listas muestran hasta 100 filas e indican truncamiento. La inferencia en CPU puede superar un minuto.')

    page('5 Planificación y costos')
    para('La planificación original comprende veinte semanas, del 15 de septiembre de 2026 al 30 de enero de 2027. El prototipo y su consolidación técnica se adelantaron respecto a algunas fases previstas. El estado al corte no debe confundirse con el cierre de la evaluación organizacional.')
    table(['Fase','Período previsto','Estado resumido'],[
        ['Planificación y acceso','Semana 1','Entorno y conexiones autorizadas disponibles'],['Diagnóstico','Semanas 2 a 4','Análisis del problema; línea base humana pendiente'],
        ['Base de conocimiento','Semanas 5 a 7','Catálogos, actas y demo; gold set ampliado pendiente'],['Recuperación','Semanas 8 a 11','Implementada; relevancia pendiente de etiquetado'],
        ['SQL y seguridad','Semanas 12 a 14','Módulo y conjunto adversarial implementados'],['Integración','Semanas 15 a 16','API e interfaz local; empaquetado documentado'],
        ['Evaluación final','Semanas 17 a 18','Pruebas preliminares; usuarios y 120 preguntas pendientes'],['Documento y defensa','Semanas 19 a 20','Avance consolidado de esta entrega']],[105,100,BODY-205])
    h2('5.1 Estimación referencial')
    table(['Rubro','Supuesto','USD'],[
        ['Trabajo del equipo','2 personas x 20 semanas x 15 h = 600 h; USD 12/h','7 200'],
        ['Hardware','Depreciación proporcional estimada','300'],['Conectividad y energía','Estimación del período','150'],
        ['Subtotal','','7 650'],['Contingencia','10 %','765'],['Total estimado','','8 415']],[100,260,BODY-360])
    note('Tablas 4 y 5. Plan y presupuesto referencial. Se corrige la multiplicación de horas del documento previo: 600, no 400. Los valores son supuestos de planificación; no representan gastos ejecutados ni ahorro medido.')

    page('6 Estudios y metodología')
    para('El trabajo se organiza como investigación aplicada de diseño de un artefacto, con comprensión del problema, construcción y evaluación iterativa (Peffers et al., 2007). La secuencia se apoya en un ciclo CRISP-DM adaptado: comprensión del negocio y de los datos, preparación del contexto, configuración de modelos, evaluación y documentación (Chapman et al., 2000).')
    para('RAG combina un generador con información recuperada de una memoria externa (Lewis et al., 2020). En esta implementación, el contexto del inventario se obtiene desde un catálogo semántico y el texto de las actas se recupera por distancia coseno. La generación de conteos y agregaciones exige ejecutar SQL sobre la fuente completa; recuperar solo filas cercanas no permite calcular correctamente el total.')
    para('La literatura de text-to-SQL recomienda controlar el lenguaje generado y evaluar su resultado. PICARD muestra la utilidad de restricciones sintácticas (Scholak et al., 2021), mientras que los trabajos de evaluación de SQL distinguen coincidencia de texto y exactitud de ejecución (Yu et al., 2018). El proyecto aplica un analizador externo después de generar la consulta, en lugar de considerar suficiente una instrucción en el prompt.')
    para('La evaluación de recuperación y generación debe separarse. Métricas como relevancia de contexto y fidelidad ayudan a identificar si el error ocurre al recuperar o al responder (Es et al., 2024). Para el histórico se emplearon intervalos bootstrap sobre preguntas y comparaciones pareadas. La unidad de remuestreo es la pregunta, no cada una de las 270 generaciones, porque las repeticiones de una misma pregunta no son independientes.')
    para('El protocolo actual conserva tres configuraciones del mismo modelo: baseline, prompt enriquecido y ajustado. La comparación permite observar efectos del conjunto de reglas, ejemplos y pistas, pero no aislar cada componente ni probar superioridad frente a otros modelos. No se realizó SHAP ni se incluye un análisis de interpretabilidad sin código o evidencia disponible.')

    page('7 Desarrollo del proyecto')
    h2('7.1 Arquitectura y flujo')
    story.extend([pipeline(),P('Figura 1. Flujo implementado. Las rutas de inventario y actas comparten entrada y evidencia, pero conservan fuentes y reglas de cálculo distintas.','caption')])
    table(['Componente','Responsabilidad'],[
        ['API y pantalla','Entrada de preguntas, resultados, aclaraciones, SQL, contexto y vista para anexos'],
        ['Application','Casos de uso, reglas de inventario, contratos y DTOs'],
        ['Infrastructure','Ollama, SqlClient, ScriptDom, directorio e índices vectoriales'],
        ['Domain','Proyecto reservado para evolución del dominio; no se presenta como un modelo de entidades completo'],
        ['Pruebas y scripts','Regresión, análisis, demo y generación documental']],[120,BODY-120])
    para('La modularidad permite comprobar la recuperación y la generación con dobles de servicios, sin requerir la base empresarial en la integración continua. Los accesos a datos son asíncronos, admiten cancelación y liberan conexiones. La publicación del repositorio no despliega la base ni hace pública la API local.')

    page('7.2 Preparación del contexto y los datos')
    para('El catálogo inventario-v2 contiene 28 entradas de categorías, estados, columnas y ejemplos. Se corrigieron los predicados de laptops para excluir accesorios y se incorporó el criterio confirmado para kits teclado-mouse. Los sinónimos y plurales se tratan antes de intentar resolver un nombre de persona. Esta preparación evita que expresiones como equipos de laptops se envíen por error al directorio.')
    para('La indexación de actas lee registros activos, convierte HTML a texto y reemplaza identificadores conocidos del propio registro, correos y secuencias numéricas largas. El texto normalizado se incrusta localmente y se almacena en actas-v1. Los textos idénticos reutilizan embeddings, pero cada acta conserva su referencia. El proceso reemplaza su versión de forma transaccional y no escribe sobre las actas originales.')
    para('Los campos de observaciones pueden estar vacíos sin impedir la recuperación: la fuente disponible incluye TextoActaEntrega y, cuando existe, TextoIntroductorio. Se evita interpretar la ausencia de observaciones como ausencia de texto. Los metadatos se consultan desde SQL y no se incorporan identificaciones personales al repositorio público.')
    para('La reproducción dispone de un conjunto ficticio de 500 activos, doce custodios y 24 actas. Sus correos pertenecen al dominio reservado example.test. Este conjunto permite revisar el despliegue sin distribuir una copia del inventario real. La demo visual utiliza respuestas sintéticas y lo declara en la pantalla; no genera evidencia de ejecución SQL o del modelo.')
    note('Los registros operacionales, conexiones y textos de actas no se publican. El repositorio contiene el código de preparación, el catálogo de reglas y resúmenes de pruebas sin identidades.')

    page('7.3 Modelos y configuración')
    table(['Elemento','Configuración registrada'],[
        ['Generador','qwen2.5-coder:7b; pesos preentrenados'],['Temperatura de la API','0,1; configurable en Rag:Temperature'],
        ['Embeddings','nomic-embed-text; 768 dimensiones'],['Recuperación','Distancia coseno exacta, K=5, umbral inicial 0,65'],
        ['Ejecución local','Ollama '+str(entorno.get('ollama','')).replace('ollama version is ','')],
        ['SDK','.NET '+str(entorno.get('dotnet_sdk','10.0.401'))],['Base','SQL Server 2025, funciones vectoriales nativas'],
        ['Recursos','CPU '+str(entorno.get('cpu',''))+'; '+str(entorno.get('ram_gb',''))+' GB de RAM']],[120,BODY-120])
    note('Tabla 6. Parámetros y entorno. El umbral no representa probabilidad de acierto y requiere calibración con consultas etiquetadas.')
    para('No se entrenaron ni ajustaron los pesos del modelo. El trabajo de adaptación consiste en preparar conocimiento de dominio, seleccionar ejemplos, configurar la inferencia y validar el sistema integrado. La expresión modelo final se refiere a la configuración seleccionada del modelo preentrenado y no a un entrenamiento propio. Esta distinción permite interpretar correctamente el criterio de la actividad académica.')
    h2('Identificación de los pesos')
    for modelo in entorno.get('modelos',[]):
        note('<b>'+escape(modelo['name'])+'</b><br/>SHA-256 '+escape(modelo['digest']))
    para('Los hashes permiten detectar cambios en los artefactos locales. La reproducción de tiempos también depende de CPU, memoria, carga, caché y orden de ejecución. Al actualizar el modelo de embeddings es necesario crear una versión nueva del índice; conservar el mismo nombre no demuestra que los vectores pertenezcan al mismo espacio.')

    page('7.4 Validación SQL e identidad')
    para('El validador analiza el árbol sintáctico con ScriptDom, exige una única sentencia SELECT y comprueba todas las referencias a tablas. La fuente autorizada del SQL generado es VistaEquipo, con esquema dbo o sin calificación. Se deniegan fuentes externas, funciones no incluidas en la lista permitida, variables, secuencias, indicaciones de tabla y formas de serialización no utilizadas por el caso de uso.')
    para('La búsqueda por compañero se ejecuta mediante consultas parametrizadas sobre nombre y correo institucional. Una coincidencia confirma el identificador; varias coincidencias producen una selección explícita y ninguna coincidencia solicita aclaración. Las aclaraciones no invocan al generador ni consultan activos. El correo no se deduce a partir del nombre.')
    para('Para una persona resuelta, el ejecutor crea una tabla temporal por conexión que contiene solo sus equipos asignados. Las referencias del SQL validado se reescriben sobre esa fuente temporal. Los filtros adicionales del modelo operan dentro del subconjunto autorizado por la consulta. Esta restricción técnica no sustituye la autorización del usuario que está utilizando la aplicación.')
    para('El lector obtiene como máximo 101 registros para indicar que la respuesta visible se limita a cien. Un conteo se ejecuta sobre el conjunto filtrado completo. El timeout SQL es de quince segundos; el timeout de inferencia es de 300 segundos. Los secretos se configuran fuera de Git y la indexación usa una conexión separada de la lectura habitual.')
    note('La cuenta SQL debe ser limitada por el administrador. La ausencia de autenticación de la API impide considerar este prototipo listo para exposición institucional o pública con datos reales.')

    page('7.5 Reproducibilidad y demostración')
    para('La entrega identifica SDK y dependencias mediante global.json y packages.lock.json. El README describe requisitos, secretos, preparación de tablas, indexación, ejecución y evaluación. GitHub Actions restaura en modo bloqueado, compila y ejecuta las pruebas sin conexiones a la base empresarial.')
    story.append(Preformatted('dotnet restore SynerGy-Rag-Inventario.slnx --locked-mode\ndotnet build SynerGy-Rag-Inventario.slnx -c Release --no-restore\ndotnet test SynerGy-Rag-Inventario.slnx -c Release\nollama pull qwen2.5-coder:7b\nollama pull nomic-embed-text',styles['code']))
    para('El despliegue completo requiere una instancia SQL autorizada. El inicializador ficticio está protegido para bases cuyo nombre contiene Demo y utiliza una transacción. La tabla auxiliar se prepara con un script separado. Después se indexan catálogo y actas mediante comandos de consola; no se ofrece un endpoint de indexación accesible por HTTP.')
    story.append(Preformatted('dotnet run --project SynerGy-Rag-Inventario -- --indexar-catalogo\ndotnet run --project SynerGy-Rag-Inventario -- --indexar-actas\ndotnet run --project SynerGy-Rag-Inventario --launch-profile http',styles['code']))
    para('La demostración visual puede iniciarse con python scripts/demo_visual.py y abrirse en el puerto 5226. Sus datos y tiempos se identifican como simulados. La aplicación completa utiliza el puerto 5225 en el perfil local y las credenciales del entorno. Las instrucciones no incluyen contraseñas ni exigen publicar datos reales.')
    note('Identificador de entrega: '+TAG+'. Repositorio: https://github.com/Wilixs102/SynerGy-Rag-Inventario. El README es la referencia operativa de comandos y conexiones.')

    page('7.6 Resultados históricos del generador')
    rows=[]
    for nombre,r in historico['configs'].items():
        rows.append([nombre,90,pct(r['exactitud_media']),f(r['exactitud_ic95'][0]*100)+' a '+f(r['exactitud_ic95'][1]*100),pct(r['sql_valida_media']),f(r['latencia_mediana_ms']/1000,2)])
    table(['Configuración','n','Componentes','IC 95 %','Aceptación original','Mediana s'],rows,[108,25,71,88,86,BODY-378])
    note('Tabla 7. Registro histórico de 270 generaciones. La columna de aceptación corresponde al validador original, no a una comprobación sintáctica o de ejecución con el analizador actual.')
    para('La configuración enriquecida mejoró 37,8 puntos porcentuales frente al baseline, y la ajustada 41,1 puntos. El análisis histórico conserva intervalos bootstrap por pregunta y comparaciones pareadas: Wilcoxon p=0,0003 y McNemar p=0,0005 para baseline frente a ajustado. La diferencia entre enriquecido y ajustado corresponde a una sola pregunta y no permite una conclusión sólida sobre la temperatura.')
    figure(ROOT/'resultados/grafico.png','Figura 2. Exactitud por componentes e intervalos bootstrap del histórico. El gráfico no mide resultados del buscador de actas ni de las consultas controladas.',maxh=8*cm)
    para('La métrica exige patrones esperados y excluye patrones prohibidos. Puede aceptar SQL semánticamente incorrecto y no calcula equivalencia de resultados. Las consultas con LIMIT mostraron que aceptación del validador original y validez de T-SQL no eran equivalentes. Las reglas de clasificación de equipos también se corrigieron después; por tanto, el 96,7 % no demuestra exactitud actual sobre el inventario real.')

    page('7.7 Regresión actual del generador')
    table(['Configuración','Intentos','Componentes','Aceptadas por parser','Mediana s','Máximo s'],[
        [k,v['intentos'],str(v['componentes_ok'])+'/'+str(v['intentos']),v['aceptadas_parser'],f(v['latencia_mediana_ms']/1000,2),f(v['latencia_max_ms']/1000,2)]
        for k,v in smoke['configs'].items()],[112,45,65,85,60,BODY-367])
    note('Tabla 8. Nueve generaciones actuales: tres preguntas del conjunto de desarrollo, una repetición por configuración. No es un conjunto independiente ni una prueba de exactitud por ejecución.')
    para('El baseline generó en la primera pregunta un filtro por NombreModelo. Las configuraciones enriquecida y ajustada generaron los predicados corregidos para laptops, con COLLATE para comparación sin distinción de tildes o mayúsculas. El evaluador textual no reconocía inicialmente esa construcción, por lo que se amplió el patrón para admitir COLLATE entre columna y LIKE. Se conservaron los SQL y las latencias capturados; el cambio del criterio está documentado.')
    para('Las otras dos preguntas comprueban conteos por estado. Las nueve consultas fueron aceptadas por el analizador actual. Este resultado permite verificar el funcionamiento del generador después de los cambios, pero tres preguntas no permiten estimar el comportamiento del catálogo completo ni demostrar generalización. No se efectuaron nuevas pruebas de significancia con esta muestra pequeña.')
    para('Los tiempos muestran el efecto del entorno CPU y de la reutilización de contexto. Se observaron generaciones de varios segundos y máximos de decenas de segundos. No se atribuye la reducción de los tiempos de consultas controladas a una mejora del LLM. El objetivo de p95 de ocho segundos sigue sin demostrarse para todas las rutas.')

    page('7.8 Pruebas automáticas e integración')
    para(f'La versión consolidada superó {ok} de {total} pruebas automáticas, con {pruebas["fallidas"]} fallidas y {pruebas["sin_ejecutar"]} sin ejecutar. Incluye contratos de embeddings, dimensiones y errores, propagación de cancelación, reemplazo del catálogo, resolución de nombres, ambigüedad, separación de rutas, reglas de inventario y seguridad SQL.')
    table(['Caso','Comprobación','Resultado','Tiempo s'],[
        [c['id'],{'conteo_laptop':'Total de laptops frente a referencia SQL','conteo_mouse':'Total de mouses con kits frente a referencia SQL','lista_laptop':'Lista y límite de resultados','asignadas':'Laptops asignadas frente a referencia SQL','sin_persona':'Nombre inexistente sin generación','ambiguo':'Nombre ambiguo con selección','actas':'Búsqueda vectorial y filtro de devolución','no_total_vectorial':'No presentar top-K como total'}[c['tipo']],c['resultado'],f(c['latencia_ms']/1000,2)]
        for c in casos],[40,240,80,BODY-360])
    note('Tabla 9. Ocho escenarios funcionales seleccionados. Las referencias de conteo se consultaron directamente en SQL. El resumen público omite valores operacionales y datos personales.')
    para('Las 30 cargas adversariales declaradas fueron rechazadas. Incluyen escritura, múltiples sentencias, acceso a otras tablas o bases, funciones no autorizadas y serialización no requerida. El resultado describe el conjunto ejecutado y no una garantía frente a cualquier ataque posible. Los archivos TRX se conservan como evidencia de ejecución local y el repositorio publica un resumen sin rutas privadas.')
    para('La auditoría NuGet posterior a actualizar System.Security.Cryptography.Pkcs a 10.0.13 no informó las vulnerabilidades señaladas previamente. Esta comprobación depende del catálogo de avisos consultado y no equivale a una auditoría integral de la aplicación. Se mantienen pendientes identidad de usuarios, permisos mínimos demostrados y pruebas de concurrencia.')

    page('7.9 Discusión de resultados')
    para('El aporte de la consolidación es una integración verificable del lenguaje natural con el inventario y el texto histórico, acompañada por controles deterministas. La corrección de los filtros muestra que la calidad semántica del dato es tan relevante como la sintaxis. Un equipo puede estar descrito con la palabra laptop sin ser una laptop, y un kit puede requerir una decisión explícita de conteo.')
    para('La separación de rutas mejora la interpretación de los resultados: el LLM se evalúa por sus generaciones, las consultas controladas por su correspondencia con reglas verificadas y el buscador de actas por la relevancia de sus coincidencias. Agregar esas rutas en una única tasa de acierto ocultaría diferencias de método, cobertura y costo.')
    para('El histórico aporta evidencia del ajuste del prompt en el dominio, con riesgo de sobreajuste porque preguntas y pistas fueron preparadas por el mismo equipo. La regresión actual es una comprobación técnica pequeña. Las pruebas funcionales verifican operaciones seleccionadas y no miden la experiencia del usuario, la cobertura total de preguntas ni el ahorro del proceso humano.')
    para('La búsqueda de actas requiere una evaluación propia. Muchas actas contienen cláusulas similares, por lo que la cercanía semántica puede producir empates y no identifica por sí sola la fecha, la persona o la vigencia. Los filtros de tipo y custodio se aplican antes del ranking y el mensaje de la interfaz evita confundir coincidencias con cantidades o asignaciones actuales.')
    para('La principal limitación de desempeño observada es la inferencia en CPU. Las consultas controladas pueden responder con rapidez, pero no eliminan las latencias de preguntas que requieren generación. Una comparación futura debe fijar modelo, hardware, orden, calentamiento y tamaño de contexto antes de atribuir diferencias de tiempo a una modificación del sistema.')

    page('7.10 Implicaciones éticas y organizacionales')
    table(['Riesgo','Control implementado','Pendiente'],[
        ['Exposición de personas','Resolución con proyección mínima; históricos seudonimizados; texto de actas tratado; vista para anexos','Autenticación y autorización por usuario'],
        ['Cifras incorrectas','Reglas explícitas, SQL visible y contraste con referencia','Evaluación independiente y revisión de nuevas categorías'],
        ['Lectura fuera de alcance','Analizador sintáctico y fuentes permitidas; SQL parametrizado en servicios internos','Comprobación institucional de permisos mínimos'],
        ['Uso laboral indebido','Alcance limitado a gestión de activos y supervisión humana','Gobernanza y política de uso formal'],
        ['Falta de evidencia','Archivos de resultados, versiones y límites declarados','Registro de auditoría por usuario y estudio del proceso']],[92,205,BODY-297])
    note('Tabla 10. Riesgos y estado de mitigaciones. La ejecución local no demuestra por sí sola cumplimiento normativo ni confidencialidad.')
    para('La información de custodios no debe utilizarse como base única para sanciones o decisiones laborales. Una asignación puede requerir contexto administrativo y una respuesta correcta del sistema no autoriza un uso distinto de su finalidad. La supervisión del área responsable y el control de acceso deben acompañar cualquier ampliación de usuarios.')
    para('La publicación excluye conexiones, contraseñas, textos de actas y volcados del inventario. Los correos del conjunto histórico fueron sustituidos por identidades ficticias sin alterar sus indicadores. El enmascaramiento de la pantalla es visual y requiere revisión antes de compartir capturas. Una credencial de prueba presente en el historial anterior requiere rotación y gestión con el administrador; retirarla del árbol actual no la elimina del historial.')

    page('8 Conclusiones y recomendaciones')
    para('Se consolidó un prototipo local funcional con tres rutas de consulta, recuperación vectorial nativa en SQL Server y trazabilidad visible del contexto y del SQL. La solución conserva el alcance de inventario y utiliza texto de actas existente en la base, sin incorporar recuperación de manuales externos ni operaciones de modificación de activos.')
    para(f'El artefacto superó {ok} pruebas automáticas y {n_ok} comprobaciones funcionales seleccionadas. La evaluación histórica registró una mejora de 55,6 % a 96,7 % por componentes, mientras que la regresión actual verificó el generador con tres preguntas. Estas evidencias tienen alcances distintos y no permiten declarar cumplida la meta global de exactitud sobre 120 preguntas independientes.')
    para('Respecto de OE1, se caracterizó el problema y se definió el instrumento de línea base, pero falta ejecutar la medición humana. OE2 dispone de fuentes autorizadas, catálogo y datos ficticios; el conjunto ampliado sigue pendiente. OE3 tiene recuperación implementada, con evaluación de relevancia abierta. OE4 cuenta con controles y 30 escenarios SQL rechazados, aunque requiere permisos institucionales e identidad. OE5 presenta evidencia preliminar y no una evaluación final con usuarios.')
    para('Se recomienda congelar una réplica de datos y un conjunto independiente, revisar respuestas de referencia con SynerGy, medir exactitud por ejecución y etiquetar la relevancia de actas. También se recomienda completar autenticación, auditoría por usuario y verificación de permisos antes de ampliar el uso. Las mejoras de latencia deben comprobarse por ruta con un protocolo de hardware y caché controlado.')

    page('9 Trabajo futuro')
    para('La siguiente evaluación debe incluir 120 preguntas revisadas por personas que no hayan redactado las pistas, con separación entre desarrollo y prueba. Las consultas equivalentes se comprobarán por resultados, no solo por coincidencia de expresiones regulares. El conjunto incluirá categorías fragmentadas, kits, preguntas ambiguas, filtros combinados y casos sin respuesta.')
    para('Para actas se requiere un conjunto con identificadores relevantes por pregunta y métricas Recall@K, MRR y precisión de los filtros. El análisis debe considerar la similitud de las plantillas y el efecto de datos faltantes. La expansión a textos más largos necesitará segmentación y revisión del tratamiento de información personal.')
    para('La evaluación con usuarios debe medir tiempo desde solicitud hasta respuesta validada, resolución sin escalamiento y utilidad percibida. Los resultados se compararán con la línea base humana. La comparación empírica con una plataforma administrada se mantendrá como un estudio separado, con acceso y condiciones equivalentes.')
    para('El desarrollo institucional requiere identidad corporativa, autorización por rol, auditoría y revisión del acceso a custodios. La exposición en red y los certificados deben configurarse con el área responsable. La entrega de este avance no autoriza un despliegue productivo ni supone que esas condiciones estén resueltas.')

    page('10 Referencias bibliográficas')
    refs=[
        'Chapman, P., Clinton, J., Kerber, R., Khabaza, T., Reinartz, T., Shearer, C. y Wirth, R. (2000). CRISP-DM 1.0: Step-by-step data mining guide. SPSS.',
        'Efron, B. y Tibshirani, R. J. (1993). An introduction to the bootstrap. Chapman &amp; Hall/CRC.',
        'Es, S., James, J., Espinosa-Anke, L. y Schockaert, S. (2024). RAGAS: Automated evaluation of retrieval augmented generation. EACL System Demonstrations, 150-158.',
        'International Organization for Standardization. (2023). ISO/IEC 25059:2023. Quality model for AI systems. ISO.',
        'Lewis, P., Perez, E., Piktus, A., Petroni, F., Karpukhin, V., Goyal, N., Küttler, H., Lewis, M., Yih, W., Rocktäschel, T., Riedel, S. y Kiela, D. (2020). Retrieval-augmented generation for knowledge-intensive NLP tasks. Advances in Neural Information Processing Systems, 33, 9459-9474.',
        'McNemar, Q. (1947). Note on the sampling error of the difference between correlated proportions or percentages. Psychometrika, 12(2), 153-157.',
        'Peffers, K., Tuunanen, T., Rothenberger, M. A. y Chatterjee, S. (2007). A design science research methodology for information systems research. Journal of Management Information Systems, 24(3), 45-77.',
        'Scholak, T., Schucher, N. y Bahdanau, D. (2021). PICARD: Parsing incrementally for constrained auto-regressive decoding from language models. EMNLP, 9895-9901. https://doi.org/10.18653/v1/2021.emnlp-main.779',
        'Wilcoxon, F. (1945). Individual comparisons by ranking methods. Biometrics Bulletin, 1(6), 80-83.',
        'Yu, T. et al. (2018). Spider: A large-scale human-labeled dataset for complex and cross-domain semantic parsing and text-to-SQL task. EMNLP, 3911-3921.',
        'Microsoft. (2026). VECTOR_DISTANCE y documentación de ScriptDom. Microsoft Learn. https://learn.microsoft.com/sql/t-sql/functions/vector-distance-transact-sql',
        'Microsoft. (2026). Aviso de seguridad de System.Security.Cryptography.Pkcs. https://github.com/advisories/GHSA-846f-f764-76mx',
        'Ollama. (2026). Generate embeddings. Documentación de la API. https://docs.ollama.com/api/embed',
    ]
    for ref in refs:story.append(P(ref,'small'))

    page('11 Anexos técnicos')
    h2('Anexo A Matriz de correspondencia de la entrega')
    table(['Criterio','Evidencia','Estado'],[
        ['Código modular','API, Application, Infrastructure y pruebas','Consolidado para entorno local'],
        ['Modelo seleccionado y validado','entorno.json, histórico y regresión del generador','Preentrenado; sin entrenamiento propio'],
        ['Mejoras incorporadas','CHANGELOG y código de reglas, vectores, actas y seguridad','Implementadas y versionadas'],
        ['Pruebas finales','pruebas_automaticas.json e integracion.json','Conjunto ejecutado superado'],
        ['Reproducción','README, global.json y packages.lock.json','Instrucciones y versiones disponibles'],
        ['Demo','demo_visual.py y datos SQL ficticios','Visual sintética; despliegue SQL documentado'],
        ['Métricas comparativas','resumen.json y generador_smoke_resumen.json','Histórico y regresión separados'],
        ['Documento y anexos','Este PDF, fuentes y guías técnicas','Avance consolidado'],
        ['Evaluación amplia y usuarios','Protocolo y metas','Pendiente, sin cifras inventadas']],[105,210,BODY-315])
    para('La matriz se construye con los criterios de la actividad proporcionada. No asigna una calificación ni sustituye la rúbrica con puntajes. Las evidencias técnicas pueden revisarse desde el repositorio y sus rutas; los secretos y los originales con identidades no forman parte de la publicación.')

    page('Anexo B Interfaz de consultas')
    figure(ASSETS/'interfaz-consultas.jpg','Figura 3. Pantalla de consultas con identidad de SynerGy. La captura inicial no contiene resultados de empleados ni datos operacionales.',maxh=16*cm)
    para('La pantalla permite formular preguntas, utilizar ejemplos, cancelar solicitudes y revisar resultados. Los paneles de SQL y contexto muestran evidencia cuando existe una respuesta. La vista para anexos enmascara visualmente datos habituales; no modifica el resultado de la API ni implementa control de acceso.')

    page('Anexo C Demostración con datos ficticios')
    figure(ASSETS/'demo-sintetica.jpg','Figura 4. Demostración visual identificada como sintética. Los datos y tiempos mostrados no proceden de una ejecución de SQL Server u Ollama.',maxh=15.5*cm)
    para('El conjunto SQL ficticio contiene 500 activos, doce custodios y 24 actas. Sus conteos esperados son 180 laptops, 144 laptops asignadas y 150 mouses con kits. Estas cantidades pertenecen al conjunto de demostración y no describen el inventario empresarial. La demo visual reproduce la presentación; las pruebas de integración se ejecutan contra la instalación SQL autorizada.')

    page('Anexo D Escenarios adversariales declarados')
    nombres=['Inserción de registros','Actualización de activos','Eliminación de registros','Eliminación de tabla','Alteración de estructura','Truncamiento','Creación de tabla','Concesión de permisos','Ejecución dinámica','SELECT INTO','SELECT seguido de otra sentencia','Dos SELECT sin separador','Comentario de línea','Comentario de bloque','Otra base de datos','Servidor y base externos','Esquema no autorizado','Tabla de colaboradores','Fuente adicional separada por coma','JOIN a metadatos','Subconsulta a metadatos','UNION con fuente no permitida','OPENROWSET','Tabla funcional OPENJSON','Variable global de servidor','Identidad SYSTEM_USER','Secuencia NEXT VALUE','Función definida por usuario','Indicación NOLOCK','Serialización FOR JSON']
    table(['Caso','Escenario','Resultado'],[[f'S{i:02}',nombre,'Rechazado'] for i,nombre in enumerate(nombres,1)],[45,BODY-125,80],compact=True)
    note('Tabla 11. Conjunto de treinta escenarios en RobustezEntregaTests.cs. Los payloads completos y la aserción se conservan en el código. El alcance es este conjunto, no todos los posibles ataques.')

    page('Anexo E Evidencias y lectura del repositorio')
    table(['Ruta','Contenido'],[
        ['resultados/resultados.csv','270 generaciones históricas con identidades ficticias'],
        ['resultados/resumen.json','Estadística histórica por pregunta y configuración'],
        ['resultados/consolidacion/generador_smoke.csv','Nueve SQL y sus latencias actuales'],
        ['resultados/consolidacion/generador_smoke_resumen.json','Resumen y cambio de criterio COLLATE'],
        ['resultados/consolidacion/pruebas_automaticas.json','Resultados de pruebas sin rutas privadas'],
        ['resultados/consolidacion/integracion.json','Ocho comprobaciones y tiempos sin datos personales'],
        ['resultados/consolidacion/entorno.json','Hardware, versiones y hashes de modelos'],
        ['data/demo/esquema_y_datos.sql','Inicializador de datos ficticios'],
        ['docs/informe.pdf','Anexo estadístico previo, conservado como histórico'],
        ['docs/matriz-entrega.md','Correspondencia ampliada con la actividad'],
        ['.github/workflows/ci.yml','Restauración, compilación y pruebas automáticas']],[235,BODY-235])
    para('Para evaluar la entrega, primero se verifica la compilación y el conjunto de pruebas, después la ejecución local con fuentes autorizadas y finalmente la correspondencia de los resultados con las métricas declaradas. Los archivos históricos no se presentan como resultados de la versión actual y las imágenes sintéticas no se usan como prueba del funcionamiento del modelo.')

    OUTPUT.parent.mkdir(parents=True,exist_ok=True)
    doc=Doc(str(OUTPUT),pagesize=A4,leftMargin=2.7*cm,rightMargin=2.5*cm,topMargin=2.5*cm,bottomMargin=2.3*cm,
            title='Avance consolidado SynerGy RAG Inventario',author='Galo Andrés Estrella y Wilian Pepinos',subject='Prototipo y avance de tesis al 29 de octubre de 2026')
    doc.addPageTemplates(PageTemplate(id='Academic',frames=[Frame(doc.leftMargin,doc.bottomMargin,doc.width,doc.height,id='body')],onPage=footer))
    doc.multiBuild(story)
    print(OUTPUT)

if __name__=='__main__':main()
