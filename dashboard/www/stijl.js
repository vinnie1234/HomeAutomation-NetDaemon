// Eén paneel achter de hele pagina: alle secties behalve de eerste (het menu en de tabs, of de weerkaart) staan samen op een donkere
// achtergrond met een zachte gloed en een dunne rand, zoals de kaart Energiestroom. Het paneel is een div achter de secties die
// meegroeit met de secties, zodat de indeling van de pagina zelf niet verandert.
// Geladen vanuit het glass-template (button_card_templates.yaml); ?v= daar ophogen na een wijziging van dit bestand.

const PAD = 16;
const CSS = `
  /* Zonder z-index: een sectie als eigen stapel laat kaarten uit een volgende sectie over een geopend venster heen vallen */
  hui-section { position: relative; }
  .huis-achtergrond {
    position: absolute; z-index: 0; pointer-events: none; box-sizing: border-box;
    border-radius: 24px;
    background: var(--huis-paneel-bg, radial-gradient(120% 90% at 50% 15%, rgba(30, 41, 82, 0.50), rgba(8, 12, 28, 0.50)));
    border: 1px solid var(--huis-paneel-rand, rgba(255, 255, 255, 0.08));
  }
`;

// Pop-ups van Bubble Card als dialoog in het midden, hetzelfde als het venster van de auto: donkere afgeronde kaart met een vaste rand.
const POPUP_CSS = `
  .bubble-pop-up.is-popup-opened {
    left: 50% !important; top: 50% !important; right: auto !important; bottom: auto !important;
    transform: translate(-50%, -50%) !important; margin: 0 !important; z-index: 2147483000 !important;
    width: min(540px, calc(100vw - 24px)) !important; height: auto !important; max-height: 88vh !important; max-height: 88dvh !important;
    display: flex !important; flex-direction: column !important; overflow: hidden !important;
    border-radius: 24px !important; border: 1px solid rgba(255, 255, 255, 0.10) !important;
  }
  .bubble-pop-up.is-popup-opened .bubble-pop-up-background {
    background: #121828 !important; opacity: 1 !important; border-radius: 24px !important; backdrop-filter: none !important;
  }
  .bubble-pop-up.is-popup-opened .bubble-header-container { position: relative !important; flex: none !important; padding-bottom: 0 !important; }
  .bubble-pop-up.is-popup-opened .bubble-header { background: transparent !important; box-shadow: none !important; }
  .bubble-pop-up.is-popup-opened .bubble-close-button { border-radius: 14px !important; background: rgba(255, 255, 255, 0.10) !important; }
  .bubble-pop-up.is-popup-opened .bubble-pop-up-container {
    position: relative !important; flex: 0 1 auto !important; height: auto !important; min-height: 0 !important; margin-top: 0 !important;
    max-height: calc(88dvh - 96px) !important; overflow: auto !important; padding-bottom: 16px !important;
  }
`;

function stylePopups() {
  const stack = [document];
  while (stack.length) {
    const node = stack.pop();
    if (node.localName === 'bubble-card' && node.shadowRoot) {
      const root = node.shadowRoot;
      if (root.querySelector('.bubble-pop-up') && !root.querySelector('style[data-huis-popup]')) {
        const style = document.createElement('style');
        style.dataset.huisPopup = '1';
        style.textContent = POPUP_CSS;
        root.appendChild(style);
      }
    }
    if (node.shadowRoot) stack.push(node.shadowRoot);
    for (const child of node.children || []) stack.push(child);
  }
}

function* views(root) {
  const stack = [root];
  while (stack.length) {
    const node = stack.pop();
    if (node.localName === 'hui-sections-view') yield node;
    if (node.shadowRoot) stack.push(node.shadowRoot);
    for (const child of node.children || []) stack.push(child);
  }
}

function layout(view) {
  const root = view.shadowRoot;
  if (!root) return;
  if (!root.querySelector('style[data-huis-paneel]')) {
    const style = document.createElement('style');
    style.dataset.huisPaneel = '1';
    style.textContent = CSS;
    root.appendChild(style);
  }
  // De eerste sectie is de kop; een sectie zonder zichtbare kaart (alleen pop-ups) hoort er niet bij
  const sections = [...root.querySelectorAll('hui-section')].slice(1).filter((s) => s.getBoundingClientRect().height > 40);
  const container = sections[0]?.parentElement;
  let back = root.querySelector('.huis-achtergrond');
  if (!container || !sections.length) { back?.remove(); return; }
  if (!back || back.parentElement !== container) {
    back?.remove();
    back = document.createElement('div');
    back.className = 'huis-achtergrond';
    container.insertBefore(back, container.firstChild);
    if (getComputedStyle(container).position === 'static') container.style.position = 'relative';
  }
  const c = container.getBoundingClientRect();
  const rects = sections.map((s) => s.getBoundingClientRect());
  const left = Math.min(...rects.map((r) => r.left)), top = Math.min(...rects.map((r) => r.top));
  const right = Math.max(...rects.map((r) => r.right)), bottom = Math.max(...rects.map((r) => r.bottom));
  back.style.left = `${left - c.left - PAD}px`;
  back.style.top = `${top - c.top - PAD}px`;
  back.style.width = `${right - left + 2 * PAD}px`;
  back.style.height = `${bottom - top + 2 * PAD}px`;
}

const waker = new WeakMap();

// Date night: een rustig, romantisch dashboard. De achtergrond en de accentkleur worden roze/paars; op Home blijft alleen de kop over
// (de klok en de knop om date night te stoppen), de kamers en de muziek; de chips en de rest verdwijnen. Meldingen verbergen zichzelf (zie het alert-template).
const ROMANTISCH = 'radial-gradient(1200px 700px at 0% 0%, rgba(255, 90, 140, 0.38), transparent 60%), radial-gradient(1000px 700px at 100% 100%, rgba(170, 70, 210, 0.45), transparent 60%), #1a0814 fixed';
let datenightWas = null;

function datenightOn() {
  const ha = document.querySelector('home-assistant');
  return ha?.hass?.states?.['input_boolean.datenight']?.state === 'on';
}

// Een playlist afspelen: met Music Assistant aan (Beheer) via Music Assistant op de speler van dezelfde speaker, anders geeft dit false terug en
// speelt de kaart zelf via Spotcast. Zelfde indeling als Spotcast.cs in NetDaemon.
const MA_SPELERS = {
  'media_player.nestmini9818': 'media_player.keuken_keuken',
  'media_player.woonkamer': 'media_player.woonkamer_woonkamer',
  'media_player.googlehome0351': 'media_player.badkamer_badkamer_speaker',
  'media_player.friends_speakers': 'media_player.gang_friends_speakers',
  'media_player.hele_huis': 'media_player.hele_huis_2',
  'media_player.hub_vincent': 'media_player.slaapkamer_hub_vincent'
};

function naarMusicAssistant(uri, instantie) {
  const m = String(uri).match(/(?:open\.spotify\.com\/(?:intl-[a-z]+\/)?|spotify:)(playlist|album|track|artist)[\/:]([A-Za-z0-9]+)/);
  return m ? `${instantie || 'spotify'}://${m[1]}/${m[2]}` : uri;
}

window.huisSpeel = (hass, entityId, uri) => {
  const speler = hass.states['input_boolean.gebruik_music_assistant']?.state === 'on' ? MA_SPELERS[entityId] : null;
  if (!speler || !uri) return false;
  // Het Spotify-account in Music Assistant van wie dit scherm bedient (Carleen heeft haar eigen account, anders is het van Vincent)
  const persoon = /carleen/i.test(hass.user?.name ?? '') ? 'carleen' : 'vincent';
  const instantie = hass.states[`input_text.ma_spotify_${persoon}`]?.state;
  hass.callService('media_player', 'shuffle_set', { entity_id: speler, shuffle: true });
  const mediaId = naarMusicAssistant(uri, ['', 'unknown', 'unavailable'].includes(instantie) ? '' : instantie);
  const soort = (String(mediaId).match(/:\/\/(playlist|album|artist|track|radio)\//) ?? [])[1] ?? 'playlist';
  hass.callService('music_assistant', 'play_media', { entity_id: speler, media_id: mediaId, media_type: soort, enqueue: 'replace', radio_mode: false });
  return true;
};

// De vraag bij het aanzetten van date night: "Muziek aanpassen?" met Ja en Nee, 30 seconden lang. Ja drukt de knop die NetDaemon volgt
// (input_button.datenight_muziek) en start de playlist; Nee en geen antwoord laten de muziek zoals hij is.
const VRAAG_SECONDEN = 30;
let vraag = null;

function sluitVraag() {
  if (!vraag) return;
  clearInterval(vraag.timer);
  vraag.el.remove();
  vraag = null;
}

function toonVraag() {
  sluitVraag();
  const ha = document.querySelector('home-assistant');
  const start = ha?.hass?.states?.['input_button.datenight_muziek']?.state;
  const el = document.createElement('div');
  el.style.cssText = 'position:fixed;inset:0;z-index:2147483600;display:flex;align-items:center;justify-content:center;background:rgba(10,3,8,0.6);padding:16px;box-sizing:border-box';
  el.innerHTML = `<div style="width:min(420px,100%);box-sizing:border-box;background:#121828;border:1px solid rgba(255,122,156,0.45);border-radius:24px;padding:22px;color:#f2f4f8;font-family:inherit;box-shadow:0 0 40px rgba(255,90,140,0.25)">
      <div style="display:flex;align-items:center;gap:12px;margin-bottom:10px">
        <span style="width:42px;height:42px;border-radius:12px;display:flex;align-items:center;justify-content:center;background:rgba(255,122,156,0.2);color:#ff7a9c"><ha-icon icon="mdi:heart" style="--mdc-icon-size:24px"></ha-icon></span>
        <div><div style="font-size:18px;font-weight:700">Date night</div><div style="opacity:.7;font-size:13px">Muziek aanpassen naar de date night playlist?</div></div></div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-top:16px">
        <span data-ja style="cursor:pointer;text-align:center;padding:14px;border-radius:14px;font-weight:700;background:rgba(255,122,156,0.25);border:1px solid rgba(255,122,156,0.6)">Ja</span>
        <span data-nee style="cursor:pointer;text-align:center;padding:14px;border-radius:14px;font-weight:700;background:rgba(255,255,255,0.08);border:1px solid transparent">Nee</span></div>
      <div data-tijd style="margin-top:12px;text-align:center;font-size:12px;opacity:.55"></div></div>`;
  document.body.appendChild(el);
  let rest = VRAAG_SECONDEN;
  const tijd = el.querySelector('[data-tijd]');
  const toon = () => { tijd.textContent = `Dit verdwijnt over ${rest} seconden`; };
  toon();
  const timer = setInterval(() => {
    rest -= 1;
    if (rest <= 0) return sluitVraag();
    toon();
    // Beantwoord op een ander scherm (de knop is ingedrukt) of date night is alweer uit: dan hoeft het hier niet meer
    const nu = document.querySelector('home-assistant')?.hass?.states;
    if (nu?.['input_button.datenight_muziek']?.state !== start || nu?.['input_boolean.datenight']?.state !== 'on') sluitVraag();
  }, 1000);
  vraag = { el, timer };
  el.addEventListener('click', (e) => {
    if (e.target.closest('[data-ja]')) {
      document.querySelector('home-assistant')?.hass?.callService('input_button', 'press', { entity_id: 'input_button.datenight_muziek' });
      sluitVraag();
    } else if (e.target.closest('[data-nee]')) {
      sluitVraag();
    }
  });
}

// GTA 6-modus (schakelaar op de pagina van Vincent): het hele dashboard in de kleuren van Vice City, met een eigen kop en aftellen op Home.
// Date night gaat voor: staat die aan, dan blijft het romantische thema.
// Zoals paars naar oranje in Discord: diep paars linksboven, magenta in het midden, oranje rechtsonder
const VICE_CITY = 'linear-gradient(135deg, #4a0e8f 0%, #6b1590 32%, #8f1a7c 58%, #b84f1f 84%, #a8701c 100%) fixed';
const GTA_VARS = {
  '--lovelace-background': VICE_CITY,
  '--accent-color': '#ff2e93',
  '--primary-color': '#19e3d9',
  '--huis-paneel-bg': 'rgba(20, 0, 44, 0.30)',
  '--huis-paneel-rand': 'rgba(255, 255, 255, 0.16)',
  '--huis-kaart-basis': 'rgba(34, 6, 66, 0.58)',
  '--huis-gloed': '0 0 14px rgba(255, 46, 147, 0.22)',
  '--ha-card-background': 'rgba(34, 6, 66, 0.58)',
  '--card-background-color': 'rgba(34, 6, 66, 0.58)',
  '--ha-card-border-color': 'rgba(255, 140, 200, 0.35)',
  '--secondary-text-color': 'rgba(255, 224, 246, 0.74)',
  '--sidebar-background-color': 'rgba(40, 8, 80, 0.55)',
  '--divider-color': 'rgba(255, 46, 147, 0.25)'
};
let gtaWas = null;

function applyGta() {
  const ha = document.querySelector('home-assistant');
  const on = ha?.hass?.states?.['input_boolean.gta_6_modus']?.state === 'on' && !datenightOn();
  const root = document.documentElement;
  // Home Assistant zet de waarden van het thema opnieuw als dat ververst wordt: staat de modus aan, dan controleren we dat elke ronde
  if (on && root.style.getPropertyValue('--lovelace-background') !== VICE_CITY) gtaWas = null;
  if (on === gtaWas) return;
  gtaWas = on;
  if (on) {
    for (const [naam, waarde] of Object.entries(GTA_VARS)) root.style.setProperty(naam, waarde);
  } else if (!datenightOn()) {
    for (const naam of Object.keys(GTA_VARS)) root.style.removeProperty(naam);
  }
}

// Het aftellen van de GTA 6-kop: dagen, uren, minuten en seconden tot het tijdstip in het attribuut target; daarna "Nu uit".
class HuisGta6Teller extends HTMLElement {
  connectedCallback() {
    if (this._timer) return;
    this.attachShadow({ mode: 'open' });
    this.shadowRoot.innerHTML = `<style>
        :host { display: block; }
        .rij { display: flex; justify-content: center; gap: 10px; margin-top: 10px; }
        .vak { min-width: 64px; padding: 8px 6px 6px; border-radius: 12px; background: rgba(18, 0, 43, 0.62); border: 1px solid rgba(255, 210, 63, 0.55); box-shadow: 0 0 14px rgba(255, 46, 147, 0.5); }
        .g { font: 800 30px/1 Impact, 'Arial Black', sans-serif; color: #fff; text-shadow: 0 0 10px #ff2e93; font-variant-numeric: tabular-nums; }
        .l { font: 600 10px/1.4 Roboto, sans-serif; letter-spacing: .18em; color: #ffd23f; margin-top: 3px; }
        .uit { margin-top: 10px; font: 800 30px/1.1 Impact, 'Arial Black', sans-serif; color: #fff; letter-spacing: .06em; text-shadow: 0 0 14px #ff2e93, 0 0 28px #ff7a1a; }
        .datum { font: 700 12px/1 Roboto, sans-serif; letter-spacing: .3em; color: #fff; opacity: .9; margin-top: 8px; }
      </style><div class="datum">19 NOVEMBER 2026</div><div id="inhoud"></div>`;
    const doel = new Date(this.getAttribute('target')).getTime();
    const teken = () => {
      const rest = Math.max(0, doel - Date.now());
      const inhoud = this.shadowRoot.getElementById('inhoud');
      if (rest === 0) { inhoud.innerHTML = '<div class="uit">NU UIT! 🌴🚗</div>'; return; }
      const d = Math.floor(rest / 86400000), u = Math.floor(rest % 86400000 / 3600000), m = Math.floor(rest % 3600000 / 60000), sec = Math.floor(rest % 60000 / 1000);
      const vak = (g, l) => `<div class="vak"><div class="g">${String(g).padStart(2, '0')}</div><div class="l">${l}</div></div>`;
      inhoud.innerHTML = `<div class="rij">${vak(d, 'DAGEN')}${vak(u, 'UREN')}${vak(m, 'MIN')}${vak(sec, 'SEC')}</div>`;
    };
    teken();
    this._timer = setInterval(() => { if (!this.isConnected) { clearInterval(this._timer); this._timer = null; return; } teken(); }, 1000);
  }
  disconnectedCallback() { clearInterval(this._timer); this._timer = null; }
}
if (!customElements.get('huis-gta6-teller')) customElements.define('huis-gta6-teller', HuisGta6Teller);

function applyDatenight() {
  const on = datenightOn();
  const root = document.documentElement;
  if (on !== datenightWas) {
    // Alleen als het net aangezet wordt, niet als de pagina opent terwijl date night al aan staat
    if (on && datenightWas === false) toonVraag();
    if (!on) sluitVraag();
    datenightWas = on;
    if (on) {
      root.style.setProperty('--lovelace-background', ROMANTISCH);
      root.style.setProperty('--accent-color', '#ff7a9c');
      root.style.setProperty('--primary-color', '#ff7a9c');
    } else {
      for (const naam of ['--lovelace-background', '--accent-color', '--primary-color', ...Object.keys(GTA_VARS)]) root.style.removeProperty(naam);
      gtaWas = null; // de GTA 6-modus kleurt daarna opnieuw
    }
  }
  const home = location.pathname.endsWith('/home');
  for (const view of views(document)) {
    const shadow = view.shadowRoot;
    if (!shadow) continue;
    // Op Home alleen de eerste sectie (de kop); op de andere pagina's blijft alles staan, maar zonder chips
    // Op Home gaan de andere secties (met hun omhulsel) weg
    // Blijven staan: de kop (0), de kamers om de lampen te bedienen (1), wat er nu speelt om de muziek te bedienen (5) en de pop-ups van de kamers (6)
    const HOME_BLIJFT = [0, 1, 5, 6];
    [...shadow.querySelectorAll('hui-section')].forEach((section, i) => {
      if (HOME_BLIJFT.includes(i)) return;
      const wrapper = section.parentElement?.classList.contains('section-container') ? section.parentElement : section;
      wrapper.style.display = on && home ? 'none' : '';
    });
  }
  // De chips (pakketten, taken, prijs, ...) staan in de schaduw van de kaarten en zijn alleen zo te bereiken
  const stack = [document];
  while (stack.length) {
    const node = stack.pop();
    if (node.localName === 'mushroom-chips-card') node.style.display = on ? 'none' : '';
    if (node.shadowRoot) stack.push(node.shadowRoot);
    for (const child of node.children || []) stack.push(child);
  }
}

function apply() {
  stylePopups();
  applyDatenight();
  applyGta();
  for (const view of views(document)) {
    layout(view);
    // Kaarten laden later en worden daarna hoger of lager: elke sectie meten, niet alleen de pagina zelf
    let ro = waker.get(view);
    if (!ro) { ro = new ResizeObserver(() => layout(view)); waker.set(view, ro); ro.observe(view); }
    const root = view.shadowRoot;
    if (root) for (const section of root.querySelectorAll('hui-section')) ro.observe(section);
  }
}

let timer = null;
const schedule = () => { clearTimeout(timer); timer = setTimeout(apply, 200); };
new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true });
window.addEventListener('resize', schedule);
window.addEventListener('location-changed', schedule);
window.addEventListener('hashchange', apply);
schedule();
// Vangnet voor inhoud die zonder meetbare wijziging van een sectie verschuift
setInterval(apply, 3000);
