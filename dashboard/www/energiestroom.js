// Energiestroom: overzicht van het huidige verbruik in de stijl van een energiefluskaart.
// Zon en net links en rechts, in het midden een ring met het verbruik van het huis, daaronder de kamers met hun apparaten
// en onderaan de totalen van vandaag. Gebruik: `type: custom:huis-energiestroom` (geladen vanuit idle-home.js, ?v= ophogen na een wijziging).

const GRID = 'sensor.p1_meter_3c39e72a64e8_active_power';       // + = afnemen van het net, - = terugleveren
const SOLAR = 'sensor.p1_meter_vermogen';                        // opwek van de zonnepanelen (W)
const PRICE_NOW = 'sensor.anwb_electricity_all_in_price_current';
const PRICE_AVG = 'sensor.anwb_electricity_all_in_price_average_today';
const COST_DAY = 'sensor.gang_p1_meter_3c39e72a64e8_p1_meter_3c39e72a64e8_active_daily_energy_cost';
const STAT_IMPORT = 'sensor.p1_meter_3c39e72a64e8_total_power_import';
const STAT_EXPORT = 'sensor.p1_meter_3c39e72a64e8_total_power_export';
const STAT_PV = 'sensor.p1_meter_energie_export';

// Kamers en hun vermogenssensoren: [naam, sensor]
const ROOMS = [
  { name: 'Woonkamer', icon: 'mdi:sofa', color: '#ffb060', devices: [
    ['Plafond', 'sensor.plafond_woonkamer_power'], ['Hue Play links', 'sensor.hue_play_links_power'],
    ['Hue Play midden', 'sensor.hue_play_midden_power'], ['Hue Play rechts', 'sensor.hue_play_rechts_power'],
    ['TV kast', 'sensor.tv_kast_power'], ['Woonkamer', 'sensor.woonkamer_power'], ['Soundbar', 'sensor.soundbar_power_meter'],
    ['Gamehoek', 'sensor.energysocket_5c2faf0d8364_active_power']] },
  { name: 'Keuken', icon: 'mdi:countertop-outline', color: '#4ade80', devices: [
    ['Keuken', 'sensor.keuken_power'], ['Koelkast', 'sensor.koelkast_power'], ['Nest Mini', 'sensor.nestmini9818_power']] },
  { name: 'Slaapkamer', icon: 'mdi:bed-outline', color: '#a78bfa', devices: [
    ['Plafond', 'sensor.plafond_slaapkamer_power'], ['Nachtkastje', 'sensor.nachtkastje_power']] },
  { name: 'Badkamer', icon: 'mdi:shower-head', color: '#60a5fa', devices: [['Plafond', 'sensor.plafond_badkamer_power']] },
  { name: 'Gang en berging', icon: 'mdi:door-open', color: '#f472b6', devices: [
    ['Hal', 'sensor.hal_power'], ['Hal (2)', 'sensor.hal_device_power'], ['Berging', 'sensor.berging_power'],
    ['Sigaret', 'sensor.sigaret_device_power'], ['Ventilatie', 'sensor.energysocket_5c2faf0c7564_active_power']] },
  { name: 'Buiten en techniek', icon: 'mdi:home-lightning-bolt-outline', color: '#facc15', devices: [
    ['Buiten deur', 'sensor.buiten_deur_power'], ['Bureau', 'sensor.plug_bureau_device_power'],
    ['Warmtepomp', 'sensor.energysocket_5c2faf02a546_active_power'], ['Wasmachine', 'sensor.wasmachine_vermogen']] },
];

const fmtW = (w) => (w === null ? '–' : Math.abs(w) >= 1000 ? `${(w / 1000).toFixed(2).replace('.', ',')} kW` : `${Math.round(w)} W`);
const fmtKwh = (v) => (v === null ? '–' : `${v.toFixed(1).replace('.', ',')} kWh`);
const fmtEur = (v, d = 2) => (v === null ? '–' : `€ ${v.toFixed(d).replace('.', ',')}`);

class HuisEnergiestroom extends HTMLElement {
  setConfig() { this._config = {}; }
  getCardSize() { return 10; }
  getGridOptions() { return { columns: 'full' }; }

  set hass(hass) {
    this._hass = hass;
    if (!this._root) this._build();
    this._update();
    if (!this._statsAt || Date.now() - this._statsAt > 300000) this._loadStats();
  }

  _num(id) {
    const s = this._hass.states[id];
    if (!s || s.state === 'unavailable' || s.state === 'unknown') return null;
    const v = parseFloat(s.state);
    return Number.isFinite(v) ? v : null;
  }

  _build() {
    this._root = this.attachShadow({ mode: 'open' });
    this._root.innerHTML = `
      <style>
        :host { display:block; --zon:#ffd166; --net:#60a5fa; --huis:#4ade80; --export:#34d399; color:var(--primary-text-color); }
        * { box-sizing:border-box; }
        .wrap { display:flex; flex-direction:column; gap:14px; padding:4px 0 8px; }
        .kop { display:flex; align-items:center; justify-content:space-between; gap:10px; flex-wrap:wrap; }
        .kop h2 { margin:0; font-size:20px; font-weight:600; }
        .pillen { display:flex; gap:8px; flex-wrap:wrap; }
        .pil { padding:5px 12px; border-radius:999px; font-size:13px; background:rgba(255,255,255,.08); border:1px solid rgba(255,255,255,.12); }
        .pil b { font-weight:600; }
        .top { display:grid; grid-template-columns:minmax(0,1fr) 110px 230px 110px minmax(0,1fr); align-items:stretch; }
        .kaart { border-radius:18px; padding:14px 16px; background:rgba(255,255,255,.07); border:1px solid rgba(255,255,255,.12);
                 backdrop-filter:none; min-width:0; }
        .kaart .k { font-size:12px; opacity:.7; text-transform:uppercase; letter-spacing:.06em; display:flex; gap:6px; align-items:center; }
        .kaart .w { font-size:28px; font-weight:600; margin:4px 0 2px; }
        .kaart .s { font-size:12px; opacity:.65; }
        .kaart ha-icon { --mdc-icon-size:18px; }
        .lijn { height:2px; position:relative; margin:auto 4px; background:rgba(255,255,255,.14); border-radius:2px; overflow:hidden; }
        .lijn i { position:absolute; top:-2px; width:6px; height:6px; border-radius:50%; background:var(--c, #fff); opacity:0; left:0; }
        .lijn.actief i { opacity:1; animation:stroom var(--d,2s) linear infinite; animation-delay:var(--o,0s); }
        .lijn.terug.actief i { animation-direction:reverse; }
        @keyframes stroom { from { left:-6px; } to { left:100%; } }
        .ring { align-self:center; position:relative; width:220px; height:220px; margin:0 auto; }
        .ring svg { width:100%; height:100%; transform:rotate(-90deg); }
        .ring .mid { position:absolute; inset:0; display:flex; flex-direction:column; align-items:center; justify-content:center; text-align:center; }
        .ring .mid .g { font-size:12px; opacity:.7; }
        .ring .mid .v { font-size:30px; font-weight:600; line-height:1.1; }
        .ring .mid .l { font-size:11px; opacity:.65; margin-top:4px; }
        #zon { align-self:center; }
        .rechts { display:flex; flex-direction:column; gap:10px; }
        .kamers { display:grid; grid-template-columns:repeat(auto-fill,minmax(210px,1fr)); gap:12px; }
        .kamer { border-left:4px solid var(--c); }
        .kamer .kop2 { display:flex; justify-content:space-between; align-items:center; font-weight:600; }
        .kamer .kop2 span:first-child { display:flex; gap:6px; align-items:center; }
        .kamer ul { list-style:none; margin:8px 0 0; padding:0; font-size:12.5px; }
        .kamer li { display:flex; justify-content:space-between; padding:2px 0; opacity:.85; }
        .tegels { display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr)); gap:12px; }
        .tegel .w { font-size:22px; }
        .tegel.zon .w { color:var(--zon); } .tegel.net .w { color:var(--net); } .tegel.exp .w { color:var(--export); }
        @media (max-width:900px) {
          .top { grid-template-columns:1fr; gap:12px; }
          .lijn { display:none; }
          .rechts { flex-direction:row; } .rechts .kaart { flex:1; }
        }
        @media (prefers-reduced-motion: reduce) { .lijn.actief i { animation:none; left:50%; } }
      </style>
      <div class="wrap">
        <div class="kop"><h2>Energiestroom</h2><div class="pillen" id="pillen"></div></div>
        <div class="top">
          <div class="kaart" id="zon"></div>
          <div class="lijn" id="l1"><i></i><i style="--o:.7s"></i><i style="--o:1.4s"></i></div>
          <div class="ring"><svg viewBox="0 0 120 120" id="ring"></svg><div class="mid" id="mid"></div></div>
          <div class="lijn" id="l2"><i></i><i style="--o:.7s"></i><i style="--o:1.4s"></i></div>
          <div class="rechts"><div class="kaart" id="net"></div><div class="kaart" id="prijs"></div></div>
        </div>
        <div class="kamers" id="kamers"></div>
        <div class="tegels" id="tegels"></div>
      </div>`;
    this._el = (id) => this._root.getElementById(id);
  }

  _update() {
    const grid = this._num(GRID);
    const zonRaw = this._num(SOLAR);
    const zon = zonRaw === null ? 0 : Math.abs(zonRaw);
    const huis = grid === null ? null : Math.max(0, grid + zon);
    const uitvoer = grid !== null && grid < 0 ? -grid : 0;
    const afname = grid !== null && grid > 0 ? grid : 0;

    // Aandeel van het verbruik dat uit eigen zon komt
    const zonDeel = huis && huis > 0 ? Math.min(1, (huis - afname) / huis) : 0;
    const eigen = zon > 0 ? Math.min(1, Math.max(0, (zon - uitvoer) / zon)) : 0;
    this._el('pillen').innerHTML =
      `<span class="pil">Autarkie nu <b>${Math.round(zonDeel * 100)} %</b></span>` +
      `<span class="pil">Eigen verbruik nu <b>${zon > 0 ? Math.round(eigen * 100) + ' %' : '–'}</b></span>`;

    this._el('zon').innerHTML =
      `<div class="k"><ha-icon icon="mdi:solar-power-variant" style="color:var(--zon)"></ha-icon>Zon</div>` +
      `<div class="w">${fmtW(zonRaw === null ? null : zon)}</div><div class="s">${zon > 0 ? 'Opwek nu' : 'Geen opwek'}</div>`;
    this._el('net').innerHTML =
      `<div class="k"><ha-icon icon="mdi:transmission-tower" style="color:var(--net)"></ha-icon>Net</div>` +
      `<div class="w">${fmtW(grid)}</div><div class="s">${grid === null ? 'Onbekend' : grid < 0 ? 'Teruglevering' : 'Afname'}</div>`;

    const prijs = this._num(PRICE_NOW), gem = this._num(PRICE_AVG), kosten = this._num(COST_DAY);
    const stijgt = prijs !== null && gem !== null ? (prijs > gem ? 'duurder dan gemiddeld' : 'goedkoper dan gemiddeld') : '';
    this._el('prijs').innerHTML =
      `<div class="k"><ha-icon icon="mdi:currency-eur"></ha-icon>Prijs nu</div>` +
      `<div class="w">${prijs === null ? '–' : fmtEur(prijs, 3) + ' /kWh'}</div>` +
      `<div class="s">${stijgt}${kosten !== null ? (stijgt ? ' · ' : '') + 'vandaag ' + fmtEur(kosten) : ''}</div>`;

    const dots = (id, w, kleur, terug) => {
      const el = this._el(id);
      el.classList.toggle('actief', w > 5);
      el.classList.toggle('terug', !!terug);
      el.style.setProperty('--c', kleur);
      el.style.setProperty('--d', `${Math.max(0.8, 3.2 - w / 1500)}s`);
    };
    dots('l1', zon, '#ffd166', false);
    dots('l2', Math.abs(grid || 0), grid !== null && grid < 0 ? '#34d399' : '#60a5fa', grid !== null && grid < 0);

    // Ring: aandeel zon (geel) en net (blauw) van het verbruik
    const R = 50, C = 2 * Math.PI * R;
    const dz = zonDeel * C, dn = (1 - zonDeel) * C;
    this._el('ring').innerHTML =
      `<circle cx="60" cy="60" r="${R}" fill="none" stroke="rgba(255,255,255,.1)" stroke-width="9"/>` +
      (huis ? `<circle cx="60" cy="60" r="${R}" fill="none" stroke="#60a5fa" stroke-width="9" stroke-dasharray="${dn} ${C}" stroke-dashoffset="${-dz}" stroke-linecap="butt"/>` +
              `<circle cx="60" cy="60" r="${R}" fill="none" stroke="#ffd166" stroke-width="9" stroke-dasharray="${dz} ${C}" stroke-linecap="butt"/>` : '');
    this._el('mid').innerHTML =
      `<ha-icon icon="mdi:home-lightning-bolt-outline" style="--mdc-icon-size:26px"></ha-icon>` +
      `<div class="g">Verbruik huis</div><div class="v">${fmtW(huis)}</div>` +
      `<div class="l"><span style="color:#ffd166">●</span> zon ${Math.round(zonDeel * 100)} % &nbsp;<span style="color:#60a5fa">●</span> net ${Math.round((1 - zonDeel) * 100)} %</div>`;

    // Kamers
    this._el('kamers').innerHTML = ROOMS.map((room) => {
      const items = room.devices
        .map(([n, id]) => [n, this._num(id)])
        .filter(([, v]) => v !== null)
        .sort((a, b) => b[1] - a[1]);
      const totaal = items.reduce((a, [, v]) => a + v, 0);
      const top = items.slice(0, 4);
      return `<div class="kaart kamer" style="--c:${room.color}">
        <div class="kop2"><span><ha-icon icon="${room.icon}" style="color:${room.color}"></ha-icon>${room.name}</span><span>${fmtW(totaal)}</span></div>
        <ul>${top.map(([n, v]) => `<li><span>${n}</span><span>${v < 10 ? v.toFixed(1).replace('.', ',') + ' W' : fmtW(v)}</span></li>`).join('') || '<li>Geen metingen</li>'}</ul>
      </div>`;
    }).join('');

    this._renderTiles();
  }

  _renderTiles() {
    const s = this._stats || {};
    const imp = s.imp ?? null, exp = s.exp ?? null, pv = s.pv ?? null;
    const huis = imp !== null ? imp + (pv ?? 0) - (exp ?? 0) : null;
    const eigenPv = pv !== null && exp !== null ? Math.max(0, pv - exp) : null;
    const autarkie = huis && huis > 0 && imp !== null ? Math.max(0, Math.min(1, 1 - imp / huis)) : null;
    const kosten = this._num(COST_DAY);
    const t = (cls, label, val, sub) => `<div class="kaart tegel ${cls}"><div class="k">${label}</div><div class="w">${val}</div><div class="s">${sub}</div></div>`;
    this._el('tegels').innerHTML =
      t('zon', 'Zon vandaag', fmtKwh(pv), eigenPv !== null ? `${fmtKwh(eigenPv)} zelf gebruikt` : '') +
      t('net', 'Van het net', fmtKwh(imp), 'afgenomen vandaag') +
      t('exp', 'Teruggeleverd', fmtKwh(exp), 'vandaag') +
      t('', 'Verbruik huis', fmtKwh(huis), autarkie !== null ? `autarkie ${Math.round(autarkie * 100)} %` : '') +
      t('', 'Kosten vandaag', fmtEur(kosten), 'tot nu toe');
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
