// Huis-dashboard: na 1 minuut niets doen (niet tikken, klikken, scrollen of typen) terug naar Home.
// Alleen op het Huis-dashboard en niet op Home zelf. Staat er een venster open (bladeren in Jellyfin, de geschiedenis van
// de medicijnen, een tijdlijn) of is iets schermvullend, dan wacht hij tot dat dicht is.
// Geladen via frontend.extra_module_url in configuration.yaml (zie README.md).
(() => {
  const DASHBOARD = '/dashboard-huis/', HOME = '/dashboard-huis/home', IDLE_MS = 60000;
  let timer;

  const somethingOpen = () => document.fullscreenElement
    || document.querySelector('#huis-jelly, #huis-jelly-detail, #huis-medlog, #huis-timeline, #huis-datum, #huis-speler');

  const goHome = () => {
    const { pathname, hash } = window.location;
    if (!pathname.startsWith(DASHBOARD) || (pathname === HOME && !hash)) return;
    if (somethingOpen()) { restart(); return; }
    // Zoals Home Assistant zelf navigeert: de URL aanpassen en het laten weten
    window.history.pushState(null, '', HOME);
    window.dispatchEvent(new CustomEvent('location-changed', { detail: { replace: false } }));
  };

  const restart = () => {
    clearTimeout(timer);
    timer = setTimeout(goHome, IDLE_MS);
  };

  // Een telefoon met de oude navigatieknoppen (terug, home, recent) onderaan: de app tekent het dashboard dan achter die knoppen en de menubalk
  // zit eronder. Per apparaat te kiezen (de tegel "Navigatieknoppen" op de eigen pagina): hoeveel ruimte onderaan, 0 = niets. De navbar-kaart
  // (mobile.safe_area) gebruikt --safe-area-inset-bottom; andere telefoons houden hun eigen waarde.
  const NAV_KEY = 'huis-navigatieknoppen', NAV_STEPS = [0, 48, 64];
  const navSpace = () => { const v = parseInt(localStorage.getItem(NAV_KEY) ?? '0', 10); return NAV_STEPS.includes(v) ? v : 0; };
  const applyNav = () => {
    const space = navSpace();
    if (space) document.documentElement.style.setProperty('--safe-area-inset-bottom', `${space}px`);
    else document.documentElement.style.removeProperty('--safe-area-inset-bottom');
  };
  window.huisNavKnoppen = {
    get: navSpace,
    // Volgende stand: uit, 48 px (de gewone balk), 64 px (een hogere); daarna weer uit
    next: () => { localStorage.setItem(NAV_KEY, String(NAV_STEPS[(NAV_STEPS.indexOf(navSpace()) + 1) % NAV_STEPS.length])); applyNav(); return navSpace(); }
  };
  applyNav();

  for (const type of ['pointerdown', 'pointermove', 'keydown', 'wheel', 'touchstart', 'scroll'])
    window.addEventListener(type, restart, { passive: true, capture: true });
  window.addEventListener('location-changed', restart);
  window.addEventListener('popstate', restart);
  restart();
})();
