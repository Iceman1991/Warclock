'use strict';
// DCS-Kampagne – Oberfläche (Design v2): Karte im Vollbild auf Canvas (DCS-Koordinaten x = Nord, z = Ost), Fenster schweben darüber.
// Daten von /api/state (jede Sekunde).
const $ = s => document.querySelector(s);
const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const FT = 0.3048, KT = 0.514444, NM = 1852;
const COL = { Blue: '#8ccff2', Red: '#f2897f', None: '#b4bab0' };            // Kartensymbole
const SC = { Blue: '#6cb4ff', Red: '#ff6a5c', None: '#a3a8ad' };             // Linien
const C = { sym: '#090b0d', text: '#e2e5e8', dim: '#8a929b', faint: '#5c646d', accent: '#e3a83b', front: '#efc35a', ok: '#5fbf74', warn: '#e6b44a', red: '#ff6a5c', bg: '#0b0d0f' };
const FONT = '"IBM Plex Sans Condensed", "Bahnschrift SemiCondensed", Bahnschrift, "Segoe UI", sans-serif', MONO = '"IBM Plex Mono", Consolas, monospace';
const TASKC = { CAP: '#5d9bff', Sweep: '#5d9bff', Escort: '#8fbaff', SEAD: '#c084fc', DEAD: '#f59e0b', Strike: '#f59e0b', OCA: '#fb923c', CAS: '#a3e635', BAI: '#84cc16', Interdiction: '#facc15', AWACS: '#9ca3af', Tanker: '#9ca3af', Recon: '#67e8f9', Jammer: '#e879f9', Antiship: '#2dd4bf' };
const KIND = { Airbase: 'Airbase', Depot: 'Depot', Factory: 'Factory', Port: 'Port', Bridge: 'Bridge', Ewr: 'EW radar', Sam: 'SAM site', Cp: 'Command post', Tel: 'Missile launcher' };
const UKIND = { Armor: 'Armor', Mech: 'Mech. infantry', Motor: 'Mot. infantry', Art: 'Artillery', Shorad: 'SHORAD' };
const WPK = { start: 'Takeoff', nav: 'Waypoint', push: 'Push', ip: 'IP', target: 'Target', egress: 'Egress', orbit: 'Station', orbit2: 'Station end', land: 'Landing' };
const TYP = { MOSCOW: 'Slava cruiser', REZKY: 'Krivak frigate', ALBATROS: 'Grisha corvette', TICONDEROG: 'Ticonderoga cruiser', PERRY: 'Perry frigate', MOLNIYA: 'Tarantul missile boat', 'FA-18C_hornet': 'F/A-18C', 'F-15ESE': 'F-15E', 'F-16C_50': 'F-16C', 'A-10C_2': 'A-10C II', AV8BNA: 'AV-8B', 'M-2000C': 'Mirage 2000C', 'Mirage-F1EE': 'Mirage F1EE', 'Mirage-F1CE': 'Mirage F1CE', 'Mirage-F1M-EE': 'Mirage F1M', 'MiG-21Bis': 'MiG-21bis', AJS37: 'AJS-37 Viggen',
  'F-4E-45MC': 'F-4E Phantom II', 'Ka-50_3': 'Ka-50 III', SA342M: 'Gazelle M', SA342L: 'Gazelle L', SA342Mistral: 'Gazelle Mistral', SA342Minigun: 'Gazelle Minigun', OH58D: 'OH-58D', 'CH-47Fbl1': 'CH-47F', 'F-86F Sabre': 'F-86F', 'MiG-29 Fulcrum': 'MiG-29 (FC)', 'F-14A-135-GR': 'F-14A Late', 'F-14A-135-GR-Early': 'F-14A Early', 'F-14A-95-GR': 'F-14A Export', 'F-14B': 'F-14B', 'F-14BU': 'F-14B(U)', 'MiG-29G': 'MiG-29G', 'F-5E-3': 'F-5E', 'AH-64D_BLK_II': 'AH-64D', KC135MPRS: 'KC-135 MPRS', 'KC-135': 'KC-135' };
const SKILL = ['Average', 'Good', 'High', 'Excellent'];
const ATTACK = ['Strike', 'OCA', 'DEAD', 'CAS', 'BAI', 'Interdiction'];
const DEFTASK = { Sam: 'DEAD', Bridge: 'Interdiction', Airbase: 'OCA' };
const WINS = { lage: ['Situation', '1', 'Sitrep'], ato: ['ATO', '2', 'ATO'], flug: ['Flight', '3', 'Flight'], log: ['Logistics', '4', 'Log'], sqn: ['Squadrons', '5', 'Squadron'], ev: ['Reports', '6', 'Reports'], deb: ['Debriefing', '7', 'Debrief'], pilot: ['Pilot', '8', 'Pilot'] };
const UP = ['in der Luft', 'im Einsatz', 'Rückflug', 'Abbruch'];
const MON = ['JAN', 'FEB', 'MAR', 'APR', 'MAY', 'JUN', 'JUL', 'AUG', 'SEP', 'OCT', 'NOV', 'DEC'];

// ---------- Hilfen ----------

const T = s => s ? Date.parse(String(s).slice(0, 19) + 'Z') : 0;          // Kampagnenzeit, als UTC gerechnet
const hm = s => s ? String(s).slice(11, 16) : '–';
const iso = ms => new Date(ms).toISOString().slice(0, 19);
// Kriegszeit flüssig zwischen den Abrufen: Sekunden in der laufenden Minute (Acc) + seither vergangene Zeit × Tempo
const now = () => T(W.Time) + ((W.Acc || 0) + Math.min((performance.now() - W.at) / 1000, Math.max(1.5, 65 / (W.Rate || 1))) * (W.Rate || 0)) * 1000;
const pad = n => String(n).padStart(2, '0');
const day = s => { const d = new Date(T(s)); return `${['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'][d.getUTCDay()]} ${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}`; };
const dtg = s => { const d = new Date(T(s)); return `${pad(d.getUTCDate())}${pad(d.getUTCHours())}${pad(d.getUTCMinutes())}L ${MON[d.getUTCMonth()]} ${String(d.getUTCFullYear()).slice(2)}`; };
const ago = s => { const m = Math.max(0, Math.round((now() - T(s)) / 60000)); return m < 90 ? `${m} min ago` : `${Math.round(m / 60)} h ago`; };
const pct = v => v < 0 ? '?' : Math.round(v * 100) + ' %';
const bar = (v, w) => v < 0 ? '<span class="dim">?</span>' : `<span class="bar" title="${pct(v)}"${w ? ` style="width:${w}px"` : ''}><i style="width:${Math.max(0, Math.min(100, v * 100))}%;background:${v < 0.35 ? 'var(--red)' : v < 0.7 ? 'var(--warn)' : 'var(--ok)'}"></i></span>`;
const chip = t => `<span class="chip"><i style="background:${TASKC[t] || '#ccc'}"></i>${esc(t)}</span>`;
const kv = rows => `<div class="kv">${rows.filter(Boolean).map(([a, b]) => `<span>${a}</span><span>${b}</span>`).join('')}</div>`;
const typ = t => TYP[t] || t;
/// Interne Werte (Status, Befehle, Startart …) bleiben deutsch wie im Spielstand – angezeigt wird englisch
const EN = { 'geplant': 'planned', 'in der Luft': 'airborne', 'im Einsatz': 'on task', 'Rückflug': 'RTB', 'Abbruch': 'aborted', 'gelandet': 'landed', 'verloren': 'lost', 'gestrichen': 'cancelled', 'kalt': 'cold', 'heiß': 'hot', 'Startbahn': 'runway', 'Luft': 'air start', 'Angriff': 'attack', 'Halten': 'hold', 'Verteidigen': 'defend', 'Rückzug': 'withdraw', 'Reserve': 'reserve', 'Auffrischen': 'refit', 'Verlegen': 'redeploy', 'Ausbruch': 'breakout', 'Bereitstellung': 'staging', 'Konsolidierung': 'consolidation', 'aktiv': 'active', 'vermisst': 'missing', 'gefangen': 'captured', 'tot': 'KIA', 'gefallen': 'KIA', 'ausgeschieden': 'retired', 'verwundet': 'wounded',
  'Luft-Luft': 'Air-to-air', 'Luft-Boden': 'Air-to-ground',
  'flugabwehr': 'air defence', 'konvoi': 'convoy', 'kriegsende': 'end of war', 'lage': 'situation', 'logistik': 'logistics', 'luft': 'air', 'spieler': 'player', 'verlust': 'loss', 'wetter': 'weather', 'see': 'naval' };
const en = v => EN[v] ?? v;
const own = x => x.Side === W.Player;
const up = f => UP.includes(f.Status) || !!f.Src;
const fname = p => String(p || '').split(/[\\/]/).pop();
const sideDe = s => s === 'Blue' ? 'Blue' : s === 'Red' ? 'Red' : 'neutral';
const baseName = q => W.ob.get(q.Base)?.Name ?? '?';
const editable = f => !!f && own(f) && f.Status === 'geplant' && !W.Mission && (!W.You || f.Owner === W.You);
const hostOnly = () => W.Mission || W.You ? 'disabled' : '';                     // Ausrüstung je Muster stellt der Host
const rt = f => pending && pending.id === f.Id ? pending.r : f.Route;
const store = (k, v) => { try { localStorage.setItem('dcscamp.' + k, JSON.stringify(v)); } catch { } };
const recall = k => { try { return JSON.parse(localStorage.getItem('dcscamp.' + k)); } catch { return null; } };

const cv = $('#map'), ctx = cv.getContext('2d');
let S, W, relief, sat, sea, terr, terrKey = '', V, HB = [];   // HB: zuletzt gezeichnete Symbole [Art, Id, x, y] für Klicks
let sel = null, down = false, drag = null, pending = null, loading = null, evKind = '', tplus = 0;
let spdOpen = false, atoPlay = false, repOpen = false, saves = [], saveSel = 0;
let assess = { id: 0, key: '', lines: [], busy: false };
let newc = { step: 0, set: 'kaukasus', side: 'Blue', fog: true, seed: '' };
const open = new Set(), form = { task: '', sqn: '' };
const st = Object.assign({ screen: 'menu', layOpen: false, run: 16, wins: {} }, recall('st') || {});
const lay = Object.assign({ terr: true, towns: true, rail: true, units: true, objs: true, conv: true, air: true, threats: true, alt: 20000 }, recall('lay') || {});
const saveSt = () => store('st', st);

const who = () => recall('who') || {};
async function api(p, body) {
  const h = { 'X-Who': encodeURIComponent(who().name || ''), 'X-Code': who().code || '' };      // Mitspieler im Netz; der Host braucht das nicht
  const r = await fetch('/api/' + p, body === undefined ? { headers: h } : { method: 'POST', body: JSON.stringify(body), headers: h });
  const t = await r.text();
  if (r.status === 403) join();
  if (!r.ok) throw new Error(t || 'Error ' + r.status);
  return t ? JSON.parse(t) : null;
}
async function run(f) { try { await f(); } catch (e) { toast(e.message); } await load(); }
function toast(m, ok) { const t = $('#toast'); t.textContent = m; t.className = ok ? 'ok' : ''; t.hidden = false; clearTimeout(toast.h); toast.h = setTimeout(() => t.hidden = true, ok ? 3500 : 7000); }
function dlg(h) { const d = $('#dlg'); d.innerHTML = `<div class="card">${h}</div>`; d.hidden = false; }
const dlgClose = () => { $('#dlg').hidden = true; };
/// Mitspieler: Name + Code des Hosts (steht im Einladungslink)
function join() {
  if ($('#dlg [data-jn]')) return;
  const w = who(), code = new URLSearchParams(location.search).get('code') || w.code || '';
  dlg(`<div class="card-h">Join the campaign</div><div class="nc-b"><p>The host runs the war – you claim and plan your own flights.</p><div class="form"><span>Your name</span><span><input data-jn maxlength="16" value="${esc(w.name || '')}" placeholder="callsign"></span><span>Code</span><span><input data-jc maxlength="6" value="${esc(code)}"></span></div></div><div class="nc-f"><span class="grow"></span><button class="go" data-act="joinok">Join</button></div>`);
}

/// HTML nur tauschen, wenn es sich geändert hat und niemand gerade klickt oder tippt (sonst gehen Klicks verloren)
function put(el, h, force) {
  if (!el || el._h === h) return;
  const a = document.activeElement;
  if (!force && (down || (a && el.contains(a) && /INPUT|SELECT|TEXTAREA/.test(a.tagName)))) return;
  el._h = h; el.innerHTML = h;
}

function load() {
  if (loading) return loading.then(load);                  // läuft schon: danach frisch holen
  loading = (async () => {
    try { W = await api('state'); }
    catch { if (!$('#dlg [data-jn]')) toast('No connection – is DcsCampaign.exe running?'); return; }
    try { W.at = performance.now(); index(); render(); }
    catch (e) { console.error(e); toast('Display error: ' + e.message); }    // nicht als „No connection“ tarnen
  })().finally(() => loading = null);
  return loading;
}

function index() {
  W.fl = new Map(W.Flights.map(f => [f.Id, f]));
  W.un = new Map(W.Units.map(u => [u.Id, u]));
  W.ob = new Map(W.Objs.map(o => [o.Id, o]));
  W.sq = new Map(W.Sqns.map(q => [q.Id, q]));
  W.pk = new Map(W.Pkgs.map(p => [p.Id, p]));
  W.mine = W.Flights.filter(f => f.Player && own(f) && (!W.You || f.Owner === W.You));                       // Mehrspieler: alle Flüge der nächsten Mission
  W.pf = W.mine.find(f => f.Id === st.cur && !/gelandet|verloren|gestrichen/.test(f.Status)) || W.mine.find(f => !/gelandet|verloren|gestrichen/.test(f.Status)) || W.mine[0] || null;
  W.Plan = W.pf && W.Plans[W.pf.Id];
}

function item(s) {
  if (!s || !W) return null;
  return s.t === 'f' ? W.fl.get(s.id) : s.t === 'u' ? W.un.get(s.id) : s.t === 'o' ? W.ob.get(s.id) : s.t === 'c' ? W.Convoys.find(c => c.Id === s.id) : s.t === 'n' ? S.Nodes[s.id] : null;
}

// ---------- Karte ----------

const cw = () => cv.clientWidth, ch = () => cv.clientHeight;
const px = z => (z - V.cz) * V.s + cw() / 2;
const py = x => (V.cx - x) * V.s + ch() / 2;
const mz = X => (X - cw() / 2) / V.s + V.cz;
const mx = Y => V.cx - (Y - ch() / 2) / V.s;
const vis = (x, y, m = 40) => x > -m && y > -m && x < cw() + m && y < ch() + m;
const kk = () => V.s / V.f;                                                // Zoom relativ zur ganzen Karte
let raf = 0;
const redraw = () => { if (!raf) raf = requestAnimationFrame(() => { raf = 0; draw(); }); };

/// Ganze Front zeigen – frei von Leiste links und Kopfleiste
function fit() {
  const xs = S.Nodes.map(n => n.X), zs = S.Nodes.map(n => n.Z);
  const x0 = Math.min(...xs), x1 = Math.max(...xs), z0 = Math.min(...zs), z1 = Math.max(...zs);
  const s = Math.min(Math.max(200, cw() - 160) / (z1 - z0), Math.max(200, ch() - 110) / (x1 - x0));
  V = { s, f: s, cz: (z0 + z1) / 2 - 40 / s, cx: (x0 + x1) / 2 + 15 / s };
}
function zoom(X, Y, k) {
  if (!V) return;
  const z = mz(X), x = mx(Y);
  V.s = Math.min(0.05, Math.max(V.f * 0.5, V.s * k));
  V.cz = z - (X - cw() / 2) / V.s; V.cx = x + (Y - ch() / 2) / V.s;
  redraw();
}
function center(o) { if (!o || !V) return; V.cx = o.X; V.cz = o.Z; if (kk() < 1.6) V.s = V.f * 1.6; redraw(); }

/// Beschriftungen sammeln und am Ende nach Vorrang setzen; was sich überdeckt, entfällt (Vorrang ≥ 9 immer).
let LQ = [];
const LP = { scale: 10, wp: 9, fr: 8, ab: 6, key: 5, '': 4, thr: 3, dim: 1 };
const LS = { '': ['500 10.5px', '#b9c0c7', FONT, .07], key: ['600 11px', C.text, FONT, .07], ab: ['600 10.5px', C.text, FONT, .07], dim: ['500 10.5px', C.faint, FONT, .07],
  fr: ['700 11px', C.front, FONT, .12], thr: ['500 10px', COL.Red, MONO, 0], wp: ['600 10px', C.text, MONO, 0], scale: ['10px', C.dim, MONO, 0] };
const label = (t, x, y, c = '') => LQ.push({ t: String(t).toUpperCase(), x, y, c, p: LP[c] });
function labels(g) {
  const done = [];
  g.lineJoin = 'round';
  for (const l of LQ.sort((a, b) => b.p - a.p)) {
    const [f, col, fam, ls] = LS[l.c];
    g.font = `${f} ${fam}`; g.letterSpacing = ls + 'em';
    const r = [l.x - 2, l.y - 11, l.x + g.measureText(l.t).width + 2, l.y + 3];
    if (l.p < 9 && done.some(d => r[0] < d[2] && r[2] > d[0] && r[1] < d[3] && r[3] > d[1])) continue;
    done.push(r);
    g.lineWidth = 3; g.strokeStyle = C.sym; g.strokeText(l.t, l.x, l.y); g.fillStyle = col; g.fillText(l.t, l.x, l.y);
  }
  g.letterSpacing = '0px';
  LQ = [];
}
function loadSat() { if (sat) return; sat = new Image(); sat.onload = () => { const c = document.createElement('canvas'); c.width = sat.naturalWidth; c.height = sat.naturalHeight; const g = c.getContext('2d'); g.filter = 'saturate(.5) brightness(.55)'; g.drawImage(sat, 0, 0); sat.c = c; redraw(); }; sat.src = '/bg/sat.png'; }

function draw() {
  const w = cw(), h = ch(), r = devicePixelRatio || 1;
  if (!W || !w || !h) return;
  if (!V) fit();
  if (cv.width !== Math.round(w * r) || cv.height !== Math.round(h * r)) { cv.width = Math.round(w * r); cv.height = Math.round(h * r); }
  const g = ctx;
  g.setTransform(r, 0, 0, r, 0, 0); g.clearRect(0, 0, w, h); g.textBaseline = 'alphabetic';
  HB = [];
  const bg = lay.sat && sat?.c ? [sat.c, S.Sat] : relief?.c ? [relief.c, S.Relief] : null;
  if (bg) { const [im, b] = bg; g.imageSmoothingEnabled = true; g.drawImage(im, px(b.Z0), py(b.X1), (b.Z1 - b.Z0) * V.s, (b.X1 - b.X0) * V.s); }
  if (lay.terr) { territory(); g.drawImage(terr.c, px(terr.z0), py(terr.x1), terr.c.width * terr.C * V.s, terr.c.height * terr.C * V.s); }
  if (lay.roads) roads(g);
  if (lay.rail) rails(g);
  if (lay.towns) towns(g);
  if (lay.terr) { front(g); fronts(g); }
  if (lay.radar) radar(g);
  if (lay.threats) threats(g);
  if (lay.supply) supply(g);
  if (lay.objs) objs(g);
  if (lay.conv) convoys(g);
  if (lay.units) units(g);
  routes(g);
  if (lay.air) flights(g);
  const p = selPos();
  if (p) { g.strokeStyle = C.accent; g.lineWidth = 1.5; g.setLineDash([4, 3]); g.beginPath(); g.arc(p[0], p[1], 15, 0, 7); g.stroke(); g.setLineDash([]); }
  scale(g);
  labels(g);
}

function isSea(x, z) {
  if (!sea) return false;
  const c = Math.floor((z - S.Relief.Z0) / sea.cell), r = Math.floor((S.Relief.X1 - x) / sea.cell);
  if (r < 0 || c < 0 || r >= sea.h || c >= sea.w) return true;            // außerhalb des Reliefs: kein Gebiet
  const j = (r * sea.w + c) * 4, d = sea.d;
  return Math.abs(d[j] - 38) < 8 && Math.abs(d[j + 1] - 72) < 8 && Math.abs(d[j + 2] - 104) < 8;   // Meerfarbe im Relief
}

/// Gebiet: jede Zelle (2,5 km) gehört dem Besitzer des nächsten Ortes. Nur neu rechnen, wenn sich Besitz ändert.
/// Front: Nulllinie von (Abstand zum nächsten blauen − zum nächsten roten Ort), per Marching Squares → glatte Linie.
function territory() {
  const key = W.Owners.join('');
  if (terr && key === terrKey) return;
  terrKey = key;
  const C = 2500, x0 = -430000, x1 = 40000, z0 = 390000, z1 = 990000;
  const cols = Math.ceil((z1 - z0) / C), rows = Math.ceil((x1 - x0) / C);
  const c = document.createElement('canvas'); c.width = cols; c.height = rows;
  const cx = c.getContext('2d'), im = cx.createImageData(cols, rows), d = im.data, N = S.Nodes, ow = W.Owners, D = new Float32Array(cols * rows).fill(NaN);
  for (let r = 0; r < rows; r++) {
    const x = x1 - (r + 0.5) * C;
    for (let k = 0; k < cols; k++) {
      const z = z0 + (k + 0.5) * C;
      if (isSea(x, z)) continue;
      let best = 2.1e9, o = 0, bb = Infinity, br = Infinity;
      for (let i = 0; i < N.length; i++) {
        const dx = N[i].X - x, dz = N[i].Z - z, q = dx * dx + dz * dz;
        if (q < best) { best = q; o = ow[i]; }
        if (ow[i] === 2) { if (q < bb) bb = q; } else if (ow[i] === 1 && q < br) br = q;
      }
      if (Math.min(bb, br) < 1.6e9) D[r * cols + k] = Math.sqrt(bb) - Math.sqrt(br);   // Front nur bis 40 km von Orten
      if (!o || best > 3.6e9) continue;                                     // > 60 km vom nächsten Ort: kein Gebiet
      const j = (r * cols + k) * 4;
      if (o === 2) { d[j] = 108; d[j + 1] = 180; d[j + 2] = 255; d[j + 3] = 24; } else { d[j] = 255; d[j + 1] = 106; d[j + 2] = 92; d[j + 3] = 30; }
    }
  }
  cx.putImageData(im, 0, 0);
  const seg = [], P = (r, k) => [x1 - (r + 0.5) * C, z0 + (k + 0.5) * C];
  for (let r = 0; r < rows - 1; r++) for (let k = 0; k < cols - 1; k++) {
    const v = [D[r * cols + k], D[r * cols + k + 1], D[(r + 1) * cols + k + 1], D[(r + 1) * cols + k]];
    if (!v.every(Number.isFinite)) continue;                                // Meer oder eine Seite ohne Orte
    const q = [P(r, k), P(r, k + 1), P(r + 1, k + 1), P(r + 1, k)];
    for (let e = 0; e < 4; e++) {
      const a = v[e], b = v[(e + 1) % 4], A = q[e], B = q[(e + 1) % 4], t = a / (a - b);
      if ((a < 0) !== (b < 0)) seg.push(A[0] + (B[0] - A[0]) * t, A[1] + (B[1] - A[1]) * t);   // 2 oder 4 Punkte = 1 oder 2 Stücke
    }
  }
  terr = { c, C, x1, z0, seg };
}

function front(g) {
  const s = terr.seg;
  g.beginPath();
  for (let i = 0; i < s.length; i += 4) { g.moveTo(px(s[i + 1]), py(s[i])); g.lineTo(px(s[i + 3]), py(s[i + 2])); }
  g.lineCap = g.lineJoin = 'round'; g.globalAlpha = 0.7; g.strokeStyle = C.sym; g.lineWidth = 5; g.stroke();
  g.globalAlpha = 1; g.strokeStyle = C.front; g.lineWidth = 1.8; g.stroke(); g.lineCap = 'butt';
}

/// Gleise: dunkle Linie mit hellen Querstrichen (Bahnsignatur), unter den Symbolen
function rails(g) {
  g.beginPath();
  for (const e of S.Edges) if (e.Rail) { const a = S.Nodes[e.A], b = S.Nodes[e.B]; g.moveTo(px(a.Z), py(a.X)); g.lineTo(px(b.Z), py(b.X)); }
  g.strokeStyle = 'rgba(9,11,13,.9)'; g.lineWidth = 3; g.stroke();
  g.setLineDash([5, 5]); g.strokeStyle = '#6b737c'; g.lineWidth = 1.2; g.stroke(); g.setLineDash([]);
}

function roads(g) {
  for (const pass of [false, true]) {
    g.beginPath();
    for (const e of S.Edges) {
      if (e.Pass !== pass || e.Rail) continue;
      const a = S.Nodes[e.A], b = S.Nodes[e.B];
      g.moveTo(px(a.Z), py(a.X)); g.lineTo(px(b.Z), py(b.X));
    }
    g.setLineDash(pass ? [4, 4] : []); g.strokeStyle = pass ? 'rgba(239,195,90,.4)' : 'rgba(160,175,190,.2)'; g.lineWidth = 1; g.stroke();
  }
  if (W.Wx.Snow) {                                                          // Schnee: Pass gesperrt
    g.beginPath();
    for (const e of S.Edges) if (e.Snow) { const a = S.Nodes[e.A], b = S.Nodes[e.B]; g.moveTo(px(a.Z), py(a.X)); g.lineTo(px(b.Z), py(b.X)); }
    g.setLineDash([2, 3]); g.strokeStyle = '#cfe8ff'; g.lineWidth = 3; g.stroke();
  }
  g.setLineDash([]);
}

function towns(g) {
  const k = kk(), ab = lay.objs ? W.Objs.filter(o => o.Kind === 'Airbase') : [];
  for (const n of S.Nodes) {
    const x = px(n.Z), y = py(n.X);
    if (!vis(x, y)) continue;
    const key = n.Key === n.Id;                                              // Schlüsselort (Achsen, Ziele)
    if (k > 1.4 || key) { g.fillStyle = C.faint; g.fillRect(x - 1.5, y - 1.5, 3, 3); }
    if ((key ? k >= 0.9 : k > 2.2) && !ab.some(o => Math.abs(o.X - n.X) < 9000 && Math.abs(o.Z - n.Z) < 9000)) label(n.Name, x + 5, y - 4, key ? 'key' : 'dim');
  }
}

/// Brennpunkte: je Achse ein Kreuz zwischen den Frontorten
function fronts(g) {
  const k = kk();
  W.Fronts.forEach((f, i) => {
    if (!f) return;
    const a = S.Nodes[f[0]], b = S.Nodes[f[1]], x = (px(a.Z) + px(b.Z)) / 2, y = (py(a.X) + py(b.X)) / 2;
    g.strokeStyle = C.front; g.lineWidth = 2.4; g.lineCap = 'square';
    g.beginPath(); g.moveTo(x - 7, y - 7); g.lineTo(x + 7, y + 7); g.moveTo(x + 7, y - 7); g.lineTo(x - 7, y + 7); g.stroke();
    g.lineCap = 'butt';
    if (k > 0.8) label(S.Axes[i].Name, x + 11, y + 15, 'fr');
  });
}

function threats(g) {
  const alt = lay.alt * FT;
  const ring = (x, z, r, enemy, name) => {
    const X = px(z), Y = py(x), R = r * V.s;
    if (!vis(X, Y, R + 20)) return;
    g.beginPath(); g.arc(X, Y, R, 0, 7);
    if (enemy) { g.fillStyle = 'rgba(255,106,92,.04)'; g.fill(); g.setLineDash([5, 4]); g.strokeStyle = COL.Red; }
    else { g.setLineDash([1, 4]); g.strokeStyle = COL.Blue; g.globalAlpha = 0.5; }
    g.lineWidth = 1; g.stroke(); g.setLineDash([]); g.globalAlpha = 1;
    if (enemy && name && R > 34) label(name, X - 14, Y - R + 13, 'thr');
  };
  for (const o of W.Objs)
    if (o.Kind === 'Sam' && o.Health > 0.3 && o.Missiles !== 0 && alt < o.MaxAlt) ring(o.X, o.Z, o.Range * (alt < 1500 ? 0.45 : 1), !own(o), o.Sys);
  if (alt < 3800) for (const u of W.Units) if (u.Kind === 'Shorad' && !own(u)) ring(u.X, u.Z, 9000, true, '');
  for (const c of W.Convoys) if (c.Navy && c.Range) ring(c.X, c.Z, c.Range, !own(c), c.What);
}

/// Frühwarnradare: Reichweite bis zum Radarhorizont 4120·(√30 + √Höhe) m wie Sim.Radar.
// ponytail: Höhe gilt hier als über Grund (Geländehöhe fehlt im Browser) – Ring statt Geländeschatten.
function radar(g) {
  const hor = 4120 * (Math.sqrt(30) + Math.sqrt(lay.alt * FT));
  for (const o of W.Objs) {
    if (o.Kind !== 'Ewr' || o.Health <= 0.3) continue;
    const X = px(o.Z), Y = py(o.X), R = Math.min(o.Range, hor) * V.s;
    if (!vis(X, Y, R + 20)) continue;
    g.beginPath(); g.arc(X, Y, R, 0, 7);
    g.globalAlpha = 0.55; g.setLineDash([10, 6]); g.strokeStyle = own(o) ? COL.Blue : C.warn; g.lineWidth = 1; g.stroke(); g.setLineDash([]); g.globalAlpha = 1;
  }
}

/// Nachschubwege: eigene Verbände → versorgendes Depot (Farbe = Versorgung), Restweg der Konvois (Gegner nur Position).
function supply(g) {
  g.lineWidth = 1; g.globalAlpha = 0.55;
  for (const u of W.Units) {
    const d = W.ob.get(u.Dp);
    if (!d) continue;
    g.strokeStyle = u.Supply < 0.35 ? C.red : u.Supply < 0.7 ? C.warn : C.ok;
    g.beginPath(); g.moveTo(px(u.Z), py(u.X)); g.lineTo(px(d.Z), py(d.X)); g.stroke();
  }
  g.setLineDash([4, 4]); g.lineWidth = 1.3; g.globalAlpha = 0.75;
  for (const c of W.Convoys) {
    const port = c.Ship && !c.Navy && W.Objs.find(o => o.Name === c.To);
    if (!c.Path.length && !port) continue;
    g.beginPath(); g.moveTo(px(c.Z), py(c.X));
    if (port) g.lineTo(px(port.Z), py(port.X));                             // Seeweg zum Hafen
    for (const n of c.Path) g.lineTo(px(S.Nodes[n].Z), py(S.Nodes[n].X));
    g.strokeStyle = SC[c.Side]; g.stroke();
  }
  g.setLineDash([]); g.globalAlpha = 1;
}

function glyph(g, k, x, y, r) {
  g.beginPath();
  switch (k) {
    case 'Sam': g.moveTo(x, y - r); g.lineTo(x + r, y + r * 0.8); g.lineTo(x - r, y + r * 0.8); g.closePath(); break;
    case 'Ewr': g.moveTo(x - r, y + r * 0.5); g.arc(x, y + r * 0.5, r, Math.PI, 0); g.closePath(); break;
    case 'Depot': g.rect(x - r, y - r, 2 * r, 2 * r); break;
    case 'Factory': g.moveTo(x - r, y + r); g.lineTo(x - r, y - r * 0.2); g.lineTo(x, y - r); g.lineTo(x + r, y - r * 0.2); g.lineTo(x + r, y + r); g.closePath(); break;
    case 'Port': g.moveTo(x - r, y - r * 0.3); g.lineTo(x + r, y - r * 0.3); g.lineTo(x + r * 0.6, y + r); g.lineTo(x - r * 0.6, y + r); g.closePath(); break;
    case 'Bridge': g.rect(x - r * 1.2, y - r * 0.45, r * 2.4, r * 0.9); break;
    case 'Cp': g.moveTo(x - r * 0.7, y + r); g.lineTo(x - r * 0.7, y - r); g.lineTo(x + r, y - r * 0.4); g.lineTo(x - r * 0.7, y + r * 0.2); g.closePath(); break;
    default: g.arc(x, y, r, 0, 7);
  }
}

function objs(g) {
  const k = kk();
  for (const o of W.Objs) {
    const x = px(o.Z), y = py(o.X), ab = o.Kind === 'Airbase';
    if (!vis(x, y) || (!ab && k < 0.9 && o.Kind !== 'Sam' && o.Kind !== 'Ewr')) continue;
    const dead = o.Health < 0.3, r = ab ? 7.5 : 5.5;
    HB.push(['o', o.Id, x, y]);
    g.globalAlpha = dead ? 0.55 : o.Seen && now() - T(o.Seen) > 6 * 3600e3 ? 0.6 : 1;
    glyph(g, o.Kind, x, y, r);
    g.fillStyle = COL[o.Side] || COL.None; g.fill(); g.strokeStyle = C.sym; g.lineWidth = 1.3; g.stroke();
    if (ab) {
      const b = S.Bases.find(b => b.Id === o.BaseId), a = (b?.RwyHdg ?? 0) * Math.PI / 180;
      g.lineWidth = 2.4; g.beginPath();
      g.moveTo(x - Math.sin(a) * 6, y + Math.cos(a) * 6); g.lineTo(x + Math.sin(a) * 6, y - Math.cos(a) * 6); g.stroke();
    }
    if (dead) { g.strokeStyle = C.red; g.lineWidth = 2; g.beginPath(); g.moveTo(x - r, y - r); g.lineTo(x + r, y + r); g.moveTo(x + r, y - r); g.lineTo(x - r, y + r); g.stroke(); }
    g.globalAlpha = 1;
    if (ab ? k > 0.75 : k > 2.4) label(o.Name, x + r + 3, y + 4, ab ? 'ab' : '');
  }
}

function convoys(g) {
  for (const c of W.Convoys) {
    const x = px(c.Z), y = py(c.X);
    if (!vis(x, y)) continue;
    HB.push(['c', c.Id, x, y]);
    g.fillStyle = COL[c.Side]; g.strokeStyle = C.sym; g.lineWidth = 1;
    if (c.Ship) {                                                           // Rumpf + Aufbau
      g.beginPath(); g.moveTo(x - 9, y - 2); g.lineTo(x + 9, y - 2); g.lineTo(x + 6, y + 4); g.lineTo(x - 6, y + 4); g.closePath(); g.fill(); g.stroke();
      g.beginPath(); g.rect(x - 2, y - 7, 5, 5); g.fill(); g.stroke();
      if (kk() > 1.3) label(c.Navy ? c.What : `Freighter → ${c.To}`, x + 12, y + 4);
    } else if (c.Rail) { g.beginPath(); g.rect(x - 9, y - 3, 18, 6); g.fill(); g.stroke(); for (const d of [-3, 3]) { g.beginPath(); g.moveTo(x + d, y - 3); g.lineTo(x + d, y + 3); g.stroke(); } }
    else for (const d of [-5, 0, 5]) { g.beginPath(); g.rect(x + d - 2, y - 2, 4, 4); g.fill(); g.stroke(); }
  }
}

/// Verbände: eigene Rechteck, Feind Raute, Stufe (II Bataillon, III Regiment, X Brigade) darüber, Stärke darunter
function units(g) {
  const occ = {}, sp = S.Nodes, k = kk();
  for (const p of W.Line || []) {                                          // Sicherungslinie: Zug an jeder Straße über die Front
    const x = px(p.Z), y = py(p.X);
    if (!vis(x, y)) continue;
    g.beginPath();
    if (own(p)) g.rect(x - 5, y - 3.5, 10, 7); else { g.moveTo(x, y - 5.5); g.lineTo(x + 6, y); g.lineTo(x, y + 5.5); g.lineTo(x - 6, y); g.closePath(); }
    g.fillStyle = COL[p.Side]; g.fill(); g.strokeStyle = C.sym; g.lineWidth = 1; g.stroke();
  }
  for (const u of W.Units) {
    let x = px(u.Z), y = py(u.X);
    if (!vis(x, y)) continue;
    const key = Math.round(u.Z * V.s / 22) + ',' + Math.round(-u.X * V.s / 22), n = occ[key] = (occ[key] || 0) + 1;   // Raster in Weltkoordinaten: Stapel bleiben beim Verschieben gleich
    y += (n - 1) * 20;
    HB.push(['u', u.Id, x, y]);
    const mine = own(u), yb = y + (mine ? 9 : 12);
    if (mine && u.Path.length && k > 1.1) {                                   // Marschziel
      const t = sp[u.Path[0]];
      g.strokeStyle = u.Order === 'Angriff' ? C.front : C.faint; g.lineWidth = 1.2; g.setLineDash([2, 3]);
      g.beginPath(); g.moveTo(x, y); g.lineTo(px(t.Z), py(t.X)); g.stroke(); g.setLineDash([]);
    }
    g.globalAlpha = !mine && u.Seen && now() - T(u.Seen) > 2 * 3600e3 ? 0.5 : 1;
    g.beginPath();
    if (mine) g.rect(x - 10, y - 7, 20, 14); else { g.moveTo(x, y - 10); g.lineTo(x + 11, y); g.lineTo(x, y + 10); g.lineTo(x - 11, y); g.closePath(); }
    g.fillStyle = COL[u.Side]; g.fill(); g.strokeStyle = C.sym; g.lineWidth = 1.3; g.stroke();
    g.fillStyle = C.sym; g.beginPath();
    const X = (a, b) => { g.moveTo(x - a, y - b); g.lineTo(x + a, y + b); g.moveTo(x + a, y - b); g.lineTo(x - a, y + b); };
    switch (u.Kind) {
      case 'Armor': g.ellipse(x, y, 5.5, 3, 0, 0, 7); break;
      case 'Mech': g.ellipse(x, y, 5.5, 3, 0, 0, 7); X(5, 3.5); break;
      case 'Motor': X(5, 3.5); g.moveTo(x, y - 4); g.lineTo(x, y + 4); break;
      case 'Art': g.arc(x, y, 2.2, 0, 7); g.fill(); break;
      case 'Shorad': g.moveTo(x - 4.5, y + 3); g.arc(x, y + 3, 4.5, Math.PI, 0); break;
    }
    g.stroke();
    const ech = /Rgt\b/.test(u.Name) ? 'III' : /Brigade|Brig\./.test(u.Name) && !/Btl|Abt/.test(u.Name) ? 'X' : 'II';
    g.font = `700 8.5px ${MONO}`; g.textAlign = 'center'; g.lineWidth = 2.5; g.strokeStyle = C.sym; g.fillStyle = C.text;
    g.strokeText(ech, x, y - (mine ? 9 : 12)); g.fillText(ech, x, y - (mine ? 9 : 12)); g.textAlign = 'start';
    if (u.Str >= 0) { g.globalAlpha *= 0.85; g.fillStyle = C.sym; g.fillRect(x - 10, yb, 20, 2.5); g.globalAlpha /= 0.85; g.fillStyle = u.Str < 0.35 ? C.red : u.Str < 0.7 ? C.warn : C.ok; g.fillRect(x - 10, yb, 20 * Math.min(1, u.Str), 2.5); }
    g.globalAlpha = 1;
    if (k > 2.6) label(u.Name.replace(/\s*\(.*?\)/, ''), x + 12, y + 4, 'dim');
  }
}

function heading(f) {
  const t = now(), n = f.Route.find(r => T(r.T) > t);
  return n ? Math.atan2(n.Z - f.Z, n.X - f.X) : 0;
}

/// Geplante Position eines eigenen Flugs zur Zeit t aus den Wegpunktzeiten (wie Air.PosAt); null = nicht in der Luft.
function posAt(f, t) {
  const r = f.Route;
  if (!r.length || t < T(r[0].T) || t > T(r[r.length - 1].T)) return null;
  for (let i = 1; i < r.length; i++) {
    const a = r[i - 1], b = r[i], tb = T(b.T);
    if (t > tb) continue;
    const k = b.Kind === 'orbit2' ? 0 : (t - T(a.T)) / Math.max(1000, tb - T(a.T));
    return { X: a.X + (b.X - a.X) * k, Z: a.Z + (b.Z - a.Z) * k, h: Math.atan2(b.Z - a.Z, b.X - a.X) };
  }
  return null;
}

function flights(g) {
  const s = sel?.t === 'f' ? sel.id : -1, t = now() + tplus * 60e3, k = kk();
  for (const f of W.Flights) {
    let p = f, hd;
    if (tplus) { if (f.Src || /gelandet|verloren/.test(f.Status) || !(p = posAt(f, t))) continue; hd = p.h; }   // Zeitschieber: Plan statt Lage
    else { if (!up(f) || f.Status === 'gelandet') continue; const q = !f.InDcs && posAt(f, t); if (q) { p = q; hd = q.h; } else hd = heading(f); }   // abstrakt: Route wie Air.PosAt, flüssig
    const x = px(p.Z), y = py(p.X);
    if (!vis(x, y)) continue;
    HB.push(['f', f.Id, x, y]);
    g.save(); g.translate(x, y); g.rotate(hd); g.globalAlpha = tplus ? 0.55 : 1;
    g.beginPath(); g.moveTo(0, -10); g.lineTo(7, 7); g.lineTo(0, 3.5); g.lineTo(-7, 7); g.closePath();
    g.fillStyle = f.Src ? COL.Red : f.Player ? C.accent : COL[f.Side]; g.fill(); g.strokeStyle = C.sym; g.lineWidth = f.Player ? 1.2 : 1; g.stroke(); g.restore();
    const me = f.Player || f.Id === s;
    if (f.Src) label(`${f.N}× ?`, x + 10, y - 6, 'thr');
    else if (k > 0.9 || me) label(f.Callsign, x + 10, y - 6, f.Player ? 'key' : '');
  }
}

/// Zeile unter dem Zeiger (Flug oder neuer Auftrag) → Route auf der Karte
let hov = null;
addEventListener('mouseover', e => {
  const t = e.target.closest?.('[data-t="f"][data-id], [data-offer]'), h = t ? t.dataset.offer ?? 'f' + t.dataset.id : null;
  if (h !== hov) { hov = h; redraw(); }
});

/// Routen: eigener Flug, ausgewählter Flug (+ sein Paket), Flug unter dem Zeiger. Wegpunkte des bearbeitbaren Flugs größer.
function routes(g) {
  const show = new Map();
  if (W.pf && W.pf.Status !== 'gelandet' && W.pf.Status !== 'verloren') show.set(W.pf.Id, 2);
  const sf = sel?.t === 'f' ? W.fl.get(sel.id) : null;
  if (sf && own(sf)) { for (const f of W.Flights) if (f.Pkg === sf.Pkg && own(f) && !show.has(f.Id)) show.set(f.Id, 1); show.set(sf.Id, 2); }
  const hf = hov?.[0] === 'f' && W.fl.get(+hov.slice(1));
  if (hf && own(hf)) show.set(hf.Id, 2);
  const t = hov?.[0] === 'o' && W.Offers[+hov.slice(1)], q = t && W.sq.get(t.Sqn), b = q && W.ob.get(q.Base);
  if (b) {                                                // neuer Auftrag: noch keine Route – Linie Heimatplatz → Ziel
    g.strokeStyle = TASKC[t.Task] || '#ddd'; g.lineWidth = 2; g.setLineDash([6, 4]);
    g.beginPath(); g.moveTo(px(b.Z), py(b.X)); g.lineTo(px(t.Z), py(t.X)); g.stroke(); g.setLineDash([]);
    g.beginPath(); g.rect(px(t.Z) - 6, py(t.X) - 6, 12, 12); g.lineWidth = 1.6; g.stroke();
  }
  const ed = edFlight();
  for (const [id, lvl] of [...show].sort((a, b) => a[1] - b[1])) {
    const f = W.fl.get(id), r = rt(f);
    if (!r.length) continue;
    g.globalAlpha = lvl === 2 ? 0.95 : 0.5; g.strokeStyle = TASKC[f.Task] || '#ddd'; g.lineWidth = lvl === 2 ? 2.2 : 1.3; g.lineJoin = 'round';
    g.beginPath(); r.forEach((p, i) => i ? g.lineTo(px(p.Z), py(p.X)) : g.moveTo(px(p.Z), py(p.X))); g.stroke();
    if (lvl === 2) r.forEach((p, i) => {
      const x = px(p.Z), y = py(p.X), big = f === ed && i > 0 && i < r.length - 1;
      g.beginPath();
      if (p.Kind === 'target') g.rect(x - 6, y - 6, 12, 12);
      else if (p.Kind === 'ip') { g.moveTo(x, y - 7); g.lineTo(x + 6.5, y + 5); g.lineTo(x - 6.5, y + 5); g.closePath(); }
      else g.arc(x, y, big ? 6 : 4, 0, 7);
      g.fillStyle = C.bg; g.fill(); g.lineWidth = 1.6; g.stroke();
      if (i > 0 && i < r.length - 1) label(String(i), x + 8, y + 4, 'wp');
    });
    g.globalAlpha = 1;
  }
}

function selPos() {
  const o = item(sel);
  if (!o) return null;
  const b = HB.find(b => b[0] === sel.t && b[1] === sel.id);
  if (b) return [b[2], b[3]];
  return sel.t === 'f' && !up(o) ? null : [px(o.Z), py(o.X)];
}

/// Maßstab oben in der Mitte
function scale(g) {
  const m = 130 / V.s / NM, steps = [1, 2, 5, 10, 20, 50, 100, 200];
  const nm = steps.findLast(s => s <= m) || 1, w = nm * NM * V.s, x = cw() / 2 - w / 2, y = 70;
  g.strokeStyle = C.dim; g.lineWidth = 1.5; g.beginPath(); g.moveTo(x, y - 5); g.lineTo(x, y); g.lineTo(x + w, y); g.lineTo(x + w, y - 5); g.stroke();
  label(`${nm} NM`, x + w / 2 - 14, y - 7, 'scale');
}

// ---------- Klicks auf der Karte ----------

function edFlight() {
  const f = sel?.t === 'f' ? W.fl.get(sel.id) : W.pf;
  return editable(f) ? f : null;
}
function hitWp(X, Y) {
  const f = edFlight();
  if (!f) return -1;
  const r = rt(f);
  for (let i = r.length - 2; i > 0; i--) if ((px(r[i].Z) - X) ** 2 + (py(r[i].X) - Y) ** 2 < 100) return i;
  return -1;
}
function hit(X, Y) {
  let best = null, bd = 14 * 14;
  const test = (t, id, x, y) => { const d = (x - X) ** 2 + (y - Y) ** 2; if (d < bd) { bd = d; best = { t, id }; } };
  for (const t of 'fuoc') for (const b of HB) if (b[0] === t) test(...b);
  if (!best) for (const n of S.Nodes) test('n', n.Id, px(n.Z), py(n.X));
  return best;
}
function segDist(x, y, ax, ay, bx, by) {
  const dx = bx - ax, dy = by - ay, l = dx * dx + dy * dy, t = l ? Math.max(0, Math.min(1, ((x - ax) * dx + (y - ay) * dy) / l)) : 0;
  return Math.hypot(x - ax - t * dx, y - ay - t * dy);
}

async function sendRoute(f, r) {
  pending = { id: f.Id, r };
  redraw();
  await run(async () => {
    const wps = r.slice(1, -1).map(p => ({ Name: p.Name, Kind: p.Kind, X: p.X, Z: p.Z, Alt: p.Alt, Spd: p.Spd }));
    assess = { id: f.Id, key: '', lines: await api('flight', { Flight: f.Id, Wps: wps }) };
  });
  pending = null;
  redraw();
}

function insertWp(f, X, Y) {
  const r = structuredClone(rt(f));
  let bi = -1, bd = 15;
  for (let i = 1; i < r.length; i++) {
    if (r[i].Kind === 'orbit2') continue;                                   // nicht in die Station hinein
    const d = segDist(X, Y, px(r[i - 1].Z), py(r[i - 1].X), px(r[i].Z), py(r[i].X));
    if (d < bd) { bd = d; bi = i; }
  }
  if (bi < 0) return toast('Alt+click directly on a route leg to insert a waypoint there.');
  const a = r[bi - 1], b = r[bi];
  r.splice(bi, 0, { Name: '', Kind: 'nav', X: mx(Y), Z: mz(X), Alt: a.Kind === 'start' ? b.Alt : a.Alt, Spd: a.Spd || b.Spd, T: a.T });
  sendRoute(f, r);
}

cv.addEventListener('mousedown', e => {
  if (e.button !== 0 || !W) return;
  const f = edFlight();
  if (f && e.altKey) { e.preventDefault(); insertWp(f, e.offsetX, e.offsetY); return; }
  const i = hitWp(e.offsetX, e.offsetY);
  if (i > 0 && rt(f)[i].Kind !== 'target') drag = { f, i, r: structuredClone(rt(f)), x: e.clientX, y: e.clientY };
  else drag = { pan: true, x: e.clientX, y: e.clientY, cx: V.cx, cz: V.cz };
});
addEventListener('mousemove', e => {
  if (!drag) return;
  const dx = e.clientX - drag.x, dy = e.clientY - drag.y;
  if (!drag.moved && dx * dx + dy * dy < 16) return;
  drag.moved = true;
  if (drag.pan) { V.cz = drag.cz - dx / V.s; V.cx = drag.cx + dy / V.s; cv.style.cursor = 'grabbing'; }
  else {
    const b = cv.getBoundingClientRect(), p = drag.r[drag.i];
    p.X = mx(e.clientY - b.top); p.Z = mz(e.clientX - b.left);
    pending = { id: drag.f.Id, r: drag.r };
  }
  redraw();
});
addEventListener('mouseup', e => {
  const d = drag;
  drag = null; cv.style.cursor = '';
  if (!d) return;
  if (d.moved) { if (!d.pan) sendRoute(d.f, d.r); return; }
  if (e.target !== cv) return;
  const h = hit(e.offsetX, e.offsetY);
  if (JSON.stringify(h) !== JSON.stringify(sel)) { form.task = ''; form.sqn = ''; }
  sel = h;
  render();
});
cv.addEventListener('contextmenu', e => {
  e.preventDefault();
  const f = edFlight(), i = hitWp(e.offsetX, e.offsetY);
  if (i <= 0) return;
  const r = structuredClone(rt(f));
  if (['target', 'orbit', 'orbit2'].includes(r[i].Kind)) return toast('Target and station points cannot be deleted.');
  r.splice(i, 1);
  sendRoute(f, r);
});
cv.addEventListener('mousemove', e => {                    // Hover: Zeiger + Name
  if (drag || !W) return;
  const h = hit(e.offsetX, e.offsetY), o = item(h);
  cv.style.cursor = o ? 'pointer' : '';
  cv.title = !o ? '' : h.t === 'f' ? (o.Src ? 'Contact' : `${o.Callsign} · ${o.Task}`) : h.t === 'c' ? (o.Navy ? o.What : o.Ship ? 'Freighter' : o.Rail ? 'Freight train' : 'Convoy') : o.Name;
});
cv.addEventListener('wheel', e => { e.preventDefault(); zoom(e.offsetX, e.offsetY, Math.exp(-e.deltaY * 0.0015)); }, { passive: false });
addEventListener('mousedown', () => down = true, true);
addEventListener('mouseup', () => down = false, true);       // Klick folgt im selben Task: kein Umbau dazwischen
addEventListener('resize', () => { redraw(); if (W) render(); });

// ---------- Kopfleiste, Leiste, Ebenen ----------

function render() {
  if (!W) return;
  if (W.You) st.screen = 'map';                                             // Mitspieler: kein Hauptmenü
  const onMap = st.screen === 'map';
  screen();
  for (const s of ['#top', '#rail', '#wins']) $(s).style.display = onMap ? '' : 'none';
  if (onMap) { topbar(); dock(); layers(); wins(); } else $('#layers').hidden = true;
  info();
  $('#over').hidden = !(W.Over || W.Truce) || !onMap;
  $('#over').innerHTML = W.Over ? esc(W.Over) : `${esc(W.Truce)} <button data-act="truce" data-v="1">Accept</button> <button data-act="truce" data-v="">Reject</button>`;
  redraw();
}

function topbar() {
  const d = Math.floor((now() - T(W.Start)) / 864e5) + 1, dn = new Date(now()).getUTCHours(), run = W.Speed || st.run;
  const p = W.pf && !/gelandet|verloren|gestrichen/.test(W.pf.Status) ? W.pf : null;
  let right = '';
  if (W.Mission) right = `<div class="stat ${W.MissionLive ? 'ok' : 'warn'}" title="${esc(W.MissionFile)}"><i></i>${W.MissionLive ? 'DCS mission running' : 'Mission ready – open in DCS'}</div><button class="danger" data-act="end" ${W.You ? 'hidden' : ''}>End mission</button>`;
  else if (p) {
    const dt = Math.round((T(p.Takeoff) - now()) / 60e3);
    right = `<div class="pf" data-act="myflt" style="cursor:pointer" title="Open planning · Takeoff ${hm(p.Takeoff)} · TOT ${hm(p.Tot)} · ${esc(p.Task)}"><span class="lbl">Your flight</span><b class="mono">${esc(p.Callsign)}</b><span class="cd mono">${dt > 0 ? `T−${Math.floor(dt / 60)}:${pad(dt % 60)}` : esc(en(p.Status))}</span></div><button class="pri" data-act="fly" ${p.Status !== 'geplant' ? 'disabled' : ''} ${W.You ? 'hidden' : ''}>Fly</button>`;
  }
  if (W.Atc) right = `<div class="stat warn" title="DCS-ATC: tower, AWACS and radio belong to the campaign"><i></i>${esc(W.Atc)}</div>${/nicht|abgebrochen|not|abort|fail/i.test(W.Atc) ? '<button data-act="atc">Retry</button>' : ''}` + right;
  const dd = spdOpen && !W.Mission ? `<div class="dd">${S.Speeds.filter(v => v).map(v => `<button data-act="setrun" data-v="${v}" class="${v === run ? 'sel' : ''}"><span class="mono">${v}×</span>${v === 1 ? '<span class="dim">real time</span>' : ''}</button>`).join('')}<span class="ddsep"></span><button data-act="plus1"><span class="mono">+1 h</span><span class="dim">skip ahead</span></button></div>` : '';
  put($('#top'), `<div class="op">Op ${esc(S.MapName)}</div><span class="vsep"></span>
    <div class="clk mono" title="War time (local) · ${dtg(W.Time)}">${hm(W.Time)}<span>${day(W.Time)} · Day ${d} ${dn >= 7 && dn < 19 ? '☀' : '☾'}</span></div>
    <div class="tc" ${W.You ? 'hidden' : ''}><button class="ib2 ${W.Speed && !W.Mission ? 'on' : ''}" data-act="play" ${W.Mission ? 'disabled' : ''} title="${W.Speed ? 'Pause' : 'Run'} clock (Space)">${W.Speed && !W.Mission ? '❚❚' : '▶'}</button><button class="spd mono" data-act="spdd" ${W.Mission ? 'disabled' : ''} title="Time compression">${W.Mission ? 'DCS' : run + '×'} <span>▾</span></button>${dd}</div>
    <span class="grow"></span>${right}<button class="ib2" data-act="menu" title="Main menu" ${W.You ? 'hidden' : ''}>☰</button>`);
}

function dock() {
  const p = W.pf, dot = { flug: p && p.Status === 'geplant', deb: W.Debrief.length && !st.wins.deb?.open && false };
  put($('#rail'), Object.entries(WINS).map(([id, [t, k, s]]) => `<button data-act="win" data-id="${id}" class="${st.wins[id]?.open ? 'on' : ''}" title="${t} (key ${k})"><span class="k">${k}</span><span class="t">${s}</span>${dot[id] ? '<i class="dot"></i>' : ''}</button>`).join('')
    + `<span class="grow"></span><button data-act="layt" class="${st.layOpen ? 'on' : ''}" title="Layers (key E)"><span class="k">E</span><span class="t">Layers</span></button><span class="rsep"></span><button data-act="zin" title="Zoom in" class="z">+</button><button data-act="zout" title="Zoom out" class="z">−</button><button data-act="zfit" title="Whole front" class="z">⌂</button>`);
}

const tplusTxt = () => tplus ? `+${tplus} min · ${hm(iso(now() + tplus * 60e3))}` : 'now';
function layers() {
  const el = $('#layers');
  el.hidden = !st.layOpen;
  if (!st.layOpen) return;
  const cb = (k, t, tip = '') => `<label title="${tip}"><input type="checkbox" data-lay="${k}" ${lay[k] ? 'checked' : ''}> ${t}</label>`;
  const sw = s => `<svg width="18" height="14" viewBox="0 0 18 14">${s}</svg>`;
  put(el, `<div class="lh"><span>Layers</span><span class="grow"></span><button class="ib" data-act="layt" title="Close">×</button></div><div class="lb">
    <h4>Map</h4>${cb('terr', 'Territory and front')}${cb('towns', 'Towns')}${cb('roads', 'Roads and passes')}${cb('rail', 'Railways')}${cb('sat', 'Satellite image')}
    <h4>Situation</h4>${cb('units', 'Ground units')}${cb('objs', 'Targets')}${cb('conv', 'Convoys, trains, freighters')}${cb('supply', 'Supply lines', 'Unit → depot (colour = supply level), convoy routes')}${cb('air', 'Flights')}
    <label title="Own flights as planned at now + x">Time <input type="range" data-rng="tplus" min="0" max="240" step="5" value="${tplus}"><span class="v">${tplusTxt()}</span></label>
    <h4>Air defence</h4>${cb('threats', 'SAM threat rings')}${cb('radar', 'Radar', 'EW radars out to the radar horizon, altitude AGL')}
    <label>Altitude <input type="range" data-rng="alt" min="0" max="40000" step="1000" value="${lay.alt}"><span class="v">${Math.round(lay.alt / 1000)}k ft</span></label>
    <div class="legend">
      ${sw(`<rect x="1" y="2" width="16" height="10" fill="${COL.Blue}" stroke="${C.sym}"/>`)}<span>friendly unit</span>
      ${sw(`<path d="M9,1L16,7L9,13L2,7Z" fill="${COL.Red}" stroke="${C.sym}"/>`)}<span>enemy</span>
      ${sw(`<circle cx="9" cy="7" r="5" fill="${COL.None}" stroke="${C.sym}"/>`)}<span>airbase</span>
      ${sw(`<path d="M9,2L15,12H3Z" fill="${COL.None}" stroke="${C.sym}"/>`)}<span>SAM site</span>
      ${sw(`<rect x="4" y="2" width="10" height="10" fill="${COL.None}" stroke="${C.sym}"/>`)}<span>depot</span>
      ${sw(`<rect x="2" y="5" width="14" height="4" fill="${COL.None}" stroke="${C.sym}"/>`)}<span>bridge</span>
      ${sw(`<rect x="0" y="4" width="18" height="6" fill="${COL.None}" stroke="${C.sym}"/><path d="M6,4V10M12,4V10" stroke="${C.sym}"/>`)}<span>freight train</span>
      ${sw(`<path d="M0,6H18L15,12H3Z" fill="${COL.None}" stroke="${C.sym}"/>`)}<span>freighter</span>
      ${sw(`<path d="M1,7H17" stroke="${C.front}" stroke-width="2.5"/>`)}<span>front line</span>
      ${sw(`<path d="M4,2L14,12M14,2L4,12" stroke="${C.front}" stroke-width="2.5"/>`)}<span>hot spot</span>
    </div>
    <p class="note">Wheel = zoom · drag = pan · click = select. Own planned flight: drag waypoints, Alt+click on the route = insert, right-click = delete.</p></div>`);
}

// ---------- Fenster ----------
// fest angedockt: links Lage und Meldungen, rechts Flug, in der Mitte von unten der Rest (Höhe je Fenster, schrumpft bei Platzmangel)
const DOCK = { lage: 'L', ev: 'L', flug: 'R' }, HGT = { ato: 290, log: 620, sqn: 640, deb: 680, pilot: 680 };
const winState = id => st.wins[id] || (st.wins[id] = { open: false });
function openWin(id, on = true) { const w = winState(id); w.open = on; if (on) w.min = false; saveSt(); render(); }
function wins() {
  const box = $('#wins');
  if (!box.children.length) box.innerHTML = '<div class="dock" id="dock-L"></div><div class="dock" id="dock-C"></div><div class="dock" id="dock-R"></div>';
  for (const [id, [t, key]] of Object.entries(WINS)) {
    const w = winState(id);
    let el = document.getElementById('w-' + id);
    if (!w.open) { el?.remove(); continue; }
    if (!el) {
      el = document.createElement('section'); el.id = 'w-' + id; el.className = 'win'; el.dataset.win = id;
      el.innerHTML = `<header class="win-h"><b class="ttl">${t}</b><span class="sub"></span><span class="grow"></span><button class="ib" data-act="wmin" data-id="${id}" title="Collapse">–</button><button class="ib" data-act="wclose" data-id="${id}" title="Close (key ${key})">×</button></header><div class="win-b"></div>`;
      if (HGT[id]) el.style.setProperty('--h', HGT[id] + 'px');
      $('#dock-' + (DOCK[id] || 'C')).append(el);
    }
    el.classList.toggle('min', !!w.min);
    put(el.querySelector('.sub'), SUB[id]());
    if (!w.min) put(el.querySelector('.win-b'), PANEL[id]());
  }
}

const SUB = {
  lage: () => `Phase ${W.Cmd.Phase} · ${esc(S.Phases[W.Cmd.Phase])}`,
  ato: () => `${W.Flights.filter(f => own(f) && f.Status === 'geplant').length} planned · ${W.Flights.filter(f => own(f) && UP.includes(f.Status)).length} airborne`,
  flug: () => myFlight() ? `your flight ${esc(W.pf.Callsign)} · ${esc(en(W.pf.Status))}` : 'no flight selected',
  log: () => `${W.Convoys.filter(own).length} transports en route`,
  sqn: () => `${W.Sqns.filter(own).reduce((a, q) => a + q.Ac, 0)} aircraft ready`,
  ev: () => `${W.Events.length} reports`,
  deb: () => W.Debrief.length ? 'last DCS mission' : 'no mission yet',
  pilot: () => { const c = me(); return c ? `${esc(c.Rank)} ${esc(c.Name)} · ${esc(en(c.Status))}` : 'no pilot yet'; },
};

const PRIO = [['OCA', 'Airbases (OCA)'], ['DEAD', 'Air defences (DEAD)'], ['Strike', 'Command, depots, industry'], ['Interdiction', 'Supply lines'], ['Antiship', 'Warships (anti-ship)'], ['BAI', 'Troops behind the front'], ['CAS', 'Close air support']];
const me = () => [...(W.Logbook || [])].reverse().find(c => c.Name === W.Me && ['aktiv', 'vermisst'].includes(c.Status));
const PANEL = {
  lage() {
    const c = W.Cmd, mine = W.Units.filter(own), foe = W.Units.filter(u => !own(u)), x = W.Wx;
    const avg = (l, k) => l.length ? l.reduce((a, u) => a + u[k], 0) / l.length : 0;
    const nw = (W.News || []).map(l => l.split('|')), d0 = nw.length ? nw[nw.length - 1][0].slice(0, 10) : '';
    let h = nw.length ? `<h2>Front Line Daily <span class="dim">${day(nw[nw.length - 1][0])}</span></h2>` + nw.filter(n => n[0].startsWith(d0)).reverse().map((n, i) => `<div class="news${i ? '' : ' top'}"><b>${esc(n[1])}</b><span><span class="mono">${hm(n[0])}</span> · ${esc(n[2])}</span></div>`).join('') : '';
    h += '<h2>Commander – air campaign phase</h2><div class="phase">'
      + [['0', 'Auto', 'AI decides'], ...S.Phases.slice(1).map((n, i) => [String(i + 1), String(i + 1), n])].map(([v, a, n]) => `<button data-act="phase" data-v="${v}" class="${String(c.Set) === v ? 'on' : ''} ${v !== '0' && +v === c.Phase ? 'cur' : ''}"><b>${a}</b>${esc(n)}</button>`).join('')
      + `</div><p class="why">${c.Set ? 'Fixed. ' : `AI: phase ${c.Phase} “${esc(S.Phases[c.Phase])}”. `}${esc(c.Why)}</p>`
      + `<p class="why">War phase ${W.Esc + 1}/3 · ROE: ${['military targets only', 'factories, power plants and ports cleared', 'government and high command cleared'][W.Esc]}</p>`;
    h += '<h2>Targeting priorities</h2><table class="prio">' + PRIO.map(([t, n]) => `<tr><td>${n}</td><td><div class="phase p3">${['off', 'normal', 'high'].map((l, v) => `<button data-act="prio" data-t="${t}" data-v="${v}" class="${(c.Prio[t] ?? 1) === v ? 'on' : ''}">${l}</button>`).join('')}</div></td></tr>`).join('')
      + `<tr><td>Main effort</td><td><div class="phase" style="grid-template-columns:repeat(${S.Axes.length + 1},minmax(0,1fr))">${[[-1, 'none'], ...S.Axes.map((a, i) => [i, a.Name])].map(([v, n]) => `<button data-act="focus" data-v="${v}" class="${c.Focus === v ? 'on' : ''}">${esc(n)}</button>`).join('')}</div></td></tr></table>`;
    if (W.Report.length) h += `<details class="rep" data-tog="rep" ${repOpen ? 'open' : ''}><summary>${esc(W.Report[0])}</summary><div>${W.Report.slice(1).filter(l => l).map(l => /^[A-ZÄÖÜ]{4,}( |$)/.test(l) ? `<b>${esc(l)}</b>` : esc(l) + '<br>').join('')}</div></details>`;
    if (W.Downed) h += `<div class="msg bad">${W.Downed} pilot(s) missing – CSAR under way</div>`;
    const wn = [x.Snow ? 'Snow – passes closed.' : x.Rain ? 'Rain.' : '',
      !x.FrontKmh ? '' : x.FrontKm > 40 ? `Weather front ${Math.round(x.FrontKm)} km west, arriving in approx. ${Math.round(x.FrontKm / x.FrontKmh)} h – no new strikes after that.` : x.FrontKm > -40 ? 'Weather front passing through – no new strikes.' : x.FrontKm > -150 ? 'Weather front has passed.' : ''].filter(Boolean).join(' ');
    h += `<h2>Weather</h2><div class="wxl"><span><b>${Math.round(x.Cloud * 100)} %</b> at ${Math.round(x.Base / FT / 100) * 100} ft</span><span>Wind <b>${String(Math.round(x.WindDir) % 360).padStart(3, '0')}°/${Math.round(x.Wind / KT)} kt</b></span><span><b>${Math.round(x.Temp)} °C</b> · QNH ${Math.round(x.Qnh * 1.33322)}</span>${x.Vis < 8000 ? `<span>Vis <b>${Math.round(x.Vis / 1000)} km</b></span>` : ''}</div>`
      + (wn ? `<p class="note">${wn}</p>` : '');
    h += '<h2>Front</h2><table>';
    S.Axes.forEach((a, i) => {
      const f = W.Fronts[i], att = W.Units.filter(u => u.Axis === i && u.Order === 'Angriff'), op = W.Ops[i];
      h += `<tr class="click" data-act="goto" data-x="${f ? S.Nodes[f[0]].X : 0}" data-z="${f ? S.Nodes[f[0]].Z : 0}"><td><b>${esc(a.Name)}</b>`
        + (op?.Op ? ` <span class="tag ${op.OpSide}">${op.OpSide === W.Player ? 'friendly' : 'enemy'} operation: ${esc(en(op.Op))} · ${ago(op.OpSince)}</span>` : att.length ? ' <span class="warn">⚔ Attack</span>' : '') + '<br>'
        + a.Keys.map(k => `<span class="tag ${['', 'Red', 'Blue'][W.Owners[k]]}">${esc(S.Nodes[k].Name)}</span>`).join('') + '</td></tr>';
    });
    const sq = W.Sqns.filter(own), ac = sq.reduce((a, q) => a + q.Ac, 0), size = sq.reduce((a, q) => a + q.Size, 0), lost = sq.reduce((a, q) => a + q.Lost, 0);
    h += '</table><h2>Forces</h2><div class="kpi">'
      + `<div><span>Friendly ground units</span><b>${mine.length}</b><small>Strength ${pct(avg(mine, 'Str'))} · Ammo ${pct(avg(mine, 'Supply'))} · Fuel ${pct(avg(mine, 'Fuel'))}</small></div>`
      + `<div><span>Enemy (detected)</span><b>${foe.length}</b><small>est. strength ${pct(avg(foe, 'Str'))}</small></div>`
      + `<div><span>Aircraft ready</span><b>${ac}/${size}</b><small>${W.Flights.filter(f => own(f) && UP.includes(f.Status)).length} flights airborne · ${lost} lost</small></div>`
      + `<div><span>Air contacts</span><b>${W.Flights.filter(f => f.Src).length}</b><small>Radar / AWACS</small></div></div>`;
    const tg = W.Objs.filter(o => !own(o) && o.Side !== 'None').sort((a, b) => a.Kind.localeCompare(b.Kind) || a.Name.localeCompare(b.Name));
    const aimed = new Set(W.Pkgs.filter(p => p.TargetKind === 'obj').map(p => p.Target));
    h += `<h2>Known targets <span class="dim">(${tg.length})</span></h2><table><tr><th>Target</th><th>Type</th><th>Condition</th><th>Seen</th></tr>`
      + tg.map(o => `<tr class="click${sel?.t === 'o' && sel.id === o.Id ? ' sel' : ''}" data-act="sel" data-t="o" data-id="${o.Id}"><td>${aimed.has(o.Id) ? '✈ ' : ''}${esc(o.Name)}</td><td class="dim">${KIND[o.Kind]}${o.Sys ? ' ' + esc(o.Sys) : ''}</td><td>${bar(o.Health)}</td><td class="dim">${o.Seen ? ago(o.Seen) : ''}</td></tr>`).join('') + '</table>';
    return h;
  },

  ato() {
    const t0 = now() - 3600e3, t1 = now() + 8 * 3600e3, span = t1 - t0, pos = t => Math.max(0, Math.min(100, (T(t) - t0) / span * 100));
    let fl = W.Flights.filter(f => own(f) && T(f.Land) > t0 && T(f.Takeoff) < t1);
    if (atoPlay) fl = fl.filter(f => W.sq.get(f.Sqn)?.Player);
    const ptot = {};                                                       // Paket-TOT aus den Flügen (Stationen haben keine)
    for (const f of fl) ptot[f.Pkg] = Math.min(ptot[f.Pkg] ?? Infinity, T(f.Tot) || T(f.Takeoff));
    fl.sort((a, b) => ptot[a.Pkg] - ptot[b.Pkg] || a.Pkg - b.Pkg || T(a.Takeoff) - T(b.Takeoff));
    let h = `<div class="row" style="margin-top:0"><label style="display:flex;gap:6px;align-items:center;cursor:pointer"><input type="checkbox" data-chg="atoplay" ${atoPlay ? 'checked' : ''}> playable squadrons only</label><span class="grow"></span>${['CAP', 'SEAD', 'DEAD', 'CAS', 'OCA', 'Interdiction'].map(chip).join(' ')}</div><div class="gantt"><div class="gaxis">`;
    for (let t = Math.ceil(t0 / 3600e3) * 3600e3; t < t1; t += 3600e3) h += `<span style="left:${(t - t0) / span * 100}%">${iso(t).slice(11, 13)}</span>`;
    h += '</div>';
    let pk = null;
    for (const f of fl) {
      if (f.Pkg !== pk) { pk = f.Pkg; const p = W.pk.get(pk); h += `<div class="gpk" title="${esc(p?.Why)}"><b>${esc(p?.Name ?? 'Package ' + pk)}</b><span>TOT ${hm(iso(ptot[pk]))}</span>${!W.You && W.Flights.some(x => x.Pkg === pk && takeable(x)) ? `<button class="ib" data-act="takepkg" data-id="${pk}" title="Multiplayer: every aircraft of this package becomes a client slot – players claim the flights">→ players</button>` : ''}</div>`; }
      const l = pos(f.Takeoff), w = Math.max(0.6, pos(f.Land) - l), done = /gelandet|verloren/.test(f.Status), me = sel?.t === 'f' && sel.id === f.Id;
      h += `<div class="grow2 ${me ? 'sel' : ''}" data-act="sel" data-t="f" data-id="${f.Id}" title="${esc(`${f.Callsign} · ${typ(f.Type)} ×${f.Alive}/${f.N} · ${f.Task} · ${en(f.Status)}\nTakeoff ${hm(f.Takeoff)} · TOT ${hm(f.Tot)} · Landing ${hm(f.Land)}`)}">`
        + `<span class="gl">${f.Player ? '★ ' : ''}${esc(f.Callsign)}${f.Status === 'verloren' ? ' ✝' : ''}</span><span class="gt"><span class="gbar" style="left:${l}%;width:${w}%;background:${TASKC[f.Task] || '#ccc'};opacity:${done ? 0.3 : 0.9};${f.Player ? 'outline:2px solid var(--accent);outline-offset:1px;' : ''}"></span>`
        + (f.Tot ? `<span class="gtot" style="left:${pos(f.Tot)}%"></span>` : '') + '</span></div>';
    }
    h += `<div class="gnow" style="left:calc((100% - 120px) * ${(now() - t0) / span})"></div></div>`;
    if (!fl.length) h += '<p class="note">No friendly flights in the next 8 hours.</p>';
    return h + '<p class="note">Bar = takeoff to landing, tick = TOT, ★ = your flight. Click shows the flight and its package on the map. New package: click a target on the map.</p>';
  },

  flug() {
    const f = myFlight(), t = f ? (FTABS.some(x => x[0] === st.ftab) ? st.ftab : 'plan') : 'list', i = FTABS.findIndex(x => x[0] === t);
    let h = `<div class="tabs steps">${FTABS.map(([k, n], j) => `<button data-act="ftab" data-v="${k}" class="${k === t ? 'on' : ''}" ${f || !j ? '' : 'disabled'}><i>${j + 1}</i>${n}</button>`).join('')}</div>`;
    if (W.Mission) h += `<div class="msg ${W.MissionLive ? '' : 'warn'}">${W.MissionLive ? 'DCS mission running – the war clock follows DCS time.' : `Mission ready: in DCS Mission → folder Kampagne → ${esc(fname(W.MissionFile))}`} <button class="stop" data-act="end" style="margin-left:8px">End mission</button></div>`;
    h += FTAB[t](f);
    if (f) h += `<div class="wnav">${i > 0 ? `<button data-act="ftab" data-v="${FTABS[i - 1][0]}">‹ ${FTABS[i - 1][1]}</button>` : '<span></span>'}`
      + `${i < FTABS.length - 1 ? `<button class="go" data-act="ftab" data-v="${FTABS[i + 1][0]}">Next: ${FTABS[i + 1][1]} ›</button>` : ''}</div>`;
    return h;
  },
  log() {
    const ob = W.Objs.filter(o => own(o) && ['Depot', 'Factory', 'Port', 'Airbase'].includes(o.Kind)).sort((a, b) => a.Kind.localeCompare(b.Kind) || a.Name.localeCompare(b.Name));
    const t = (v, cap) => cap > 0 ? `${bar(v / cap, 44)} <span class="mono">${Math.round(v)} t</span>` : v > 0 ? `<span class="mono">${Math.round(v)} t</span>` : '–';
    let h = '<h2>Supply inflow <span class="dim">trains 05:00 and 17:00</span></h2>' + W.Gates.map(g => `<div class="msg ${g.Open ? '' : 'bad'}">${esc(g.At)}: ${g.Open ? `open · ${g.Tons} t per train` : 'cut – railway station lost'}</div>`).join('');
    h += '<h2>Depots, factories, ports, airbases</h2><table><tr><th>Name</th><th>Condition</th><th>Ammo</th><th>Fuel</th></tr>'
      + ob.map(o => `<tr class="click" data-act="sel" data-t="o" data-id="${o.Id}"><td>${esc(o.Name)}<br><span class="dim">${KIND[o.Kind]}</span></td><td>${bar(o.Health, 44)}</td><td>${t(o.Stock, o.Cap)}</td><td>${o.Kind === 'Factory' ? '' : t(o.Fuel, o.Cap)}</td></tr>`).join('') + '</table>';
    const br = W.Objs.filter(o => own(o) && o.Kind === 'Bridge' && o.Flow > 1).sort((a, b) => b.Flow - a.Flow).slice(0, 6);
    if (br.length) h += '<h2>Chokepoints <span class="dim">bridges by supply traffic</span></h2><table>' + br.map(o => `<tr class="click" data-act="sel" data-t="o" data-id="${o.Id}"><td>${esc(o.Name)}</td><td class="n">~${Math.round(o.Flow)} t/day</td><td>${bar(o.Health)}</td></tr>`).join('') + '</table>';
    const co = W.Convoys.filter(own);
    h += `<h2>En route <span class="dim">(${co.length})</span></h2>` + (co.length ? '<table><tr><th>Transport</th><th>To</th><th class="n">Ammo / fuel</th></tr>'
      + co.map(c => `<tr class="click" data-act="sel" data-t="c" data-id="${c.Id}"><td>${esc(c.What)}<br><span class="dim">${c.Trucks} ${c.Navy ? 'ships' : c.Ship ? 'holds' : c.Rail ? 'wagons' : 'trucks'}</span></td><td>${esc(c.To)}${c.Troops ? `<br><span class="dim">${esc(c.Troops)}</span>` : ''}</td><td class="n">${c.Troops ? '–' : `${Math.round(c.Tons)} / ${Math.round(c.Fuel)} t`}</td></tr>`).join('') + '</table>' : '<p class="note">none</p>');
    const low = W.Units.filter(own).sort((a, b) => Math.min(a.Supply, a.Fuel) - Math.min(b.Supply, b.Fuel)).slice(0, 15);
    h += '<h2>Unit supply <span class="dim">lowest first</span></h2><table><tr><th>Unit</th><th>Ammo</th><th>Fuel</th><th>Supply line</th></tr>'
      + low.map(u => `<tr class="click" data-act="sel" data-t="u" data-id="${u.Id}" title="${esc(u.Why)}"><td>${esc(u.Name)}<br><span class="dim">${esc(W.ob.get(u.Dp)?.Name ?? '')}</span></td><td>${bar(u.Supply, 44)}</td><td>${bar(u.Fuel, 44)}</td><td>${u.Line > 0 ? `<span class="mono">${pct(u.Line)}</span>` : '<span class="warn">cut off</span>'}</td></tr>`).join('') + '</table>';
    return h;
  },

  sqn() {
    let h = '<h2>Friendly squadrons</h2><table><tr><th>Squadron</th><th>Base</th><th class="n">Ready</th><th class="n">Rep.</th><th class="n">Lost</th><th class="n">Pilots</th></tr>';
    for (const q of W.Sqns.filter(own)) {
      const act = q.Pilots.filter(p => p.Status === 'aktiv').length;
      h += `<tr class="click" data-act="pilots" data-id="${q.Id}"><td>${open.has(q.Id) ? '▾' : '▸'} <b>${esc(q.Name)}</b>${q.Player ? ' <span class="tag">playable</span>' : ''}<br><span class="dim">${typ(q.Type)} · ${q.Roles.join(', ')}</span></td><td>${esc(baseName(q))}</td><td class="n">${q.Ac}/${q.Size}</td><td class="n">${q.Repair}</td><td class="n">${q.Lost}</td><td class="n">${act}/${q.Pilots.length}</td></tr>`;
      if (open.has(q.Id)) h += `<tr><td colspan="6" style="padding:4px 0 10px 18px"><table><tr><th>Pilot</th><th>Skill</th><th class="n">Sorties</th><th class="n">Kills</th><th>Fitness</th><th>Status</th></tr>`
        + q.Pilots.map(p => `<tr><td>${esc(p.Name)}</td><td>${SKILL[p.Skill] ?? p.Skill}</td><td class="n">${p.Sorties}</td><td class="n">${p.Kills}</td><td>${bar(Math.max(0, 1 - p.Fatigue), 50)}</td><td class="${p.Status === 'aktiv' ? '' : /gefallen|vermisst|gefangen/.test(p.Status) ? 'red' : 'warn'}">${esc(en(p.Status))}</td></tr>`).join('') + '</table></td></tr>';
    }
    h += '</table><h2>Enemy <span class="dim">known squadrons</span></h2><table><tr><th>Squadron</th><th>Type</th><th>Base</th></tr>'
      + W.Sqns.filter(q => !own(q)).map(q => `<tr><td>${esc(q.Name)}</td><td>${typ(q.Type)}</td><td>${esc(baseName(q))}</td></tr>`).join('') + '</table>';
    return h;
  },

  ev() {
    const kinds = [...new Set(W.Events.map(e => e.Kind))].sort();
    const ev = W.Events.filter(e => !evKind || e.Kind === evKind).slice(-300).reverse();
    return `<div class="row" style="margin-top:0"><button data-act="evk" data-v="" class="${!evKind ? 'on' : ''}">all</button>${kinds.map(k => `<button data-act="evk" data-v="${esc(k)}" class="${k === evKind ? 'on' : ''}">${esc(en(k))}</button>`).join('')}</div>` + events(ev);
  },

  pilot() {
    const L = W.Logbook || [], c = me();
    let h = `<h2>Your pilot</h2><p>DCS player name <input data-chg="me" value="${esc(W.Me)}" placeholder="as in DCS" style="width:150px"> <span class="dim">the logbook follows this name – set by your first sortie</span></p>`;
    if (c) {
      const med = {};
      for (const m of c.Medals) { const k = m.replace(/ \(.*$/, ''); med[k] = (med[k] || 0) + 1; }
      h += kv([['Rank', `<b>${esc(c.Rank)}</b>`], ['Status', `<span class="${c.Status === 'aktiv' ? '' : 'red'}">${esc(en(c.Status))}</span>`], ['Sorties', c.Sorties], ['Flight hours', c.Hours.toFixed(1)],
        ['Kills', `${c.Air} air · ${c.Ground} ground`], ['Decorations', Object.entries(med).map(([k, n]) => esc(k) + (n > 1 ? ' ×' + n : '')).join(' · ') || '<span class="dim">none yet</span>']]);
      if (c.Status === 'vermisst') h += '<p class="warn">MIA behind enemy lines – CSAR is on it. No new sorties until you are home.</p>';
      h += '<p><button data-act="newpilot">Retire – start a new pilot</button></p>';
      h += '<h2>Logbook</h2>' + (c.Book.length ? '<table><tr><th>Date</th><th>Flight</th><th>Task</th><th class="n">h</th><th class="n">A/G</th><th>Result</th></tr>'
        + c.Book.map(b => `<tr><td class="mono">${day(b.Up)}<br>${hm(b.Up)}</td><td><span class="mono">${esc(b.Callsign)}</span><br><span class="dim">${typ(b.Type)}</span></td><td>${chip(b.Task)}<br><span class="dim">${esc(b.Target)}</span></td><td class="n">${b.Hours.toFixed(1)}</td><td class="n">${b.Air}/${b.Ground}</td><td>${esc(b.Result)}</td></tr>`).join('') + '</table>'
        : '<p class="dim">No sorties yet.</p>');
    } else h += '<p class="dim">No pilot yet – your first DCS sortie opens the logbook (rank, kills, decorations; shot down = MIA, captured or KIA).</p>';
    const rest = L.filter(x => x !== c);
    if (rest.length) h += '<h2>Roster <span class="dim">all human pilots</span></h2><table><tr><th>Pilot</th><th>Rank</th><th>Status</th><th class="n">Sorties</th><th class="n">A/G</th><th class="n">Medals</th></tr>'
      + rest.slice().reverse().map(x => `<tr><td>${esc(x.Name)}</td><td>${esc(x.Rank)}</td><td class="${x.Status === 'aktiv' ? '' : 'red'}">${esc(en(x.Status))}</td><td class="n">${x.Sorties}</td><td class="n">${x.Air}/${x.Ground}</td><td class="n">${x.Medals.length}</td></tr>`).join('') + '</table>';
    return h;
  },

  deb() {
    const S_ = W.Sorties || [], gc = ['plan', 'soon', 'lost'], gn = ['Success', 'Partial success', 'Failure'];
    let h = '<h2>Last DCS mission</h2>';
    if (S_.length) h += S_.map(s => `<div class="mine"><div class="row" style="margin:0 0 4px;justify-content:space-between"><span><span class="cs">${esc(s.Callsign)}</span> ${typ(s.Type)} · ${chip(s.Task)}${s.Owner ? ' · ' + esc(s.Owner) : ''}</span><span class="st ${gc[s.Grade]}">${gn[s.Grade]}</span></div>`
      + `<div>${esc(s.Why)}</div>${s.Air || s.Ground ? `<div class="rel">kills: ${s.Air} air · ${s.Ground} ground</div>` : ''}${s.Log.length ? `<details><summary class="dim">${s.Log.length} events</summary>${s.Log.map(l => `<div class="rel mono">${esc(l)}</div>`).join('')}</details>` : ''}</div>`).join('')
      + (W.You ? '' : '<div class="btnrow"><button data-act="tacview" title="newest Tacview recording since the mission (Documents\\Tacview)">Open in Tacview</button></div>');
    h += (S_.length ? `<details><summary class="dim">All reports (${W.Debrief.length})</summary>` : '') + (W.Debrief.length ? W.Debrief.map(l => `<div class="msg ${/verloren|abgeschossen|ausgestiegen|vermisst|gefallen|Absturz|lost|shot down|eject|missing|KIA|crash/i.test(l) ? 'bad' : /⚠|nicht|fehl|not |fail/i.test(l) ? 'warn' : ''}">${esc(l)}</div>`).join('') : '<p class="note">No mission debriefed yet.</p>') + (S_.length ? '</details>' : '');
    const done = W.Flights.filter(f => own(f) && /gelandet|verloren/.test(f.Status)).sort((a, b) => T(b.Land) - T(a.Land)).slice(0, 40);
    h += '<h2>Completed friendly flights</h2><table><tr><th>Landing</th><th>Flight</th><th>Status</th><th>Result</th></tr>'
      + done.map(f => `<tr class="click" data-act="sel" data-t="f" data-id="${f.Id}"><td class="mono">${hm(f.Land)}</td><td><span class="mono">${esc(f.Callsign)}</span><br>${chip(f.Task)}</td><td class="${f.Status === 'verloren' ? 'red' : ''}">${esc(en(f.Status))}</td><td>${esc(f.Result) || '<span class="dim">–</span>'}</td></tr>`).join('') + '</table>';
    return h;
  },
};

function events(l) {
  return l.map(e => `<div class="ev ${e.Side}" data-act="goto" data-x="${e.X}" data-z="${e.Z}"><span class="t">${day(e.T)}<br>${hm(e.T)}</span><span>${esc(e.Text)}<span class="kd">${esc(en(e.Kind))}</span></span></div>`).join('');
}

// ---------- Flug: Liste und Planung ----------

const FTABS = [['list', 'Tasking'], ['plan', 'Plan'], ['wpn', 'Weapons'], ['kit', 'Cartridge'], ['go', 'Start']];
const ART = { 'Luft-Luft': ['CAP', 'Escort', 'Sweep'], 'Luft-Boden': ['Strike', 'OCA', 'CAS', 'BAI', 'Interdiction', 'Antiship'], 'SEAD/DEAD': ['SEAD', 'DEAD', 'Jammer'] };
const seg = (act, cur, opts) => `<span class="seg">${opts.map(([v, n]) => `<button data-act="${act}" data-v="${v}" class="${v === cur ? 'on' : ''}">${n}</button>`).join('')}</span>`;
const myFlight = () => W.pf && !/gelandet|verloren|gestrichen/.test(W.pf.Status) ? W.pf : null;
const playable = f => !!W.sq.get(f.Sqn)?.Player;
const takeable = f => own(f) && playable(f) && !W.Mission && (!f.Player || !!W.You && !f.Owner) && (!W.You || W.Mp) && ['geplant', 'in der Luft', 'im Einsatz'].includes(f.Status);
const dur = m => m >= 60 ? `${Math.floor(m / 60)}:${pad(m % 60)} h` : `${m} min`;
const tgt = f => (W.pk.get(f.Pkg)?.Name ?? '').replace(/^(Paket|Package) \d+\s*[–-]\s*/, '');
const kitOf = t => W.Kits[t] || {};

/// Status für Menschen: Farbe + Wort + wann
function fstat(f) {
  const m = Math.round((T(f.Takeoff) - now()) / 60e3);
  switch (f.Status) {
    case 'geplant': return m > 25 ? ['plan', en(f.Status), `takeoff in ${dur(m)}`] : ['soon', m > 0 ? 'starting soon' : 'taxiing', m > 0 ? `takeoff in ${m} min – tight for a cold start` : 'takeoff now'];
    case 'in der Luft': return ['air', en(f.Status), `for ${dur(-m)} · landing ${hm(f.Land)}`];
    case 'im Einsatz': return ['ops', en(f.Status), `TOT ${hm(f.Tot)} · landing ${hm(f.Land)}`];
    case 'Rückflug': case 'Abbruch': return ['back', en(f.Status), `landing ${hm(f.Land)}`];
    case 'gelandet': return ['done', en(f.Status), `landed ${hm(f.Land)}`];
    default: return ['lost', en(f.Status), ''];
  }
}

/// Zeile der Auftragsliste: geplanter Flug (r.f) oder neuer Auftrag (r.o)
function fragRow(r) {
  if (r.f) {
    const x = r.f, [c, t, rel] = fstat(x), up = x.Status !== 'geplant';
    return `<tr data-q="${esc(qs(r))}" class="click${x.Player ? ' me' : ''}" data-act="sel" data-t="f" data-id="${x.Id}" title="${esc(x.Why)}"><td class="mono">${hm(x.Tot)}<div class="rel">${up ? `<span class="st ${c}">${t}</span> ` : ''}${esc(rel)}</div></td>`
      + `<td>${typ(x.Type)} ×${x.N}<div class="rel mono">${esc(x.Callsign)}</div></td><td>${chip(x.Task)}<div class="rel">${esc(tgt(x))}</div></td>`
      + `<td class="n">${takeable(x) ? `<button data-act="take" data-id="${x.Id}" ${up ? 'title="Mission starts in the air at its current position"' : ''}>${x.Player ? 'Claim' : up ? 'Jump in' : W.Mp ? '+ add' : 'Select'}</button>` : x.Player ? `<span class="tag">${W.Mp ? esc(x.Owner || 'in mission') : 'your flight'}</span>` : ''}</td></tr>`;
  }
  const o = r.o, q = W.sq.get(o.Sqn), t = { obj: 'o', unit: 'u' }[o.Kind];
  return `<tr data-q="${esc(qs(r))}" data-offer="o${r.i}" class="${t ? 'click' : ''}" ${t ? `data-act="sel" data-t="${t}" data-id="${o.Id}"` : ''} title="${esc(o.Why)}"><td class="mono">≈ ${hm(o.Tot)}<div class="rel">new · ${o.Nm} NM</div></td>`
    + `<td>${typ(o.Type)} ×2<div class="rel">${esc(q ? baseName(q) : '')}</div></td><td>${chip(o.Task)}<div class="rel">${esc(o.Name)}</div></td>`
    + `<td class="n"><button data-act="offer" data-i="${r.i}">${W.Mp ? '+ add' : 'Select'}</button></td></tr>`;
}

let kprof = '';
/// Filter der Auftragsliste (bleibt im Browser gespeichert); Suchtext je Zeile
const of = Object.assign({ typ: '', art: '', src: '', base: '', t: '', sort: '', q: '' }, recall('of') || {});
const ofSet = (k, v) => { of[k] = v; store('of', of); render(); };
const qs = r => [typ(r.Type), r.Task, r.Base, r.Q].join(' ').toLowerCase();
const tgtWp = x => x.Route.find(p => p.Kind === 'target' || p.Kind === 'orbit') || x.Route[x.Route.length - 1];
/// Mehrspieler: Host lädt Mitspieler ein (Link + Code), Mitspieler sieht, als wer er drin ist
const invite = () => W.You ? `<div class="msg">Joined as <b>${esc(W.You)}</b> – claim a flight below (“Claim” = slot opened by the host), plan it, the host generates the mission.</div>`
  : !W.Mp ? '' : W.Invite ? `<div class="msg">Players open in their browser: ${W.Invite.Ips.map(ip => `<b class="mono">http://${ip}:${W.Invite.Port}/?code=${W.Invite.Code}</b>`).join(' or ') || 'no network found'}<br>Online: ${W.Online.length ? W.Online.map(esc).join(', ') : 'nobody yet'} · over the internet they need a VPN (e.g. the one you use for the DCS server) or port ${W.Invite.Port} forwarded</div>`
  : '<div class="row"><button data-act="invite">Invite players over the network</button><span class="dim">they plan their own flights in their browser · Windows may ask once whether Warclock may use the network</span></div>';
const FTAB = {
  list(f) {
    let h = '';
    const ses = W.Mp ? W.mine.filter(x => !/gelandet|verloren|gestrichen/.test(x.Status)) : [];
    h += invite();
    if (ses.length) {
      h += '<h2>Mission flights <span class="dim">multiplayer</span></h2><table class="ftbl">' + ses.map(x => { const [c, t] = fstat(x);
        return `<tr class="click${x === f ? ' me' : ''}" data-act="cur" data-id="${x.Id}" title="plan"><td class="mono">${hm(x.Tot)}<div class="rel"><span class="st ${c}">${t}</span></div></td>`
          + `<td>${typ(x.Type)} ×${x.N}<div class="rel mono">${esc(x.Callsign)}</div></td><td>${chip(x.Task)}<div class="rel">${[...crew(x)].filter(k => k === 'P').length} players · ${esc(x.Owner || 'open')} · ${esc(tgt(x))}</div></td>`
          + `<td class="n">${W.Mission ? '' : `<button class="ib" data-act="drop" data-id="${x.Id}" title="${W.You ? 'give the slot back – another player can claim it' : 'remove from mission – the AI flies it'}">✕</button>`}</td></tr>`; }).join('') + '</table>';
    } else if (f) {
      const [c, t, rel] = fstat(f);
      h += `<div class="mine"><div class="row" style="margin:0 0 4px;justify-content:space-between"><span class="cs">${esc(f.Callsign)}</span><span class="st ${c}">${t}</span></div>`
        + `<div>${typ(f.Type)} ×${f.Alive} · ${chip(f.Task)} · ${esc(tgt(f))}</div><div class="rel mono">Takeoff ${hm(f.Takeoff)} · TOT ${hm(f.Tot)} – ${esc(rel)}</div></div>`;
    } else if (W.pf) h += `<div class="msg ${W.pf.Status === 'verloren' ? 'bad' : ''}">Last flight ${esc(W.pf.Callsign)}: ${esc(en(W.pf.Status))}${W.pf.Result ? ' – ' + esc(W.pf.Result) : ''}</div>`;
    if (W.Mission && W.pf?.Status === 'gelandet') h += '<div class="msg">Landed. End the mission in DCS – then continue here with the next flight.</div>';
    const all = [...W.Flights.filter(x => own(x) && playable(x) && ['geplant', 'in der Luft', 'im Einsatz'].includes(x.Status)).map(x => ({ f: x, Type: x.Type, Task: x.Task, Tot: x.Tot,
        Base: x.Route[0]?.Name ?? '', Nm: Math.round(Math.hypot(tgtWp(x).X - x.Route[0].X, tgtWp(x).Z - x.Route[0].Z) / 1852), Start: T(x.Takeoff), Q: x.Callsign + ' ' + tgt(x) })),
      ...(W.Mission ? [] : W.Offers.map((o, i) => ({ o, i, Type: o.Type, Task: o.Task, Tot: o.Tot, Base: W.sq.get(o.Sqn) ? baseName(W.sq.get(o.Sqn)) : '', Nm: o.Nm, Start: now() + 45 * 6e4, Q: o.Name })))]
      .sort((a, b) => T(a.Tot) - T(b.Tot));
    const l = all.filter(r => (!of.typ || r.Type === of.typ) && (!of.art || (ART[of.art] ? ART[of.art].includes(r.Task) : r.Task === of.art)) && (!of.src || (of.src === 'neu') === !!r.o)
        && (!of.base || r.Base === of.base) && (!of.t || r.Start - now() <= of.t * 6e4) && (!of.q || qs(r).includes(of.q.toLowerCase())))
      .sort(of.sort === 'nah' ? (a, b) => a.Nm - b.Nm : of.sort === 'typ' ? (a, b) => typ(a.Type).localeCompare(typ(b.Type)) : () => 0);
    const opt = (k, list) => `<select data-chg="of" data-k="${k}">${list.map(([v, n]) => `<option value="${esc(v)}" ${v === String(of[k]) ? 'selected' : ''}>${esc(n)}</option>`).join('')}</select>`;
    h += '<div class="row">' + opt('typ', [['', 'all aircraft'], ...[...new Set(all.map(r => r.Type))].map(t => [t, typ(t)])])
      + opt('src', [['', 'planned + new'], ['geplant', 'planned flights'], ['neu', 'new tasks']])
      + opt('art', [['', 'all tasks'], ...Object.keys(ART).map(a => [a, en(a)]), ...[...new Set(all.map(r => r.Task))].map(t => [t, t])])
      + opt('base', [['', 'all bases'], ...[...new Set(all.map(r => r.Base))].filter(Boolean).sort().map(b => [b, b])])
      + opt('t', [['', 'any start time'], ['30', 'start ≤ 30 min'], ['60', 'start ≤ 1 h'], ['120', 'start ≤ 2 h']])
      + opt('sort', [['', 'sort by TOT'], ['nah', 'sort by distance'], ['typ', 'sort by aircraft']])
      + `<input type="search" data-chg="of" data-k="q" placeholder="search: target, callsign, base" value="${esc(of.q)}" style="flex:1;min-width:140px">`
      + (Object.values(of).some(v => v) ? '<button class="ib" data-act="oclear" title="reset all filters">✕</button>' : '') + `<span class="dim">${l.length}/${all.length}</span></div>`;
    h += l.length ? `<table class="ftbl"><tr><th>TOT</th><th>Type</th><th>Task · target</th><th></th></tr>${l.map(fragRow).join('')}</table>`
      : '<p class="note">Nothing matches right now: squadrons without free aircraft, fuel or ammo, no Su-25T at night – or change the filters.</p>';
    return h + `<p class="note">Pick a task, then Next through Plan, Weapons and Cartridge – the defaults are fine. “new” creates a package (with SEAD and escort if needed), “Jump in” takes over an airborne flight. ${W.Mp ? 'Multiplayer: “+ add” puts more flights into the same mission, clicking a flight above plans it, ✕ hands it back to the AI.' : 'You can switch at any time; the AI flies the old flight.'}</p>`;
  },

  plan: f => card(f),

  wpn(f) {
    const ed = editable(f), sta = S.Stations[f.Type] || {}, pre = S.Loads[f.Type] || [];
    const cur = f.Pyl || pre.find(p => p.Name === f.Load)?.Pyl || {};
    const wn = id => { const w = S.Wpn[id]; return !id ? '— empty —' : w ? (w[1] > 1 ? `${w[1]}× ${w[0]}` : w[0]) : id; };
    let h = `<h2>Loadout</h2><select data-chg="load" ${ed ? '' : 'disabled'} style="width:100%">${f.Pyl ? '<option selected>— custom loadout —</option>' : ''}${pre.map(p => `<option ${!f.Pyl && p.Name === f.Load ? 'selected' : ''}>${esc(p.Name)}</option>`).join('')}</select>`
      + (f.Pyl ? `<p class="why">${esc(f.Load) || '–'}</p>` : '');
    const nums = Object.keys(sta).map(Number).sort((a, b) => a - b);
    if (nums.length) h += '<h2>Stations</h2><table class="stn">' + nums.map(n => {
      const ids = ['', ...sta[n]];
      if (cur[n] && !ids.includes(cur[n])) ids.push(cur[n]);
      return `<tr><td>Stn ${n}</td><td><select data-chg="pyl" data-n="${n}" ${ed ? '' : 'disabled'}>${ids.map(id => `<option value="${esc(id)}" ${id === (cur[n] || '') ? 'selected' : ''}>${esc(wn(id))}</option>`).join('')}</select></td></tr>`;
    }).join('') + '</table>';
    return h + `<p class="note">Each station lists everything a DCS loadout of this aircraft carries there; custom loadouts from the DCS mission editor (Saved Games\\DCS\\MissionEditor\\UnitPayloads) are added after restarting the campaign. The whole flight carries the same loadout.${ed ? '' : ' <b>Flight is under way – can no longer be changed.</b>'}</p>`;
  },

  prof(f) {                                                   // Profile: gespeicherte Ausrüstung je Muster, für alle Kampagnen
    const ps = W.Profiles?.[f.Type] || [], dis = hostOnly();
    if (!ps.includes(kprof)) kprof = ps[0] || '';
    return `<h2>Profiles <span class="dim">saved ${typ(f.Type)} setups – kept for future campaigns</span></h2><div class="row">`
      + (ps.length ? `<select data-chg="kprof">${ps.map(n => `<option ${n === kprof ? 'selected' : ''}>${esc(n)}</option>`).join('')}</select><button data-act="kload" ${dis}>Load</button><button class="ib" data-act="kdrop" title="delete profile">✕</button><span class="dim">·</span>` : '')
      + `<input data-kname maxlength="24" placeholder="profile name" style="width:150px"><button data-act="ksave">Save current setup</button></div>`
      + '<p class="note">A profile named “Default” is loaded automatically into every new campaign.</p>';
  },

  abw(f) {
    const k = kitOf(f.Type), tk = S.Tanks[f.Type], dis = hostOnly(), sum = tk.Chaff + tk.Flare;
    return `<h2>Countermeasures and fuel</h2><div class="form">`
      + `<span>Chaff</span><span><input type="number" min="0" max="${sum}" step="10" data-chg="kit" data-k="Chaff" value="${k.Chaff >= 0 ? k.Chaff : tk.Chaff}" ${dis}> <span class="dim">chaff + flares max. ${sum} together</span></span>`
      + `<span>Flares</span><span><input type="number" min="0" max="${sum}" step="10" data-chg="kit" data-k="Flare" value="${k.Flare >= 0 ? k.Flare : tk.Flare}" ${dis}></span>`
      + `<span>Gun</span><span><input type="number" min="0" max="100" step="10" data-chg="kit" data-k="Gun" value="${k.Gun ?? 100}" ${dis}> %</span>`
      + `<span>Fuel</span><span><input type="number" min="10" max="100" step="5" data-chg="kit" data-k="Fuel" value="${k.Fuel ?? 100}" ${dis}> % internal <span class="dim">${Math.round(tk.Fuel * (k.Fuel ?? 100) / 100)} kg</span></span></div>`
      + (f.Type === 'FA-18C_hornet' ? '' : '<p class="note">Countermeasure programs for this aircraft are set in the cockpit.</p>');
  },

  ale(f) {
    const c = W.Plan.Cmds, lim = [[0, 6, 1], [1, 24, 1], [0.25, 5, 0.25], [0, 6, 1]], dis = hostOnly();
    return '<h2>ALE-47 programs <span class="dim">MAN 1–6</span></h2><table class="cmds"><tr><th>Prog.</th><th>Chaff</th><th>Repeat</th><th>Interval s</th><th>Flares</th></tr>'
      + S.Cmds.map(pr => `<tr><td class="mono">${pr.replace('_', ' ')}</td>${c[pr].map((v, i) => `<td><input type="number" data-chg="cmd" data-p="${pr}" data-i="${i}" value="${v}" min="${lim[i][0]}" max="${lim[i][1]}" step="${lim[i][2]}" ${dis}></td>`).join('')}</tr>`).join('') + '</table>'
      + (W.Plan.Dtc ? `<p class="note">Per release: chaff × repeat at the interval, plus flares. Loaded into the jet via the cartridge (template “${esc(W.Plan.Dtc)}”).</p>`
        : '<div class="msg warn">No F/A-18C cartridge template – programs only work with a template (see Navigation and systems).</div>');
  },

  com(f) {
    const P = W.Plan, dis = hostOnly();
    if (f.Type === 'Su-25T') return '<h2>Radio</h2><p class="note">The Su-25T has no radio presets in the mission editor – the frequencies are on the kneeboard.</p>'
      + '<table class="comm"><tr><th>Ch</th><th>MHz</th><th>Name</th></tr>' + P.Comm[0].slice(0, 6).map((c, i) => `<tr><td class="mono">${i + 1}</td><td class="mono">${c.F.toFixed(3)}</td><td>${esc(c.N)}</td></tr>`).join('') + '</table>';
    const names = { 'F-14B': ['ARC-159 pilot', 'ARC-182 RIO'], 'F-15ESE': ['UHF 1', 'UHF 2'] }[f.Type] || ['COMM 1', 'COMM 2'];
    const cell = (r, i) => {
      const c = P.Comm[r][i], k = `${r + 1}-${i + 1}`, o = c.Own ? ' class="own"' : '';
      return `<td${o}><input class="f" type="number" step="0.025" min="30" max="400" data-chg="comm" data-c="${k}" data-f="F" value="${c.F}" ${dis}></td>`
        + `<td${o}><input class="n" maxlength="14" data-chg="comm" data-c="${k}" data-f="N" value="${esc(c.N)}" ${dis}>${c.Own ? `<button class="ib" data-act="commx" data-c="${k}" title="back to default">↺</button>` : ''}</td>`;
    };
    return `<h2>Radio presets <span class="dim">20 per radio</span></h2><table class="comm"><tr><th>Ch</th><th colspan="2">${names[0]}</th><th colspan="2">${names[1]}</th></tr>`
      + P.Comm[0].map((_, i) => `<tr><td class="mono">${i + 1}</td>${cell(0, i)}${cell(1, i)}</tr>`).join('') + '</table>'
      + '<p class="note">Default per mission: 1 flight, 2 package, 3 AWACS, 4/5 tankers, then friendly airfields (home first), 20 guard. Your changes (yellow) stay for every flight of this aircraft; ↺ = back to default. Everything is also on the kneeboard.</p>';
  },

  sys(f) {
    const k = kitOf(f.Type), P = W.Plan, dis = hostOnly();
    let h = `<h2>Navigation and systems</h2><div class="form"><span>TACAN</span><span><input data-chg="kit" data-k="Tacan" data-s="1" value="${esc(k.Tacan || '')}" placeholder="${esc(P.Tacan || '–')}" style="width:80px" ${dis}> <span class="dim">empty = home base ${esc(P.Home)}${P.Tacan && !k.Tacan ? ' (' + esc(P.Tacan) + ')' : ''}</span></span>`;
    if (f.Type === 'FA-18C_hornet') {
      h += `<span>Baro warning</span><span><input type="number" step="100" min="0" data-chg="kit" data-k="AltBaro" value="${P.Alt[0]}" ${dis}> ft</span><span>Radar warning</span><span><input type="number" step="50" min="0" data-chg="kit" data-k="AltRdr" value="${P.Alt[1]}" ${dis}> ft</span>`
        + `<span>Helmet</span><span class="seg">${['none', 'JHMCS', 'NVG'].map((n, i) => `<button data-act="kitv" data-k="Hmd" data-v="${i}" class="${(k.Hmd ?? 1) === i ? 'on' : ''}" ${dis}>${n}</button>`).join('')}</span>`
        + `<span>Rockets</span><span class="seg">${['single', 'ripple'].map((n, i) => `<button data-act="kitv" data-k="Ripple" data-v="${i}" class="${!!k.Ripple === !!i ? 'on' : ''}" ${dis}>${n}</button>`).join('')}</span></div>`
        + (P.Dtc ? `<div class="msg">Cartridge (DTC): template “${esc(P.Dtc)}” + campaign – radio 1–20 with names, ALE-47 programs, altitude warnings, TACAN, known SAM rings (MEZ), front line (FLOT). Loads automatically at start; RWR and HARM stay as in the template.</div>`
          : '<div class="msg warn">No template found: save an F/A-18C cartridge once in the DCS mission editor (Saved Games\\DCS\\DTC), then restart the campaign.</div>');
    } else h += '</div>';
    return h + '<p class="note">Laser codes (default 1688), datalink and everything else in the cockpit.</p>'
      + `<div class="btnrow"><button data-act="kitreset" ${dis}>Reset ${typ(f.Type)} setup to default</button></div>`;
  },

  kit: f => FTAB.prof(f) + FTAB.abw(f) + (f.Type === 'FA-18C_hornet' ? FTAB.ale(f) : '') + FTAB.com(f) + FTAB.sys(f),

  go(f) {
    const P = W.Plan, k = kitOf(f.Type), tk = S.Tanks[f.Type], [c, t] = fstat(f), a = assess.id === f.Id ? assess.lines : null;
    check(f);
    let h = `<div class="mine"><div class="row" style="margin:0 0 4px;justify-content:space-between"><span class="cs">${esc(f.Callsign)}</span><span class="st ${c}">${t}</span></div><div>${typ(f.Type)} ×${f.Alive} · ${chip(f.Task)} · ${esc(tgt(f))}</div></div>`
      + `<div class="form">${W.You ? '' : `<span>Mission</span><span>${seg('mp', W.Mp ? '1' : '', [['', 'Single player'], ['1', 'Multiplayer']])}</span>`
      + `<span>Performance</span><span>${seg('perf', String(W.Perf ?? 1), [['0', 'Low-end PC'], ['1', 'Normal'], ['2', 'High-end PC']])}</span>`}`
      + `<span>Crew</span><span>${[...crew(f)].map((c, i) => `${i + 1} ${c === 'P' ? (W.Mp ? 'Player' : i ? 'AI' : 'You') : 'AI'}`).join(' · ')}</span>`
      + `<span>Takeoff</span><span>${hm(f.Takeoff)} ${esc(P.Home)} · ${esc(en(f.Start))}</span><span>TOT</span><span class="mono" style="color:var(--accent)">${hm(f.Tot)}</span><span>Landing</span><span>${hm(f.Land)}</span>`
      + `<span>Weapons</span><span>${esc(f.Load) || '–'}</span><span>Countermeasures</span><span>${k.Chaff >= 0 ? k.Chaff : tk.Chaff} chaff · ${k.Flare >= 0 ? k.Flare : tk.Flare} flares · gun ${k.Gun ?? 100} % · fuel ${k.Fuel ?? 100} %</span>`
      + `<span>Radio</span><span>${P.Comm[0].slice(0, 5).map((x, i) => `${i + 1} ${esc(x.N)} <span class="mono dim">${x.F.toFixed(1)}</span>`).join(' · ')}</span><span>TACAN</span><span>${esc(k.Tacan || P.Tacan || '–')}</span></div>`;
    if (W.Mp) h += `<h2>Mission flights <span class="dim">${W.mine.length}</span></h2><table class="ftbl">${W.mine.map(x => `<tr class="click${x === f ? ' me' : ''}" data-act="cur" data-id="${x.Id}"><td class="mono">${hm(x.Takeoff)}<div class="rel">${esc(en(x.Start))}</div></td><td>${typ(x.Type)}<div class="rel mono">${esc(x.Callsign)}</div></td><td>${[...crew(x)].map((c, i) => `${i + 1} ${c === 'P' ? 'Player' : 'AI'}`).join(' · ')}<div class="rel">${esc(x.Owner || 'open')}</div></td></tr>`).join('')}</table>`
      + '<div class="msg">Multiplayer: DCS → Multiplayer → New Server → this mission (folder Kampagne). Other players take the “Player” slots, the AI flies the rest. If someone leaves while airborne, the AI flies their aircraft home – slot “…U” in the slot list brings it back. Once everyone has landed the campaign debriefs; the server loads the next mission by itself (needs the live-connection hook). Dedicated server on this PC: works the same – the app copies the hook into its Saved Games profile (restart the server once), load this mission there or any mission first, then “Fly” sends the next one.</div>';
    if (a) h += a.filter(l => l.startsWith('⚠')).map(l => `<div class="msg warn">${esc(l)}</div>`).join('') || '<div class="msg">Check: fuel, air defences and timing OK.</div>';
    if (W.You) return h + `<p class="note">The host generates the mission. In DCS take the slots of ${esc(f.Callsign)} (${esc(W.You)}).</p>`;
    return h + (W.Mission ? '' : `<div class="btnrow"><button class="go big" data-act="fly">Generate mission and fly${W.Mp && W.mine.length > 1 ? ` (${W.mine.length} flights)` : ''}</button></div>` + '<p class="note">Writes the mission to Saved Games\\DCS\\Missions\\Kampagne (older campaign missions there are deleted) and fast-forwards the campaign to mission start.</p>');
  },
};

/// Besatzung je Maschine: Einzelspieler „Du“ + KI; Mehrspieler je Platz Spieler oder KI (Klick wechselt)
const crew = f => (f.Crew || 'P').padEnd(f.N, 'K').slice(0, f.N);
const crewSeg = f => `<span class="seg">${[...crew(f)].map((c, i) => W.Mp
  ? `<button data-act="crew" data-i="${i}" class="${c === 'P' ? 'on' : ''}" title="Player or AI">${i + 1} ${c === 'P' ? 'Player' : 'AI'}</button>`
  : `<button disabled class="${i ? '' : 'on'}">${i + 1} ${i ? 'AI' : 'You'}</button>`).join('')}</span>`;

/// Eigener Flug: Kopf, Start/TOT, Wegpunkte, Prüfung, Fliegen.
function card(f) {
  const r = rt(f), plan = editable(f), pk = W.pk.get(f.Pkg), q = W.sq.get(f.Sqn), mates = W.Flights.filter(x => x.Pkg === f.Pkg && x !== f);
  let h = `<div class="fhead"><div><div class="cs">${esc(f.Callsign)}</div><div>${typ(f.Type)} ×${f.Alive} · ${chip(f.Task)} · <b>${esc(en(f.Status))}</b></div></div>`
    + `<div class="times"><span>Takeoff</span><span>Push</span><span>TOT</span><span>Landing</span><b>${hm(f.Takeoff)}</b><b>${hm(r.find(p => p.Kind === 'push')?.T)}</b><b style="color:var(--accent)">${hm(f.Tot)}</b><b>${hm(f.Land)}</b></div></div>`;
  h += `<p class="why">${esc(pk?.Name ?? '')} – ${esc(f.Why)}${f.Result ? ' – ' + esc(f.Result) : ''}<br>Radio <span class="mono">${f.Freq.toFixed(2)} MHz</span>${f.Tacan ? ` · TACAN <span class="mono">${f.Tacan}X</span>` : ''} · ${esc(q?.Name ?? '')}${q ? ', ' + esc(baseName(q)) : ''}</p>`;
  if (mates.length) h += '<div class="row">' + mates.map(x => `<span class="tag" data-act="sel" data-t="f" data-id="${x.Id}" style="cursor:pointer"><span class="mono">${esc(x.Callsign)}</span>&nbsp;${x.Task}&nbsp;<span class="dim">${hm(x.Tot)}</span></span>`).join('') + '</div>';
  if (plan) {
    const tg = f.TargetKind === 'obj' && W.ob.get(f.Target), dmpi = tg && !['Recon', 'DEAD', 'SEAD'].includes(f.Task) && S.Parts[tg.Kind];   // Zielpunkt (Teil)
    const ms = f.Player ? W.Sqns.filter(x => x.Player && x.Side === f.Side) : [];   // Muster wechseln: jede spielbare Staffel (belegte KI-Flüge plant der Server um)
    h += '<div class="form">' + (ms.length > 1 ? `<span>Type</span><span>${seg('msqn', String(f.Sqn), ms.map(x => [String(x.Id), typ(x.Type) + (ms.some(y => y !== x && y.Type === x.Type) ? ' ' + esc(baseName(x)) : '')]))}</span>` : '')
      + ((S.Variants[f.Type] ?? []).length > 1 ? `<span>Variant</span><span>${seg('mvar', f.Type, S.Variants[f.Type].map(t => [t, typ(t)]))}</span>` : '')
      + `<span>Start</span><span class="seg">${Object.entries({ kalt: 25, heiß: 10, Startbahn: 2, Luft: -1e9 }).map(([s, m]) => { const left = Math.floor((T(f.Takeoff) - now()) / 60e3), no = f.Status === 'geplant' && left < m;   // zu knapp: DCS-Mission wählt die nächstschnellere
          return `<button data-act="fstart" data-v="${s}" class="${s === f.Start ? 'on' : ''}" ${no ? `disabled title="takeoff in ${left} min – ${en(s)} start needs ${m} min (move the TOT)"` : ''}>${en(s)}</button>`; }).join('')}</span>`
      + (dmpi ? `<span>Aim</span><span class="seg">${[-1, ...dmpi.map((_, i) => i)].map(i => `<button data-act="fpart" data-v="${i}" class="${i === f.Part ? 'on' : ''}" title="${i < 0 ? 'AI picks: runway first, then shelters / the fuller store' : 'aim point'}">${i < 0 ? 'Auto' : esc(dmpi[i])}</button>`).join('')}</span>` : '')
      + `<span>TOT</span><span><input type="time" data-chg="tot" value="${hm(f.Tot)}"> <span class="dim">times are recalculated</span></span>`
      + `<span>Crew</span><span>${crewSeg(f)} <button class="ib" data-act="crewn" data-v="-1" title="one aircraft fewer – it stays with the squadron" ${f.N > 1 ? '' : 'disabled'}>−</button><button class="ib" data-act="crewn" data-v="1" title="one more aircraft from the squadron">+</button></span></div>`;
  }
  h += '<h2>Waypoints</h2><table class="wpt"><tr><th>#</th><th>Point</th><th>Time</th><th>Alt ft</th><th>Speed kt</th><th></th></tr>';
  r.forEach((p, i) => {
    const mid = i > 0 && i < r.length - 1, kind = WPK[p.Kind] || p.Kind;
    const add = plan && i < r.length - 1 ? `<button class="ib" data-act="wpadd" data-i="${i}" title="insert a waypoint after ${i} (middle of the leg – then drag it on the map)">+</button>` : '';
    h += `<tr><td>${i}</td><td>${plan && mid ? `<input data-chg="wpname" data-i="${i}" maxlength="12" value="${esc(p.Name)}" placeholder="${esc(kind)}" style="width:96px">${p.Kind !== 'nav' ? ` <span class="dim">${kind}</span>` : ''}`
        : `${esc(p.Name || kind)}${p.Name && p.Kind !== 'nav' && kind !== p.Name ? ` <span class="dim">${kind}</span>` : ''}`}</td><td class="mono">${hm(p.T)}</td>`
      + (plan && mid ? `<td><input type="number" step="500" data-chg="wpalt" data-i="${i}" value="${Math.round(p.Alt / FT / 100) * 100}"></td><td><input type="number" step="10" data-chg="wpspd" data-i="${i}" value="${Math.round(p.Spd / KT)}"></td>`
        + `<td>${['target', 'orbit', 'orbit2'].includes(p.Kind) ? '' : `<button class="ib" data-act="wpdel" data-i="${i}" title="delete waypoint">✕</button>`}${add}</td>`
        : `<td class="mono">${mid ? Math.round(p.Alt / FT / 100) * 100 : ''}</td><td class="mono">${mid ? Math.round(p.Spd / KT) : ''}</td><td>${add}</td>`) + '</tr>';
  });
  h += '</table>';
  if (plan) { h += '<p class="note">+ inserts a waypoint, ✕ deletes it; name, altitude and speed are editable here. Map: drag waypoints · Alt+click on the route = insert · right-click = delete. TOT stays, the takeoff time moves.</p>'; check(f); }
  if (assess.id === f.Id && assess.lines.length) h += '<h2>Check</h2>' + assess.lines.map(l => `<div class="msg ${l.startsWith('⚠') ? 'warn' : ''}">${esc(l)}</div>`).join('');
  return h;
}

/// Prüfung (Sprit, Flugabwehr, Timing) holen, wenn sich Route oder Lage-Minute geändert hat
function check(f) {
  const k = f.Id + JSON.stringify(f.Route.map(p => [p.X, p.Z, p.Alt, p.Spd])) + f.Load + W.Time.slice(0, 16);
  if (assess.key === k || assess.busy) return;
  assess.busy = true;
  api('assess?id=' + f.Id).then(l => { assess = { id: f.Id, key: k, lines: l, busy: false }; render(); }).catch(() => assess.busy = false);
}

// ---------- Auswahl ----------

function info() {
  const el = $('#info'), o = item(sel);
  if (!o || st.screen !== 'map') { el.hidden = true; el._h = ''; return; }
  el.hidden = false;
  el.dataset.side = sel.t === 'n' ? ['None', 'Red', 'Blue'][W.Owners[o.Id]] : o.Side;
  put(el, '<button class="ib" data-act="unsel" title="Clear selection (Esc)">✕</button>' + INFO[sel.t](o));
}

function attackForm(kind, id, def) {
  if (W.You) return '';                                                     // Pakete plant der Host
  const task = form.task || def, role = task === 'DEAD' ? 'Strike' : task;
  const sq = W.Sqns.filter(q => own(q) && q.Roles.includes(role) && q.Ac > 0);
  return `<h2 style="margin-top:12px">Plan an attack</h2><div class="row"><select data-chg="ptask">${ATTACK.map(t => `<option ${t === task ? 'selected' : ''}>${t}</option>`).join('')}</select>`
    + `<select data-chg="psqn" style="flex:1;min-width:0"><option value="">Squadron: auto</option>${sq.map(q => `<option value="${q.Id}" ${String(q.Id) === form.sqn ? 'selected' : ''}>${esc(q.Name)} (${typ(q.Type)}, ${q.Ac} ready)${q.Player ? ' ★' : ''}</option>`).join('')}</select></div>`
    + `<div class="btnrow" style="margin-top:4px"><button class="go" data-act="pkg" data-kind="${kind}" data-id="${id}" data-task="${task}">Create package</button><span class="note" style="margin:5px 0 0">★ = playable, then take it over under “Flight”</span></div>`;
}

const INFO = {
  u(u) {
    const age = u.Seen ? (now() - T(u.Seen)) / 3600e3 : 99;
    if (!own(u)) return `<h3 class="${u.Side}">${esc(u.Name)}</h3><div class="dim">${UKIND[u.Kind]} · seen ${u.Seen ? ago(u.Seen) : '?'} (${esc(u.Why)})</div>`
      + kv([['est. strength', `${bar(u.Str)} ${pct(u.Str)}`], ['Report', `<span class="tag">${age < 3 ? 'confirmed' : age < 12 ? 'probable' : 'suspected'}</span>`]]) + attackForm('unit', u.Id, 'BAI');
    const to = u.Path.length ? S.Nodes[u.Path[u.Path.length - 1]].Name : '', dp = W.ob.get(u.Dp);
    return `<h3 class="${u.Side}">${esc(u.Name)}</h3><div class="dim">${UKIND[u.Kind]} · ${u.Veh} vehicles (${esc(u.Template)})</div>`
      + kv([['Strength', `${bar(u.Str)} ${pct(u.Str)}`], ['Ammo', `${bar(u.Supply)} ${pct(u.Supply)}`], ['Fuel', `${bar(u.Fuel)} ${pct(u.Fuel)}`],
        ['Supply line', u.Line > 0 ? `${bar(u.Line)} ${pct(u.Line)}` : '<span class="warn">cut off</span>'], ['Morale', `${bar(u.Morale)} ${pct(u.Morale)}`], ['Dug in', `${bar(u.Dig)} ${pct(u.Dig)}`],
        ['Order', `<b>${esc(en(u.Order))}</b>${to ? ' → ' + esc(to) : ''}`], dp && ['Depot', esc(dp.Name)]])
      + (u.Why ? `<p class="why">${esc(u.Why)}</p>` : '');
  },
  o(o) {
    const h = `<h3 class="${o.Side}">${esc(o.Name)}</h3><div class="dim">${o.BaseId < 0 ? 'Aircraft carrier' : KIND[o.Kind]}${o.Sys && o.BaseId >= 0 ? ' ' + esc(o.Sys) : ''} · ${sideDe(o.Side)}${o.Decoy ? ' · <span class="warn">decoy</span>' : ''}</div>`;
    const rows = [['Condition', `${bar(o.Health)} ${pct(o.Health)}${o.Claim ? ' <span class="warn">claimed – unconfirmed, needs recon</span>' : ''}`]], b = o.Kind === 'Airbase' && S.Bases.find(b => b.Id === o.BaseId);
    if (b) rows.push(['Runway heading', `${esc(b.Rwy)} · ${Math.round(b.RwyHdg)}°`]);
    const pn = S.Parts[o.Kind];                                               // Teile (eigene, sonst nur nach Aufklärung/Meldung)
    if (pn && (o.Parts || own(o))) pn.forEach((n, i) => { const v = o.Parts ? o.Parts[i] : o.Health; rows.push(['· ' + n, `${bar(v)} ${pct(v)}${n === 'Runway' && v < 0.4 ? ' <span class="warn">closed</span>' : ''}`]); });
    if (o.Kind === 'Sam') rows.push(['Range', `${own(o) ? '' : 'approx. '}${Math.round(o.Range / NM)} NM · up to ${Math.round(o.MaxAlt / FT / 1000)}k ft${own(o) ? ` · ${o.Missiles} missiles` : ''}${o.Quiet ? ' · <span class="warn">radar off</span>' : ''}`]);
    if (own(o) && (o.Cap > 0 || o.Stock > 0)) {
      const t = v => o.Cap > 0 ? `${bar(v / o.Cap)} ${Math.round(v)} / ${Math.round(o.Cap)} t` : `${Math.round(v)} t`;
      rows.push(['Ammo', t(o.Stock)]);
      if (o.Kind !== 'Factory') rows.push(['Fuel', t(o.Fuel)]);
    }
    if (o.Arms) rows.push(['Weapons', Object.entries(o.ArmsFull || {}).map(([c, n]) => `<span class="${(o.Arms[c] || 0) < n * 0.25 ? 'warn' : ''}">${c} ${o.Arms[c] || 0}/${n}</span>`).join(' · ') || '–']);
    if (own(o) && o.Flow > 1) rows.push(['Supply', `~${Math.round(o.Flow)} t/day`]);
    const sq = W.Sqns.filter(q => q.Base === o.Id);
    if (sq.length) rows.push(['Squadrons', sq.map(q => `${esc(q.Name)} <span class="dim">(${typ(q.Type)}${own(q) ? ', ' + q.Ac + ' ready' : ''})</span>`).join('<br>')]);
    if (own(o)) return h + kv(rows);
    if (o.Seen) rows.push(['Seen', `${ago(o.Seen)} <span class="dim">${esc(o.Src)}</span>`]);
    const pk = W.Pkgs.filter(p => p.TargetKind === 'obj' && p.Target === o.Id);
    if (pk.length) rows.push(['Planned', pk.map(p => `${esc(p.Name)} (TOT ${hm(p.Tot)})`).join('<br>')]);
    return h + kv(rows) + attackForm('obj', o.Id, DEFTASK[o.Kind] || 'Strike');
  },
  f(f) {
    if (f.Src) return `<h3 class="${f.Side}">Air contact</h3>` + kv([['Count', `${f.N} aircraft`], ['Altitude', `approx. ${Math.round(f.Alt / FT / 1000)}k ft`], ['Reported', `${esc(f.Src)}${f.Takeoff ? ' · ' + ago(f.Takeoff) : ''}`]]);
    const pk = W.pk.get(f.Pkg), q = W.sq.get(f.Sqn);
    let h = `<h3 class="${f.Side}"><span class="mono">${esc(f.Callsign)}</span>${f.Player ? ` <span class="tag">${W.Mp ? esc(f.Owner || 'open slot') : 'your flight'}</span>` : ''}</h3><div>${typ(f.Type)} ×${f.Alive}/${f.N} · ${chip(f.Task)} · <b>${esc(en(f.Status))}</b></div>`
      + kv([['Squadron', esc(q?.Name ?? '')], ['Times', `<span class="mono">Takeoff ${hm(f.Takeoff)} · TOT ${hm(f.Tot)} · Landing ${hm(f.Land)}</span>`], ['Package', esc(pk?.Name ?? '')], ['Loadout', esc(f.Load) || '–'],
        ['Radio', `<span class="mono">${f.Freq.toFixed(2)} MHz${f.Tacan ? ` · TACAN ${f.Tacan}X` : ''}</span>`], f.Result && ['Result', esc(f.Result)]]);
    if (f.Why) h += `<p class="why">${esc(f.Why)}</p>`;
    if (takeable(f)) h += `<div class="btnrow"><button class="go" data-act="take" data-id="${f.Id}">${f.Player ? 'Claim this flight' : f.Status === 'geplant' ? 'Take this flight' : 'Take over (air start)'}</button></div>`;
    if (editable(f)) h += '<p class="note">Route: drag waypoints · Alt+click on the line = insert · right-click = delete</p>';
    return h;
  },
  c(c) {
    if (c.Navy) return `<h3 class="${c.Side}">${esc(c.What)}</h3>` + kv([['Ships', `${c.Trucks} · ${c.Types.map(typ).join(', ')}`], ['Air defence', `${Math.round(c.Range / NM)} NM`], c.Missiles >= 0 && ['Anti-ship missiles', c.Missiles], own(c) && ['Home port', esc(c.To)]])
      + (own(c) ? '' : '<p class="why">Enemy surface group – strike it with anti-ship missiles (Harpoon, Kh-31A, Kh-35) from outside its air defence.</p>');
    const items = c.Ship ? 'holds' : c.Rail ? 'wagons' : 'trucks';
    return `<h3 class="${c.Side}">${esc(c.What)}</h3>` + kv([['Size', `${c.Trucks} ${items}`], ['Destination', esc(c.To)], c.Troops ? ['Carrying', esc(c.Troops)] : c.Tons >= 0 && ['Cargo', `ammo ${Math.round(c.Tons)} t · fuel ${Math.round(c.Fuel)} t`]])
      + (own(c) ? '' : '<p class="why">Enemy supply – a worthwhile interdiction target.</p>');
  },
  n(n) {
    const ow = ['None', 'Red', 'Blue'][W.Owners[n.Id]], near = W.Units.filter(u => Math.hypot(u.X - n.X, u.Z - n.Z) < 4000);
    return `<h3 class="${ow}">${esc(n.Name)}</h3><div class="dim">Town · ${sideDe(ow)}${n.Key === n.Id ? ' · key town' : ''}</div>`
      + (near.length ? '<div class="row">' + near.map(u => `<span class="tag ${u.Side}" data-act="sel" data-t="u" data-id="${u.Id}" style="cursor:pointer">${esc(u.Name)}</span>`).join('') + '</div>' : '');
  },
};

// ---------- Hauptmenü, Neue Kampagne ----------

function getSaves() { api('saves').then(l => { saves = l; saveSel = Math.max(0, l.findIndex(s => !s.Current)); render(); }).catch(() => { }); }

/// Kräfteverlauf: eigener Anteil an Bodenstärke, Flugzeugen, SAM-Stellungen je Tag (50 % = Gleichstand)
function ratioSvg(H) {
  if (!H || H.length < 2) return '<span class="dim">chart from day 2</span>';
  const me = W.Player === 'Blue' ? 'Blue' : 'Red', foe = me === 'Blue' ? 'Red' : 'Blue', w = 280, h = 64;
  const ser = [['G', 'Ground', 'var(--ok)'], ['A', 'Air', 'var(--blue)'], ['S', 'Air defense', 'var(--warn)']];
  const share = (p, k) => p[me + k] + p[foe + k] > 0 ? p[me + k] / (p[me + k] + p[foe + k]) : 0.5;
  const x = i => (4 + i * (w - 8) / (H.length - 1)).toFixed(1), y = v => (h - 4 - v * (h - 8)).toFixed(1);
  return `<svg width="${w}" height="${h}" style="display:block;background:rgba(255,255,255,.03)"><line x1="0" x2="${w}" y1="${y(.5)}" y2="${y(.5)}" stroke="var(--none)" stroke-dasharray="3 3" stroke-width=".7"/>`
    + ser.map(([k, , c]) => `<path d="${H.map((p, i) => (i ? 'L' : 'M') + x(i) + ',' + y(share(p, k))).join('')}" stroke="${c}" fill="none" stroke-width="1.6"/>`).join('') + '</svg>'
    + ser.map(([k, t, c]) => `<span style="color:${c}">■</span> ${t} ${pct(share(H[H.length - 1], k))}`).join(' &nbsp;') + ` <span class="dim">· own share, ${H.length} days</span>`;
}
const CVS = [['CVN_75', 'Supercarrier (CVN-75 Truman)'], ['Stennis', 'Stennis (no module)'], ['', 'no carrier']];
const PCT = [50, 75, 100, 125, 150].map(v => [v, v + ' %']);
const DIFF = [['Air', 'Air', PCT], ['Ground', 'Ground', PCT], ['Sam', 'Air defense', PCT], ['Skill', 'Pilots', [[-1, 'Rookie'], [0, 'Normal'], [1, 'Veteran'], [2, 'Ace']]], ['Supply', 'Supply', PCT]];

function screen() {
  const el = $('#screen');
  if (st.screen === 'map') return put(el, '');
  const d = Math.floor((now() - T(W.Start)) / 864e5) + 1, c = W.Cmd;
  if (st.screen === 'menu') {
    const sv = saves[saveSel], cur = !sv || sv.Current, keys = S.Nodes.filter(n => n.Key === n.Id);
    const mineK = keys.filter(n => W.Owners[n.Id] === (W.Player === 'Blue' ? 2 : 1)).length, sq = W.Sqns.filter(own);
    return put(el, `<div class="scr menu"><div class="menu-l"><div class="brand-xl"><img src="icon.svg" alt="">Warclock</div><div class="tagline">Dynamic war · ${esc(S.Setup)}</div>
      <button class="mi primary" data-act="cont"><b>Continue</b><span>Day ${d} · ${day(W.Time)} ${hm(W.Time)} · ${sideDe(W.Player)} · Phase ${c.Phase} ${esc(S.Phases[c.Phase])}</span></button>
      <button class="mi" data-act="newc"><b>New campaign</b><span>Theatre, side, fog of war, seed</span></button>
      <button class="mi" data-act="loadsel" ${cur ? 'disabled' : ''}><b>Load save</b><span>${cur ? 'pick an older save on the right' : esc(sv.File)}</span></button>
      <div class="menu-foot">Campaign running · ${esc(location.host)}<br>Saves %LOCALAPPDATA%\\DCS-Kampagne\\saves · autosave every minute<br>Missions Saved Games\\DCS\\Missions\\Kampagne</div></div>
      <div class="menu-r"><div class="card"><div class="card-h">Saves</div><div class="saves">${saves.map((x, i) => `<div class="save ${i === saveSel ? 'sel' : ''}" data-act="save" data-i="${i}"><b>${esc(x.File)}</b><span>${x.Current ? 'current war' : 'archive'}</span><span>saved ${esc(String(x.T).slice(0, 16).replace('T', ' '))}</span><span></span></div>`).join('')}</div></div>
      ${cur ? `<div class="card"><div class="card-h">Current war</div><div class="sdet"><span>War clock</span><span class="mono">Day ${d} · ${day(W.Time)} ${hm(W.Time)}</span>
        <span>Side</span><span>${sideDe(W.Player)}${W.Fog ? ' · fog of war' : ''}</span><span>Phase</span><span>${c.Phase} ${esc(S.Phases[c.Phase])}</span>
        <span>Front</span><span>${mineK} of ${keys.length} key towns held</span>${W.Over ? `<span>Result</span><span class="warn">${esc(W.Over)}</span>` : ''}
        <span>Force ratio</span><span>${ratioSvg(W.History)}</span>
        <span>Own forces</span><span>${W.Units.filter(own).length} ground units · ${sq.reduce((a, q) => a + q.Ac, 0)} aircraft ready</span><span>Losses</span><span>${sq.reduce((a, q) => a + q.Lost, 0)} aircraft</span>
        <span>Latest reports</span><span>${W.Events.slice(-3).reverse().map(e => `<span class="mono dim">${hm(e.T)}</span> ${esc(e.Text)}`).join('<br>')}</span></div></div>`
        : '<div class="card"><div class="card-h">Archived war</div><div class="sdet"><span>Load</span><span>The current war is archived in the process – nothing is lost.</span></div></div>'}
      </div></div>`);
  }
  const n = newc, steps = ['Theatre', 'Options', 'Air forces', 'Begin'], set = S.Setups.find(x => x.Id === n.set) || S.Setups[0];
  let b = '';
  if (n.step === 0) b = S.Setups.map(x => `<button class="theater ${x.Id === set.Id ? 'sel' : ''}" data-act="nset" data-v="${x.Id}" ${x.Installed ? '' : 'disabled'}><div class="ph">${esc(x.MapName)}</div><div><b>${esc(x.Name)}</b><br><span class="dim">${x.Installed ? esc(x.Text) : `Map ${esc(x.MapName)} is not installed in DCS.`}</span></div></button>`).join('')
    + `<div class="sides">${[['Blue', set.BlueText, `Pilot and commander. Playable: ${set.Play.map(t => TYP[t] || t).join(', ')}.`], ['Red', set.RedText, 'Commander only: plan packages, set the phase, follow the war.']].map(([s, t, x]) => `<button class="side ${s} ${n.side === s ? 'sel' : ''}" data-act="nside" data-v="${s}"><b>${esc(t)}</b><span>${esc(x)}</span></button>`).join('')}</div>`;
  if (n.step === 1) b = `<div class="opt" data-act="nfog"><input type="checkbox" ${n.fog ? 'checked' : ''} style="margin-top:3px"><b>Fog of war</b><span>The enemy is shown only where radar, AWACS, recon and ground troops can see him. Detected SAM sites relocate, some are decoys.</span></div>`
    + (set.Cv ? `<div class="opt" style="cursor:default"><i></i><b>Aircraft carrier</b><span><div class="row">${CVS.map(([v, t]) => `<button class="${n.cv === v ? 'on' : ''}" data-act="ncv" data-v="${v}" ${v === 'CVN_75' && !S.Supercarrier ? 'disabled' : ''}>${t}</button>`).join('')}</div>F/A-18C and F-14B take off from and land on the carrier, otherwise on land. Supercarrier requires the DCS module${S.Supercarrier ? '' : ' (not installed)'}.</span></div>` : '')
    + `<div class="opt" style="cursor:default"><i></i><b>Difficulty</b><span><div class="row diff">${DIFF.map(([k, t, o]) => `<label>${t}<select data-chg="ndiff" data-k="${k}">${o.map(([v, l]) => `<option value="${v}" ${n[k] === v ? 'selected' : ''}>${l}</option>`).join('')}</select></label>`).join('')}</div>Enemy strength and skill, like the BMS campaign options. Ground above 100 %: your own units start weaker.</span></div>`
    + `<div class="opt" style="cursor:default"><i></i><b>Start time</b><span><div class="row"><input type="time" data-chg="nstart" value="${esc(n.start)}" style="width:120px"></div>Local time when the war begins (day 1). Darkness 19:00–07:00.</span></div>`
    + `<div class="opt" style="cursor:default"><i></i><b>Seed</b><span><div class="row"><input type="number" data-chg="nseed" value="${esc(n.seed)}" placeholder="random" style="width:120px"><button data-act="nroll">Roll</button></div>Same seed = same course of the war, given the same decisions.</span></div>`;
  if (n.step === 2) {   // Luftwaffen: Muster je Staffel frei wählen (eigene DCS-Module)
    const sq = n.sq ??= set.Sqns.map((q, i) => ({ ...q, I: i }));
    const opts = t => (S.Types.some(x => x.Type === t) ? '' : `<option value="${esc(t)}" selected>${esc(typ(t))}</option>`)
      + [['Player aircraft', true], ['AI only', false]].map(([g, f]) => `<optgroup label="${g}">${S.Types.filter(x => x.Fly === f).map(x => `<option value="${esc(x.Type)}" ${x.Type === t ? 'selected' : ''}>${esc(typ(x.Type))}</option>`).join('')}</optgroup>`).join('');
    b = ['Blue', 'Red'].map(s => `<div class="sqh"><b class="${s}">${esc(s === 'Blue' ? set.BlueText : set.RedText)}</b><span class="grow"></span><button data-act="nsqa" data-v="${s}">+ Squadron</button></div>`
      + sq.map((q, i) => q.Side !== s ? '' : `<div class="sqr"><select data-chg="nsqt" data-i="${i}">${opts(q.Type)}</select><input data-chg="nsqn" data-i="${i}" value="${esc(q.Name)}"><span class="cnt"><button data-act="nsqc" data-i="${i}" data-v="-1">−</button><span class="mono">${q.N}</span><button data-act="nsqc" data-i="${i}" data-v="1">+</button></span><button data-act="nsqx" data-i="${i}" title="Remove squadron">✕</button></div>`).join('')).join('')
      + `<p class="note">Choose the aircraft each side flies – swap in the modules you own. A new type flies its usual roles; new squadrons are based automatically.</p>`;
  }
  if (n.step === 3) b = `<div class="sdet" style="padding:0"><span>Theatre</span><span>${esc(set.Name)}</span><span>Side</span><span class="${n.side}">${n.side === 'Blue' ? esc(set.BlueText) : esc(set.RedText) + ' (commander only)'}</span><span>Fog of war</span><span>${n.fog ? 'on' : 'off'}</span>${set.Cv ? `<span>Aircraft carrier</span><span>${CVS.find(c => c[0] === n.cv)[1]}</span>` : ''}<span>Difficulty</span><span>${DIFF.map(([k, t, o]) => `${t} ${o.find(x => x[0] === n[k])[1]}`).join(' · ')}</span><span>Air forces</span><span>${['Blue', 'Red'].map(s => `${s} ${(n.sq || set.Sqns).filter(q => q.Side === s).length} squadrons`).join(' · ')}</span><span>Seed</span><span class="mono">${esc(n.seed) || 'random'}</span></div>`
    + `<p class="note warn" style="margin-top:14px">The current war (day ${d}) is archived (saves\\kampagne-DATE.json) and can be restored via “Load save”.</p>`;
  put(el, `<div class="scr newc"><div class="card nc"><div class="card-h">New campaign<span class="steps">${steps.map((s, i) => `<span class="${i === n.step ? 'on' : ''}">${i + 1} ${s}</span>`).join('')}</span></div><div class="nc-b">${b}</div>
    <div class="nc-f"><button data-act="ncancel">Cancel</button><span class="grow"></span>${n.step ? '<button data-act="nback">Back</button>' : ''}${n.step < 3 ? '<button class="on" data-act="nnext">Next</button>' : '<button class="go" data-act="ngo">Start the war</button>'}</div></div></div>`);
}

function flyDlg(r) {
  dlg(`<div class="card-h">Mission generated</div><div class="nc-b"><p class="mono"><b>${esc(fname(r.File))}</b><br><span class="dim">${esc(r.File)}</span></p>`
    + `<p>Mission start ${hm(r.Start)} · schedule: ${r.Flights} flights · ${r.Units} ground units · ${r.Sites} sites – they spawn in DCS as soon as they come near you.</p>`
    + (r.Notes.length ? `<p class="warn">${r.Notes.map(esc).join('<br>')}</p>` : '')
    + (W.Mp ? '<p>Multiplayer: if the server is already running a campaign mission, it loads this one by itself. Otherwise: <b>Multiplayer</b> → <b>New Server</b> → folder <b>Kampagne</b> → this file.</p>' : '')
    + '<p>In DCS: <b>Mission</b> → folder <b>Kampagne</b> → open the file → fly. The war clock waits for DCS and then follows DCS time. The kneeboard (sitrep, tasking, map) is in the mission. When the mission ends, the campaign takes over again.</p></div>'
    + '<div class="nc-f"><span class="grow"></span><button class="on" data-act="dlgclose">OK</button></div>');
}

// ---------- Aktionen ----------

async function flt(x) { assess = { id: W.pf.Id, key: '', lines: await api('flight', { Flight: W.pf.Id, ...x }), busy: false }; }
function editWp(i, fn) { const f = W.pf, r = structuredClone(rt(f)); fn(r[i]); sendRoute(f, r); }
function totIso(v) {
  const old = T(W.pf.Tot), [h, m] = v.split(':').map(Number), d = new Date(old);
  d.setUTCHours(h, m, 0, 0);
  let t = d.getTime();
  if (t - old > 12 * 3600e3) t -= 864e5; else if (old - t > 12 * 3600e3) t += 864e5;     // über Mitternacht
  return iso(t);
}
const setSpeed = v => run(() => api('speed', { Speed: v }));

const ACT = {
  play: () => setSpeed(W.Speed ? 0 : st.run),
  spdd: () => { spdOpen = !spdOpen; topbar(); },
  setrun: d => { st.run = +d.v; saveSt(); spdOpen = false; if (W.Speed) setSpeed(st.run); else render(); },
  plus1: () => { spdOpen = false; run(() => api('advance', { Minutes: 60 })); },
  menu: () => { st.screen = 'menu'; saveSt(); spdOpen = false; getSaves(); if (W.Speed && !W.Mission) setSpeed(0); else render(); },
  cont: () => { st.screen = 'map'; saveSt(); render(); },
  save: d => { saveSel = +d.i; render(); },
  loadsel: () => dlg(`<div class="card-h">Load save?</div><div class="nc-b"><p><b class="mono">${esc(saves[saveSel].File)}</b></p><p>The current war is archived and can be loaded again later.</p></div><div class="nc-f"><span class="grow"></span><button data-act="dlgclose">Cancel</button><button class="go" data-act="loadok">Load</button></div>`),
  loadok: () => run(async () => { await api('load', { File: saves[saveSel].File }); st.screen = 'map'; saveSt(); location.reload(); }),   // Schauplatz kann wechseln: Straßennetz, Karte neu
  newc: () => { newc = { step: 0, set: S.Scen, side: 'Blue', fog: true, seed: '', start: '10:00', cv: S.Supercarrier ? 'CVN_75' : 'Stennis', Air: 100, Ground: 100, Sam: 100, Skill: 0, Supply: 100 }; st.screen = 'new'; render(); },
  ncv: d => { newc.cv = d.v; render(); },
  nset: d => { newc.set = d.v; newc.sq = null; render(); },
  nsqa: d => { const t = S.Types.find(x => x.Fly).Type; newc.sq.push({ Side: d.v, Type: t, Name: `${typ(t)} Squadron`, N: 8, I: -1 }); render(); },
  nsqc: d => { const q = newc.sq[+d.i]; q.N = Math.min(24, Math.max(1, q.N + +d.v)); render(); },
  nsqx: d => { newc.sq.splice(+d.i, 1); render(); },
  nside: d => { newc.side = d.v; render(); },
  nfog: () => { newc.fog = !newc.fog; render(); },
  nroll: () => { newc.seed = String(Math.floor(Math.random() * 65535)); render(); },
  nnext: () => { newc.step++; render(); },
  nback: () => { newc.step--; render(); },
  ncancel: () => { st.screen = 'menu'; render(); },
  ngo: () => run(async () => {
    const set = S.Setups.find(x => x.Id === newc.set);
    await api('new', { Setup: newc.set, Side: newc.side, Fog: newc.fog, Seed: newc.seed === '' ? null : +newc.seed, Start: newc.start, Carrier: set?.Cv ? newc.cv : '', Air: newc.Air, Ground: newc.Ground, Sam: newc.Sam, Skill: newc.Skill, Supply: newc.Supply, Sqns: newc.sq });
    st.screen = 'map'; saveSt(); location.reload();
  }),
  win: d => openWin(d.id, !st.wins[d.id]?.open),
  wclose: d => openWin(d.id, false),
  wmin: d => { const w = st.wins[d.id]; w.min = !w.min; saveSt(); render(); },
  layt: () => { st.layOpen = !st.layOpen; saveSt(); render(); },
  take: d => run(async () => { await api('take', { Flight: +d.id }); sel = { t: 'f', id: +d.id }; st.cur = +d.id; st.ftab = 'plan'; openWin('flug'); }),
  cur: d => { st.cur = +d.id; sel = { t: 'f', id: +d.id }; index(); st.ftab = 'plan'; openWin('flug'); },
  drop: d => run(() => api('drop', { Flight: +d.id })),
  takepkg: d => run(() => api('takepkg', { Pkg: +d.id })),
  tacview: () => run(() => api('tacview', {})),
  invite: () => run(() => api('invite', {})),
  joinok: () => { store('who', { name: $('[data-jn]').value.trim(), code: $('[data-jc]').value.trim() }); location.reload(); },
  myflt: () => { st.ftab = 'plan'; openWin('flug'); },
  ftab: d => { st.ftab = d.v; openWin('flug'); },
  oclear: () => { for (const k in of) of[k] = ''; store('of', of); render(); },
  crew: d => run(() => { const f = W.pf, c = [...crew(f)]; c[+d.i] = c[+d.i] === 'P' ? 'K' : 'P'; return api('flight', { Flight: f.Id, Crew: c.join('') }); }),
  crewn: d => run(() => api('flight', { Flight: W.pf.Id, N: W.pf.N + +d.v })),
  mp: d => run(() => api('mp', { Mp: d.v === '1', Flight: W.pf?.Id })),
  perf: d => run(() => api('perf', { Perf: +d.v })),
  offer: d => run(async () => { const o = W.Offers[+d.i], r = await api('offer', { Task: o.Task, Kind: o.Kind, Target: o.Id, Sqn: o.Sqn }); sel = { t: 'f', id: r.Id }; st.cur = r.Id; st.ftab = 'plan'; openWin('flug'); }),
  kitv: d => run(() => api('kit', { Type: W.pf.Type, [d.k]: d.k === 'Ripple' ? d.v === '1' : +d.v })),
  commx: d => run(() => api('kit', { Type: W.pf.Type, Comm: { [d.c]: null } })),
  kitreset: () => run(() => api('kit', { Type: W.pf.Type, Reset: true })),
  ksave: () => run(() => api('kit', { Type: W.pf.Type, Save: $('[data-kname]').value.trim() })),
  kload: () => run(() => api('kit', { Type: W.pf.Type, Load: kprof })),
  kdrop: () => run(() => api('kit', { Type: W.pf.Type, Drop: kprof })),
  fly: () => run(async () => flyDlg(await api('fly', {}))),
  atc: () => run(() => api('atc', {})),
  end: () => dlg(`<div class="card-h">End mission?</div><div class="nc-b"><p>${W.MissionLive ? 'Everything DCS has reported so far is applied; the rest is simulated by the campaign again.' : 'The mission has not been started in DCS yet. Your flight will be cancelled.'}</p></div><div class="nc-f"><span class="grow"></span><button data-act="dlgclose">Cancel</button><button class="stop" data-act="endok">End mission</button></div>`),
  endok: () => run(async () => { await api('end', {}); dlgClose(); openWin('deb'); }),
  dlgclose: dlgClose,
  sel: d => { sel = { t: d.t, id: +d.id }; form.task = ''; form.sqn = ''; const o = item(sel); if (o && (d.t !== 'f' || up(o))) center(o); render(); },
  unsel: () => { sel = null; render(); },
  goto: d => { if (+d.x || +d.z) center({ X: +d.x, Z: +d.z }); },
  zin: () => zoom(cw() / 2, ch() / 2, 1.5),
  zout: () => zoom(cw() / 2, ch() / 2, 1 / 1.5),
  zfit: () => { fit(); redraw(); },
  pkg: d => run(async () => {
    const r = await api('package', { Kind: d.kind, Target: +d.id, Task: form.task || d.task, Sqn: form.sqn ? +form.sqn : null });
    toast(`Package ${r.Id} created – see ATO`, true);
    openWin('ato');
  }),
  pilots: d => { const id = +d.id; open.has(id) ? open.delete(id) : open.add(id); render(); },
  wpdel: d => { const f = W.pf, r = structuredClone(rt(f)); r.splice(+d.i, 1); sendRoute(f, r); },
  wpadd: d => {                                               // neuer Wegpunkt in der Mitte des Schenkels; Höhe/Fahrt vom Nachbarn (nicht vom Start oder der Landung)
    const f = W.pf, r = structuredClone(rt(f)), i = +d.i, a = r[i], b = r[i + 1], src = i > 0 ? a : b;
    r.splice(i + 1, 0, { Name: '', Kind: 'nav', X: (a.X + b.X) / 2, Z: (a.Z + b.Z) / 2, Alt: src.Alt, Spd: src.Spd });
    sendRoute(f, r);
  },
  fstart: d => run(() => flt({ Start: d.v })),
  fpart: d => run(() => flt({ Part: +d.v })),
  msqn: d => run(() => flt({ Sqn: +d.v })),
  mvar: d => run(() => flt({ Type: d.v })),
  phase: d => run(() => api('phase', { Phase: +d.v })),
  truce: d => run(() => api('truce', { Accept: !!d.v })),
  newpilot: () => confirm('Retire your pilot? The next DCS sortie starts a new logbook.') && run(() => api('newpilot', {})),
  prio: d => run(() => api('prio', { Task: d.t, V: +d.v })),
  focus: d => run(() => api('prio', { Focus: +d.v })),
  evk: d => { evKind = d.v; render(); },
};

const CHG = {
  me: v => run(() => api('me', { Name: v })),
  kprof: v => { kprof = v; },
  of: (v, d) => ofSet(d.k, v),
  load: v => run(() => flt({ Load: v })),
  pyl: (v, d) => run(() => flt({ Pyl: { [d.n]: v } })),
  kit: (v, d) => run(() => api('kit', { Type: W.pf.Type, [d.k]: d.s ? v : +v })),
  cmd: (v, d) => { const a = [...W.Plan.Cmds[d.p]]; a[+d.i] = +v; run(() => api('kit', { Type: W.pf.Type, Cmds: { [d.p]: a } })); },
  comm: (v, d) => { const [r, i] = d.c.split('-').map(Number), c = W.Plan.Comm[r - 1][i - 1], x = { F: c.F, N: c.N }; x[d.f] = d.f === 'F' ? +v : v; run(() => api('kit', { Type: W.pf.Type, Comm: { [d.c]: x } })); },
  tot: v => v && run(() => flt({ Tot: totIso(v) })),
  wpalt: (v, d) => editWp(+d.i, p => p.Alt = +v * FT),
  wpname: (v, d) => editWp(+d.i, p => p.Name = v.trim().slice(0, 12)),
  wpspd: (v, d) => editWp(+d.i, p => p.Spd = +v * KT),
  ptask: v => { form.task = v; form.sqn = ''; put($('#info'), '<button class="ib" data-act="unsel" title="Clear selection (Esc)">✕</button>' + INFO[sel.t](item(sel)), true); },
  psqn: v => { form.sqn = v; },
  atoplay: (v, d, t) => { atoPlay = t.checked; render(); },
  nseed: v => { newc.seed = v; },
  nstart: v => { newc.start = v || '10:00'; },
  nsqt: (v, d) => { const q = newc.sq[+d.i]; if (q.I < 0 && q.Name === `${typ(q.Type)} Squadron`) q.Name = `${typ(v)} Squadron`; q.Type = v; render(); },
  nsqn: (v, d) => { newc.sq[+d.i].Name = v; },
  ndiff: (v, d) => { newc[d.k] = +v; render(); },
};

document.addEventListener('click', e => {
  if (spdOpen && !e.target.closest('.tc')) { spdOpen = false; topbar(); }
  const b = e.target.closest('[data-act]');
  if (b && !b.disabled) { e.stopPropagation(); ACT[b.dataset.act]?.(b.dataset, b); }
});
document.addEventListener('change', e => {
  const t = e.target, c = t.closest('[data-chg]');
  if (c) { if (t.tagName === 'INPUT' && t.type !== 'checkbox') t.blur(); CHG[c.dataset.chg]?.(t.value, c.dataset, t); return; }
  if (t.dataset.lay) {
    lay[t.dataset.lay] = t.checked; store('lay', lay);
    if (t.dataset.lay === 'sat' && t.checked) loadSat();
    redraw();
  }
});
document.addEventListener('input', e => {
  if (e.target.dataset.k === 'q' && e.target.dataset.chg === 'of') {        // Live-Suche: Zeilen ausblenden, Liste baut sich beim Verlassen neu
    const q = e.target.value.toLowerCase();
    for (const tr of e.target.closest('.row').nextElementSibling?.querySelectorAll('tr[data-q]') ?? []) tr.hidden = !tr.dataset.q.includes(q);
    return;
  }
  const r = e.target.dataset.rng;
  if (!r) return;
  if (r === 'alt') { lay.alt = +e.target.value; store('lay', lay); } else tplus = +e.target.value;
  const v = e.target.nextElementSibling;
  if (v) v.textContent = r === 'alt' ? Math.round(lay.alt / 1000) + 'k ft' : tplusTxt();
  redraw();
});
document.addEventListener('toggle', e => { if (e.target.dataset?.tog === 'rep') repOpen = e.target.open; }, true);
addEventListener('keydown', e => {
  const a = document.activeElement;
  if (a && (/SELECT|TEXTAREA/.test(a.tagName) || (a.tagName === 'INPUT' && !['checkbox', 'range'].includes(a.type)))) return;
  if (e.key === 'Escape') { if (!$('#dlg').hidden) dlgClose(); else { sel = null; render(); } return; }
  if (st.screen !== 'map' || !W) return;
  const w = Object.entries(WINS).find(([, [, k]]) => k === e.key);
  if (w) { openWin(w[0], !st.wins[w[0]]?.open); return; }
  if (e.key === ' ' && !W.Mission && !W.You) { e.preventDefault(); ACT.play(); }
  if (e.key === 'e' || e.key === 'E') ACT.layt();
});

// ---------- Start ----------

(async function init() {
  if (!Object.keys(st.wins).length) st.wins = { flug: { open: true }, ato: { open: true } };
  if (lay.sat) loadSat();
  for (;;) {                                                              // App startet noch (oder wird gerade aktualisiert): weiter versuchen
    try { S = await api('static'); break; }
    catch { if (!$('#dlg [data-jn]')) toast('No connection – is DcsCampaign.exe running?'); await new Promise(ok => setTimeout(ok, 1500)); }
  }
  $('#toast').hidden = true;
  relief = new Image();
  await new Promise(ok => { relief.onload = relief.onerror = ok; relief.src = '/bg/relief.png'; });
  if (relief.naturalWidth) {
    const c = document.createElement('canvas'); c.width = relief.naturalWidth; c.height = relief.naturalHeight;
    const g = c.getContext('2d', { willReadFrequently: true }); g.drawImage(relief, 0, 0);
    const im = g.getImageData(0, 0, c.width, c.height), d = im.data;
    sea = { w: c.width, h: c.height, d: new Uint8ClampedArray(d), cell: (S.Relief.Z1 - S.Relief.Z0) / c.width };
    for (let j = 0; j < d.length; j += 4) {                                 // Design: Meer fast schwarz, Land dunkel mit leichtem Relief
      if (Math.abs(d[j] - 38) < 8 && Math.abs(d[j + 1] - 72) < 8 && Math.abs(d[j + 2] - 104) < 8) { d[j] = 9; d[j + 1] = 11; d[j + 2] = 13; continue; }
      const v = 13 + (0.3 * d[j] + 0.59 * d[j + 1] + 0.11 * d[j + 2]) / 255 * 26;
      d[j] = v; d[j + 1] = v + 3; d[j + 2] = v + 6;
    }
    g.putImageData(im, 0, 0);
    relief.c = c;
  }
  await load();
  if (st.screen === 'menu') getSaves();
  setInterval(() => loading || document.hidden || load(), 1000);
  (function tick(ts) { if (W?.Rate && !document.hidden && ts - (tick.t || 0) > 33) { tick.t = ts; draw(); } requestAnimationFrame(tick); })(0);   // Uhr läuft: Karte ~30 fps
})();
