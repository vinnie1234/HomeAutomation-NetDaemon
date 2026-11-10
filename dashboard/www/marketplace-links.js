// Huis-dashboard: in het venster Custom Component Monitor (Systeem) opent een tik op een onderdeel (kaart, thema of integratie) meteen
// de pagina van dat onderdeel in de Marketplace van Home Assistant (/marketplace/repository/<id>), in plaats van GitHub.
// Wordt geladen door de tegel op Systeem: import('/local/huis/marketplace-links.js?v=N').then(m => m.install()); N ophogen na een wijziging.
const CARDS = ['custom-component-monitor-card', 'recently-installed-unused-card', 'update-action-tracker-card', 'hacs-new-items-card'];
const CACHE_MS = 10 * 60 * 1000;
let repositories = null, loadedAt = 0, installed = false;

const hass = () => document.querySelector('home-assistant').hass;

async function load() {
  if (repositories && Date.now() - loadedAt < CACHE_MS) return repositories;
  repositories = await hass().callWS({ type: 'marketplace/repositories/list' });
  loadedAt = Date.now();
  return repositories;
}

// "https://github.com/owner/repo(/...)" -> "owner/repo"
const fullNameOf = href => { const m = /github\.com\/([^/]+\/[^/#?]+)/i.exec(href ?? ''); return m ? m[1].replace(/\.git$/, '').toLowerCase() : null; };

async function open(item) {
  const link = item.querySelector('a[href]')?.getAttribute('href');
  const name = item.querySelector('.item-name')?.textContent.trim().toLowerCase();
  const all = await load();
  const fullName = fullNameOf(link);
  // Eerst op de repository van de link, anders op de naam van een geïnstalleerd onderdeel
  const found = (fullName && all.find(r => r.full_name.toLowerCase() === fullName))
    ?? (name && all.find(r => r.installed && r.name.toLowerCase() === name));
  if (!found) return false;
  history.pushState(null, '', `/marketplace/repository/${found.id}`);
  window.dispatchEvent(new CustomEvent('location-changed'));
  return true;
}

const STYLE = '.item { cursor: pointer; } .item:active { background: rgba(255,255,255,0.08); }';

export function install() {
  if (installed) return;
  installed = true;

  // De rijen laten zien dat ze aan te tikken zijn (de kaart zelf kent dat niet)
  window.addEventListener('pointerover', e => {
    const host = e.composedPath().find(el => el.localName && CARDS.includes(el.localName));
    const root = host?.shadowRoot;
    if (root && !root.querySelector('style[data-marketplace-links]')) {
      const style = document.createElement('style');
      style.dataset.marketplaceLinks = '';
      style.textContent = STYLE;
      root.appendChild(style);
    }
  }, true);

  window.addEventListener('click', e => {
    const path = e.composedPath();
    if (!path.some(el => el.localName && CARDS.includes(el.localName))) return;
    const item = path.find(el => el.classList?.contains('item'));
    if (!item) return;
    // De link naar GitHub en de rij zelf doen hetzelfde: de pagina in de Marketplace
    e.preventDefault();
    e.stopPropagation();
    const github = item.querySelector('a[href]')?.href;
    const fallback = () => { if (github) window.open(github, '_blank', 'noopener'); };
    open(item).then(ok => { if (!ok) fallback(); }).catch(fallback);
  }, true);
}
