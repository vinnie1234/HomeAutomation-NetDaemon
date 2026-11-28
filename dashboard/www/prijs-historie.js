// De stroomprijs in een grafiek, met de geschiedenis erachter: per dag, week of maand, en terug in de tijd.
//
// Het element <huis-prijs-historie> haalt de uurprijzen (langetermijnstatistieken van de prijssensor) zelf op en tekent ze als lijn.
//   entity    de sensor met de prijs (standaard sensor.anwb_electricity_all_in_price_current)
//   period    dag, week of maand (standaard week)
//   compact   alleen de grafiek van de laatste 7 dagen, zonder knoppen; tik gaat naar de kaart zelf (button-card)
// Volledig: Dag | Week | Maand, vorige en volgende periode, "Nu", aanwijzen of aantikken toont de prijs op dat uur, en daaronder het
// gemiddelde, de laagste en de hoogste prijs van de periode met de verandering ten opzichte van de vorige.
//
// Gebruik (zie de template price_history in templates/button_card_templates.yaml):
//   import('/local/huis/prijs-historie.js?v=1'); en dan <huis-prijs-historie entity="sensor.…" period="week"></huis-prijs-historie>
// ?v= ophogen na een wijziging van dit bestand.

const BLUE = '#5b9dff';
const GREEN = '#4ade80';
const RED = '#ff6b6b';
const PERIODS = [['dag', 'Dag'], ['week', 'Week'], ['maand', 'Maand']];
const DEFAULT_ENTITY = 'sensor.anwb_electricity_all_in_price_current';
const HOUR = 3600000;
// Tussen twee uurwaarden mag hooguit dit zitten, anders is er een gat in de lijn
const GAP = 100 * 60000;
const REFRESH = 5 * 60000;

const startOfDay = date => { const d = new Date(date); d.setHours(0, 0, 0, 0); return d; };
const addDays = (date, days) => { const d = new Date(date); d.setDate(d.getDate() + days); return d; };
const cap = text => text.charAt(0).toUpperCase() + text.slice(1);
const euro = value => value.toLocaleString('nl-NL', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const esc = text => String(text ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const toMs = value => typeof value === 'number' ? (value > 1e11 ? value : value * 1000) : Date.parse(value);

/**
 * Het venster van een periode. Dag: de kalenderdag. Week: de laatste 7 dagen tot en met vandaag. Maand: de kalendermaand.
 * offset 0 is nu, 1 is de vorige periode, enzovoort.
 */
export function windowFor(period, offset, now = new Date()) {
  if (period === 'dag') {
    const start = addDays(startOfDay(now), -offset);
    return { start, end: addDays(start, 1) };
  }
  if (period === 'week') {
    const end = addDays(startOfDay(now), 1 - 7 * offset);
    return { start: addDays(end, -7), end };
  }
  const start = new Date(now.getFullYear(), now.getMonth() - offset, 1);
  return { start, end: new Date(start.getFullYear(), start.getMonth() + 1, 1) };
}

/** De titel van het venster, bijvoorbeeld "Vandaag · donderdag 26 november" of "21 nov – 27 nov 2026". */
export function windowTitle(period, offset, win) {
  const format = (date, options) => date.toLocaleDateString('nl-NL', options);
  if (period === 'dag') {
    const day = format(win.start, { weekday: 'long', day: 'numeric', month: 'long' });
    return offset === 0 ? `Vandaag · ${day}` : offset === 1 ? `Gisteren · ${day}` : cap(day);
  }
  if (period === 'week') {
    const range = `${format(win.start, { day: 'numeric', month: 'short' })} – ${format(addDays(win.end, -1), { day: 'numeric', month: 'short', year: 'numeric' })}`;
    return offset === 0 ? `Laatste 7 dagen · ${range}` : range;
  }
  return cap(format(win.start, { month: 'long', year: 'numeric' }));
}

/** Een nette schaal voor de verticale as: de stap en de waarden waar een lijn komt. */
export function niceScale(min, max) {
  const span = Math.max(max - min, 0.02);
  const step = [0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2].find(s => span / s <= 6) ?? 5;
  const first = Math.floor((min - span * 0.05) / step + 1e-9) * step;
  const ticks = [];
  for (let v = first; v < max + span * 0.05 + step - 1e-9; v += step) ticks.push(Math.round(v * 1e6) / 1e6);
  return { ticks, min: ticks[0], max: ticks[ticks.length - 1] };
}

/** De plaatsen op de horizontale as: [tijd, tekst, lijn tekenen]. */
export function xTicks(period, win) {
  const ticks = [];
  if (period === 'dag') {
    for (let h = 0; h <= 24; h += 3) {
      const t = new Date(win.start.getFullYear(), win.start.getMonth(), win.start.getDate(), h).getTime();
      ticks.push({ t, text: h === 24 ? '' : `${String(h).padStart(2, '0')}:00`, line: true, anchor: 'middle' });
    }
  } else if (period === 'week') {
    for (let i = 0; i < 7; i++) {
      const day = addDays(win.start, i);
      ticks.push({ t: day.getTime(), text: '', line: true });
      ticks.push({ t: addDays(day, 0).getTime() + 12 * HOUR, text: day.toLocaleDateString('nl-NL', { day: 'numeric', month: 'short' }), line: false, anchor: 'middle' });
    }
  } else {
    const days = Math.round((win.end - win.start) / (24 * HOUR));
    for (let d = 1; d <= days; d += 7) {
      const day = addDays(win.start, d - 1);
      ticks.push({ t: day.getTime(), text: day.toLocaleDateString('nl-NL', { day: 'numeric', month: 'short' }), line: true, anchor: 'start' });
    }
  }
  return ticks;
}

/**
 * Het gemiddelde, de laagste en de hoogste waarde van de punten. Een punt kan een eigen laagste (lo) en hoogste (hi) hebben, zoals een dag
 * met uurprijzen; low en high zijn dan de echte uitersten, met het moment van het punt.
 */
export function summarize(points) {
  if (!points.length) return null;
  const lo = p => p.lo ?? p.v, hi = p => p.hi ?? p.v;
  let low = points[0], high = points[0], sum = 0;
  for (const p of points) {
    sum += p.v;
    if (lo(p) < lo(low)) low = p;
    if (hi(p) > hi(high)) high = p;
  }
  return { average: sum / points.length, low: { t: low.t, v: lo(low), day: low.day }, high: { t: high.t, v: hi(high), day: high.day } };
}

const STYLE = `
  /* pointer-events: een button-card zonder eigen tik-actie (de pop-up) is 'disabled' en zet klikken uit voor alles erin; de knoppen moeten werken */
  :host { display: block; pointer-events: auto; color: var(--primary-text-color, #e8ecf5); font-family: inherit; }
  * { box-sizing: border-box; }
  .wrap { display: flex; flex-direction: column; gap: 10px; text-align: left; }
  .bar { display: flex; flex-direction: column; gap: 10px; }
  .seg { display: flex; align-self: center; padding: 3px; border-radius: 14px; background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.08); }
  .seg button { border: 0; background: transparent; color: inherit; font: inherit; font-size: 13px; font-weight: 600; padding: 7px 20px; border-radius: 11px; cursor: pointer; opacity: 0.7; }
  .seg button[aria-pressed="true"] { background: rgba(91,157,255,0.28); box-shadow: inset 0 0 0 1px rgba(91,157,255,0.6); opacity: 1; }
  .nav { display: grid; grid-template-columns: 44px 1fr 44px; align-items: center; gap: 6px; }
  .nav > button { border: 0; width: 44px; height: 40px; border-radius: 12px; background: rgba(255,255,255,0.06); color: inherit; cursor: pointer; display: grid; place-items: center; padding: 0; }
  .nav > button:disabled { opacity: 0.25; cursor: default; }
  .nav > button ha-icon { --mdc-icon-size: 24px; margin: 0; }
  .title { text-align: center; font-size: 14px; font-weight: 700; line-height: 1.25; }
  .title small { display: block; font-size: 11px; font-weight: 500; opacity: 0.65; }
  .title .link { border: 0; background: transparent; padding: 2px 6px; color: ${BLUE}; cursor: pointer; font: inherit; font-size: 12px; font-weight: 600; }
  .chart { position: relative; min-height: 120px; transition: opacity 0.2s; touch-action: pan-y; user-select: none; -webkit-user-select: none; }
  .chart.loading { opacity: 0.45; }
  .chart svg { display: block; width: 100%; overflow: visible; }
  .chart .msg { position: absolute; inset: 0; display: grid; place-items: center; font-size: 13px; opacity: 0.7; text-align: center; padding: 12px; }
  .stats { display: flex; gap: 8px; flex-wrap: wrap; }
  .stat { flex: 1 1 90px; min-width: 0; padding: 9px 10px; border-radius: 14px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.07); }
  .stat .label { font-size: 11px; opacity: 0.65; }
  .stat .value { font-size: 19px; font-weight: 700; line-height: 1.2; }
  .stat .sub { font-size: 11px; opacity: 0.7; }
  .unit { font-size: 11px; opacity: 0.65; }
`;

class HuisPrijsHistorie extends HTMLElement {
  static get observedAttributes() { return ['entity', 'period', 'compact']; }

  constructor() {
    super();
    this._offset = 0;
    this._points = [];
    this._future = [];
    this._previous = null;
    this._cache = new Map();
    this._request = 0;
    this._visible = false;
    this._state = 'loading';
    this.attachShadow({ mode: 'open' });
  }

  get entity() { return this.getAttribute('entity') || DEFAULT_ENTITY; }
  get compact() { return this.hasAttribute('compact'); }
  get period() { return this.compact ? 'week' : this._period; }

  connectedCallback() {
    this._period = PERIODS.some(([key]) => key === this.getAttribute('period')) ? this.getAttribute('period') : 'week';
    this._build();
    this._resize = new ResizeObserver(() => { if (this._width !== this._chart.clientWidth) this._draw(); });
    this._resize.observe(this._chart);
    // Pas laden als hij echt in beeld is (een pop-up die dicht is, haalt niets op) en dan elke vijf minuten bijwerken
    this._seen = new IntersectionObserver(entries => {
      const visible = entries.some(e => e.isIntersecting);
      if (visible && !this._visible) { this._offset = 0; this._load(); }
      this._visible = visible;
    });
    this._seen.observe(this);
    this._timer = setInterval(() => { if (this._visible && this._offset === 0) this._load(true); }, REFRESH);
  }

  disconnectedCallback() {
    this._resize?.disconnect();
    this._seen?.disconnect();
    clearInterval(this._timer);
    this._visible = false;
  }

  attributeChangedCallback(name, old, value) {
    if (!this.isConnected || old === value) return;
    if (name === 'period' && PERIODS.some(([key]) => key === value)) this._period = value;
    this._load();
  }

  get _hass() { return document.querySelector('home-assistant')?.hass; }

  _build() {
    const full = !this.compact;
    this.shadowRoot.innerHTML = `<style>${STYLE}</style><div class="wrap">
      ${full ? `<div class="bar">
        <div class="seg" role="group" aria-label="Periode">${PERIODS.map(([key, label]) => `<button data-p="${key}" aria-pressed="${key === this.period}">${label}</button>`).join('')}</div>
        <div class="nav">
          <button data-a="prev" aria-label="Vorige periode"><ha-icon icon="mdi:chevron-left"></ha-icon></button>
          <div class="title"></div>
          <button data-a="next" aria-label="Volgende periode"><ha-icon icon="mdi:chevron-right"></ha-icon></button>
        </div></div>` : ''}
      <div class="chart" role="img"></div>
      ${full ? '<div class="stats"></div>' : ''}</div>`;
    this._chart = this.shadowRoot.querySelector('.chart');
    this._titleEl = this.shadowRoot.querySelector('.title');
    this._statsEl = this.shadowRoot.querySelector('.stats');
    this._chart.addEventListener('pointermove', e => this._point(e));
    this._chart.addEventListener('pointerdown', e => this._point(e));
    this._chart.addEventListener('pointerleave', () => this._hideTip());
    this.shadowRoot.addEventListener('click', e => {
      const button = e.target.closest('button');
      if (!button) return;
      e.stopPropagation();
      if (button.dataset.p) { this._period = button.dataset.p; this._offset = 0; this._load(); }
      else if (button.dataset.a === 'prev') { this._offset += 1; this._load(); }
      else if (button.dataset.a === 'next' && this._offset > 0) { this._offset -= 1; this._load(); }
      else if (button.dataset.a === 'now') { this._offset = 0; this._load(); }
    });
  }

  // Een dag of een week in uurprijzen; een maand per dag (het gemiddelde met de laagste en de hoogste prijs van die dag)
  async _statistics(win, period) {
    const perDay = period === 'maand';
    const key = `${period}-${win.start.getTime()}-${win.end.getTime()}`;
    const cached = this._cache.get(key);
    if (cached && Date.now() - cached.at < REFRESH) return cached.points;
    const result = await this._hass.callWS({
      type: 'recorder/statistics_during_period',
      start_time: win.start.toISOString(),
      end_time: win.end.toISOString(),
      statistic_ids: [this.entity],
      period: perDay ? 'day' : 'hour',
      types: perDay ? ['mean', 'min', 'max'] : ['mean']
    });
    const points = (result?.[this.entity] ?? [])
      .map(row => ({ t: toMs(row.start), v: row.mean, ...(perDay ? { lo: row.min, hi: row.max, day: true } : {}) }))
      .filter(p => Number.isFinite(p.t) && Number.isFinite(p.v))
      .sort((a, b) => a.t - b.t);
    this._cache.set(key, { at: Date.now(), points });
    return points;
  }

  // De prijzen van de komende uren (vandaag, en morgen zodra ze bekend zijn) staan in de sensor zelf
  _futurePoints(win, last) {
    const prices = this._hass?.states?.[this.entity]?.attributes?.hourly_prices ?? {};
    return Object.entries(prices)
      .map(([time, price]) => ({ t: Date.parse(time), v: parseFloat(price?.all_in_price) }))
      .filter(p => Number.isFinite(p.t) && Number.isFinite(p.v) && p.t > last && p.t < win.end.getTime())
      .sort((a, b) => a.t - b.t);
  }

  async _load(quiet = false) {
    if (!this._hass?.callWS) { this._state = 'error'; this._draw(); return; }
    const request = ++this._request;
    const offset = this._offset, period = this.period;
    const win = windowFor(period, offset);
    if (!quiet) {
      this._state = 'loading';
      this._chart.classList.add('loading');
      this._renderBar(period, offset);
    }
    try {
      const [points, previous] = await Promise.all([
        this._statistics(win, period),
        this.compact ? Promise.resolve(null) : this._statistics(windowFor(period, offset + 1), period)
      ]);
      if (request !== this._request) return;
      this._win = win;
      this._shown = { period, offset };
      this._points = points;
      this._future = offset === 0 && period !== 'maand' ? this._futurePoints(win, points.length ? points[points.length - 1].t : win.start.getTime() - 1) : [];
      this._previous = previous ? summarize(previous) : null;
      this._state = points.length || this._future.length ? 'ready' : 'empty';
    } catch (error) {
      if (request !== this._request) return;
      console.warn('huis-prijs-historie', error);
      this._state = 'error';
    }
    this._chart.classList.remove('loading');
    this._draw();
  }

  _draw() {
    if (!this._chart) return;
    const message = { loading: 'Laden…', error: 'De prijzen konden niet worden opgehaald', empty: 'Geen prijzen in deze periode' }[this._state];
    if (this._state !== 'ready') {
      // Een bestaande grafiek blijft staan terwijl er geladen wordt
      if (this._state === 'loading' && this._chart.querySelector('svg')) return;
      this._chart.innerHTML = `<div class="msg">${message}</div>`;
      this._chart.style.height = `${this.compact ? 170 : 240}px`;
      this._renderBar();
      this._renderStats();
      return;
    }
    const { period } = this._shown;
    const width = this._width = Math.max(this._chart.clientWidth, 200);
    const height = this.compact ? 170 : 240;
    const margin = { left: 46, right: 10, top: 12, bottom: 24 };
    const all = [...this._points, ...this._future];
    const scale = niceScale(Math.min(...all.map(p => p.lo ?? p.v)), Math.max(...all.map(p => p.hi ?? p.v)));
    const t0 = this._win.start.getTime(), t1 = this._win.end.getTime();
    const x = t => margin.left + (t - t0) / (t1 - t0) * (width - margin.left - margin.right);
    const y = v => margin.top + (1 - (v - scale.min) / (scale.max - scale.min)) * (height - margin.top - margin.bottom);
    const bottom = height - margin.bottom;
    this._geometry = { x, y, t0, t1, margin, width, height, all };

    const grid = scale.ticks.map(v => `<line x1="${margin.left}" x2="${width - margin.right}" y1="${y(v)}" y2="${y(v)}" stroke="rgba(255,255,255,0.09)"/>
      <text x="${margin.left - 6}" y="${y(v) + 4}" text-anchor="end" font-size="11" fill="rgba(255,255,255,0.7)">${euro(v)}</text>`).join('');
    const axis = xTicks(period, this._win).map(tick => `${tick.line ? `<line x1="${x(tick.t)}" x2="${x(tick.t)}" y1="${margin.top}" y2="${bottom}" stroke="rgba(255,255,255,0.06)"/>` : ''}
      ${tick.text ? `<text x="${x(tick.t)}" y="${height - 6}" text-anchor="${tick.anchor ?? 'middle'}" font-size="11" fill="rgba(255,255,255,0.7)">${esc(tick.text)}</text>` : ''}`).join('');

    // De lijn in stukken: een gat van meer dan een uur of twee (bij dagen: anderhalve dag) is een gat in de gegevens
    const gap = period === 'maand' ? 36 * HOUR : GAP;
    const segments = [];
    for (const p of this._points) {
      const last = segments[segments.length - 1];
      if (last && p.t - last[last.length - 1].t <= gap) last.push(p); else segments.push([p]);
    }
    const line = seg => seg.map((p, i) => `${i ? 'L' : 'M'}${x(p.t).toFixed(1)} ${y(p.v).toFixed(1)}`).join('');
    // Uurprijzen: een vlak onder de lijn. Dagen: een band van de laagste tot de hoogste prijs van die dag, met het gemiddelde als lijn
    const area = seg => period === 'maand'
      ? `${seg.map((p, i) => `${i ? 'L' : 'M'}${x(p.t).toFixed(1)} ${y(p.hi).toFixed(1)}`).join('')}${[...seg].reverse().map(p => `L${x(p.t).toFixed(1)} ${y(p.lo).toFixed(1)}`).join('')}Z`
      : `${line(seg)} L${x(seg[seg.length - 1].t).toFixed(1)} ${bottom} L${x(seg[0].t).toFixed(1)} ${bottom}Z`;
    const lines = segments.map(seg => `<path d="${area(seg)}" fill="${period === 'maand' ? 'rgba(91,157,255,0.2)' : 'url(#fill)'}"/>
      <path d="${line(seg)}" fill="none" stroke="${BLUE}" stroke-width="2" stroke-linejoin="round" stroke-linecap="round"/>
      ${seg.length === 1 ? `<circle cx="${x(seg[0].t)}" cy="${y(seg[0].v)}" r="2.5" fill="${BLUE}"/>` : ''}`).join('');
    const lastPoint = this._points[this._points.length - 1];
    const future = this._future.length
      ? `<path d="${line(lastPoint && this._future[0].t - lastPoint.t <= GAP ? [lastPoint, ...this._future] : this._future)}" fill="none" stroke="${BLUE}" stroke-width="2" stroke-dasharray="4 4" stroke-linecap="round" opacity="0.75"/>`
      : '';

    // De laagste en de hoogste prijs van de periode, met een stip
    const summary = summarize(this._points);
    const marks = summary && !this.compact ? [[summary.low, GREEN], [summary.high, RED]].map(([p, color]) =>
      `<circle cx="${x(p.t)}" cy="${y(p.v)}" r="4" fill="${color}" stroke="#0b1020" stroke-width="1.5"/>`).join('') : '';
    const now = Date.now();
    const nowLine = now > t0 && now < t1 && period !== 'maand'
      ? `<line x1="${x(now)}" x2="${x(now)}" y1="${margin.top}" y2="${bottom}" stroke="rgba(255,255,255,0.35)" stroke-dasharray="2 3"/>` : '';

    this._chart.setAttribute('aria-label', summary
      ? `Stroomprijs, gemiddeld ${euro(summary.average)} euro per kWh, laagste ${euro(summary.low.v)}, hoogste ${euro(summary.high.v)}` : 'Stroomprijs');
    this._chart.style.height = '';
    this._chart.innerHTML = `<svg viewBox="0 0 ${width} ${height}" width="${width}" height="${height}">
      <defs><linearGradient id="fill" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${BLUE}" stop-opacity="0.28"/><stop offset="1" stop-color="${BLUE}" stop-opacity="0"/></linearGradient></defs>
      <text x="${margin.left}" y="9" font-size="11" fill="rgba(255,255,255,0.7)">€/kWh</text>
      ${grid}${axis}${nowLine}${lines}${future}${marks}
      <g class="tip" visibility="hidden" pointer-events="none">
        <line class="tip-line" y1="${margin.top}" y2="${bottom}" stroke="rgba(255,255,255,0.5)"/>
        <circle class="tip-dot" r="4.5" fill="${BLUE}" stroke="#fff" stroke-width="1.5"/>
        <rect class="tip-box" rx="8" height="40" fill="#0b1020" stroke="rgba(255,255,255,0.25)"/>
        <text class="tip-value" font-size="14" font-weight="700" fill="#fff"/>
        <text class="tip-time" font-size="11" fill="rgba(255,255,255,0.75)"/>
      </g></svg>`;
    this._renderBar();
    this._renderStats();
  }

  _renderBar(period = this._shown?.period ?? this.period, offset = this._shown?.offset ?? this._offset) {
    if (this.compact || !this._titleEl) return;
    const win = windowFor(period, offset);
    this._titleEl.innerHTML = `${esc(windowTitle(period, offset, win))}${offset > 0 ? '<small><button class="link" data-a="now">Terug naar nu</button></small>' : ''}`;
    this.shadowRoot.querySelectorAll('.seg button').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.p === period)));
    this.shadowRoot.querySelector('[data-a="next"]').disabled = offset === 0;
  }

  _renderStats() {
    if (!this._statsEl) return;
    const summary = this._state === 'ready' ? summarize(this._points) : null;
    if (!summary) { this._statsEl.innerHTML = ''; return; }
    const { period } = this._shown;
    const when = p => {
      const time = new Date(p.t).toLocaleTimeString('nl-NL', { hour: '2-digit', minute: '2-digit' });
      if (period === 'dag') return time;
      const day = new Date(p.t).toLocaleDateString('nl-NL', { weekday: 'short', day: 'numeric', month: 'short' });
      return p.day ? day : `${day} ${time}`;
    };
    let change = '';
    if (this._previous && this._previous.average > 0) {
      const percent = Math.round((summary.average / this._previous.average - 1) * 100);
      const word = { dag: 'vorige dag', week: 'vorige week', maand: 'vorige maand' }[period];
      change = `${percent > 0 ? '+' : ''}${percent}% t.o.v. ${word}`;
    }
    const color = this._previous && summary.average > this._previous.average ? RED : GREEN;
    this._statsEl.innerHTML = `
      <div class="stat"><div class="label">Gemiddeld</div><div class="value">€ ${euro(summary.average)}</div><div class="sub" style="color:${change ? color : 'inherit'}">${esc(change) || '&nbsp;'}</div></div>
      <div class="stat"><div class="label">Laagste</div><div class="value" style="color:${GREEN}">€ ${euro(summary.low.v)}</div><div class="sub">${esc(when(summary.low))}</div></div>
      <div class="stat"><div class="label">Hoogste</div><div class="value" style="color:${RED}">€ ${euro(summary.high.v)}</div><div class="sub">${esc(when(summary.high))}</div></div>`;
  }

  // Aanwijzen of aantikken: het dichtstbijzijnde uur met zijn prijs
  _point(event) {
    const g = this._geometry;
    const svg = this._chart.querySelector('svg');
    if (!g || !svg || this._state !== 'ready') return;
    const box = svg.getBoundingClientRect();
    const px = (event.clientX - box.left) * (g.width / box.width);
    const t = g.t0 + (px - g.margin.left) / (g.width - g.margin.left - g.margin.right) * (g.t1 - g.t0);
    let best = null;
    for (const p of g.all) if (!best || Math.abs(p.t - t) < Math.abs(best.t - t)) best = p;
    if (!best || Math.abs(best.t - t) > (best.day ? 18 : 2) * HOUR) { this._hideTip(); return; }

    const cx = g.x(best.t), cy = g.y(best.v);
    const when = new Date(best.t);
    const time = when.toLocaleTimeString('nl-NL', { hour: '2-digit', minute: '2-digit' });
    const day = when.toLocaleDateString('nl-NL', { weekday: 'short', day: 'numeric', month: 'short' });
    const tip = svg.querySelector('.tip');
    const label = `€ ${euro(best.v)}`;
    // Een dag toont het gemiddelde en zijn laagste en hoogste prijs
    const detail = best.day ? `${day} · ${euro(best.lo)} – ${euro(best.hi)}` : `${day} ${time}`;
    const boxWidth = best.day ? 170 : 112;
    const left = cx + 10 + boxWidth > g.width ? cx - 10 - boxWidth : cx + 10;
    const top = Math.max(g.margin.top, Math.min(cy - 20, g.height - g.margin.bottom - 42));
    tip.setAttribute('visibility', 'visible');
    const set = (selector, attrs) => { const el = tip.querySelector(selector); for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v); return el; };
    set('.tip-line', { x1: cx, x2: cx });
    set('.tip-dot', { cx, cy });
    set('.tip-box', { x: left, y: top, width: boxWidth });
    set('.tip-value', { x: left + 10, y: top + 17 }).textContent = label;
    set('.tip-time', { x: left + 10, y: top + 32 }).textContent = detail;
  }

  _hideTip() { this._chart.querySelector('.tip')?.setAttribute('visibility', 'hidden'); }
}

if (typeof customElements !== 'undefined' && !customElements.get('huis-prijs-historie')) customElements.define('huis-prijs-historie', HuisPrijsHistorie);
