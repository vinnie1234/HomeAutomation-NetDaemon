// Huis-dashboard: de kaart "NetDaemon activiteit" (type: custom:netdaemon-activiteit-card).
// Laat precies zien wat NetDaemon doet: een lamp die aangaat, een scene, een bericht, een status die verandert. Elke actie komt van
// NetDaemon (ActivityLog.cs) als event netdaemon_activity; de template-sensor sensor.netdaemon_activiteit (ha/template.yaml) bewaart ze in
// de geschiedenis van Home Assistant. Deze kaart leest die geschiedenis, houdt nieuwe acties live bij en filtert op app, ruimte, persoon,
// soort, periode en tekst. Een link kan het filter vooraf zetten: ?app=Alarm, ?ruimte=Woonkamer of ?persoon=Carleen.
// Geladen via frontend.extra_module_url in configuration.yaml (zie README.md).
(() => {
  const ENTITY = 'sensor.netdaemon_activiteit';
  const PERIODS = [['1 uur', 1], ['6 uur', 6], ['24 uur', 24], ['7 dagen', 168]];
  const PAGE = 150;

  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const pad = n => String(n).padStart(2, '0');
  const split = v => String(v ?? '').split(',').map(x => x.trim()).filter(Boolean);
  const dayLabel = d => {
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const diff = Math.round((today - new Date(d.getFullYear(), d.getMonth(), d.getDate())) / 86400000);
    return diff === 0 ? 'Vandaag' : diff === 1 ? 'Gisteren' : d.toLocaleDateString('nl-NL', { weekday: 'long', day: 'numeric', month: 'long' });
  };

  class NetDaemonActiviteitCard extends HTMLElement {
    constructor() {
      super();
      this.attachShadow({ mode: 'open' });
      this._entries = [];
      this._seen = new Set();
      this._hours = 24;
      this._limit = PAGE;
      this._filter = { app: '', ruimte: '', persoon: '', soort: '', tekst: '' };
      const q = new URLSearchParams(location.search);
      this._filter.app = q.get('app') ?? '';
      this._filter.ruimte = q.get('ruimte') ?? '';
      this._filter.persoon = q.get('persoon') ?? '';
      this._build();
    }

    setConfig(config) { this._config = config ?? {}; if (config?.uren) this._hours = config.uren; }
    getCardSize() { return 12; }

    set hass(hass) {
      const first = !this._hass;
      this._hass = hass;
      if (first) { this._load(); this._subscribe(); return; }
      // De sensor houdt de laatste actie bij: een vangnet voor als de verbinding even weg was
      const a = hass.states[ENTITY]?.attributes;
      if (a?.id && !this._seen.has(a.id)) { this._add(a, Date.now()); this._sortAndShow(); }
    }

    // Realtime: elke actie van NetDaemon komt als event binnen en staat meteen bovenaan, ook als er er meer tegelijk komen
    // (de sensor laat er dan maar een zien). De geschiedenis erachter komt van de sensor.
    async _subscribe() {
      if (this._unsub || !this._hass?.connection) return;
      try {
        this._unsub = await this._hass.connection.subscribeEvents(ev => {
          this._add(ev.data, Date.now());
          this._sortAndShow();
        }, 'netdaemon_activity');
      } catch (e) { /* geen live; de sensor vangt het op */ }
    }

    connectedCallback() { if (this._hass) this._subscribe(); }
    disconnectedCallback() { this._unsub?.(); this._unsub = null; }

    _add(a, fallbackTime) {
      if (!a?.id || this._seen.has(a.id)) return;
      this._seen.add(a.id);
      const t = a.time ? Date.parse(a.time) : fallbackTime;
      this._entries.push({ ...a, _t: Number.isNaN(t) ? fallbackTime : t });
    }

    async _load() {
      this._status('Geschiedenis ophalen…');
      try {
        const end = new Date(), start = new Date(end - this._hours * 3600000);
        const res = await this._hass.callWS({
          type: 'history/history_during_period', start_time: start.toISOString(), end_time: end.toISOString(), entity_ids: [ENTITY],
          include_start_time_state: false, significant_changes_only: false, minimal_response: false, no_attributes: false
        });
        this._entries = []; this._seen = new Set();
        for (const row of res?.[ENTITY] ?? []) this._add(row.a, (row.lu ?? 0) * 1000);
        const now = this._hass.states[ENTITY]?.attributes;
        if (now?.id) this._add(now, Date.now());
        this._status('');
      } catch (e) {
        this._status('De geschiedenis kon niet worden opgehaald (staat sensor.netdaemon_activiteit er al, zie ha/template.yaml?). Nieuwe acties komen wel live binnen.');
      }
      this._sortAndShow();
    }

    _status(text) { this._statusEl.textContent = text; this._statusEl.style.display = text ? 'block' : 'none'; }

    _sortAndShow() {
      this._entries.sort((x, y) => y._t - x._t);
      const cutoff = Date.now() - this._hours * 3600000;
      this._entries = this._entries.filter(e => e._t >= cutoff);
      this._fillOptions();
      this._render();
    }

    _matches(e) {
      const f = this._filter;
      if (f.app && e.app !== f.app) return false;
      if (f.ruimte && !split(e.room).includes(f.ruimte)) return false;
      if (f.persoon && !split(e.person).includes(f.persoon)) return false;
      if (f.soort && e.kind !== f.soort) return false;
      if (f.tekst) {
        const hay = `${e.text} ${e.app} ${e.entity} ${e.service} ${e.room} ${e.person}`.toLowerCase();
        if (!f.tekst.toLowerCase().split(/\s+/).every(w => hay.includes(w))) return false;
      }
      return true;
    }

    _count(entries, pick) {
      const m = new Map();
      for (const e of entries) for (const v of pick(e)) m.set(v, (m.get(v) ?? 0) + 1);
      return [...m.entries()].sort((a, b) => a[0].localeCompare(b[0], 'nl'));
    }

    _fillOptions() {
      const set = (id, label, options, current) => {
        const el = this.shadowRoot.getElementById(id);
        el.innerHTML = `<option value="">${label}</option>` + options.map(([v, n]) =>
          `<option value="${esc(v)}"${v === current ? ' selected' : ''}>${esc(v)} (${n})</option>`).join('');
        // Een filter dat uit een link komt maar (nog) niets oplevert blijft zichtbaar, zodat je het kunt wissen
        if (current && !options.some(([v]) => v === current)) el.insertAdjacentHTML('beforeend', `<option value="${esc(current)}" selected>${esc(current)} (0)</option>`);
      };
      set('app', 'Alle apps', this._count(this._entries, e => [e.app]), this._filter.app);
      set('ruimte', 'Alle ruimtes', this._count(this._entries, e => split(e.room)), this._filter.ruimte);
      set('persoon', 'Iedereen', this._count(this._entries, e => split(e.person)), this._filter.persoon);
    }

    _render() {
      const list = this.shadowRoot.getElementById('list');
      const shown = this._entries.filter(e => this._matches(e));
      this.shadowRoot.getElementById('count').textContent = `${shown.length} van ${this._entries.length} acties`;
      if (!shown.length) {
        list.innerHTML = `<div class="empty">${this._entries.length ? 'Niets voor dit filter.' : 'Nog geen acties in deze periode.'}</div>`;
        return;
      }
      let html = '', day = '';
      for (const e of shown.slice(0, this._limit)) {
        const d = new Date(e._t);
        const label = dayLabel(d);
        if (label !== day) { day = label; html += `<div class="day">${esc(label)}</div>`; }
        const chips = [`<span class="chip app" data-set="app" data-v="${esc(e.app)}">${esc(e.app)}</span>`]
          .concat(split(e.room).map(r => `<span class="chip room" data-set="ruimte" data-v="${esc(r)}">${esc(r)}</span>`))
          .concat(split(e.person).map(p => `<span class="chip person" data-set="persoon" data-v="${esc(p)}">${esc(p)}</span>`));
        html += `<div class="row ${esc(e.kind)}">
            <span class="time">${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}</span>
            <ha-icon icon="${e.kind === 'status' ? 'mdi:gauge' : 'mdi:flash'}"></ha-icon>
            <div class="what"><div class="text">${esc(e.text)}</div>
              <div class="meta">${chips.join('')}<span class="code">${esc(e.service || e.entity)}</span></div></div>
          </div>`;
      }
      if (shown.length > this._limit) html += `<div class="more" id="more">Toon meer (${shown.length - this._limit} verborgen)</div>`;
      list.innerHTML = html;
      this.shadowRoot.getElementById('more')?.addEventListener('click', () => { this._limit += PAGE; this._render(); });
    }

    _build() {
      const root = this.shadowRoot;
      root.innerHTML = `<style>
          :host { display: block; }
          ha-card { padding: 14px 14px 10px; overflow: hidden; }
          .head { display: flex; align-items: baseline; justify-content: space-between; gap: 8px; margin-bottom: 10px; }
          .title { font-size: 18px; font-weight: 700; }
          #count { font-size: 12px; opacity: .7; }
          .controls { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 8px; margin-bottom: 8px; }
          select, input { width: 100%; box-sizing: border-box; padding: 9px 10px; border-radius: 12px; font: inherit; font-size: 14px;
            color: var(--primary-text-color); background: rgba(255,255,255,.08); border: 1px solid rgba(255,255,255,.14); }
          select option { color: #111; }
          .tools { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin: 2px 0 8px; }
          .clear { cursor: pointer; font-size: 12px; padding: 4px 10px; border-radius: 10px; border: 1px solid rgba(255,255,255,.2); opacity: .85; }
          #status { display: none; padding: 10px 0; opacity: .8; font-size: 13px; }
          .day { margin: 12px 0 4px; font-size: 12px; font-weight: 700; letter-spacing: .04em; text-transform: uppercase; opacity: .65; }
          .row { display: grid; grid-template-columns: 62px 22px 1fr; gap: 6px; align-items: start; padding: 7px 0; border-top: 1px solid rgba(255,255,255,.07); }
          .time { font-variant-numeric: tabular-nums; font-size: 12px; opacity: .7; padding-top: 2px; }
          .row ha-icon { --mdc-icon-size: 16px; opacity: .8; margin-top: 2px; }
          .row.status ha-icon { color: #7aa2ff; } .row.actie ha-icon { color: #ffd166; }
          .text { font-size: 14px; line-height: 1.35; word-break: break-word; }
          .meta { display: flex; flex-wrap: wrap; gap: 5px; margin-top: 3px; align-items: center; }
          .chip { cursor: pointer; font-size: 11px; font-weight: 600; padding: 1px 8px; border-radius: 9px; background: rgba(255,255,255,.1); }
          .chip.app { background: rgba(122,162,255,.22); } .chip.room { background: rgba(74,222,128,.2); } .chip.person { background: rgba(255,143,171,.24); }
          .code { font-size: 11px; opacity: .5; font-family: ui-monospace, monospace; }
          .empty, .more { padding: 18px 0; text-align: center; opacity: .75; font-size: 14px; }
          .more { cursor: pointer; font-weight: 600; }
        </style>
        <ha-card>
          <div class="head"><span class="title">NetDaemon activiteit</span><span id="count"></span></div>
          <div class="controls">
            <select id="app"></select><select id="ruimte"></select><select id="persoon"></select>
            <select id="soort"><option value="">Alles</option><option value="actie">Acties</option><option value="status">Statussen</option></select>
            <select id="periode">${PERIODS.map(([l, h]) => `<option value="${h}"${h === 24 ? ' selected' : ''}>${l}</option>`).join('')}</select>
          </div>
          <input id="tekst" type="search" placeholder="Zoek op tekst, lamp of dienst…" />
          <div class="tools"><span></span><span class="clear" id="clear">Filters wissen</span></div>
          <div id="status"></div>
          <div id="list"></div>
        </ha-card>`;
      this._statusEl = root.getElementById('status');
      const bind = (id, key) => root.getElementById(id).addEventListener('change', ev => { this._filter[key] = ev.target.value; this._limit = PAGE; this._render(); });
      bind('app', 'app'); bind('ruimte', 'ruimte'); bind('persoon', 'persoon'); bind('soort', 'soort');
      root.getElementById('tekst').addEventListener('input', ev => { this._filter.tekst = ev.target.value.trim(); this._limit = PAGE; this._render(); });
      root.getElementById('periode').addEventListener('change', ev => { this._hours = Number(ev.target.value); this._limit = PAGE; this._load(); });
      root.getElementById('clear').addEventListener('click', () => {
        this._filter = { app: '', ruimte: '', persoon: '', soort: '', tekst: '' };
        root.getElementById('soort').value = ''; root.getElementById('tekst').value = '';
        this._limit = PAGE; this._fillOptions(); this._render();
      });
      // Een chip in een regel zet dat filter
      root.getElementById('list').addEventListener('click', ev => {
        const chip = ev.target.closest?.('.chip');
        if (!chip) return;
        this._filter[chip.dataset.set] = chip.dataset.v;
        this._limit = PAGE; this._fillOptions(); this._render();
      });
    }
  }

  if (!customElements.get('netdaemon-activiteit-card')) customElements.define('netdaemon-activiteit-card', NetDaemonActiviteitCard);
  window.customCards = window.customCards ?? [];
  window.customCards.push({ type: 'netdaemon-activiteit-card', name: 'NetDaemon activiteit', description: 'Wat NetDaemon doet, met filters op app, ruimte en persoon' });
})();
