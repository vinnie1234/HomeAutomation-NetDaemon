// Huis-dashboard: de muziekspeler. Opent bij een tik op de muziektegel (template music_now_card) als venster of schermvullend:
// bedienen (hoes, balk, volume, shuffle, herhalen), bladeren en zoeken (playlists, albums, artiesten), het apparaat kiezen waarop
// Spotify speelt, en een QR-code voor een Spotify Jam. Elk Spotify-account (media_player.spotify_*) is te kiezen.
// Wordt door de kaart geladen met import('/local/huis/muziek-speler.js?v=N'); N ophogen na een wijziging.
// Terwijl de speler open staat (#huis-speler) gaat idle-home.js niet terug naar Home.
import qrcode from './qrcode.js';

const ID = 'huis-speler';
const JAM_LINK = 'input_text.spotify_jam_link';

const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const hass = () => document.querySelector('home-assistant').hass;
const clock = seconds => {
  if (!Number.isFinite(seconds)) return '0:00';
  const s = Math.max(0, Math.round(seconds));
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
};
const accounts = () => Object.keys(hass().states).filter(id => id.startsWith('media_player.spotify_')).sort();
const nameOf = id => String(hass().states[id]?.attributes.friendly_name ?? id).replace(/^Spotify\s+/i, '').split(' ')[0];

const CSS = `
  #${ID} { position: fixed; inset: 0; z-index: 9999; display: flex; align-items: center; justify-content: center; background: rgba(4,6,12,0.72);
    backdrop-filter: blur(6px); font-family: var(--paper-font-body1_-_font-family, Roboto, sans-serif); color: #f2f4f8; pointer-events: auto; }
  #${ID} * { box-sizing: border-box; }
  #${ID} .box { width: min(680px, 96vw); height: min(880px, 94vh); display: flex; flex-direction: column; overflow: hidden; border-radius: 28px;
    background: linear-gradient(160deg, rgba(30,36,54,0.98), rgba(14,18,30,0.98)); border: 1px solid rgba(255,255,255,0.1); box-shadow: 0 24px 80px rgba(0,0,0,0.6); }
  #${ID}.full .box { width: 100vw; height: 100vh; border-radius: 0; border: 0; }
  #${ID} header { display: flex; align-items: center; gap: 8px; padding: 12px 14px; border-bottom: 1px solid rgba(255,255,255,0.08); }
  #${ID} header .title { flex: 1; min-width: 0; font-weight: 700; font-size: 16px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  #${ID} .ib { width: 42px; height: 42px; border-radius: 50%; border: 0; display: inline-flex; align-items: center; justify-content: center; cursor: pointer;
    background: rgba(255,255,255,0.08); color: inherit; flex: 0 0 auto; }
  #${ID} .ib:active { background: rgba(255,255,255,0.2); }
  #${ID} .ib.on { color: #1db954; background: rgba(29,185,84,0.16); }
  #${ID} .chips { display: flex; gap: 6px; padding: 8px 14px 0; flex-wrap: wrap; }
  #${ID} .chip { padding: 5px 12px; border-radius: 14px; font-size: 13px; font-weight: 600; cursor: pointer; border: 1px solid rgba(255,255,255,0.18); background: transparent; color: inherit; }
  #${ID} .chip.on { border-color: #1db954; color: #1db954; background: rgba(29,185,84,0.12); }
  #${ID} nav { display: flex; gap: 4px; padding: 10px 14px 0; }
  #${ID} nav .tab { flex: 1; padding: 10px 6px; border-radius: 14px; border: 0; cursor: pointer; font-weight: 600; font-size: 14px; color: rgba(255,255,255,0.7); background: transparent; }
  #${ID} nav .tab.on { background: rgba(255,255,255,0.1); color: #fff; }
  #${ID} main { flex: 1; min-height: 0; overflow: auto; padding: 14px; }
  #${ID} .now { display: grid; gap: 16px; justify-items: center; align-content: start; }
  #${ID} .cover { width: min(100%, 24vh); aspect-ratio: 1; border-radius: 20px; object-fit: cover; background: rgba(255,255,255,0.08); box-shadow: 0 14px 40px rgba(0,0,0,0.5); }
  #${ID} .meta { width: 100%; text-align: left; }
  #${ID}.full .cover { width: min(100%, 46vh); }
  #${ID} .meta .t { font-size: 22px; font-weight: 800; line-height: 1.2; }
  #${ID} .meta .a { font-size: 16px; opacity: .75; margin-top: 2px; }
  #${ID} .meta .d { font-size: 13px; color: #1db954; margin-top: 6px; font-weight: 600; }
  #${ID} .bar { width: 100%; }
  #${ID} .times { display: flex; justify-content: space-between; font-size: 12px; opacity: .65; margin-top: 2px; }
  #${ID} input[type=range] { width: 100%; accent-color: #1db954; height: 28px; }
  #${ID} .ctl { display: flex; align-items: center; justify-content: center; gap: 14px; width: 100%; }
  #${ID} .ctl .ib { width: 52px; height: 52px; }
  #${ID} .ctl .big { width: 76px; height: 76px; background: #1db954; color: #04130a; }
  #${ID} .vol { display: flex; align-items: center; gap: 10px; width: 100%; }
  #${ID} .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(130px, 1fr)); gap: 12px; }
  #${ID} .item { position: relative; border-radius: 16px; background: rgba(255,255,255,0.06); overflow: hidden; cursor: pointer; text-align: left; border: 0; color: inherit; padding: 0; }
  #${ID} .item img, #${ID} .item .ph { width: 100%; aspect-ratio: 1; object-fit: cover; display: flex; align-items: center; justify-content: center; background: rgba(255,255,255,0.08); }
  #${ID} .item .n { padding: 8px 10px 2px; font-weight: 700; font-size: 14px; line-height: 1.25; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
  #${ID} .item .s { padding: 0 10px 10px; font-size: 12px; opacity: .65; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  #${ID} .item .play { position: absolute; right: 8px; top: 8px; width: 38px; height: 38px; border-radius: 50%; border: 0; background: #1db954; color: #04130a; cursor: pointer; display: flex; align-items: center; justify-content: center; }
  #${ID} .row { display: flex; align-items: center; gap: 12px; padding: 10px 12px; border-radius: 14px; background: rgba(255,255,255,0.06); margin-bottom: 8px; cursor: pointer; border: 0; width: 100%; color: inherit; text-align: left; font-size: 15px; font-weight: 600; }
  #${ID} .row.on { background: rgba(29,185,84,0.16); color: #1db954; }
  #${ID} .crumbs { display: flex; align-items: center; gap: 8px; margin-bottom: 12px; }
  #${ID} .crumbs .t { flex: 1; font-weight: 700; font-size: 16px; }
  #${ID} .search { display: flex; gap: 8px; margin-bottom: 12px; }
  #${ID} input[type=text], #${ID} input[type=search] { flex: 1; min-width: 0; padding: 11px 14px; border-radius: 14px; border: 1px solid rgba(255,255,255,0.18); background: rgba(255,255,255,0.07); color: inherit; font-size: 15px; }
  #${ID} .hint { opacity: .7; font-size: 14px; line-height: 1.45; }
  #${ID} .qr { background: #fff; padding: 12px; border-radius: 20px; width: min(100%, 360px); margin: 14px auto; }
  #${ID} .qr svg { width: 100%; height: auto; display: block; }
  #${ID} .toast { position: absolute; left: 50%; bottom: 24px; transform: translateX(-50%); padding: 10px 18px; border-radius: 16px; background: rgba(0,0,0,0.85); font-size: 14px; max-width: 90%; }
  @media (min-width: 900px) and (orientation: landscape) {
    #${ID}.full .now { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 28px 40px; align-items: center; justify-items: stretch; height: 100%; }
    #${ID}.full .now .cover { width: min(100%, 70vh); justify-self: center; }
    #${ID}.full .now { padding: 0 2vw; }
    #${ID}.full .now .side { display: grid; gap: 18px; }
  }
`;

let root, entity, tab = 'nu', full = false, tick, dragging = false;
let browse = { stack: [], items: null, loading: false, error: '', query: '', results: null };

const icon = (name, size = 24) => `<ha-icon icon="${name}" style="--mdc-icon-size:${size}px"></ha-icon>`;
const state = () => hass().states[entity];
const call = (domain, service, data) => hass().callService(domain, service, { entity_id: entity, ...data }).catch(e => toast(e?.message ?? 'Dat lukte niet'));

function toast(text) {
  const el = document.createElement('div');
  el.className = 'toast';
  el.textContent = text;
  root.querySelector('.box').appendChild(el);
  setTimeout(() => el.remove(), 3500);
}

export function close() {
  clearInterval(tick);
  document.getElementById(ID)?.remove();
  root = null;
}

export function open(entityId) {
  if (document.getElementById(ID)) close();
  entity = entityId;
  tab = 'nu';
  browse = { stack: [], items: null, loading: false, error: '', query: '', results: null };
  root = document.createElement('div');
  root.id = ID;
  root.className = full ? 'full' : '';
  root.innerHTML = `<style>${CSS}</style><div class="box"></div>`;
  document.body.appendChild(root);
  root.addEventListener('click', e => { if (e.target === root) close(); });
  document.addEventListener('keydown', onKey);
  build();
  tick = setInterval(update, 500);
}

function onKey(e) {
  if (!document.getElementById(ID)) { document.removeEventListener('keydown', onKey); return; }
  if (e.key === 'Escape') close();
}

// ---- opbouw

function build() {
  const box = root.querySelector('.box');
  const all = accounts();
  box.innerHTML = `
    <header>
      <button class="ib" data-a="close" title="Terug">${icon('mdi:arrow-left')}</button>
      <div class="title" id="sp-title">Muziek</div>
      <button class="ib" data-a="more" title="Instellingen van de speler in Home Assistant">${icon('mdi:cog-outline')}</button>
      <button class="ib" data-a="full" title="Schermvullend">${icon(full ? 'mdi:fullscreen-exit' : 'mdi:fullscreen')}</button>
    </header>
    ${all.length > 1 ? `<div class="chips">${all.map(id => `<button class="chip ${id === entity ? 'on' : ''}" data-a="account" data-id="${id}">${esc(nameOf(id))}</button>`).join('')}</div>` : ''}
    <nav>${[['nu', 'Speler', 'mdi:music'], ['bladeren', 'Bladeren', 'mdi:folder-music'], ['apparaten', 'Apparaten', 'mdi:speaker'], ['jam', 'Jam', 'mdi:qrcode']]
      .map(([k, label, ic]) => `<button class="tab ${k === tab ? 'on' : ''}" data-a="tab" data-k="${k}">${label}</button>`).join('')}</nav>
    <main id="sp-main"></main>`;
  box.onclick = onClick;
  render();
}

function onClick(e) {
  const t = e.target.closest('[data-a]');
  if (!t) return;
  const a = t.dataset.a;
  const s = state();
  if (a === 'close') close();
  else if (a === 'more') { close(); document.querySelector('home-assistant').dispatchEvent(new CustomEvent('hass-more-info', { bubbles: true, composed: true, detail: { entityId: entity } })); }
  else if (a === 'full') { full = !full; root.classList.toggle('full', full); t.innerHTML = icon(full ? 'mdi:fullscreen-exit' : 'mdi:fullscreen'); }
  else if (a === 'account') { entity = t.dataset.id; browse = { stack: [], items: null, loading: false, error: '', query: '', results: null }; build(); }
  else if (a === 'tab') { tab = t.dataset.k; root.querySelectorAll('nav .tab').forEach(x => x.classList.toggle('on', x.dataset.k === tab)); render(); }
  else if (a === 'playpause') call('media_player', 'media_play_pause');
  else if (a === 'prev') call('media_player', 'media_previous_track');
  else if (a === 'next') call('media_player', 'media_next_track');
  else if (a === 'shuffle') call('media_player', 'shuffle_set', { shuffle: !s?.attributes.shuffle });
  else if (a === 'repeat') call('media_player', 'repeat_set', { repeat: { off: 'all', all: 'one', one: 'off' }[s?.attributes.repeat ?? 'off'] });
  else if (a === 'mute') call('media_player', 'volume_mute', { is_volume_muted: !s?.attributes.is_volume_muted });
  else if (a === 'source') { call('media_player', 'select_source', { source: t.dataset.s }); toast(`Speelt op ${t.dataset.s}`); setTimeout(render, 600); }
  else if (a === 'open') openItem(browse.results ?? browse.items, +t.dataset.i);
  else if (a === 'playitem') { e.stopPropagation(); playItem(currentList()[+t.dataset.i]); }
  else if (a === 'back') { browse.stack.pop(); browse.results = null; browse.items = null; loadCurrent(); }
  else if (a === 'dosearch') search();
  else if (a === 'savejam') { const v = root.querySelector('#sp-jam')?.value.trim() ?? ''; hass().callService('input_text', 'set_value', { entity_id: JAM_LINK, value: v }); setTimeout(render, 400); }
  else if (a === 'clearjam') { hass().callService('input_text', 'set_value', { entity_id: JAM_LINK, value: '' }); setTimeout(render, 400); }
}

// ---- tabs

function render() {
  const main = root.querySelector('#sp-main');
  if (tab === 'nu') renderNow(main);
  else if (tab === 'bladeren') renderBrowse(main);
  else if (tab === 'apparaten') renderDevices(main);
  else renderJam(main);
  update();
}

function renderNow(main) {
  main.innerHTML = `<div class="now">
      <img class="cover" id="sp-cover" alt="">
      <div class="side" style="width:100%;display:grid;gap:16px">
        <div class="meta"><div class="t" id="sp-t"></div><div class="a" id="sp-a"></div><div class="d" id="sp-d"></div></div>
        <div class="bar"><input type="range" id="sp-pos" min="0" max="1000" value="0"><div class="times"><span id="sp-p"></span><span id="sp-l"></span></div></div>
        <div class="ctl">
          <button class="ib" data-a="shuffle" id="sp-shuffle" title="Willekeurig">${icon('mdi:shuffle-variant')}</button>
          <button class="ib" data-a="prev" title="Vorige">${icon('mdi:skip-previous', 30)}</button>
          <button class="ib big" data-a="playpause" id="sp-pp">${icon('mdi:play', 42)}</button>
          <button class="ib" data-a="next" title="Volgende">${icon('mdi:skip-next', 30)}</button>
          <button class="ib" data-a="repeat" id="sp-repeat" title="Herhalen">${icon('mdi:repeat')}</button>
        </div>
        <div class="vol"><button class="ib" data-a="mute" id="sp-mute">${icon('mdi:volume-high')}</button><input type="range" id="sp-vol" min="0" max="100" value="0"></div>
      </div></div>`;
  const pos = main.querySelector('#sp-pos'), vol = main.querySelector('#sp-vol');
  pos.addEventListener('pointerdown', () => { dragging = true; });
  pos.addEventListener('change', () => { dragging = false; const d = state()?.attributes.media_duration; if (d) call('media_player', 'media_seek', { seek_position: Math.round(pos.value / 1000 * d) }); });
  vol.addEventListener('change', () => call('media_player', 'volume_set', { volume_level: vol.value / 100 }));
}

function update() {
  if (!root) return;
  const s = state();
  const a = s?.attributes ?? {};
  const title = root.querySelector('#sp-title');
  if (title) title.textContent = `${nameOf(entity)} · ${a.source ?? (s?.state === 'playing' ? 'Spotify' : 'Spotify (speelt niet)')}`;
  if (tab !== 'nu') return;
  const set = (id, fn) => { const el = root.querySelector(id); if (el) fn(el); };
  set('#sp-cover', el => { const src = a.entity_picture ?? ''; if (el.getAttribute('src') !== src) el.setAttribute('src', src); el.style.visibility = src ? 'visible' : 'hidden'; });
  set('#sp-t', el => { el.textContent = a.media_title ?? (s?.state === 'playing' ? 'Spotify' : 'Er speelt niets'); });
  set('#sp-a', el => { el.textContent = [a.media_artist ?? a.media_album_artist, a.media_album_name].filter(Boolean).join(' · '); });
  set('#sp-d', el => { el.textContent = a.source ? `Speelt op ${a.source}` : (s?.state === 'playing' ? '' : 'Kies bij Apparaten waar het moet spelen, of begin bij Bladeren'); });
  set('#sp-pp', el => { const ic = s?.state === 'playing' ? 'mdi:pause' : 'mdi:play'; if (el.dataset.ic !== ic) { el.dataset.ic = ic; el.innerHTML = icon(ic, 42); } });
  set('#sp-shuffle', el => el.classList.toggle('on', !!a.shuffle));
  set('#sp-repeat', el => { el.classList.toggle('on', (a.repeat ?? 'off') !== 'off'); const ic = a.repeat === 'one' ? 'mdi:repeat-once' : 'mdi:repeat'; if (el.dataset.ic !== ic) { el.dataset.ic = ic; el.innerHTML = icon(ic); } });
  set('#sp-mute', el => { const ic = a.is_volume_muted ? 'mdi:volume-off' : 'mdi:volume-high'; if (el.dataset.ic !== ic) { el.dataset.ic = ic; el.innerHTML = icon(ic); } });
  set('#sp-vol', el => { if (document.activeElement !== el && a.volume_level != null) el.value = Math.round(a.volume_level * 100); });
  const duration = a.media_duration;
  let position = a.media_position;
  if (position != null && s.state === 'playing' && a.media_position_updated_at) position += (Date.now() - new Date(a.media_position_updated_at).getTime()) / 1000;
  if (duration && position != null) position = Math.min(duration, position);
  set('#sp-pos', el => { if (!dragging && duration && position != null) el.value = Math.round(position / duration * 1000); el.disabled = !duration; });
  set('#sp-p', el => { el.textContent = position != null ? clock(position) : ''; });
  set('#sp-l', el => { el.textContent = duration ? clock(duration) : ''; });
}

// ---- bladeren en zoeken

const currentList = () => browse.results ?? browse.items ?? [];

function renderBrowse(main) {
  const supported = ((state()?.attributes.supported_features ?? 0) & 131072) !== 0;
  if (!supported) { main.innerHTML = `<div class="hint">Deze speler kan niet bladeren. Kies hierboven een Spotify-account.</div>`; return; }
  if (browse.items === null && !browse.loading && !browse.results) { loadCurrent(); return; }
  const top = browse.stack[browse.stack.length - 1];
  const list = currentList();
  main.innerHTML = `
    <div class="search"><input type="search" id="sp-q" placeholder="Zoek een nummer, album, artiest of playlist" value="${esc(browse.query)}"><button class="ib" data-a="dosearch" title="Zoeken">${icon('mdi:magnify')}</button></div>
    ${browse.stack.length || browse.results ? `<div class="crumbs"><button class="ib" data-a="back" title="Terug">${icon('mdi:arrow-left')}</button><div class="t">${esc(browse.results ? `Zoeken: ${browse.query}` : top.title)}</div></div>` : ''}
    ${browse.loading ? '<div class="hint">Laden…</div>' : browse.error ? `<div class="hint">${esc(browse.error)}</div>` : `<div class="grid">${list.map((it, i) => `
      <div class="item" data-a="open" data-i="${i}">
        ${it.thumbnail ? `<img loading="lazy" src="${esc(it.thumbnail)}" alt="">` : `<div class="ph">${icon('mdi:music', 36)}</div>`}
        ${it.can_play ? `<button class="play" data-a="playitem" data-i="${i}" title="Afspelen">${icon('mdi:play', 22)}</button>` : ''}
        <div class="n">${esc(it.title)}</div><div class="s">${esc(it.media_class === 'directory' ? '' : it.media_class ?? '')}</div></div>`).join('') || '<div class="hint">Niets gevonden.</div>'}</div>`}`;
  const q = main.querySelector('#sp-q');
  q?.addEventListener('keydown', e => { if (e.key === 'Enter') { browse.query = q.value; search(); } });
  q?.addEventListener('input', () => { browse.query = q.value; });
}

async function loadCurrent() {
  browse.loading = true; browse.error = ''; browse.items = null;
  renderBrowseSoon();
  const top = browse.stack[browse.stack.length - 1];
  try {
    const res = await hass().callWS({ type: 'media_player/browse_media', entity_id: entity, ...(top ? { media_content_id: top.id, media_content_type: top.type } : {}) });
    browse.items = res.children ?? [];
  } catch (e) {
    browse.items = [];
    browse.error = e?.message ?? 'Bladeren lukte niet';
  }
  browse.loading = false;
  renderBrowseSoon();
}

function renderBrowseSoon() {
  const main = root?.querySelector('#sp-main');
  if (main && tab === 'bladeren') renderBrowse(main);
}

async function search() {
  const q = (root.querySelector('#sp-q')?.value ?? browse.query).trim();
  if (!q) return;
  browse.query = q; browse.loading = true; browse.error = ''; browse.results = [];
  renderBrowseSoon();
  try {
    const res = await hass().callWS({ type: 'media_player/search_media', entity_id: entity, search_query: q });
    browse.results = res.result ?? res.results ?? [];
  } catch (e) {
    browse.results = [];
    browse.error = `Zoeken lukt hier niet (${e?.message ?? 'onbekende fout'}). Blader door de playlists en albums.`;
  }
  browse.loading = false;
  renderBrowseSoon();
}

function openItem(list, i) {
  const it = (list ?? [])[i];
  if (!it) return;
  if (it.can_expand) {
    browse.stack.push({ id: it.media_content_id, type: it.media_content_type, title: it.title });
    browse.results = null; browse.items = null;
    loadCurrent();
  } else if (it.can_play) playItem(it);
}

function playItem(it) {
  if (!it) return;
  call('media_player', 'play_media', { media_content_id: it.media_content_id, media_content_type: it.media_content_type });
  toast(`Speelt: ${it.title}`);
  setTimeout(() => { tab = 'nu'; root?.querySelectorAll('nav .tab').forEach(x => x.classList.toggle('on', x.dataset.k === tab)); if (root) render(); }, 700);
}

// ---- apparaten en jam

function renderDevices(main) {
  const a = state()?.attributes ?? {};
  const sources = a.source_list ?? [];
  main.innerHTML = sources.length
    ? `<div class="hint" style="margin-bottom:10px">Waar moet ${esc(nameOf(entity))} spelen? Een apparaat staat hier zodra Spotify het kent (open Spotify er een keer op).</div>
       ${sources.map(s => `<button class="row ${s === a.source ? 'on' : ''}" data-a="source" data-s="${esc(s)}">${icon(s === a.source ? 'mdi:volume-high' : 'mdi:speaker')}<span>${esc(s)}</span></button>`).join('')}`
    : '<div class="hint">Spotify kent nu geen apparaten. Open Spotify op een telefoon of speaker, dan staat die hier.</div>';
}

function renderJam(main) {
  const link = String(hass().states[JAM_LINK]?.state ?? '');
  const has = /^https?:\/\//.test(link);
  let svg = '';
  if (has) {
    try { const q = qrcode(0, 'M'); q.addData(link); q.make(); svg = q.createSvgTag(4, 0).replace(/width="[^"]*"/, '').replace(/height="[^"]*"/, ''); }
    catch (e) { svg = ''; }
  }
  main.innerHTML = `
    ${has ? `<div class="hint" style="text-align:center">Laat anderen deze code scannen om de Jam te joinen.</div><div class="qr">${svg || 'De link is te lang voor een code.'}</div>` : ''}
    <div class="hint">${has ? 'Een andere Jam? Plak de nieuwe link.' : 'Start in de Spotify-app een Jam (de speler → Jam starten), kopieer de link en plak hem hier. Dan komt er een QR-code om te delen.'}</div>
    <div class="search" style="margin-top:12px"><input type="text" id="sp-jam" placeholder="https://spotify.link/…" value="${esc(link === 'unknown' ? '' : link)}">
      <button class="ib" data-a="savejam" title="Bewaren">${icon('mdi:check')}</button>${has ? `<button class="ib" data-a="clearjam" title="Wissen">${icon('mdi:delete-outline')}</button>` : ''}</div>`;
}
