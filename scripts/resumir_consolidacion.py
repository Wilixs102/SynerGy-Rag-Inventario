"""Resume evidencias sin ejecutar modelos ni convertir proxies en exactitud real."""
import csv
import json
import re
import statistics
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'resultados/consolidacion'

def main():
    preguntas_path=ROOT/'tests/SynerGy.RAG.Evaluacion/preguntas.json'
    preguntas=json.loads(preguntas_path.read_text(encoding='utf-8-sig'))
    ajustadas=0
    for p in preguntas:
        nuevos=[r.replace(r'(NombreCategoria|Descripcion)\s+LIKE',r'(NombreCategoria|Descripcion)(?:\s+COLLATE\s+\w+)?\s+LIKE') for r in p['requeridos']]
        if nuevos!=p['requeridos']:ajustadas+=1
        p['requeridos']=nuevos
    preguntas_path.write_text(json.dumps(preguntas,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    por_id={str(p['id']):p for p in preguntas}
    ruta=OUT/'generador_smoke.csv'
    with ruta.open(encoding='utf-8-sig',newline='') as f:
        lector=csv.DictReader(f);campos=lector.fieldnames;filas=list(lector)
    for f in filas:
        p=por_id[f['id_pregunta']]
        ok=f['sql_valida']=='1' and all(re.search(r,f['sql'],re.I) for r in p['requeridos']) and not any(re.search(r,f['sql'],re.I) for r in p['prohibidos'])
        f['componentes_ok']='1' if ok else '0'
    with ruta.open('w',encoding='utf-8',newline='') as f:
        escritor=csv.DictWriter(f,fieldnames=campos);escritor.writeheader();escritor.writerows(filas)
    resumen={}
    for nombre in dict.fromkeys(f['config'] for f in filas):
        grupo=[f for f in filas if f['config']==nombre]
        latencias=[int(f['latencia_ms']) for f in grupo]
        resumen[nombre]={'intentos':len(grupo),'aceptadas_parser':sum(int(f['sql_valida']) for f in grupo),
            'componentes_ok':sum(int(f['componentes_ok']) for f in grupo),'proporcion_componentes':statistics.mean(int(f['componentes_ok']) for f in grupo),
            'latencia_mediana_ms':statistics.median(latencias),'latencia_min_ms':min(latencias),'latencia_max_ms':max(latencias)}
    datos={'fecha_corte':'2026-10-29','preguntas':3,'repeticiones':1,'generaciones':len(filas),
        'alcance':'Prueba de regresión sobre tres preguntas del conjunto de desarrollo. No es un conjunto independiente ni una evaluación de ejecución SQL.',
        'criterio':'Se aceptó COLLATE entre la columna y LIKE para reconocer SQL equivalente; no se modificaron las generaciones ni sus latencias. Los indicadores históricos de las 270 generaciones se conservaron.',
        'configs':resumen}
    (OUT/'generador_smoke_resumen.json').write_text(json.dumps(datos,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'preguntas_con_compatibilidad_collate':ajustadas,'resumen':resumen},ensure_ascii=False))

if __name__=='__main__':main()
