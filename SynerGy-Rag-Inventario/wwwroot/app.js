'use strict';
const $ = id => document.getElementById(id);
let respuesta = null;
let preguntaActual = '';
let ocupado = false;
let controlador = null;
let reloj = null;
let inicio = 0;
let toastTimer;
const anexos = () => $('anexos').checked;
const sugerencias = [...document.querySelectorAll('[data-pregunta]')];

function icono(nombre) {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('class', 'icon');
  svg.setAttribute('aria-hidden', 'true');
  const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
  use.setAttribute('href', '#i-' + nombre);
  svg.appendChild(use);
  return svg;
}
function texto(tag, contenido, clase) {
  const nodo = document.createElement(tag);
  nodo.textContent = contenido;
  if (clase) nodo.className = clase;
  return nodo;
}
function estado(etiqueta, tipo = 'neutral') {
  $('result-status').className = 'status status-' + tipo;
  $('result-status').replaceChildren(document.createElement('span'), document.createTextNode(etiqueta));
}
function notificar(mensaje) {
  clearTimeout(toastTimer);
  $('toast').textContent = mensaje;
  $('toast').hidden = false;
  toastTimer = setTimeout(() => { $('toast').hidden = true; }, 4500);
}
function actualizarControles() {
  $('q').disabled = ocupado || anexos();
  $('q').hidden = anexos();
  $('question-mask').hidden = !anexos();
  $('annex-notice').hidden = !anexos();
  $('b').disabled = ocupado || anexos();
  $('cancelar').hidden = !ocupado;
  sugerencias.forEach(b => { b.disabled = ocupado || anexos(); });
  $('loading').hidden = !ocupado;
  $('resultados').setAttribute('aria-busy', String(ocupado));
}
function carga(valor) {
  ocupado = valor;
  clearInterval(reloj);
  if (valor) {
    inicio = performance.now();
    $('elapsed').textContent = '0 s';
    reloj = setInterval(() => { $('elapsed').textContent = Math.floor((performance.now() - inicio) / 1000) + ' s'; }, 1000);
  }
  actualizarControles();
}

const etiquetas = {
  idequipo: 'Código de equipo', serie: 'Serie', nombremarca: 'Marca', nombremodelo: 'Modelo',
  nombrecategoria: 'Categoría', descripcion: 'Descripción', usuariocustodioactivo: 'Custodio',
  valorcompra: 'Valor de compra', fechacompra: 'Fecha de compra', fechaasignacion: 'Fecha de asignación',
  fechadevolucion: 'Fecha de devolución', idestadoasignacion: 'Estado de asignación',
  numerofactura: 'Factura', idubicacion: 'Ubicación', total: 'Total', tipoequipo: 'Tipo de equipo',
  idacta: 'Código de acta', numeroacta: 'Número de acta', tipoacta: 'Tipo de acta', fechaacta: 'Fecha de acta'
};
function normalizar(clave) { return clave.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^a-z0-9]/gi, '').toLowerCase(); }
function sensible(clave, valor) {
  return /serie|correo|custodio|usuario|email|identificaci|factura|nombrecompleto|idequipo|idacta|numeroacta|^qr$/.test(normalizar(clave)) ||
    (typeof valor === 'string' && valor.includes('@'));
}
function formato(valor) {
  if (valor === null || valor === undefined || valor === '') return '—';
  if (typeof valor === 'number') return new Intl.NumberFormat('es-EC', { maximumFractionDigits: 2 }).format(valor);
  if (typeof valor === 'boolean') return valor ? 'Sí' : 'No';
  if (typeof valor === 'object') return JSON.stringify(valor);
  if (/^\d{4}-\d{2}-\d{2}T/.test(valor) && !Number.isNaN(Date.parse(valor)))
    return new Date(valor).toLocaleDateString('es-EC', { timeZone: 'UTC' });
  return String(valor);
}
function sqlVisible(sql) { return anexos() ? sql.replace(/N?'(?:''|[^'])*'/gi, "'[dato oculto]'") : sql; }
function tabla(filas) {
  const head = $('t').querySelector('thead');
  const body = $('t').querySelector('tbody');
  head.replaceChildren(); body.replaceChildren();
  const columnas = [...new Set(filas.flatMap(f => Object.keys(f)))];
  const cabecera = document.createElement('tr');
  columnas.forEach(c => { const th = texto('th', etiquetas[normalizar(c)] ?? c); th.scope = 'col'; cabecera.appendChild(th); });
  head.appendChild(cabecera);
  filas.forEach(fila => {
    const tr = document.createElement('tr');
    columnas.forEach(c => {
      const oculto = anexos() && sensible(c, fila[c]);
      tr.appendChild(texto('td', oculto ? 'Dato oculto' : formato(fila[c]), oculto ? 'table-mask' : ''));
    });
    body.appendChild(tr);
  });
}
function render() {
  actualizarControles();
  $('empty').hidden = !!respuesta || ocupado;
  $('response').hidden = !respuesta;
  if (!respuesta) { estado(ocupado ? 'Procesando' : 'Sin consulta'); return; }
  const d = respuesta;
  const filas = Array.isArray(d.filas) ? d.filas : [];
  const contexto = Array.isArray(d.contexto) ? d.contexto : [];
  const aclaracion = !!d.requiereAclaracion;
  estado(d.esDemostracion ? 'Demostración' : d.cancelada ? 'Cancelada' : d.error ? 'Revisar consulta' : aclaracion ? 'Por aclarar' : 'Completada',
    d.cancelada ? 'neutral' : d.error ? 'error' : aclaracion ? 'warning' : 'success');
  $('answered-question').textContent = anexos() ? 'Consulta de inventario · vista para documentación' : preguntaActual;
  $('error').hidden = !d.error;
  $('error').textContent = anexos() && d.error ? 'La consulta no pudo completarse. Sal de la vista para anexos para revisar el detalle.' : d.error ?? '';
  $('mensaje').hidden = !d.mensaje;
  $('mensaje').textContent = anexos() && d.mensaje
    ? (aclaracion ? 'Se requiere aclarar la consulta antes de continuar.' : d.cancelada ? 'Consulta cancelada.' : d.custodio ? 'Consulta del custodio seleccionado.' : d.mensaje)
    : d.mensaje ?? '';
  $('candidatos').replaceChildren();
  for (const [i, c] of (d.candidatos ?? []).entries()) {
    const li = document.createElement('li');
    const opcion = document.createElement('button');
    opcion.type = 'button'; opcion.className = 'candidate-button'; opcion.disabled = anexos();
    const datos = document.createElement('span');
    datos.append(texto('strong', anexos() ? `Colaborador ${String(i + 1).padStart(2, '0')}` : c.nombre),
      texto('small', anexos() ? 'Correo oculto' : c.correo));
    opcion.append(icono('users'), datos, icono('arrow'));
    opcion.addEventListener('click', () => {
      $('q').value = c.preguntaSugerida || `Muéstrame los equipos asignados a ${c.correo}`;
      $('q').focus();
      $('mensaje').textContent = 'Compañero seleccionado. Pulsa Consultar inventario para continuar.';
      $('candidatos').replaceChildren();
    });
    li.appendChild(opcion); $('candidatos').appendChild(li);
  }
  $('metrics').hidden = aclaracion || !!d.cancelada;
  $('metric-rows').textContent = filas.length;
  $('metric-time').textContent = d.esDemostracion ? 'Simulado' : Number.isFinite(d.latenciaMs) ? (d.latenciaMs / 1000).toLocaleString('es-EC', { maximumFractionDigits: 1 }) + ' s' : '—';
  document.querySelector('.source-note').textContent = d.esDemostracion
    ? 'Datos ficticios para mostrar la interfaz. Sin ejecución del pipeline de inteligencia artificial.'
    : 'Los resultados provienen de la consulta al inventario. El contexto recuperado ayuda a interpretar tu pregunta.';
  $('metric-context').textContent = contexto.length;
  $('truncado').hidden = !d.truncado;
  $('no-rows').hidden = filas.length > 0 || !!d.error || aclaracion || !!d.cancelada;
  $('table-wrap').hidden = !filas.length;
  tabla(filas);
  $('evidence').hidden = !d.sql && !contexto.length;
  $('sql-details').hidden = !d.sql;
  $('sql').querySelector('code').textContent = sqlVisible(d.sql ?? '');
  document.querySelector('.sql-toolbar > span').textContent = d.esDemostracion ? 'Ejemplo visual sin ejecución SQL' : d.metodoGeneracion === 'consulta_validada'
    ? 'Consulta validada del inventario' : 'Propuesta del modelo';
  $('contexto').hidden = !contexto.length;
  $('source-count').textContent = contexto.length + ' fragmentos';
  $('fuentes').replaceChildren();
  for (const c of contexto) {
    const li = document.createElement('li');
    const top = document.createElement('div'); top.className = 'source-top';
    top.append(texto('strong', c.id), texto('span', 'Distancia coseno ' + Number(c.distancia).toFixed(3)));
    li.append(top, texto('p', anexos() ? String(c.contenido).replace(/[\w.+-]+@[\w.-]+/g, '[correo oculto]') : c.contenido));
    $('fuentes').appendChild(li);
  }
}

$('f').addEventListener('submit', async e => {
  e.preventDefault();
  if (ocupado || anexos()) return;
  const pregunta = $('q').value.trim();
  if (!pregunta) { $('q').setCustomValidity('Escribe una pregunta para consultar el inventario.'); $('q').reportValidity(); return; }
  preguntaActual = pregunta;
  respuesta = null;
  const solicitud = new AbortController(); controlador = solicitud;
  carga(true); render();
  try {
    const r = await fetch('/api/preguntas', { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ pregunta }), signal: solicitud.signal });
    const d = await r.json().catch(() => { throw new Error('El servidor devolvió una respuesta no válida.'); });
    if (!r.ok) throw new Error(d.error ?? d.mensaje ?? d.title ?? 'No se pudo completar la consulta.');
    if (controlador !== solicitud) return;
    respuesta = d;
  } catch (err) {
    if (controlador !== solicitud) return;
    respuesta = err.name === 'AbortError'
      ? { filas: [], contexto: [], cancelada: true, mensaje: 'Consulta cancelada. Puedes escribir otra pregunta.' }
      : { filas: [], contexto: [], error: err.message || 'No se pudo conectar con el servidor.' };
  } finally {
    if (controlador === solicitud) { controlador = null; carga(false); render(); }
  }
});
$('q').addEventListener('input', () => $('q').setCustomValidity(''));
$('q').addEventListener('keydown', e => { if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') { e.preventDefault(); $('f').requestSubmit(); } });
$('cancelar').addEventListener('click', () => controlador?.abort());
$('anexos').addEventListener('change', render);
sugerencias.forEach(b => b.addEventListener('click', () => { $('q').value = b.dataset.pregunta; $('q').setCustomValidity(''); $('q').focus(); }));
$('nueva').addEventListener('click', () => {
  controlador?.abort(); controlador = null; carga(false);
  respuesta = null; preguntaActual = ''; $('q').value = ''; $('q').setCustomValidity('');
  $('sql-details').open = false; $('contexto').open = false;
  render(); if (!anexos()) $('q').focus();
});
document.querySelector('a[href="#guia"]').addEventListener('click', () => { $('guia').open = true; });
$('ver-evidencia').addEventListener('click', () => {
  if (!respuesta?.sql && !respuesta?.contexto?.length) { notificar('Realiza una consulta para revisar el SQL y el contexto recuperado.'); return; }
  $('sql-details').open = !!respuesta.sql;
  $('contexto').open = !!respuesta.contexto?.length;
  $('evidence').scrollIntoView({ behavior: 'smooth', block: 'start' });
});
$('copiar').addEventListener('click', async () => {
  try { await navigator.clipboard.writeText(sqlVisible(respuesta?.sql ?? '')); notificar('Consulta SQL copiada.'); }
  catch { notificar('No se pudo copiar automáticamente. Selecciona el texto del SQL.'); }
});
$('imprimir').addEventListener('click', () => window.print());
render();
