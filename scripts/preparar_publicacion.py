"""Seudonimiza identidades del conjunto histórico sin modificar las métricas."""
import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--backup-dir", type=Path)
    args = parser.parse_args()
    preguntas = ROOT / "tests/SynerGy.RAG.Evaluacion/preguntas.json"
    contenido = preguntas.read_text(encoding="utf-8-sig")
    originales = json.loads(contenido)
    cambios = {}
    for p in originales:
        for mail in re.findall(r"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", p["pregunta"]):
            if mail.endswith("@example.test"):
                continue
            cambios[mail] = f"persona{p['id']}@example.test"
    rutas = [preguntas, ROOT / "resultados/resultados.csv"]
    editados = []
    for ruta in rutas:
        texto = ruta.read_text(encoding="utf-8-sig")
        nuevo = texto
        for original, sustituto in cambios.items():
            parte = original.split("@")[0]
            reemplazo = sustituto.split("@")[0]
            nuevo = nuevo.replace(original, sustituto)
            nuevo = nuevo.replace(parte.replace(".", "\\\\."), reemplazo)
            nuevo = nuevo.replace(parte.replace(".", "\\."), reemplazo)
            nuevo = nuevo.replace(parte, reemplazo)
        if nuevo != texto:
            if args.backup_dir:
                destino = args.backup_dir / ruta.relative_to(ROOT)
                destino.parent.mkdir(parents=True, exist_ok=True)
                if not destino.exists():
                    shutil.copy2(ruta, destino)
            ruta.write_text(nuevo, encoding="utf-8")
            editados.append(str(ruta.relative_to(ROOT)))
    manifiesto = ROOT / "resultados/consolidacion/privacidad_historico.json"
    manifiesto.parent.mkdir(parents=True, exist_ok=True)
    anterior = json.loads(manifiesto.read_text(encoding="utf-8")) if manifiesto.exists() else {}
    manifiesto.write_text(json.dumps({
        "fecha_corte": "2026-10-29",
        "transformacion": "Sustitución de correos y sus patrones por identidades example.test. No se regeneraron las 270 respuestas.",
        "metricas_historicas": "Se conservan los indicadores sql_valida, componentes_ok y latencia_ms originales.",
        "identidades_sustituidas": anterior.get("identidades_sustituidas", 0) + len(cambios),
        "archivos": [{"ruta": str(p.relative_to(ROOT)), "sha256_publicado": hashlib.sha256(p.read_bytes()).hexdigest()} for p in rutas]
    }, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"identidades_sustituidas": len(cambios), "archivos_editados": editados}, ensure_ascii=False))

if __name__ == "__main__":
    main()
