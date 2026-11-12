// Eén paneel achter de hele pagina: alle secties behalve de eerste (het menu en de tabs, of de weerkaart) staan samen op een donkere
// achtergrond met een zachte gloed en een dunne rand, zoals de kaart Energiestroom. Het paneel is een div achter de secties die
// meegroeit met de secties, zodat de indeling van de pagina zelf niet verandert.
// Geladen vanuit het glass-template (button_card_templates.yaml); ?v= daar ophogen na een wijziging van dit bestand.

const PAD = 16;
const CSS = `
  hui-section { position: relative; z-index: 1; }
  .huis-achtergrond {
    position: absolute; z-index: 0; pointer-events: none; box-sizing: border-box;
    border-radius: 24px;
    background: radial-gradient(120% 90% at 50% 15%, rgba(30, 41, 82, 0.50), rgba(8, 12, 28, 0.50));
    border: 1px solid rgba(255, 255, 255, 0.08);
  }
`;

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

function apply() {
  for (const view of views(document)) {
    layout(view);
    if (!view.dataset.huisWaker) {
      view.dataset.huisWaker = '1';
      new ResizeObserver(() => layout(view)).observe(view);
    }
  }
}

let timer = null;
const schedule = () => { clearTimeout(timer); timer = setTimeout(apply, 200); };
new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true });
window.addEventListener('resize', schedule);
window.addEventListener('location-changed', schedule);
schedule();
