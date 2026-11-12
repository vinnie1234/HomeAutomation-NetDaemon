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
    background: radial-gradient(120% 90% at 50% 15%, rgba(30, 41, 82, 0.50), rgba(8, 12, 28, 0.50));
    border: 1px solid rgba(255, 255, 255, 0.08);
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

function applyDatenight() {
  const on = datenightOn();
  const root = document.documentElement;
  if (on !== datenightWas) {
    datenightWas = on;
    if (on) {
      root.style.setProperty('--lovelace-background', ROMANTISCH);
      root.style.setProperty('--accent-color', '#ff7a9c');
      root.style.setProperty('--primary-color', '#ff7a9c');
    } else {
      root.style.removeProperty('--lovelace-background');
      root.style.removeProperty('--accent-color');
      root.style.removeProperty('--primary-color');
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
