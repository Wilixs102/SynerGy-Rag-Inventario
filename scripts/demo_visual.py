"""Demo de interfaz con datos ficticios. No llama a SQL Server ni a Ollama."""
import argparse
import json
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
WEB=ROOT/'SynerGy-Rag-Inventario/wwwroot'

def respuesta(pregunta):
    q=pregunta.lower()
    datos={'pregunta':pregunta,'sql':None,'filas':[],'error':None,'latenciaMs':0,'contexto':[],
           'esDemostracion':True,'metodoGeneracion':'demostracion_sintetica',
           'mensaje':'Demostración visual con datos ficticios. No se ejecutaron SQL, embeddings ni un modelo.'}
    if 'acta' in q:
        datos['filas']=[{'NumeroActa':f'DEMO-2026-{i:04}','tipo_acta':'Entrega','FechaActa':'2026-10-01T00:00:00'} for i in range(1,4)]
        return datos
    if any(x in q for x in ['total','cuánt','cuant','cantidad']):
        datos['filas']=[{'tipo_equipo':'mouses' if 'mouse' in q else 'laptops','total':150 if 'mouse' in q else 180}]
        filtro = "NombreCategoria = 'MOUSE' OR (NombreCategoria = 'PERIFÉRICO' AND Descripcion LIKE '%Mouse%')" if 'mouse' in q else "NombreCategoria = 'LAPTOP'"
        datos['sql']=f'SELECT COUNT(DISTINCT IdEquipo) AS total FROM dbo.VistaEquipo WHERE {filtro}'
        return datos
    datos['filas']=[{'IdEquipo':i,'Serie':f'DEMO-{i:06}','NombreMarca':'MARCA DEMO','NombreModelo':f'MODELO-DEMO-{i}',
                     'NombreCategoria':'LAPTOP','usuario_custodio_activo':'persona01@example.test'} for i in range(1,5)]
    datos['sql']="SELECT IdEquipo, Serie, NombreMarca, NombreModelo, NombreCategoria FROM dbo.VistaEquipo WHERE usuario_custodio_activo = 'persona01@example.test'"
    return datos

class Handler(SimpleHTTPRequestHandler):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,directory=str(WEB),**kwargs)
    def do_GET(self):
        if self.path.split('?')[0] in ('/','/index.html'):
            html=(WEB/'index.html').read_text(encoding='utf-8-sig').replace('<body>','<body data-demo="true">')
            aviso='<div class="notice notice-warning" role="note"><strong>DEMO SINTÉTICA</strong> · Datos ficticios. Sin conexión a SQL Server ni Ollama. Esta vista no es evidencia de validación del modelo.</div>'
            html=html.replace('<main id="consulta">','<main id="consulta">'+aviso)
            contenido=html.encode('utf-8')
            self.send_response(200);self.send_header('Content-Type','text/html; charset=utf-8');self.send_header('Content-Length',len(contenido));self.end_headers();self.wfile.write(contenido)
        else:
            super().do_GET()
    def do_POST(self):
        if self.path!='/api/preguntas':self.send_error(404);return
        try:
            largo=int(self.headers.get('Content-Length','0'))
            if largo>16000:raise ValueError('Solicitud demasiado grande')
            pregunta=json.loads(self.rfile.read(largo)).get('pregunta','')
            if not isinstance(pregunta,str) or not pregunta.strip():raise ValueError('La pregunta es obligatoria')
            datos=respuesta(pregunta)
            cuerpo=json.dumps(datos,ensure_ascii=False).encode('utf-8')
            self.send_response(200);self.send_header('Content-Type','application/json; charset=utf-8');self.send_header('Content-Length',len(cuerpo));self.end_headers();self.wfile.write(cuerpo)
        except (ValueError,json.JSONDecodeError):
            self.send_error(400,'Solicitud invalida')
    def log_message(self,*args):pass

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--puerto',type=int,default=5226);args=parser.parse_args()
    print(f'Demo sintética en http://127.0.0.1:{args.puerto} - sin SQL/Ollama',flush=True)
    ThreadingHTTPServer(('127.0.0.1',args.puerto),Handler).serve_forever()
