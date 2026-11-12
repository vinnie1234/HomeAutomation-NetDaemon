// Energiestroom: overzicht van het huidige verbruik als energiefluskaart.
// Zon en net links, in het midden een ring met het verbruik van het huis, rechts de stroomprijs, daaronder de kamers met hun apparaten
// (met stippellijnen van de ring naar elke kamer) en onderaan de totalen van vandaag.
// Gebruik: `type: custom:huis-energiestroom` (geladen vanuit de tab-template, ?v= daar ophogen na een wijziging van dit bestand).

const GRID = 'sensor.p1_meter_3c39e72a64e8_active_power';       // + = afnemen van het net, - = terugleveren
const SOLAR = 'sensor.p1_meter_vermogen';                        // opwek van de zonnepanelen (W)
const PRICE_NOW = 'sensor.anwb_electricity_all_in_price_current';
const PRICE_AVG = 'sensor.anwb_electricity_all_in_price_average_today';
const PRICE_LOW = 'sensor.anwb_electricity_all_in_price_lowest_today';
const PRICE_HIGH = 'sensor.anwb_electricity_all_in_price_highest_today';
const COST_NOW = 'sensor.gang_p1_meter_3c39e72a64e8_p1_meter_3c39e72a64e8_active_real_time_energy_cost';
const COST_DAY = 'sensor.gang_p1_meter_3c39e72a64e8_p1_meter_3c39e72a64e8_active_daily_energy_cost';
const COST_MONTH = 'sensor.gang_p1_meter_3c39e72a64e8_p1_meter_3c39e72a64e8_active_monthly_energy_cost';
const STANDBY = 'sensor.all_standby_power';
const STAT_IMPORT = 'sensor.p1_meter_3c39e72a64e8_total_power_import';
const STAT_EXPORT = 'sensor.p1_meter_3c39e72a64e8_total_power_export';
const STAT_PV = 'sensor.p1_meter_energie_export';

/** Onder deze opwek (W) meet de omvormer alleen ruis: dat telt als 0. */
const SOLAR_NOISE = 5;
const COLOR_SOLAR = '#ffc83d';
const COLOR_NET = '#ff5d73';
const COLOR_PRICE = '#5b9dff';
const COLOR_HOME = '#22d3ee';

// Kamers en hun vermogenssensoren: [naam, sensor]
const ROOMS = [
  { name: 'Woonkamer', icon: 'mdi:sofa', color: '#8b8cff', devices: [
    ['Plafond', 'sensor.plafond_woonkamer_power'], ['Hue Play links', 'sensor.hue_play_links_power'],
    ['Hue Play midden', 'sensor.hue_play_midden_power'], ['Hue Play rechts', 'sensor.hue_play_rechts_power'],
    ['TV kast', 'sensor.tv_kast_power'], ['Woonkamer', 'sensor.woonkamer_power'], ['Soundbar', 'sensor.soundbar_power_meter'],
    ['Gamehoek', 'sensor.energysocket_5c2faf0d8364_active_power'], ['Koelkast (lamp)', 'sensor.koelkast_power'],
    ['Buiten deur (lamp)', 'sensor.buiten_deur_power']] },
  { name: 'Keuken', icon: 'mdi:silverware-fork-knife', color: '#4fd1c5', devices: [
    ['Keuken', 'sensor.keuken_power'], ['Bureau', 'sensor.plug_bureau_device_power'], ['Nest Mini', 'sensor.nestmini9818_power']] },
  { name: 'Slaapkamer', icon: 'mdi:bed-outline', color: '#f6ad55', devices: [
    ['Plafond', 'sensor.plafond_slaapkamer_power'], ['Nachtkastje', 'sensor.nachtkastje_power']] },
  { name: 'Badkamer', icon: 'mdi:shower-head', color: '#38bdf8', devices: [
    ['Plafond', 'sensor.plafond_badkamer_power'], ['Wasmachine', 'sensor.wasmachine_vermogen']] },
  { name: 'Gang en berging', icon: 'mdi:door-open', color: '#f472b6', devices: [
    ['Hal', 'sensor.hal_power'], ['Hal (2)', 'sensor.hal_device_power'], ['Berging', 'sensor.berging_power'],
    ['Sigaret', 'sensor.sigaret_device_power'], ['Ventilatie', 'sensor.energysocket_5c2faf0c7564_active_power'],
    ['Warmtepomp', 'sensor.energysocket_5c2faf02a546_active_power']] },
];

const fmtW = (w) => (w === null ? '–' : Math.abs(w) >= 1000 ? `${(w / 1000).toFixed(2).replace('.', ',')} kW` : `${Math.round(w)} W`);
const fmtEur = (v, d = 2) => (v === null ? '–' : `€ ${v.toFixed(d).replace('.', ',')}`);
/** Groot getal met kleine eenheid: 2100 wordt `2,10<small>kW</small>`. */
const big = (w) => {
  if (w === null) return '–';
  return Math.abs(w) >= 1000
    ? `${(w / 1000).toFixed(2).replace('.', ',')}<small>kW</small>`
    : `${Math.round(w)}<small>W</small>`;
};

class HuisEnergiestroom extends HTMLElement {
  setConfig() { this._config = {}; }
  getCardSize() { return 12; }
  getGridOptions() { return { columns: 'full' }; }

  set hass(hass) {
    this._hass = hass;
    if (!this._root) this._build();
    this._cards?.forEach((c) => { c.hass = hass; });
    this._update();
    if (!this._statsAt || Date.now() - this._statsAt > 300000) this._loadStats();
  }

  disconnectedCallback() { this._resize?.disconnect(); this._resize = null; }
  connectedCallback() {
    if (this._root && !this._resize) { this._resize = new ResizeObserver(() => this._drawLines()); this._resize.observe(this._el('wrap')); }
  }

  _num(id) {
    const s = this._hass.states[id];
    if (!s || s.state === 'unavailable' || s.state === 'unknown') return null;
    const v = parseFloat(s.state);
    return Number.isFinite(v) ? v : null;
  }

  _go(path) {
    history.pushState(null, '', path);
    window.dispatchEvent(new CustomEvent('location-changed'));
  }

  _build() {
    this._root = this.attachShadow({ mode: 'open' });
    this._root.innerHTML = `
      <style>
        :host { display:block; color:var(--primary-text-color); }
        * { box-sizing:border-box; }
        .wrap { position:relative; display:flex; flex-direction:column; gap:22px; padding:22px; border-radius:24px;
                background:radial-gradient(120% 90% at 50% 35%, rgba(30,41,82,.55), rgba(8,12,28,.55)); border:1px solid rgba(255,255,255,.08); }
        svg.lijnen { position:absolute; inset:0; width:100%; height:100%; pointer-events:none; overflow:visible; }
        svg.lijnen path { fill:none; stroke-linecap:round; stroke-width:3; stroke-dasharray:0.1 10; opacity:.22; }
        svg.lijnen path.aan { opacity:.95; animation:stroom var(--d,2.2s) linear infinite; }
        svg.lijnen path.terug { animation-direction:reverse; }
        @keyframes stroom { from { stroke-dashoffset:0; } to { stroke-dashoffset:-20.2; } }
        .kop { display:flex; align-items:center; justify-content:space-between; gap:10px; flex-wrap:wrap; position:relative; }
        .kop h2 { margin:0; font-size:19px; font-weight:600; display:flex; align-items:center; gap:10px; }
        .kop h2 i { width:8px; height:8px; border-radius:50%; background:#34d399; box-shadow:0 0 8px #34d399; }
        .kop h2 span { font-size:14px; font-weight:400; opacity:.65; }
        .pillen { display:flex; gap:8px; flex-wrap:wrap; }
        .pil { padding:6px 14px; border-radius:999px; font-size:13px; background:rgba(255,255,255,.05); border:1px solid rgba(255,255,255,.14); opacity:.9; }
        .pil b { font-weight:700; }
        .top { display:grid; grid-template-columns:minmax(220px,360px) 300px minmax(220px,360px); justify-content:space-between; gap:0 40px; align-items:center; position:relative; }
        .links { display:flex; flex-direction:column; gap:22px; }
        .kaart { border-radius:18px; padding:16px 18px; min-width:0; position:relative;
                 background:linear-gradient(160deg, color-mix(in srgb, var(--c) 10%, rgba(10,14,32,.85)), rgba(10,14,32,.85));
                 border:1.5px solid color-mix(in srgb, var(--c) 55%, transparent); }
        .kaart.klik { cursor:pointer; pointer-events:auto; }
        .kaart .kk { display:flex; align-items:center; gap:12px; margin-bottom:10px; }
        .kaart .ic { width:38px; height:38px; border-radius:10px; display:flex; align-items:center; justify-content:center; flex:none;
                     background:color-mix(in srgb, var(--c) 20%, transparent); color:var(--c); }
        .kaart .lb { font-size:12.5px; letter-spacing:.14em; text-transform:uppercase; font-weight:600; opacity:.7; }
        .kaart .gr { font-size:40px; font-weight:700; line-height:1.05; color:var(--c); }
        .kaart .gr small { font-size:18px; font-weight:500; opacity:.8; margin-left:5px; }
        .chip { display:inline-block; margin-top:10px; padding:3px 12px; border-radius:8px; font-size:11.5px; letter-spacing:.1em; font-weight:700;
                text-transform:uppercase; color:var(--c); background:color-mix(in srgb, var(--c) 18%, transparent); }
        .reg { font-size:13px; opacity:.78; margin-top:10px; }
        .reg b { font-weight:700; }
        .balk { height:7px; border-radius:4px; background:rgba(255,255,255,.12); margin:14px 0 6px; position:relative; }
        .balk i { position:absolute; top:-3px; width:3px; height:13px; border-radius:2px; background:var(--c); box-shadow:0 0 6px var(--c); transform:translateX(-50%); }
        .balk-l { display:flex; justify-content:space-between; font-size:12px; opacity:.7; }
        .ringkolom { position:relative; width:300px; height:300px; margin:0 auto; }
        .legenda { position:absolute; left:0; right:0; top:-26px; text-align:center; font-size:12.5px; opacity:.85; white-space:nowrap; }
        .legenda span { margin:0 6px; }
        .ringkolom svg { width:100%; height:100%; }
        .mid { position:absolute; inset:0; display:flex; flex-direction:column; align-items:center; justify-content:center; text-align:center; pointer-events:none; }
        .mid .v { font-size:46px; font-weight:700; line-height:1.05; color:${COLOR_HOME}; }
        .mid .v small { font-size:20px; font-weight:500; margin-left:4px; opacity:.85; }
        .mid .l { font-size:12.5px; letter-spacing:.16em; margin-top:6px; opacity:.8; }
        .mid ha-icon { --mdc-icon-size:30px; color:${COLOR_HOME}; margin-bottom:6px; }
        .kamers { display:grid; grid-template-columns:repeat(auto-fit,minmax(190px,1fr)); gap:16px; position:relative; }
        .kamer { padding:14px 14px 12px; }
        .kamer .kk { margin-bottom:6px; }
        .kamer .gr { font-size:30px; margin-bottom:8px; }
        .kamer .gr small { font-size:15px; }
        .kamer .reg { margin:0; padding:4px 0; display:flex; align-items:center; gap:8px; font-size:12.5px; opacity:.9; }
        .kamer .reg i { width:7px; height:7px; border-radius:50%; background:var(--c); flex:none; }
        .kamer .reg.nul { opacity:.4; } .kamer .reg.nul i { background:#64748b; }
        .kamer .reg span { flex:1; min-width:0; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
        .kamer .reg b { font-variant-numeric:tabular-nums; }
        .tegels { display:grid; grid-template-columns:repeat(auto-fit,minmax(135px,1fr)); gap:12px; position:relative; border-top:1px solid rgba(255,255,255,.08); padding-top:18px; }
        .tegel { padding:11px 14px; border-radius:12px; background:rgba(255,255,255,.04); border:1px solid rgba(255,255,255,.07); cursor:pointer; }
        .tegel .t { font-size:11px; letter-spacing:.14em; text-transform:uppercase; opacity:.7; display:flex; align-items:center; gap:7px; }
        .tegel .t i { width:7px; height:7px; border-radius:50%; background:var(--c); }
        .tegel .w { font-size:22px; font-weight:700; margin-top:4px; }
        .tegel .w small { font-size:12px; font-weight:400; opacity:.65; margin-left:3px; }
        .kamers { margin-top:38px; }
        .extra { display:grid; grid-template-columns:repeat(auto-fit,minmax(300px,1fr)); gap:16px; align-items:start; position:relative; }
        .extra > * { min-width:0; }
        @media (max-width:1100px) {
          .kamers { margin-top:0; }
          .top { grid-template-columns:1fr; gap:22px; }
          .ringkolom { order:-1; margin-top:22px; }
          svg.lijnen { display:none; }
          .wrap { padding:14px; }
        }
        @media (prefers-reduced-motion: reduce) { svg.lijnen path.aan { animation:none; } }
      </style>
      <div class="wrap" id="wrap">
        <svg class="lijnen" id="lijnen"></svg>
        <div class="kop"><h2><i></i>Energiestroom <span id="klok"></span></h2><div class="pillen" id="pillen"></div></div>
        <div class="top">
          <div class="links"><div class="kaart" id="zon"></div><div class="kaart" id="net"></div></div>
          <div class="ringkolom" id="ringkolom">
            <div class="legenda" id="legenda"></div>
            <svg viewBox="0 0 300 300" id="ring"></svg>
            <div class="mid" id="mid"></div>
          </div>
          <div class="kaart klik" id="prijs"></div>
        </div>
        <div class="kamers" id="kamers"></div>
        <div class="tegels" id="tegels"></div>
        <div class="extra" id="extra"></div>
      </div>`;
    this._el = (id) => this._root.getElementById(id);
    this._el('prijs').addEventListener('click', () => this._go('/dashboard-huis/energie-prijs'));
    this._el('tegels').addEventListener('click', (e) => {
      const t = e.target.closest('[data-pad]');
      if (t) this._go(t.dataset.pad);
    });
    this._resize = new ResizeObserver(() => this._drawLines());
    this._resize.observe(this._el('wrap'));
    this._embed();
  }

  /** Het verloop van 24 uur, het wasadvies en thuiswerken: bestaande kaarten, hier in het overzicht gezet. */
  async _embed() {
    try {
      const helpers = await window.loadCardHelpers();
      const configs = [
        { type: 'custom:mini-graph-card', name: 'Laatste 24 uur', icon: 'mdi:chart-areaspline', hours_to_show: 24, points_per_hour: 4,
          line_width: 2, smoothing: true, show: { fill: 'fade', labels: false, extrema: true },
          color_thresholds: [{ value: 0, color: '#4ade80' }, { value: 1500, color: '#ffb060' }, { value: 2500, color: '#ff6b6b' }],
          entities: [{ entity: GRID, name: 'Verbruik' }] },
        { type: 'custom:button-card', template: 'wash_advice' },
        { type: 'custom:button-card', template: 'homework_tile' },
      ];
      this._cards = configs.map((c) => { const el = helpers.createCardElement(c); el.hass = this._hass; this._el('extra').appendChild(el); return el; });
    } catch (e) {
      console.warn('huis-energiestroom: kaarten niet geladen', e);
    }
  }

  _update() {
    const grid = this._num(GRID);
    const zonRaw = this._num(SOLAR);
    let zon = zonRaw === null ? 0 : Math.abs(zonRaw);
    if (zon < SOLAR_NOISE) zon = 0;
    const huis = grid === null ? null : Math.max(0, grid + zon);
    const uitvoer = grid !== null && grid < 0 ? -grid : 0;
    const afname = grid !== null && grid > 0 ? grid : 0;

    // Aandeel van het verbruik dat uit eigen zon komt, en van de opwek dat zelf gebruikt wordt
    const zonDeel = huis && huis > 0 ? Math.min(1, zon / huis) : 0;
    const eigen = zon > 0 ? Math.min(1, Math.max(0, (zon - uitvoer) / zon)) : 0;
    this._geo = { zon, afname, uitvoer, huis, zonDeel };

    const nu = new Date();
    this._el('klok').textContent = `${String(nu.getHours()).padStart(2, '0')}:${String(nu.getMinutes()).padStart(2, '0')}`;
    this._el('pillen').innerHTML =
      `<span class="pil" title="Welk deel van het verbruik nu uit je eigen zonnepanelen komt">Eigen stroom nu <b>${Math.round(zonDeel * 100)} %</b></span>` +
      `<span class="pil" title="Welk deel van de opwek je zelf gebruikt in plaats van terug te leveren">Zon zelf gebruikt <b>${zon > 0 ? Math.round(eigen * 100) + ' %' : '0 %'}</b></span>`;

    // Zon en net
    this._el('zon').style.setProperty('--c', COLOR_SOLAR);
    this._el('zon').innerHTML =
      `<div class="kk"><span class="ic"><ha-icon icon="mdi:solar-power-variant"></ha-icon></span><span class="lb">Zonnepanelen</span></div>` +
      `<div class="gr">${big(zonRaw === null ? null : zon)}</div>` +
      `<span class="chip">${zon > 0 ? 'Opwek' : 'Geen opwek'}</span>` +
      (zon > 0 ? `<div class="reg">→ ${uitvoer > 0 ? `Huis <b>${fmtW(zon - uitvoer)}</b> · Net <b>${fmtW(uitvoer)}</b>` : `Huis <b>${fmtW(zon)}</b>`}</div>` : '');
    const netKleur = grid !== null && grid < 0 ? '#34d399' : COLOR_NET;
    this._el('net').style.setProperty('--c', netKleur);
    this._el('net').innerHTML =
      `<div class="kk"><span class="ic"><ha-icon icon="mdi:transmission-tower"></ha-icon></span><span class="lb">Net</span></div>` +
      `<div class="gr">${big(grid === null ? null : Math.abs(grid))}</div>` +
      `<span class="chip">${grid === null ? 'Onbekend' : grid < 0 ? 'Teruglevering' : 'Afname'}</span>`;

    // Prijs
    const prijs = this._num(PRICE_NOW), gem = this._num(PRICE_AVG), laag = this._num(PRICE_LOW), hoog = this._num(PRICE_HIGH);
    const kostNu = this._num(COST_NOW);
    const oordeel = prijs === null || gem === null ? '' : prijs < gem * 0.95 ? 'Goedkoop' : prijs > gem * 1.05 ? 'Duur' : 'Normaal';
    const prijsKleur = oordeel === 'Goedkoop' ? '#34d399' : oordeel === 'Duur' ? COLOR_NET : COLOR_PRICE;
    const pos = prijs !== null && laag !== null && hoog !== null && hoog > laag ? Math.min(100, Math.max(0, ((prijs - laag) / (hoog - laag)) * 100)) : null;
    this._el('prijs').style.setProperty('--c', prijsKleur);
    this._el('prijs').innerHTML =
      `<div class="kk"><span class="ic"><ha-icon icon="mdi:currency-eur"></ha-icon></span><span class="lb">Stroomprijs</span></div>` +
      `<div class="gr">${prijs === null ? '–' : fmtEur(prijs)}<small>/kWh</small></div>` +
      (oordeel ? `<span class="chip">${oordeel}</span>` : '') +
      (pos !== null ? `<div class="balk"><i style="left:${pos}%"></i></div><div class="balk-l"><span>Laagste ${fmtEur(laag)}</span><span>Gem. ${fmtEur(gem)}</span><span>Hoogste ${fmtEur(hoog)}</span></div>` : '') +
      (kostNu !== null ? `<div class="reg">Kost nu <b>${fmtEur(kostNu)}</b> per uur</div>` : '');

    // Ring: aandeel zon en net van het verbruik, met kleine tussenruimtes
    const cx = 150, cy = 150, R = 124, C = 2 * Math.PI * R;
    const delen = huis ? [[zonDeel, COLOR_SOLAR], [1 - zonDeel, COLOR_NET]].filter(([d]) => d > 0.004) : [];
    let start = 0;
    const gap = delen.length > 1 ? 10 : 0;
    this._el('ring').innerHTML =
      `<circle cx="${cx}" cy="${cy}" r="${R + 17}" fill="none" stroke="rgba(255,255,255,.12)" stroke-width="1" stroke-dasharray="1 5"/>` +
      `<circle cx="${cx}" cy="${cy}" r="${R - 20}" fill="rgba(6,10,28,.75)" stroke="rgba(255,255,255,.1)" stroke-width="1.5"/>` +
      `<circle cx="${cx}" cy="${cy}" r="${R}" fill="none" stroke="rgba(255,255,255,.07)" stroke-width="13"/>` +
      delen.map(([d, kleur]) => {
        const len = Math.max(1, d * C - gap);
        const el = `<circle cx="${cx}" cy="${cy}" r="${R}" fill="none" stroke="${kleur}" stroke-width="13" stroke-linecap="round"
          stroke-dasharray="${len} ${C}" stroke-dashoffset="${-(start + gap / 2)}" transform="rotate(-90 ${cx} ${cy})"/>`;
        start += d * C;
        return el;
      }).join('');
    this._el('legenda').innerHTML =
      `<span><b style="color:${COLOR_SOLAR}">●</b> Zon ${Math.round(zonDeel * 100)} %</span><span><b style="color:${COLOR_NET}">●</b> Net ${Math.round((1 - zonDeel) * 100)} %</span>`;
    this._el('mid').innerHTML =
      `<ha-icon icon="mdi:home-lightning-bolt-outline"></ha-icon><div class="v">${big(huis)}</div><div class="l">HUISVERBRUIK</div>`;

    // Kamers
    this._rooms = ROOMS.map((room) => {
      const items = room.devices
        .map(([n, id]) => [n, this._num(id)])
        .filter(([, v]) => v !== null)
        .sort((a, b) => b[1] - a[1]);
      return { room, items, totaal: items.reduce((a, [, v]) => a + v, 0) };
    });
    this._el('kamers').innerHTML = this._rooms.map(({ room, items, totaal }) => {
      const regels = items.slice(0, 4).map(([n, v]) =>
        `<div class="reg${v < 0.5 ? ' nul' : ''}"><i></i><span>${n}</span><b>${v < 10 ? v.toFixed(v < 0.5 ? 0 : 1).replace('.', ',') + ' W' : fmtW(v)}</b></div>`).join('');
      return `<div class="kaart kamer" style="--c:${room.color}">
        <div class="kk"><span class="ic"><ha-icon icon="${room.icon}"></ha-icon></span><span class="lb">${room.name}</span></div>
        <div class="gr">${big(totaal)}</div>${regels || '<div class="reg nul">Geen metingen</div>'}</div>`;
    }).join('');

    this._renderTiles();
    this._drawLines();
  }

  /** Stippellijnen: zon en net naar de ring, van de ring naar elke kamer. Het element blijft staan zodat de animatie doorloopt. */
  _drawLines() {
    const svg = this._el?.('lijnen');
    if (!svg || !this._geo || !this._rooms) return;
    const wrap = this._el('wrap').getBoundingClientRect();
    if (!wrap.width || getComputedStyle(svg).display === 'none') return;
    const rel = (el) => {
      const r = el.getBoundingClientRect();
      return { l: r.left - wrap.left, r: r.right - wrap.left, t: r.top - wrap.top, b: r.bottom - wrap.top,
               cx: (r.left + r.right) / 2 - wrap.left, cy: (r.top + r.bottom) / 2 - wrap.top };
    };
    const ring = rel(this._el('ringkolom'));
    const rr = 150 * (ring.r - ring.l) / 300; // straal waar de lijnen de ring raken
    const at = (deg) => [ring.cx + Math.cos(deg * Math.PI / 180) * rr * 0.97, ring.cy + Math.sin(deg * Math.PI / 180) * rr * 0.97];
    const { zon, afname, uitvoer } = this._geo;
    const defs = [];

    const zonR = rel(this._el('zon')), netR = rel(this._el('net'));
    defs.push({ k: 'zon', a: [zonR.r, zonR.cy], b: at(205), h: 'h', kleur: COLOR_SOLAR, w: zon, terug: false });
    defs.push({ k: 'net', a: [netR.r, netR.cy], b: at(155), h: 'h', kleur: uitvoer > 0 ? '#34d399' : COLOR_NET, w: Math.max(afname, uitvoer), terug: uitvoer > 0 });

    const n = this._rooms.length;
    this._rooms.forEach(({ room, totaal }, i) => {
      const card = this._el('kamers').children[i];
      if (!card) return;
      const kr = rel(card);
      const deg = n === 1 ? 90 : 48 + (84 * i) / (n - 1);
      defs.push({ k: `r${i}`, a: at(deg), b: [kr.cx, kr.t], h: 'v', kleur: room.color, w: totaal, terug: false });
    });

    for (const d of defs) {
      let p = svg.querySelector(`path[data-k="${d.k}"]`);
      if (!p) { p = document.createElementNS('http://www.w3.org/2000/svg', 'path'); p.dataset.k = d.k; svg.appendChild(p); }
      const [x1, y1] = d.a, [x2, y2] = d.b;
      const c = d.h === 'h'
        ? [(x2 - x1) * 0.55, 0, -(x2 - x1) * 0.15, (y2 - y1) * 0.1]
        : [0, (y2 - y1) * 0.5, 0, -(y2 - y1) * 0.5];
      p.setAttribute('d', `M${x1.toFixed(1)} ${y1.toFixed(1)} C${(x1 + c[0]).toFixed(1)} ${(y1 + c[1]).toFixed(1)} ${(x2 + c[2]).toFixed(1)} ${(y2 + c[3]).toFixed(1)} ${x2.toFixed(1)} ${y2.toFixed(1)}`);
      p.setAttribute('stroke', d.kleur);
      p.classList.toggle('aan', d.w >= 1);
      p.classList.toggle('terug', d.terug);
      p.style.setProperty('--d', `${Math.max(0.9, 3.4 - d.w / 1200).toFixed(2)}s`);
    }
  }

  _renderTiles() {
    const s = this._stats || {};
    const imp = s.imp ?? null, exp = s.exp ?? null, pv = s.pv ?? null;
    const huis = imp !== null ? imp + (pv ?? 0) - (exp ?? 0) : null;
    const eigen = huis && huis > 0 && imp !== null ? Math.max(0, Math.min(1, 1 - imp / huis)) : null;
    const standby = this._num(STANDBY);
    const t = (kleur, label, val, pad) =>
      `<div class="tegel" style="--c:${kleur}" data-pad="${pad}"><div class="t"><i></i>${label}</div><div class="w">${val}</div></div>`;
    const kwh = (v) => (v === null ? '–' : `${v.toFixed(1).replace('.', ',')}<small>kWh</small>`);
    this._el('tegels').innerHTML =
      t(COLOR_SOLAR, 'Zon vandaag', kwh(pv), '/dashboard-huis/energie-verbruik') +
      t(COLOR_NET, 'Van het net', kwh(imp), '/dashboard-huis/energie-verbruik') +
      t('#34d399', 'Teruggeleverd', kwh(exp), '/dashboard-huis/energie-verbruik') +
      t(COLOR_HOME, 'Verbruik huis', kwh(huis), '/dashboard-huis/energie-verbruik') +
      t('#8b8cff', 'Eigen stroom', eigen === null ? '–' : `${Math.round(eigen * 100)}<small>%</small>`, '/dashboard-huis/energie-verbruik') +
      t(COLOR_PRICE, 'Kosten vandaag', fmtEur(this._num(COST_DAY)), '/dashboard-huis/energie-kosten') +
      t(COLOR_PRICE, 'Kosten maand', fmtEur(this._num(COST_MONTH)), '/dashboard-huis/energie-kosten') +
      t('#94a3b8', 'Sluipverbruik', standby === null ? '–' : `${Math.round(standby)}<small>W</small>`, '/dashboard-huis/energie-verbruik');
  }

  async _loadStats() {
    this._statsAt = Date.now();
    try {
      const start = new Date(); start.setHours(0, 0, 0, 0);
      const r = await this._hass.callWS({
        type: 'recorder/statistics_during_period', start_time: start.toISOString(),
        statistic_ids: [STAT_IMPORT, STAT_EXPORT, STAT_PV], period: 'hour', types: ['change'],
      });
      const sum = (id) => (r[id] ? r[id].reduce((a, x) => a + (x.change || 0), 0) : null);
      this._stats = { imp: sum(STAT_IMPORT), exp: sum(STAT_EXPORT), pv: sum(STAT_PV) };
      this._renderTiles();
    } catch (e) {
      console.warn('huis-energiestroom: statistieken niet beschikbaar', e);
    }
  }
}

if (!customElements.get('huis-energiestroom')) customElements.define('huis-energiestroom', HuisEnergiestroom);
