// Paneelachtergrond van de secties: elke sectie van een pagina is een donker paneel met een zachte gloed en een dunne rand, zoals de kaart
// Energiestroom. De eerste sectie (het menu en de tabs) blijft zonder paneel.
// Geladen vanuit het glass-template (button_card_templates.yaml); ?v= daar ophogen na een wijziging van dit bestand.

const CSS = `
  hui-section.huis-paneel {
    display: block;
    box-sizing: border-box;
    padding: 18px;
    border-radius: 24px;
    background: radial-gradient(120% 90% at 50% 20%, rgba(30, 41, 82, 0.50), rgba(8, 12, 28, 0.50));
    border: 1px solid rgba(255, 255, 255, 0.08);
  }
  @media (max-width: 600px) { hui-section.huis-paneel { padding: 10px; border-radius: 18px; } }
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

function apply() {
  for (const view of views(document)) {
    const root = view.shadowRoot;
    if (!root) continue;
    if (!root.querySelector('style[data-huis-paneel]')) {
      const style = document.createElement('style');
      style.dataset.huisPaneel = '1';
      style.textContent = CSS;
      root.appendChild(style);
    }
    // De eerste sectie van elke pagina is de kop met het menu en de tabs: geen paneel
    [...root.querySelectorAll('hui-section')].forEach((section, i) => {
      // Een sectie met alleen pop-ups (zonder zichtbare kaart) krijgt geen paneel
      const leeg = section.getBoundingClientRect().height - (section.classList.contains('huis-paneel') ? 36 : 0) < 40;
      section.classList.toggle('huis-paneel', i > 0 && !leeg);
    });
  }
}

let timer = null;
const schedule = () => { clearTimeout(timer); timer = setTimeout(apply, 250); };
new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true });
window.addEventListener('location-changed', schedule);
schedule();
