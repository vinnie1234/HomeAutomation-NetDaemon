// Huis-dashboard: na 30 seconden niets doen (niet tikken, klikken, scrollen of typen) terug naar Home.
// Alleen op het Huis-dashboard en niet op Home zelf. Staat er een venster open (bladeren in Jellyfin, de geschiedenis van
// de medicijnen, een tijdlijn) of is iets schermvullend, dan wacht hij tot dat dicht is.
// Geladen via frontend.extra_module_url in configuration.yaml (zie README.md).
(() => {
  const DASHBOARD = '/dashboard-huis/', HOME = '/dashboard-huis/home', IDLE_MS = 30000;
  let timer;

  const somethingOpen = () => document.fullscreenElement
    || document.querySelector('#huis-jelly, #huis-jelly-detail, #huis-medlog, #huis-timeline, #huis-datum');

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

  for (const type of ['pointerdown', 'pointermove', 'keydown', 'wheel', 'touchstart', 'scroll'])
    window.addEventListener(type, restart, { passive: true, capture: true });
  window.addEventListener('location-changed', restart);
  window.addEventListener('popstate', restart);
  restart();
})();
