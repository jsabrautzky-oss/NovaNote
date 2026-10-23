'use strict';

const log = document.getElementById('log');
const busyBox = document.getElementById('busy');
const jumpBtn = document.getElementById('jump');

const wv = (window.chrome && window.chrome.webview) ? window.chrome.webview : null;
const msgs = new Map();
const tools = new Map();
const dirty = new Set();
const lastRender = new Map();
let raf = 0;
let stick = true;

marked.use({ gfm: true, breaks: false });

const STATUS = {
  running: '⏳ Läuft…',
  pending: '🟡 Warten auf Bestätigung',
  done: '✅ Abgeschlossen',
  error: '❌ Fehler',
  dismissed: '🚫 Verworfen'
};

function send(payload) {
  if (wv) wv.postMessage(payload);
}

function textOf(value) {
  if (value === null || value === undefined) return '';
  if (typeof value === 'string') return value;
  return JSON.stringify(value, null, 2);
}

function prettyJson(raw) {
  if (!raw) return '{}';
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return String(raw);
  }
}

function shortArgs(raw) {
  if (!raw) return '{}';
  try {
    const obj = JSON.parse(raw);
    if (obj && typeof obj === 'object') {
      const parts = Object.keys(obj).map(k => {
        let v = typeof obj[k] === 'string' ? obj[k].replace(/\r?\n/g, ' ') : JSON.stringify(obj[k]);
        if (v.length > 60) v = v.slice(0, 60) + '…';
        return `${k}=${v}`;
      });
      return parts.length ? parts.join(', ') : '{}';
    }
  } catch {
  }
  const flat = String(raw).replace(/\r?\n/g, ' ');
  return flat.length > 90 ? flat.slice(0, 90) + '…' : flat;
}

function fenceGuard(md) {
  const fences = (md.match(/```/g) || []).length;
  return fences % 2 === 1 ? md + '\n```' : md;
}

function sanitize(html) {
  const doc = new DOMParser().parseFromString(html, 'text/html');
  doc.querySelectorAll('script,style,iframe,object,embed,link,meta,base,form').forEach(n => n.remove());
  doc.querySelectorAll('*').forEach(n => {
    for (const attr of Array.from(n.attributes)) {
      const name = attr.name.toLowerCase();
      const value = attr.value.trim().toLowerCase();
      if (name.startsWith('on') || name === 'src' || name === 'srcset' ||
          value.startsWith('javascript:') || value.startsWith('data:text/html')) {
        n.removeAttribute(attr.name);
      }
    }
  });
  return doc.body.innerHTML;
}

function makeButton(label, cls, handler) {
  const b = document.createElement('button');
  b.type = 'button';
  b.className = cls ? `btn ${cls}` : 'btn';
  b.textContent = label;
  b.addEventListener('click', handler);
  return b;
}

function flash(button, label) {
  const old = button.textContent;
  button.textContent = label;
  button.disabled = true;
  setTimeout(() => {
    button.textContent = old;
    button.disabled = false;
  }, 900);
}

function copyText(value) {
  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard.writeText(value).catch(() => fallbackCopy(value));
    return;
  }
  fallbackCopy(value);
}

function fallbackCopy(value) {
  const ta = document.createElement('textarea');
  ta.value = value;
  ta.style.position = 'fixed';
  ta.style.left = '-9999px';
  document.body.appendChild(ta);
  ta.select();
  try {
    document.execCommand('copy');
  } catch {
  }
  ta.remove();
}

function decorateCode(root) {
  root.querySelectorAll('pre').forEach(pre => {
    const code = pre.querySelector('code');
    if (code && !code.dataset.hl) {
      hljs.highlightElement(code);
      code.dataset.hl = '1';
    }
    if (pre.parentElement && pre.parentElement.classList.contains('code')) return;
    const lang = code ? ((code.className || '').match(/language-([\w+-]+)/) || [])[1] || '' : '';
    const wrap = document.createElement('div');
    wrap.className = 'code';
    const head = document.createElement('div');
    head.className = 'codehead';
    const label = document.createElement('span');
    label.className = 'lang';
    label.textContent = lang || 'code';
    head.appendChild(label);
    head.appendChild(makeButton('Kopieren', '', () => {
      copyText(code ? code.textContent : pre.textContent);
      flash(head.querySelectorAll('.btn')[0], 'Kopiert');
    }));
    head.appendChild(makeButton('In Editor einfügen', 'accent', () => {
      send({ action: 'insertIntoEditor', content: code ? code.textContent : pre.textContent });
      flash(head.querySelectorAll('.btn')[1], 'Eingefügt');
    }));
    pre.parentNode.insertBefore(wrap, pre);
    wrap.appendChild(head);
    wrap.appendChild(pre);
  });
}

function renderMessage(id) {
  const entry = msgs.get(id);
  if (!entry) return;
  if (entry.role === 'info') {
    entry.body.textContent = entry.raw;
    lastRender.set(id, Date.now());
    return;
  }
  entry.body.innerHTML = sanitize(marked.parse(fenceGuard(entry.raw)));
  entry.body.classList.add('md');
  decorateCode(entry.body);
  lastRender.set(id, Date.now());
}

function schedule(id) {
  dirty.add(id);
  if (!raf) raf = requestAnimationFrame(flush);
}

function flush() {
  raf = 0;
  const now = Date.now();
  for (const id of dirty) {
    const last = lastRender.get(id) || 0;
    if (now - last >= 90) renderMessage(id);
  }
  if (dirty.size && raf === 0) raf = requestAnimationFrame(flush);
  scrollDown();
}

function scrollDown() {
  if (stick) log.scrollTop = log.scrollHeight;
  jumpBtn.hidden = stick;
}

function atBottom() {
  return log.scrollHeight - log.scrollTop - log.clientHeight < 60;
}

log.addEventListener('scroll', () => {
  stick = atBottom();
  jumpBtn.hidden = stick;
});

jumpBtn.addEventListener('click', () => {
  stick = true;
  log.scrollTop = log.scrollHeight;
  jumpBtn.hidden = true;
});

function makeImages(images) {
  const grid = document.createElement('div');
  grid.className = 'imgs';
  for (const src of images) {
    const img = document.createElement('img');
    img.className = 'thumb';
    img.src = src;
    img.alt = 'Angehängtes Bild';
    img.loading = 'lazy';
    img.addEventListener('click', () => openLightbox(src));
    grid.appendChild(img);
  }
  return grid;
}

const overlay = document.createElement('div');
overlay.className = 'lightbox';
overlay.hidden = true;
const bigImg = document.createElement('img');
overlay.appendChild(bigImg);
document.body.appendChild(overlay);
overlay.addEventListener('click', () => {
  overlay.hidden = true;
  bigImg.src = '';
});

function openLightbox(src) {
  bigImg.src = src;
  overlay.hidden = false;
}

function closeLightbox() {
  overlay.hidden = true;
  bigImg.src = '';
}

function imageList(value) {
  if (!Array.isArray(value)) return [];
  return value.filter(v => typeof v === 'string' && /^data:image\/[a-z0-9.+-]+;base64,/i.test(v));
}

function addMessage(ev) {
  const id = ev.id;
  const role = ev.role || 'assistant';
  const el = document.createElement('div');
  el.className = `msg ${role}`;
  const images = imageList(ev.images);
  if (images.length) el.appendChild(makeImages(images));
  const body = document.createElement('div');
  body.className = role === 'info' ? 'body' : 'body md';
  el.appendChild(body);
  log.appendChild(el);
  msgs.set(id, { role, body, raw: ev.content || '' });
  renderMessage(id);
  scrollDown();
}

function appendChunk(ev) {
  const entry = msgs.get(ev.id);
  if (!entry) return;
  entry.raw += ev.chunk || '';
  schedule(ev.id);
  scrollDown();
}

function setMessage(ev) {
  const entry = msgs.get(ev.id);
  if (!entry) return;
  entry.raw = ev.content || '';
  renderMessage(ev.id);
  scrollDown();
}

function removeMessage(ev) {
  const entry = msgs.get(ev.id);
  if (!entry) return;
  entry.body.parentNode.remove();
  msgs.delete(ev.id);
}

function addToolCall(ev) {
  const det = document.createElement('details');
  det.className = 'tool';
  const sum = document.createElement('summary');
  const status = document.createElement('span');
  status.className = 'tstatus';
  const name = document.createElement('span');
  name.className = 'tname';
  name.textContent = ev.toolName || 'tool';
  const short = document.createElement('span');
  short.className = 'tshort';
  short.textContent = shortArgs(ev.args);
  sum.appendChild(status);
  sum.appendChild(name);
  sum.appendChild(short);

  const body = document.createElement('div');
  body.className = 'tbody';

  const argsLabel = document.createElement('div');
  argsLabel.className = 'tlabel';
  argsLabel.textContent = 'Arguments';
  const argsPre = document.createElement('pre');
  argsPre.className = 'json';
  argsPre.textContent = prettyJson(ev.args);

  const resLabel = document.createElement('div');
  resLabel.className = 'tlabel';
  resLabel.textContent = 'Result';
  resLabel.hidden = true;
  const resPre = document.createElement('pre');
  resPre.className = 'result';
  resPre.hidden = true;

  const actions = document.createElement('div');
  actions.className = 'tactions';
  actions.hidden = true;
  const confirm = makeButton('Bestätigen', 'accent', () => {
    confirm.disabled = true;
    discard.disabled = true;
    send({ action: 'confirmToolCall', id: ev.id });
  });
  const discard = makeButton('Verwerfen', '', () => {
    confirm.disabled = true;
    discard.disabled = true;
    send({ action: 'discardToolCall', id: ev.id });
  });
  actions.appendChild(confirm);
  actions.appendChild(discard);

  body.appendChild(argsLabel);
  body.appendChild(argsPre);
  body.appendChild(resLabel);
  body.appendChild(resPre);
  body.appendChild(actions);
  det.appendChild(sum);
  det.appendChild(body);
  log.appendChild(det);

  tools.set(ev.id, { det, status, resLabel, resPre, actions, confirm, discard });
  setStatus(ev.id, 'running');
  scrollDown();
}

function setStatus(id, state) {
  const t = tools.get(id);
  if (!t) return;
  t.status.textContent = STATUS[state] || state || '';
  t.det.classList.toggle('error', state === 'error');
  t.actions.hidden = state !== 'pending';
  if (state === 'pending') {
    t.det.open = true;
    t.confirm.disabled = false;
    t.discard.disabled = false;
  }
  scrollDown();
}

function updateToolResult(ev) {
  const t = tools.get(ev.id);
  if (!t) return;
  t.resLabel.hidden = false;
  t.resPre.hidden = false;
  t.resPre.textContent = textOf(ev.result);
  t.resPre.classList.toggle('err', !!ev.isError);
  setStatus(ev.id, ev.isError ? 'error' : 'done');
}

function setBusy(ev) {
  busyBox.hidden = !ev.busy;
  if (ev.busy && stick) scrollDown();
}

function setTheme(ev) {
  document.documentElement.classList.toggle('dark', !!ev.dark);
}

function clearChat() {
  log.innerHTML = '';
  msgs.clear();
  tools.clear();
  dirty.clear();
  lastRender.clear();
  stick = true;
  jumpBtn.hidden = true;
  closeLightbox();
}

function handle(ev) {
  if (!ev || typeof ev !== 'object') return;
  switch (ev.type) {
    case 'addMessage': addMessage(ev); break;
    case 'appendChunk': appendChunk(ev); break;
    case 'setMessage': setMessage(ev); break;
    case 'removeMessage': removeMessage(ev); break;
    case 'addToolCall': addToolCall(ev); break;
    case 'updateToolResult': updateToolResult(ev); break;
    case 'updateToolStatus': setStatus(ev.id, ev.state); break;
    case 'setBusy': setBusy(ev); break;
    case 'setTheme': setTheme(ev); break;
    case 'clearChat': clearChat(); break;
  }
}

if (wv) wv.addEventListener('message', e => handle(e.data));

window.addEventListener('keydown', e => {
  if (e.key === 'Escape') {
    if (!overlay.hidden) {
      closeLightbox();
      e.preventDefault();
      return;
    }
    send({ action: 'chatEscape' });
  }
});
