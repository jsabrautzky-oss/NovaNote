'use strict';

const log = document.getElementById('log');
const busyBox = document.getElementById('busy');
const busyLabel = document.getElementById('busyLabel');
const busyStat = document.getElementById('busyStat');
const thinkLine = document.getElementById('thinkLine');
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
  streaming: '✍️ schreibt…',
  running: '⏳ Läuft…',
  done: '✅ Abgeschlossen',
  error: '❌ Fehler',
  cancelled: '⏹ Abgebrochen'
};

const STATE_LABEL = {
  thinking: 'KI denkt',
  toolcall: 'KI schreibt Werkzeug',
  tool: 'Werkzeug läuft',
  writing: 'KI antwortet',
  answer: 'Antwort fertig'
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
  const entry = { role, el, body, raw: ev.content || '', reasonRaw: ev.reasoning || '', det: null, rbody: null, rsum: null, rcnt: null, live: false };
  msgs.set(id, entry);
  if (role === 'assistant' && entry.reasonRaw) {
    ensureReason(id);
    updateReason(entry);
  }
  renderMessage(id);
  scrollDown();
}

function addRound(ev) {
  const div = document.createElement('div');
  div.className = 'round';
  div.textContent = `Runde ${ev.round || 1}`;
  log.appendChild(div);
  noteActivity();
  scrollDown();
}

function ensureReason(id) {
  const entry = msgs.get(id);
  if (!entry || entry.det) return entry;
  const det = document.createElement('details');
  det.className = 'reason';
  const sum = document.createElement('summary');
  const label = document.createElement('span');
  label.className = 'rlabel';
  label.textContent = 'Denken';
  const cnt = document.createElement('span');
  cnt.className = 'rcnt';
  sum.appendChild(label);
  sum.appendChild(cnt);
  const rbody = document.createElement('div');
  rbody.className = 'rbody';
  det.appendChild(sum);
  det.appendChild(rbody);
  entry.el.insertBefore(det, entry.body);
  entry.det = det;
  entry.rbody = rbody;
  entry.rsum = label;
  entry.rcnt = cnt;
  det.open = false;
  det.addEventListener('toggle', () => {
    if (det.open) scrollReasonBottom(entry);
  });
  return entry;
}

function updateReason(entry) {
  if (!entry.det) return;
  const len = entry.reasonRaw.length;
  entry.rbody.textContent = entry.reasonRaw;
  entry.rsum.textContent = entry.live ? 'Denken…' : 'Denken';
  entry.rcnt.textContent = len > 0 ? `${len.toLocaleString('de-DE')} Zeichen` : '';
  entry.det.classList.toggle('live', !!entry.live);
  if (entry.det.open) scrollReasonBottom(entry);
}

function scrollReasonBottom(entry) {
  const b = entry.rbody;
  if (!b) return;
  if (b.scrollHeight - b.scrollTop - b.clientHeight < 80) b.scrollTop = b.scrollHeight;
}

function appendReasoning(ev) {
  const entry = msgs.get(ev.id);
  if (!entry) return;
  ensureReason(ev.id);
  entry.reasonRaw += ev.chunk || '';
  entry.live = true;
  curReason = entry;
  updateReason(entry);
  noteActivity();
  scrollDown();
}

function appendChunk(ev) {
  const entry = msgs.get(ev.id);
  if (!entry) return;
  entry.raw += ev.chunk || '';
  if (entry.det && entry.live) {
    entry.live = false;
    updateReason(entry);
  }
  schedule(ev.id);
  noteActivity();
  scrollDown();
}

function setMessage(ev) {
  const entry = msgs.get(ev.id);
  if (!entry) return;
  entry.raw = ev.content || '';
  if (typeof ev.reasoning === 'string') {
    entry.reasonRaw = ev.reasoning;
    if (entry.reasonRaw) {
      ensureReason(ev.id);
      updateReason(entry);
    }
  }
  renderMessage(ev.id);
  noteActivity();
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
  const badge = document.createElement('span');
  badge.className = 'trepeat';
  sum.appendChild(status);
  sum.appendChild(name);
  sum.appendChild(short);
  sum.appendChild(badge);

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

  body.appendChild(argsLabel);
  body.appendChild(argsPre);
  body.appendChild(resLabel);
  body.appendChild(resPre);
  det.appendChild(sum);
  det.appendChild(body);
  log.appendChild(det);

  tools.set(ev.id, { det, status, name, short, badge, argsPre, resLabel, resPre, rawArgs: ev.args || '' });
  if (ev.repeat > 1) setRepeat(ev.id, ev.repeat);
  setStatus(ev.id, ev.live ? 'streaming' : 'running');
  noteActivity();
  scrollDown();
}

function updateToolArgs(ev) {
  const t = tools.get(ev.id);
  if (!t) return;
  if (ev.name) t.name.textContent = ev.name;
  if (typeof ev.args === 'string') {
    t.rawArgs = ev.args;
    t.argsPre.textContent = ev.final ? prettyJson(ev.args) : ev.args;
    t.argsPre.classList.toggle('partial', !ev.final);
  } else {
    t.rawArgs += ev.delta || '';
    t.argsPre.textContent = t.rawArgs;
    t.argsPre.classList.add('partial');
  }
  t.short.textContent = shortArgs(t.rawArgs);
  if (ev.repeat > 1) setRepeat(ev.id, ev.repeat);
  setStatus(ev.id, ev.final ? 'running' : 'streaming');
  noteActivity();
  scrollDown();
}

function setRepeat(id, n) {
  const t = tools.get(id);
  if (!t) return;
  t.badge.textContent = `🔁 ${n}. Mal`;
  t.det.classList.add('repeat');
}

function setStatus(id, state) {
  const t = tools.get(id);
  if (!t) return;
  t.status.textContent = STATUS[state] || state || '';
  t.det.classList.toggle('error', state === 'error');
  t.det.classList.toggle('streaming', state === 'streaming');
  t.det.classList.toggle('cancelled', state === 'cancelled');
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

let busyNow = false;
let busyHideTimer = 0;
let actTimer = 0;
let actStart = 0;
let lastAct = 0;
let actRound = 0;
let actTools = 0;
let actTokens = 0;
let actState = 'thinking';
let actTool = '';
let curReason = null;

function fmtTokens(n) {
  return n >= 1000 ? (n / 1000).toFixed(1).replace('.', ',') + 'k' : String(n);
}

function noteActivity() {
  lastAct = Date.now();
  if (busyNow) busyBox.classList.remove('stale');
}

function setActivity(ev) {
  if (ev.round) actRound = ev.round;
  if (ev.state) actState = ev.state;
  if (ev.state === 'thinking') actTool = '';
  else if (ev.tool) actTool = ev.tool;
  if (typeof ev.tools === 'number') actTools = ev.tools;
  if (typeof ev.tokens === 'number') actTokens = ev.tokens;
  noteActivity();
  paintActivity();
}

function thinkPreview() {
  if (!curReason || !curReason.reasonRaw) return '';
  const lines = curReason.reasonRaw.split(/\r?\n/).filter(s => s.trim().length);
  if (!lines.length) return '';
  const line = lines[lines.length - 1].trim();
  return line.length > 140 ? '…' + line.slice(-140) : line;
}

function paintActivity() {
  if (!busyNow) return;
  busyLabel.textContent = STATE_LABEL[actState] || 'KI arbeitet';
  const secs = Math.max(0, Math.round((Date.now() - actStart) / 1000));
  const bits = [];
  if (actRound > 1) bits.push('Runde ' + actRound);
  if (actTools > 0) bits.push(actTools + (actTools === 1 ? ' Schritt' : ' Schritte'));
  if (actTool) bits.push(actTool);
  bits.push(secs + ' s');
  if (actTokens > 0) bits.push(fmtTokens(actTokens) + ' Token');
  busyStat.textContent = bits.join(' · ');
  const idle = Math.round((Date.now() - lastAct) / 1000);
  const stale = idle >= 15;
  busyBox.classList.toggle('stale', stale);
  thinkLine.textContent = stale ? `keine Daten seit ${idle} s – mit Esc abbrechen` : thinkPreview();
}

function setBusy(ev) {
  busyNow = !!ev.busy;
  if (busyHideTimer) {
    clearTimeout(busyHideTimer);
    busyHideTimer = 0;
  }
  if (busyNow) {
    actStart = Date.now();
    lastAct = actStart;
    actRound = 0;
    actTools = 0;
    actState = 'thinking';
    actTool = '';
    curReason = null;
    if (!actTimer) actTimer = setInterval(paintActivity, 1000);
    busyBox.hidden = false;
    void busyBox.offsetWidth;
    busyBox.classList.add('on');
    paintActivity();
    if (stick) scrollDown();
  } else {
    if (actTimer) {
      clearInterval(actTimer);
      actTimer = 0;
    }
    busyBox.classList.remove('stale');
    thinkLine.textContent = '';
    for (const entry of msgs.values()) {
      if (entry.live) {
        entry.live = false;
        updateReason(entry);
      }
    }
    curReason = null;
    busyBox.classList.remove('on');
    busyHideTimer = setTimeout(() => {
      busyHideTimer = 0;
      if (!busyNow) busyBox.hidden = true;
    }, 600);
  }
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
  curReason = null;
  actRound = 0;
  actTools = 0;
  actState = 'thinking';
  actTool = '';
  busyBox.classList.remove('stale');
  thinkLine.textContent = '';
  stick = true;
  jumpBtn.hidden = true;
  closeLightbox();
}

function handle(ev) {
  if (!ev || typeof ev !== 'object') return;
  switch (ev.type) {
    case 'addMessage': addMessage(ev); break;
    case 'appendChunk': appendChunk(ev); break;
    case 'appendReasoning': appendReasoning(ev); break;
    case 'setMessage': setMessage(ev); break;
    case 'removeMessage': removeMessage(ev); break;
    case 'addRound': addRound(ev); break;
    case 'addToolCall': addToolCall(ev); break;
    case 'updateToolArgs': updateToolArgs(ev); break;
    case 'updateToolResult': updateToolResult(ev); break;
    case 'updateToolStatus': setStatus(ev.id, ev.state); break;
    case 'activity': setActivity(ev); break;
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
