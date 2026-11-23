// Hockey: wanneer een wedstrijd of training afgelast moet worden, volgens de regels van de KNHB (Weerprotocol en Procedure afgelastingen 2026).
// Wordt geladen door de kaart "Buitenactiviteiten" (samenvatting) en de pagina Hockey (wedstrijden per uur getoetst).
// window.huisHockey = { toets, wedstrijd, dag, velden, procedure, regels, ... }
//
// officieel = staat zo in de KNHB-stukken; anders is het een eigen inschatting, omdat de KNHB daar geen getal voor noemt.
// niveau 1 = let op / keuring afwachten, niveau 2 = afgelasting door de thuisvereniging ligt voor de hand.
(() => {
  const REGELS = [
    { id: 'sneeuw', officieel: true, test: u => u.f.condition === 'snowy' ? 2 : 0,
      kort: 'sneeuw',
      tekst: 'Sneeuw: een besneeuwd veld bespelen wordt afgeraden, "advies: veld niet bespelen, afgelasting door thuisvereniging" (Weerprotocol, besneeuwd veld).' },
    { id: 'ijzel', officieel: true, test: u => u.t <= 0 && (u.r > 0 || ['snowy-rainy', 'hail'].includes(u.f.condition)) ? 2 : ['snowy-rainy', 'hail'].includes(u.f.condition) ? 1 : 0,
      kort: 'ijzel of natte sneeuw',
      tekst: 'Neerslag bij vorst: ijzel of natte sneeuw maakt het veld glad en bevroren, "bespeling is tijdelijk niet mogelijk" (Weerprotocol, ijzel en bevroren veld).' },
    { id: 'vorst-extreem', officieel: true, test: u => u.t <= -10 ? 2 : 0,
      kort: 'extreme vorst',
      tekst: 'Extreme vorst (onder -10°): daar wordt voor kunstgras het veld zelf een probleem (Weerprotocol, vorst).' },
    { id: 'vorst', officieel: true, test: u => u.t <= 0 && u.t > -10 && !(u.r > 0) ? (u.t <= -2 || u.vocht >= 85 ? 2 : 1) : 0,
      kort: 'bevroren veld',
      tekst: 'Vorst: in de KNHB-tabel is een bevroren veld "advies: veld niet bespelen, afgelasting door thuisvereniging" (uitgangspunt is veiligheid). Droge, kale vorst is voor kunstgras zelf geen probleem, maar bij een hoge luchtvochtigheid (of vanaf ongeveer -2°, mijn inschatting) wordt het veld hard, glad en met onvoorspelbaar balgedrag (Weerprotocol, bevroren veld). De vereniging keurt het veld ter plekke.' },
    { id: 'dooi', officieel: false, test: u => u.t > 0 && u.t <= 4 && u.min4 <= -2 ? 1 : 0,
      kort: 'veld kan nog bevroren zijn',
      tekst: 'Net na de vorst: het veld kan na een koude nacht nog bevroren of glad zijn terwijl de lucht al boven nul is. Laat het veld ter plekke keuren (eigen inschatting, de KNHB noemt hier geen grens).' },
    { id: 'windchill', officieel: true, test: u => u.gevoel <= -20 ? 2 : u.gevoel <= -15 ? 1 : 0,
      kort: 'gevoelstemperatuur onder -15°',
      tekst: 'Gevoelstemperatuur onder -15° (KNMI code geel) of -20° (code oranje): lokale beoordeling door de vereniging, met de leeftijd van de spelers (Weerprotocol, tabel). Bij -20° is afgelasten mijn inschatting.' },
    { id: 'onweer', officieel: true, test: u => ['lightning', 'lightning-rainy'].includes(u.f.condition) ? 1 : 0,
      kort: 'onweer',
      tekst: 'Onweer: niet vooraf afgelasten, maar de scheidsrechter staakt zodra er minder dan 10 seconden tussen flits en knal zit. De dug-out is geen veilige plek (Weerprotocol, onweer).' },
    { id: 'wateroverlast', officieel: false, test: u => u.r >= 5 ? 2 : u.r >= 2.5 ? 1 : 0,
      kort: 'hevige regen',
      tekst: 'Hevige regen: staat het veld blank, dan is het onbespeelbaar (Weerprotocol, wateroverlast). De KNHB noemt geen hoeveelheid; 2,5 mm per uur als waarschuwing en 5 mm per uur als grens zijn een eigen inschatting.' },
    { id: 'storm', officieel: false, test: u => u.st >= 90 ? 2 : u.st >= 75 ? 1 : 0,
      kort: 'storm',
      tekst: 'Storm: bij KNMI code geel, oranje of rood geldt het beleid van de KNHB (geel: lokaal, oranje: afgelasting tot 11:00 uur, rood: afgelasting). De KNHB noemt geen windsnelheid; 75 en 90 km/h in windstoten zijn een eigen inschatting. Kijk op knmi.nl.' },
    { id: 'hitte', officieel: true, test: u => u.t >= 30 ? 1 : 0,
      kort: 'hitte',
      tekst: 'Warm weer: er wordt gewoon gehockeyd. Alleen in uiterste gevallen kan de vereniging het veld afkeuren omdat het voor de keeper niet verantwoord is. Inkorten van de wedstrijd mag niet (Weerprotocol, warm weer). 30° is het voorbeeld dat de KNHB noemt.' }
  ];

  const gevoelsTemp = u => u.t <= 10 && u.w > 4.8 ? 13.12 + 0.6215 * u.t - 11.37 * Math.pow(u.w, 0.16) + 0.3965 * u.t * Math.pow(u.w, 0.16) : u.t;
  const uur = d => `${String(d.getHours()).padStart(2, '0')}:00`;
  const NIVEAUS = [['Niet afgelasten', '#4ade80'], ['Let op: wacht de keuring af', '#ffb060'], ['Afgelasten ligt voor de hand', '#ff6b6b']];
  const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);

  // Een uur van de verwachting als de uren die de kaart ook gebruikt: { tijd, t, w, st, r, f }
  const uitUur = f => {
    const w = parseFloat(f.wind_speed) || 0;
    return { tijd: new Date(f.datetime), t: parseFloat(f.temperature), w, st: parseFloat(f.wind_gust_speed) || w, r: parseFloat(f.precipitation) || 0, f };
  };

  // De uren (met t, w, st, r, f en tijd) toetsen aan de regels. alle = alle uren van de verwachting, voor de koude van de uren daarvoor
  const toets = (venster, vocht, alle) => {
    const lijst = alle ?? venster;
    const punten = REGELS.map(regel => {
      const raak = venster.map(u => {
        const i = lijst.indexOf(u);
        const min4 = Math.min(...lijst.slice(Math.max(0, i - 3), i + 1).map(x => x.t));
        return { u, n: regel.test({ ...u, vocht, gevoel: gevoelsTemp(u), min4 }) };
      }).filter(x => x.n > 0);
      if (!raak.length) return null;
      return { ...regel, n: Math.max(...raak.map(x => x.n)), van: raak[0].u.tijd, tot: new Date(raak[raak.length - 1].u.tijd.getTime() + 3600000) };
    }).filter(Boolean).sort((x, y) => y.n - x.n);
    const niveau = punten.length ? Math.max(...punten.map(p => p.n)) : 0;
    const [woord, kleur] = NIVEAUS[niveau];
    return { niveau, woord, kleur, punten, van: venster[0]?.tijd, tot: venster.length ? new Date(venster[venster.length - 1].tijd.getTime() + 3600000) : null };
  };

  // Een wedstrijddag: de uren van 08:00 tot 20:00 (is dat er niet, dan alles)
  const dag = (uren, vocht) => {
    let venster = uren.filter(u => { const h = u.tijd.getHours(); return h >= 8 && h < 20; });
    if (!venster.length) venster = uren;
    return toets(venster, vocht, uren);
  };

  // Een wedstrijd of training: de uren vanaf het uur van de aanvang tot een uur na het einde (wedstrijd 70 minuten, training zoals opgegeven).
  // verwachting = ruwe uren van de verwachting (sensor.weer_verwachting_hockey). null als de verwachting dat moment niet bevat.
  const moment = (start, einde, verwachting, vocht) => {
    const van = new Date(start), tot = new Date(einde ?? van.getTime() + 70 * 60000);
    const alle = verwachting.map(uitUur);
    const venster = alle.filter(u => u.tijd.getTime() + 3600000 > van.getTime() && u.tijd.getTime() < tot.getTime());
    // De verwachting moet het begin van de wedstrijd dekken
    if (!venster.length || venster[0].tijd.getTime() > van.getTime()) return null;
    return toets(venster, vocht, alle);
  };

  // De keuring van de thuisvereniging: uiterlijk 07:00 voor wedstrijden tot 10:30, 08:30 tot 13:00, 11:00 daarna (Procedure afgelastingen 2026)
  const keuring = tijd => {
    const [h, m] = String(tijd ?? '').split(':').map(Number);
    if (isNaN(h)) return null;
    const minuten = h * 60 + (m || 0);
    return minuten < 10 * 60 + 30 ? '07:00' : minuten < 13 * 60 ? '08:30' : '11:00';
  };

  const periode = p => `${uur(p.van)} tot ${uur(p.tot)}`;

  const punten = (g, metTijd = true) => g.punten.length ? g.punten.map(p => `<div style="padding:8px 0;border-top:1px solid rgba(255,255,255,0.08);font-size:12.5px">
      <div style="display:flex;justify-content:space-between;gap:8px"><b style="color:${p.n === 2 ? '#ff6b6b' : '#ffb060'}">${p.n === 2 ? 'Reden om af te keuren' : 'Let op'}${metTijd ? ` · ${periode(p)}` : ` · ${esc(p.kort)}`}</b><span style="opacity:.6;white-space:nowrap">${p.officieel ? 'KNHB-regel' : 'eigen inschatting'}</span></div>
      <div style="opacity:.8;margin-top:2px">${p.tekst}</div></div>`).join('')
    : `<div style="padding:8px 0;border-top:1px solid rgba(255,255,255,0.08);font-size:12.5px;opacity:.8">In de verwachting staat niets wat volgens het KNHB Weerprotocol reden is om af te keuren.</div>`;

  // Advies per soort veld (Weerprotocol). soort = 'water' | 'kunstgras' | undefined (dan beide)
  const velden = (g, soort) => {
    const heeft = (...ids) => g.punten.some(p => ids.includes(p.id));
    const vorstig = heeft('vorst', 'vorst-extreem', 'ijzel', 'sneeuw');
    const lijst = [
      { soort: 'kunstgras', naam: 'Zand- of semizandkunstgras', icon: 'mdi:grain', niveau: g.niveau,
        tekst: vorstig ? 'Vorst op zand is geen probleem voor het gras, wel voor het speeloppervlak: hard door bevriezing, oneffenheden door bevroren zand en bladresten, plaatselijk glad (vooral in de schaduw) en onvoorspelbaar balgedrag. Is een van die dingen zo, dan mag het veld worden afgekeurd wegens blessuregevaar. Test: sla een paar ballen in de lengte en de breedte van het veld. Rijp op zich is geen reden, glad worden wel; lichte rijp kan met een sleepnet weg.'
          : heeft('dooi') ? 'Na de vorst: controleer vooral of het veld al ontdooid en niet meer hard of glad is.' : 'Geen bijzonderheden in het Weerprotocol voor dit weer.' },
      { soort: 'water', naam: 'Waterkunstgras', icon: 'mdi:water', niveau: vorstig ? Math.max(g.niveau, 2) : g.niveau,
        tekst: vorstig ? 'Een waterveld moet nat bespeeld worden. In de winter staan de waterinstallaties meestal uit, dus het veld kan niet nat gemaakt worden. Een droger waterveld geeft meer grip en een hogere balstuit, en met name de grip kan bij hoge snelheid tot blessures leiden (Weerprotocol). Bij vorst komt daar mijn eigen inschatting bij dat het water bevriest: het veld is dan niet nat te maken en niet veilig.'
          : 'Controleer of het veld vochtig genoeg is en of het veilig is om op te spelen: de vraag is of bespelen een onnodig risico voor de spelers geeft (Weerprotocol). Droog weer zonder besproeiing geeft meer grip en een hogere balstuit.' }
    ].filter(v => !soort || v.soort === soort);
    return lijst.map(v => { const [w, k] = v.niveau === 2 ? ['Afkeuren', '#ff6b6b'] : v.niveau === 1 ? ['Let op', '#ffb060'] : ['Kan spelen', '#4ade80'];
      return `<div style="padding:8px 0;border-top:1px solid rgba(255,255,255,0.08);font-size:12.5px"><div style="display:flex;justify-content:space-between;gap:8px;align-items:center"><b style="display:flex;align-items:center;gap:6px"><ha-icon icon="${v.icon}" style="--mdc-icon-size:16px"></ha-icon>${v.naam}</b><span style="padding:2px 10px;border-radius:8px;font-weight:700;color:${k};background:${k}22">${w}</span></div><div style="opacity:.8;margin-top:3px">${v.tekst}</div></div>`; }).join('');
  };

  const procedure = () => `<b>Wie beslist en wanneer</b> (Procedure afgelastingen 2026): de thuisspelende vereniging keurt het veld, een team kan niet zelf afzeggen (dat geldt als niet opkomen). Keuring uiterlijk 07:00 voor wedstrijden tot 10:30, 08:30 voor wedstrijden tussen 10:30 en 13:00 en 11:00 voor wedstrijden na 13:00. Is er geen bespeelbaar veld, dan wordt afgelast door het bestuur of de wedstrijdsecretaris, na akkoord van de competitieleiding. De scheidsrechter kan het veld ook ter plekke afkeuren. Bij een centrale afgelasting door de KNHB (altijd bij KNMI code rood) staat dat op knhb.nl/afgelastingen. Reis je ver, dan bepaalt de afreizende vereniging of komen verantwoord is.`;

  window.huisHockey = { REGELS, NIVEAUS, toets, dag, moment, keuring, punten, velden, procedure, uur, esc, uitUur, periode };
  window.dispatchEvent(new Event('huis-hockey-klaar'));
})();
