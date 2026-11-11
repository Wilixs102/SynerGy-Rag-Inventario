'use strict';
const $ = id => document.getElementById(id);
let respuesta = null;
let modoBusqueda = 'inventario';
let preguntaActual = '';
let ocupado = false;
let controlador = null;
let reloj = null;
let inicio = 0;
let toastTimer;
const historialClave = 'synergy.historial.v1';
let historial = [];
let historialPersistente = true;
try { const datos = JSON.parse(sessionStorage.getItem(historialClave) || '[]'); if (Array.isArray(datos)) historial = datos.filter(x => x && typeof x.pregunta === 'string' && Number.isFinite(x.ms) && x.ms >= 0 && Number.isFinite(x.fecha)).slice(-50); } catch { historialPersistente = false; }
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
  $('nueva').disabled = ocupado;
  for (const modo of ['inventario', 'actas']) $('modo-' + modo).disabled = ocupado || anexos();
  $('cancelar').hidden = !ocupado;
  sugerencias.forEach(b => { b.disabled = ocupado || anexos(); });
  $('loading').hidden = !ocupado;
  $('resultados').setAttribute('aria-busy', String(ocupado));
}
function seleccionarModo(modo) {
  modoBusqueda = modo;
  const actas = modo === 'actas';
  for (const valor of ['inventario', 'actas']) {
    const boton = $('modo-' + valor);
    boton.setAttribute('aria-pressed', String(valor === modo));
    boton.className = 'button ' + (valor === modo ? 'button-primary' : 'button-quiet');
  }
  $('b').querySelector('span').textContent = actas ? 'Buscar actas por significado' : 'Consultar inventario';
  $('modo-ayuda').textContent = actas
    ? 'Describe un tema: responsabilidad de custodia, cuidado o devolución. Recupera actas por similitud; no calcula stock ni asignaciones actuales.'
    : 'Consulta equipos, modelos, asignaciones y totales actuales.';
  $('q').placeholder = actas ? 'Por ejemplo: responsabilidad por daños y cuidado de los equipos' : 'Por ejemplo: Muéstrame los equipos asignados a Nombre Apellido';
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
  idacta: 'Código de acta', numeroacta: 'Número de acta', tipoacta: 'Tipo de acta', fechaacta: 'Fecha de acta', distanciacoseno: 'Distancia coseno (menor es mejor)', textorelacionado: 'Texto relacionado'
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
function valorEquipo(fila, nombre) {
  const clave = Object.keys(fila).find(c => normalizar(c) === normalizar(nombre));
  return clave === undefined ? null : fila[clave];
}
function tipoDispositivo(fila) {
  const categoria = String(valorEquipo(fila, 'NombreCategoria') ?? '').trim();
  const descripcion = String(valorEquipo(fila, 'Descripcion') ?? '');
  if (normalizar(categoria) === 'laptop' || (normalizar(categoria) === 'computador' && /laptop/i.test(descripcion))) return 'Laptop';
  if (normalizar(categoria) === 'periferico' && /teclado/i.test(descripcion) && /mouse/i.test(descripcion)) return 'Kit de teclado y mouse';
  return categoria || 'Dispositivo';
}
function detalleDispositivos(filas) {
  const zona = $('equipos-detalle'); zona.replaceChildren();
  const esDetalle = filas.length > 0 && filas.every(f => valorEquipo(f, 'IdEquipo') !== null &&
    Object.keys(f).some(c => ['nombremodelo', 'nombrecategoria', 'descripcion'].includes(normalizar(c))));
  zona.hidden = !esDetalle;
  $('tabla-detalle').hidden = !filas.length;
  $('tabla-detalle').open = !esDetalle;
  if (!esDetalle) return;
  zona.append(texto('h3', 'Dispositivos encontrados (' + filas.length + ')', 'device-section-title'));
  const grid = texto('div', '', 'device-grid');
  filas.forEach(fila => {
    const card = texto('article', '', 'device-card');
    const visible = campo => anexos() && sensible(campo, valorEquipo(fila, campo)) ? 'Dato oculto' : formato(valorEquipo(fila, campo));
    card.append(texto('p', tipoDispositivo(fila), 'device-type'));
    card.append(texto('h4', ['NombreMarca', 'NombreModelo'].filter(c => valorEquipo(fila,c)).map(visible).join(' · ') || 'Marca y modelo no registrados', 'device-model'));
    card.append(texto('p', 'Serie: ' + (anexos() ? 'Dato oculto' : formato(valorEquipo(fila, 'Serie'))), 'device-subtitle'));
    card.append(texto('p', valorEquipo(fila, 'Descripcion') ? visible('Descripcion') : 'Sin descripción registrada', 'device-description'));
    const estado = valorEquipo(fila, 'IdEstadoAsignacion');
    card.append(texto('span', ({1:'Disponible',2:'Asignado',5:'En bodega',8:'De baja',9:'Dañado',10:'Extraviado',11:'Robado'})[estado] || 'Estado: ' + formato(estado), 'device-state'));
    const details = document.createElement('details');
    details.append(texto('summary', 'Ver todos los datos del equipo'));
    const dl = document.createElement('dl');
    Object.entries(fila).forEach(([clave, valor]) => {
      dl.append(texto('dt', etiquetas[normalizar(clave)] ?? clave));
      dl.append(texto('dd', anexos() && sensible(clave, valor) ? 'Dato oculto' : formato(valor)));
    });
    details.append(dl); card.append(details); grid.append(card);
  });
  zona.append(grid);
}
function tabla(filas) {
  detalleDispositivos(filas);
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
  renderHistorial();
  $('empty').hidden = !!respuesta || ocupado;
  $('response').hidden = !respuesta;
  if (!respuesta) { estado(ocupado ? 'Procesando' : 'Sin consulta'); return; }
  const d = respuesta;
  const filas = Array.isArray(d.filas) ? d.filas : [];
  const contexto = Array.isArray(d.contexto) ? d.contexto : [];
  const aclaracion = !!d.requiereAclaracion;
  const esActas = d.metodoGeneracion === 'busqueda_vectorial_actas';
  const vector = d.busquedaVectorial;
  $('vector-evidence').hidden = !vector;
  $('vector-evidence').replaceChildren();
  if (vector) {
    $('vector-evidence').append(texto('h3', 'Búsqueda vectorial ejecutada en SQL Server'));
    $('vector-evidence').append(texto('p', `${vector.tabla} · ${vector.versionCatalogo} · ${vector.modelo} · ${vector.dimensiones} dimensiones`));
    $('vector-evidence').append(texto('p', `${vector.funcion}('${vector.metrica}', Embedding, @consulta) · búsqueda ${vector.estrategia} · máximo ${vector.topK} coincidencias · distancia máxima ${vector.distanciaMaxima}`));
    $('vector-evidence').append(texto('p', 'Una distancia menor indica mayor cercanía semántica. No es un porcentaje de confianza. Las actas históricas no demuestran una asignación actual.'));
  }
  $('contexto').open = esActas;
  $('resultados-titulo').textContent = esActas ? 'Actas relacionadas por significado' : 'Resultados de tu consulta';
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
  $('metric-time').textContent = d.esDemostracion ? 'Simulado' : Number.isFinite(d.tiempoTotalMs ?? d.latenciaMs) ? segundos(d.tiempoTotalMs ?? d.latenciaMs) : '—';
  document.querySelector('.source-note').textContent = d.esDemostracion
    ? 'Datos ficticios para mostrar la interfaz. Sin ejecución del pipeline de inteligencia artificial.'
    : esActas ? 'Fuente: texto de actas activas. Los resultados están ordenados por distancia coseno; no se utiliza el generador SQL para esta búsqueda.' : 'Los resultados provienen de la consulta al inventario. El contexto recuperado ayuda a interpretar tu pregunta.';
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
  const modoSolicitud = modoBusqueda === 'actas' || /\bactas?\b/i.test(pregunta) ? 'actas' : 'inventario';
  const comienzo = performance.now();
  const solicitud = new AbortController(); controlador = solicitud;
  carga(true); render();
  try {
    const r = await fetch('/api/preguntas', { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ pregunta, modo: modoBusqueda }), signal: solicitud.signal });
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
    if (controlador === solicitud) {
      respuesta.tiempoTotalMs = performance.now() - comienzo;
      registrarBusqueda(pregunta, modoSolicitud, respuesta);
      controlador = null; carga(false); render();
    }
  }
});
$('q').addEventListener('input', () => $('q').setCustomValidity(''));
$('q').addEventListener('keydown', e => { if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') { e.preventDefault(); $('f').requestSubmit(); } });
$('cancelar').addEventListener('click', () => controlador?.abort());
$('anexos').addEventListener('change', render);
for (const modo of ['inventario', 'actas']) $('modo-' + modo).addEventListener('click', () => seleccionarModo(modo));
sugerencias.forEach(b => b.addEventListener('click', () => { seleccionarModo(/\bactas?\b/i.test(b.dataset.pregunta) ? 'actas' : 'inventario'); $('q').value = b.dataset.pregunta; $('q').setCustomValidity(''); $('q').focus(); }));
$('nueva').addEventListener('click', () => {
  controlador?.abort(); controlador = null; carga(false);
  respuesta = null; preguntaActual = ''; $('q').value = ''; $('q').setCustomValidity('');
  $('sql-details').open = false; $('contexto').open = false;
  render(); if (!anexos()) $('q').focus();
});
document.querySelector('a[href="#guia"]').addEventListener('click', () => { $('guia').open = true; });
$('copiar').addEventListener('click', async () => {
  try { await navigator.clipboard.writeText(sqlVisible(respuesta?.sql ?? '')); notificar('Consulta SQL copiada.'); }
  catch { notificar('No se pudo copiar automáticamente. Selecciona el texto del SQL.'); }
});
$('imprimir').addEventListener('click', () => window.print());
function segundos(ms) { return (ms / 1000).toLocaleString('es-EC', {minimumFractionDigits: 2, maximumFractionDigits: 2}) + ' s'; }
function guardarHistorial() {
  try { sessionStorage.setItem(historialClave, JSON.stringify(historial)); historialPersistente = true; }
  catch { historialPersistente = false; }
}
function registrarBusqueda(pregunta, modo, resultado) {
  if (resultado.esDemostracion) return;
  const estado = resultado.cancelada ? 'Cancelada' : resultado.error ? 'Error' : resultado.requiereAclaracion ? 'Por aclarar' : 'Completada';
  const ruta = resultado.metodoGeneracion === 'busqueda_vectorial_actas' ? 'Vectorial · actas'
    : resultado.metodoGeneracion === 'consulta_validada' ? 'SQL validado'
    : resultado.metodoGeneracion === 'modelo_local' ? (resultado.sqlReutilizado ? 'SQL reutilizado' : 'Modelo local')
    : modo === 'actas' ? 'Actas' : 'Inventario';
  historial.push({fecha:Date.now(), pregunta, ruta, estado, ms:resultado.tiempoTotalMs, filas:resultado.filas?.length ?? 0, actas:modo === 'actas' || resultado.metodoGeneracion === 'busqueda_vectorial_actas'});
  historial = historial.slice(-50); guardarHistorial();
}
function renderHistorial() {
  const completadas = historial.filter(x => x.estado === 'Completada');
  $('dash-total').textContent = historial.length;
  $('dash-ok').textContent = completadas.length;
  $('dash-media').textContent = completadas.length ? segundos(completadas.reduce((s,x) => s+x.ms,0)/completadas.length) : '—';
  $('dash-actas').textContent = historial.filter(x => x.actas).length;
  $('historial-storage').hidden = historialPersistente;
  $('dash-empty').hidden = historial.length > 0;
  $('dash-table').hidden = !historial.length;
  $('dash-rows').replaceChildren();
  for (const x of [...historial].reverse()) {
    const tr = document.createElement('tr');
    for (const valor of [new Date(x.fecha).toLocaleString('es-EC'), anexos() ? 'Pregunta oculta' : x.pregunta, x.ruta, x.estado, segundos(x.ms), x.filas]) tr.append(texto('td', String(valor)));
    $('dash-rows').append(tr);
  }
  $('dash-chart').replaceChildren();
  const ultimas = historial.slice(-10), max = Math.max(1,...ultimas.map(x=>x.ms));
  ultimas.forEach((x,i) => {
    const fila = texto('div','','duration-row');
    const etiqueta = 'Consulta ' + (historial.length - ultimas.length + i + 1);
    const pista = texto('div','','duration-track');
    const barra = texto('span','','duration-bar'); barra.style.width = Math.max(1,x.ms/max*100) + '%'; pista.append(barra);
    fila.append(texto('span',etiqueta),pista,texto('span',segundos(x.ms) + ' · ' + x.estado));
    $('dash-chart').append(fila);
  });
}
$('limpiar-historial').addEventListener('click', () => { historial = []; guardarHistorial(); renderHistorial(); });
render();
