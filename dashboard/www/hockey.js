// Hockey: wanneer een wedstrijd of training afgelast moet worden, volgens de regels van Union (unionhockey.nl/weerprotocol) en de KNHB
// (Weerprotocol en Procedure afgelastingen 2026, knhb.nl). Union is strenger dan de KNHB (bijvoorbeeld vorst: vanaf 0 graden niet betreden), dus voor
// de velden van Union gaat het Union-protocol voor.
// Wordt geladen door de weerkaart (het hockeyveld en de melding), de kaart "Buitenactiviteiten" (samenvatting) en de pagina Hockey.
// window.huisHockey = { toets, wedstrijd, dag, velden, procedure, regels, melding, ... }
//
// bron: 'Union' en 'KNHB' staan zo in de stukken (de getallen die we daarbij gebruiken ook); 'eigen' is een eigen inschatting, omdat de stukken daar
// geen getal voor noemen. niveau 1 = let op / keuring afwachten, niveau 2 = afkeuren ligt voor de hand.
(() => {
  const REGELS = [
    { id: 'sneeuw', bron: 'Union', test: u => u.f.condition === 'snowy' ? 2 : 0,
      kort: 'sneeuw',
      tekst: 'Sneeuw: bij sneeuw, vorst, ijzel of rijp wordt het veld afgekeurd. Een met sneeuw bedekt veld betreden is verboden (ijsvorming, schade aan de mat), en sneeuwvrij maken met bezems, schuivers of sproeiers ook (Union Weerprotocol). De KNHB: "advies: veld niet bespelen, afgelasting door thuisvereniging".' },
    { id: 'ijzel', bron: 'Union', test: u => u.t <= 0 && (u.r > 0 || ['snowy-rainy', 'hail'].includes(u.f.condition)) ? 2 : ['snowy-rainy', 'hail'].includes(u.f.condition) ? 1 : 0,
      kort: 'ijzel of natte sneeuw',
      tekst: 'Neerslag bij vorst: ijzel maakt het veld glad en bevroren, dan wordt het veld afgekeurd (Union Weerprotocol). Ook de KNHB: "bespeling is tijdelijk niet mogelijk".' },
    { id: 'vorst', bron: 'Union', test: u => u.t <= 0 ? 2 : 0,
      kort: 'vorst, niet betreden',
      tekst: 'Vorst: vanaf 0 graden en lager worden de velden niet betreden, vanwege mogelijke beschadiging van het kunstgras, en bij langdurige vorst wordt het veld afgekeurd vanwege de gevaarlijk harde onderlaag (Union Weerprotocol). De KNHB: een bevroren veld is "advies: veld niet bespelen, afgelasting door thuisvereniging", met veiligheid als uitgangspunt; droge kale vorst is voor kunstgras zelf geen probleem, extreme vorst onder -10° wel.' },
    { id: 'opvriezen', bron: 'eigen', test: u => u.t > 0 && u.t <= 4 && u.gevoel <= 0 && u.vocht >= 85 ? 1 : u.t > 0 && u.t <= 4 && u.min4 <= -2 ? 1 : 0,
      kort: 'veld kan opvriezen of nog bevroren zijn',
      tekst: 'Ook boven 0° kan het veld opvriezen door de gevoelstemperatuur in combinatie met de luchtvochtigheid. Je herkent het aan glinstering op het veld, een hol klinkende bal en grasvezels die niet meer flexibel zijn; dat is altijd interpretatie ter plekke (Union Weerprotocol). De grenzen die ik hier gebruik (gevoelstemperatuur van 0° of lager bij minstens 85% vochtigheid, of een uur na een vorstperiode van -2° of kouder) zijn een eigen inschatting.' },
    { id: 'windchill', bron: 'KNHB', test: u => u.gevoel <= -20 ? 2 : u.gevoel <= -15 ? 1 : 0,
      kort: 'gevoelstemperatuur onder -15°',
      tekst: 'Gevoelstemperatuur onder -15° (KNMI code geel) of -20° (code oranje): lokale beoordeling door de vereniging, met de leeftijd van de spelers (KNHB Weerprotocol, tabel). Union: extra voorzichtig bij de jongste spelers (O8, O9, O10). Bij -20° is afkeuren mijn inschatting.' },
    { id: 'knmi', bron: 'Union', test: u => u.code === 'rood' || u.code === 'oranje' ? 2 : u.code === 'geel' ? 1 : 0,
      kort: 'KNMI-waarschuwing',
      tekst: 'KNMI code rood of oranje: algehele afgelasting, zonder uitzonderingen (Union Weerprotocol, trainingen). Code geel: overleg ter plekke tussen de technisch manager en de clubmanager. De KNHB: code rood is afgelasting, code oranje in beginsel tot 11:00 uur afgelasting, code geel lokaal.' },
    { id: 'onweer', bron: 'KNHB', test: u => ['lightning', 'lightning-rainy'].includes(u.f.condition) ? 1 : 0,
      kort: 'onweer',
      tekst: 'Onweer: niet vooraf afgelasten, maar de scheidsrechter staakt zodra er minder dan 10 seconden tussen flits en knal zit (KNHB). Tijdens een training kunnen teams schuilen in het clubhuis, de dug-out is geen veilige plek.' },
    { id: 'mist', bron: 'Union', test: u => u.f.condition === 'fog' ? 1 : 0,
      kort: 'mist',
      tekst: 'Mist: de doelen aan de andere kant van het veld moeten zichtbaar zijn (Union Weerprotocol, speelveldcriteria). Dat beoordeel je ter plekke.' },
    { id: 'regen', bron: 'Union', test: u => u.r >= 2.5 ? 1 : 0,
      kort: 'veel regen',
      tekst: 'Regen: het veld wordt alleen afgekeurd als er langdurig plassen blijven staan, zodat de bal niet meer rolt (Union Weerprotocol); staat het veld blank, dan is het onbespeelbaar (KNHB). Of het zover komt kan de verwachting niet zeggen; vanaf 2,5 mm per uur is dat mijn waarschuwing.' },
    { id: 'storm', bron: 'eigen', test: u => u.st >= 90 ? 2 : u.st >= 75 ? 1 : 0,
      kort: 'storm',
      tekst: 'Storm: een zware storm valt onder het weerbeleid van de KNHB, dat de KNMI-codes volgt (geel: lokaal, oranje: afgelasting tot 11:00 uur, rood: afgelasting). Geen van beide noemt een windsnelheid; 75 en 90 km/h in windstoten zijn een eigen inschatting. Kijk op knmi.nl.' },
    { id: 'hitte', bron: 'KNHB', test: u => u.t >= 30 ? 1 : 0,
      kort: 'hitte',
      tekst: 'Warm weer: er wordt gewoon gehockeyd. Alleen in uiterste gevallen kan de vereniging het veld afkeuren omdat het voor de keeper niet verantwoord is. Inkorten van de wedstrijd mag niet (KNHB Weerprotocol). 30° is het voorbeeld dat de KNHB noemt.' }
  ];

  // Geen regel van Union of de KNHB maar een voorzorg: direct na een afgelasting kan het veld nog niet goed zijn
  const NAWERKING = { id: 'nawerking', bron: 'eigen', n: 1, test: () => 0, kort: 'kort na het afgelasten',
    tekst: 'Direct na een periode met afgelasten (vorst, ijzel, veel water) kan het veld nog bevroren, glad of nat zijn terwijl het weer al beter is. Laat het veld ter plekke keuren. De drie uur en de 3° zijn een eigen inschatting; Union en de KNHB noemen daar geen grens voor.' };
  REGELS.push(NAWERKING);
  const gevoelsTemp = u => u.t <= 10 && u.w > 4.8 ? 13.12 + 0.6215 * u.t - 11.37 * Math.pow(u.w, 0.16) + 0.3965 * u.t * Math.pow(u.w, 0.16) : u.t;
  const uur = d => `${String(d.getHours()).padStart(2, '0')}:00`;
  const dagSleutel = d => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
  // De dag erbij als het niet vandaag is ("morgen 06:00"), zodat een tijdvak over middernacht niet als "12:00 tot 10:00" leest
  const dagLabel = d => { const verschil = Math.round((dagSleutel(d) - dagSleutel(new Date())) / 86400000);
    return verschil === 0 ? '' : verschil === 1 ? 'morgen ' : verschil === -1 ? 'gisteren ' : `${d.toLocaleDateString('nl-NL', { weekday: 'short' })} `; };
  const tijdLabel = d => `${dagLabel(d)}${uur(d)}`;
  // van tot tot (tot is het eind, dus het uur erna); op dezelfde dag zonder de dag twee keer te noemen
  const span = (van, tot) => dagSleutel(van) === dagSleutel(new Date(tot.getTime() - 1)) ? `${tijdLabel(van)} tot ${uur(tot)}` : `${tijdLabel(van)} tot ${tijdLabel(tot)}`;
  const NIVEAUS = [['Niet afgelasten', '#4ade80'], ['Let op: wacht de keuring af', '#ffb060'], ['Afgelasten ligt voor de hand', '#ff6b6b']];
  const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  // De naam van een veld zoals Union hem in het schema zet, en zoals wij hem noemen: het Rabobank-veld is het D-veld
  const veldNaam = naam => String(naam ?? '').replace(/^rabobank[- ]?veld/i, 'D-veld');
  // Jeugd: een team tot en met O18 (Meisjes O14-2, Jongens O10-Groen, O7-rood MIX); senioren (Dames 2, Dames Onder 25-1, Heren 45-1) niet
  const isJeugd = m => {
    const o = /(?:^|[\s/])O(\d{1,2})(?!\d)/.exec(m.team ?? '');
    if (o) return parseInt(o[1], 10) <= 18;
    const c = /^Onder (\d{1,2})$/.exec(m.category ?? '');
    return c ? parseInt(c[1], 10) <= 18 : /^mini/i.test(m.category ?? '');
  };
  const bronNaam = b => b === 'eigen' ? 'eigen inschatting' : `${b}-regel`;

  // Een uur van de verwachting als de uren die de kaart ook gebruikt: { tijd, t, w, st, r, f }
  const uitUur = f => {
    const w = parseFloat(f.wind_speed) || 0;
    return { tijd: new Date(f.datetime), t: parseFloat(f.temperature), w, st: parseFloat(f.wind_gust_speed) || w, r: parseFloat(f.precipitation) || 0, f };
  };

  // De omstandigheden naast de verwachting: de luchtvochtigheid van nu en de KNMI-code. Een getal telt als luchtvochtigheid.
  const context = c => typeof c === 'object' && c !== null ? c : { vocht: c };

  // De uren (met t, w, st, r, f en tijd) toetsen aan de regels. alle = alle uren van de verwachting, voor de koude van de uren daarvoor
  const toets = (venster, omstandigheden, alle) => {
    const lijst = alle ?? venster, { vocht, code } = context(omstandigheden);
    // Per uur: welke regels gelden en hoe zwaar (van alle uren, want na een afgelasting telt het eerdere uur mee)
    const alleUren = lijst.map((u, i) => {
      const min4 = Math.min(...lijst.slice(Math.max(0, i - 3), i + 1).map(x => x.t));
      const treffers = REGELS.map(regel => ({ regel, n: regel.test({ ...u, vocht, code, gevoel: gevoelsTemp(u), min4 }) })).filter(x => x.n > 0);
      return { u, treffers, niveau: treffers.length ? Math.max(...treffers.map(x => x.n)) : 0 };
    });
    // Na een periode met afgelasten staat er altijd "Let op": het veld kan nog bevroren of nat zijn. Dat duurt tot het echt weg is
    // (meer dan 3 uur later en boven de 3°) of tot er door iets anders weer "Let op" staat.
    let sindsAfgelasten = null;
    alleUren.forEach(x => {
      if (x.niveau === 2) { sindsAfgelasten = 0; return; }
      if (sindsAfgelasten === null) return;
      sindsAfgelasten += 1;
      if (x.niveau === 0 && (sindsAfgelasten <= 3 || x.u.t <= 3)) { x.treffers = [{ regel: NAWERKING, n: 1 }]; x.niveau = 1; }
      else if (x.niveau === 0) sindsAfgelasten = null;
    });
    const perUur = venster.map(u => alleUren[lijst.indexOf(u)]);
    const punten = REGELS.map(regel => {
      const raak = perUur.filter(x => x.treffers.some(t => t.regel === regel));
      if (!raak.length) return null;
      // De uren die samen vallen vormen een tijdvak; zijn er gaten, dan zijn het meer tijdvakken
      const perioden = [];
      raak.forEach(({ u }) => {
        const laatste = perioden[perioden.length - 1];
        if (laatste && u.tijd.getTime() - laatste.tot.getTime() < 1000) laatste.tot = new Date(u.tijd.getTime() + 3600000);
        else perioden.push({ van: u.tijd, tot: new Date(u.tijd.getTime() + 3600000) });
      });
      return { ...regel, n: Math.max(...raak.map(x => x.treffers.find(t => t.regel === regel).n)), perioden, van: perioden[0].van, tot: perioden[perioden.length - 1].tot };
    }).filter(Boolean).sort((x, y) => y.n - x.n);
    // De tijdlijn: uren met dezelfde situatie vormen een tijdvak, met de reden waarom het wel of niet kan
    const tijdlijn = [];
    perUur.forEach(x => {
      const sleutel = `${x.niveau}:${x.treffers.map(t => t.regel.id).sort().join(',')}`;
      const laatste = tijdlijn[tijdlijn.length - 1];
      const einde = new Date(x.u.tijd.getTime() + 3600000);
      if (laatste && laatste.sleutel === sleutel && x.u.tijd.getTime() - laatste.tot.getTime() < 1000) { laatste.tot = einde; laatste.uren.push(x.u); }
      else tijdlijn.push({ sleutel, niveau: x.niveau, regels: x.treffers.map(t => t.regel), van: x.u.tijd, tot: einde, uren: [x.u] });
    });
    const niveau = punten.length ? Math.max(...punten.map(p => p.n)) : 0;
    const [woord, kleur] = NIVEAUS[niveau];
    return { niveau, woord, kleur, punten, tijdlijn, van: venster[0]?.tijd, tot: venster.length ? new Date(venster[venster.length - 1].tijd.getTime() + 3600000) : null };
  };

  // Een wedstrijddag: de uren van 08:00 tot 20:00 (is dat er niet, dan alles)
  const dag = (uren, omstandigheden) => {
    let venster = uren.filter(u => { const h = u.tijd.getHours(); return h >= 8 && h < 20; });
    if (!venster.length) venster = uren;
    return toets(venster, omstandigheden, uren);
  };

  // Een wedstrijd of training: de uren vanaf het uur van de aanvang tot een uur na het einde (wedstrijd 70 minuten, training zoals opgegeven).
  // verwachting = ruwe uren van de verwachting (sensor.weer_verwachting_hockey). null als de verwachting dat moment niet bevat.
  const moment = (start, einde, verwachting, omstandigheden) => {
    const van = new Date(start), tot = new Date(einde ?? van.getTime() + 70 * 60000);
    const alle = verwachting.map(uitUur);
    const venster = alle.filter(u => u.tijd.getTime() + 3600000 > van.getTime() && u.tijd.getTime() < tot.getTime());
    // De verwachting moet het begin van de wedstrijd dekken
    if (!venster.length || venster[0].tijd.getTime() > van.getTime()) return null;
    return toets(venster, omstandigheden, alle);
  };

  // De keuring van de thuisvereniging: uiterlijk 07:00 voor wedstrijden tot 10:30, 08:30 tot 13:00, 11:00 daarna (Union en KNHB)
  const keuring = tijd => {
    const [h, m] = String(tijd ?? '').split(':').map(Number);
    if (isNaN(h)) return null;
    const minuten = h * 60 + (m || 0);
    return minuten < 10 * 60 + 30 ? '07:00' : minuten < 13 * 60 ? '08:30' : '11:00';
  };

  const periode = p => p.perioden.slice(0, 3).map(r => span(r.van, r.tot)).join(', ') + (p.perioden.length > 3 ? ' en later' : '');

  const punten = (g, metTijd = true) => g.punten.length ? g.punten.map(p => `<div style="padding:8px 0;border-top:1px solid rgba(255,255,255,0.08);font-size:12.5px">
      <div style="display:flex;justify-content:space-between;gap:8px"><b style="color:${p.n === 2 ? '#ff6b6b' : '#ffb060'}">${p.n === 2 ? 'Reden om af te lasten' : 'Let op'}${metTijd ? ` · ${periode(p)}` : ` · ${esc(p.kort)}`}</b><span style="opacity:.6;white-space:nowrap">${bronNaam(p.bron)}</span></div>
      <div style="opacity:.8;margin-top:2px">${p.tekst}</div></div>`).join('')
    : `<div style="padding:8px 0;border-top:1px solid rgba(255,255,255,0.08);font-size:12.5px;opacity:.8">In de verwachting staat niets wat volgens het weerprotocol van Union of de KNHB reden is om af te lasten.</div>`;

  // Waarom het in een tijdvak wel kan: de waarden van die uren in een zin
  const kanBecause = uren => {
    const ts = uren.map(u => Math.round(u.t)), wmax = Math.round(Math.max(...uren.map(u => u.st))), rmax = Math.max(...uren.map(u => u.r));
    const temp = Math.min(...ts) === Math.max(...ts) ? `${ts[0]}°` : `${Math.min(...ts)}° tot ${Math.max(...ts)}°`;
    return `${temp}, ${rmax >= 0.1 ? `regen tot ${String(Math.round(rmax * 10) / 10).replace('.', ',')} mm per uur` : 'droog'}, windstoten tot ${wmax} km/h: boven het vriespunt en geen reden uit het protocol om af te lasten`;
  };

  // Per tijdvak wat er aan de hand is: kan het, kan het niet en waarom
  const tijdlijn = g => !g.tijdlijn?.length ? '' : g.tijdlijn.map(t => {
    const [kleur, woord] = t.niveau === 2 ? ['#ff6b6b', 'Afgelasten'] : t.niveau === 1 ? ['#ffb060', 'Let op'] : ['#4ade80', 'Kan'];
    const redenen = t.niveau === 0 ? kanBecause(t.uren)
      : t.regels.sort((x, y) => y.n - x.n).map(r => `${esc(r.kort)} <span style="opacity:.55">(${bronNaam(r.bron)})</span>`).join('<br>');
    return `<div style="display:grid;grid-template-columns:96px 1fr;gap:2px 10px;padding:8px 0;border-top:1px solid rgba(255,255,255,0.08);font-size:12.5px;align-items:start">
        <div><div style="font-weight:700">${span(t.van, t.tot)}</div><span style="display:inline-block;margin-top:3px;padding:2px 9px;border-radius:8px;font-weight:700;color:${kleur};background:${kleur}22">${woord}</span></div>
        <div style="opacity:.85;align-self:center">${redenen}</div></div>`;
  }).join('');

  // De uitleg van elke regel die meespeelt, één keer
  const uitleg = g => g.punten.length ? g.punten.map(p => `<div style="padding:8px 0;border-top:1px solid rgba(255,255,255,0.08);font-size:12.5px">
      <div style="display:flex;justify-content:space-between;gap:8px"><b style="color:${p.n === 2 ? '#ff6b6b' : '#ffb060'}">${esc(p.kort)}</b><span style="opacity:.6;white-space:nowrap">${bronNaam(p.bron)}</span></div>
      <div style="opacity:.8;margin-top:2px">${p.tekst}</div></div>`).join('')
    : `<div style="padding:8px 0;font-size:12.5px;opacity:.8">Er speelt geen enkele regel mee.</div>`;

  // Advies per soort veld. Het protocol van Union maakt geen onderscheid; het KNHB Weerprotocol wel. soort = 'water' | 'kunstgras' | undefined (dan beide)
  const velden = (g, soort) => {
    const heeft = (...ids) => g.punten.some(p => ids.includes(p.id));
    const vorstig = heeft('vorst', 'ijzel', 'sneeuw');
    const lijst = [
      { soort: 'kunstgras', naam: 'Zand- of semizandkunstgras', icon: 'mdi:grain', niveau: g.niveau,
        tekst: vorstig ? 'Union: vanaf 0° wordt het veld niet betreden. De KNHB legt uit waarom: vorst is voor het gras zelf geen probleem, wel voor het speeloppervlak: hard door bevriezing, oneffenheden door bevroren zand en bladresten, plaatselijk glad (vooral in de schaduw) en onvoorspelbaar balgedrag. Is een van die dingen zo, dan mag het veld worden afgekeurd wegens blessuregevaar. Test: sla een paar ballen in de lengte en de breedte van het veld. Lichte rijp kan met een sleepnet weg.'
          : heeft('opvriezen') ? 'Controleer ter plekke of het veld glinstert, hol klinkt of niet meer soepel is, en laat een bal rollen: hij moet rollen en niet stuiteren (Union).' : 'Geen bijzonderheden voor dit weer.' },
      { soort: 'water', naam: 'Waterkunstgras', icon: 'mdi:water', niveau: g.niveau,
        tekst: vorstig ? 'Union: vanaf 0° wordt het veld niet betreden, ook een waterveld niet. De KNHB: een waterveld moet nat bespeeld worden, maar in de winter staan de waterinstallaties meestal uit, dus het veld is niet nat te maken. Een droger waterveld geeft meer grip en een hogere balstuit, en met name de grip kan bij hoge snelheid tot blessures leiden. Dat het water op het veld kan bevriezen is mijn eigen aanvulling.'
          : 'Controleer of het veld vochtig genoeg is en of het veilig is om op te spelen: de vraag is of bespelen een onnodig risico voor de spelers geeft (KNHB). Droog weer zonder besproeiing geeft meer grip en een hogere balstuit.' }
    ].filter(v => !soort || v.soort === soort);
    return lijst.map(v => { const [w, k] = v.niveau === 2 ? ['Afgelasten', '#ff6b6b'] : v.niveau === 1 ? ['Let op', '#ffb060'] : ['Kan spelen', '#4ade80'];
      return `<div style="padding:8px 0;border-top:1px solid rgba(255,255,255,0.08);font-size:12.5px"><div style="display:flex;justify-content:space-between;gap:8px;align-items:center"><b style="display:flex;align-items:center;justify-content:flex-start;gap:6px"><ha-icon icon="${v.icon}" style="--mdc-icon-size:16px"></ha-icon>${v.naam}</b><span style="padding:2px 10px;border-radius:8px;font-weight:700;color:${k};background:${k}22">${w}</span></div><div style="opacity:.8;margin-top:3px">${v.tekst}</div></div>`; }).join('');
  };

  const procedure = () => `<b>Wedstrijden</b> (Union en KNHB): de thuisvereniging keurt het veld, bij Union het wedstrijdsecretariaat (junioren op zaterdag, senioren op zondag) samen met de clubmanager. Keuring uiterlijk 07:00 voor wedstrijden tot 10:30, 08:30 voor wedstrijden tussen 10:30 en 13:00 en 11:00 voor wedstrijden na 13:00. Union informeert de bezoekende club via LISA en zet een bericht op de website en in de app. Een team kan niet zelf afzeggen (dat geldt als niet opkomen). De scheidsrechter kan het veld ook ter plekke afkeuren of de wedstrijd staken. Bij een centrale afgelasting door de KNHB (altijd bij KNMI code rood) staat dat op knhb.nl/afgelastingen. Reis je ver, dan bepaalt de afreizende vereniging of komen verantwoord is.<br><br><b>Trainingen</b> (Union): de clubmanager en/of technisch manager beslist, uiterlijk 1 uur voordat de eerste training begint, en plaatst een bericht op de website en in de app. Trainers beslissen niet zelf over het doorgaan van een training. Bij onweer of een dreigend onbespeelbaar veld kunnen teams schuilen in het clubhuis.`;

  // ---- wat moet een persoon vandaag (en een wedstrijd al de avond ervoor)
  const MARGE_MIN = 15;          // marge bovenop de reistijd
  const AANKOMST_WEDSTRIJD = 20; // minuten voor de aanvang aanwezig op het eigen veld
  const AANKOMST_TRAINING = 10;
  const NA_UREN = 17;            // vanaf dit uur de avond ervoor laten we de wedstrijd van morgen zien

  // De ANWB-route van huis naar de hockey van een persoon: sensor.anwb_route_thuis_hockey_<persoon>_*. Zonder eigen route de eerste die er is.
  const route = (states, slug) => {
    const eigen = states[`sensor.anwb_route_thuis_hockey_${slug}_reistijd`];
    const bron = eigen ?? Object.values(states).find(x => /^sensor\.anwb_route_thuis_hockey_.*_reistijd$/.test(x.entity_id));
    const min = parseFloat(bron?.state);
    if (isNaN(min)) return null;
    return { reis: min, normaal: parseFloat(bron.attributes.normal_duration_min) || min };
  };

  // De teams van een persoon: de sensoren van de integratie waar deze persoon aan gekoppeld is
  const teamsVan = (states, persoonId) => Object.values(states)
    .filter(x => x.entity_id.startsWith('sensor.union_') && x.attributes.team_id && x.attributes.person === persoonId);

  // De omstandigheden van nu bij Union: de luchtvochtigheid en de KNMI-code (none, geel, oranje, rood)
  const omstandigheden = states => ({
    vocht: parseFloat(states['sensor.weer_nijmegen_luchtvochtigheid']?.state),
    code: String(states['sensor.weer_nijmegen_waarschuwingscode']?.state ?? 'none').toLowerCase()
  });

  // opties: { persoon: 'carleen', proef: false }. proef = test: de eerstvolgende dingen van het team, ook als het niet vandaag is.
  const persoon = (states, { persoon: slug, proef = false, nu = new Date() }) => {
    let teams = teamsVan(states, `person.${slug}`);
    if (!teams.length && proef) teams = Object.values(states).filter(x => x.entity_id.startsWith('sensor.union_') && x.attributes.team_id).slice(0, 1);
    if (!teams.length) return { teams: [], items: [] };
    const dagStart = d => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
    const vandaag = dagStart(nu), morgen = vandaag + 86400000;
    const verwachting = (states['sensor.weer_verwachting_hockey']?.attributes.forecast?.length ? states['sensor.weer_verwachting_hockey'] : states['sensor.weer_verwachting_per_uur'])?.attributes.forecast ?? [];
    const ctx = omstandigheden(states);
    const ritje = route(states, slug);
    const items = [];
    teams.forEach(t => {
      const a = t.attributes;
      (a.matches ?? []).filter(m => m.start).forEach(m => items.push({ soort: 'Wedstrijd', team: a.team, start: new Date(m.start), einde: new Date(new Date(m.start).getTime() + 70 * 60000),
        thuis: m.home, tegen: m.opponent, veld: m.field, plaats: [m.location, m.city].filter(Boolean).join(', '), afgelast: m.cancelled, opmerking: m.remarks, veldsoort: m.field_type,
        aankomst: m.arrival_time ? (([h, mi]) => new Date(new Date(m.start).setHours(h, mi, 0, 0)))(m.arrival_time.split(':').map(Number)) : null }));
      (a.trainings ?? []).forEach(x => items.push({ soort: 'Training', team: a.team, start: new Date(x.start), einde: new Date(x.end), thuis: true, veld: x.field, plaats: 'Union', afgelast: x.cancelled, opmerking: x.comment, aankomst: null }));
    });
    const zichtbaar = items.filter(i => i.einde.getTime() >= nu.getTime() - 15 * 60000).filter(i => {
      if (proef) return true;
      const d = dagStart(i.start);
      if (d === vandaag) return true;
      return i.soort === 'Wedstrijd' && d === morgen && nu.getHours() >= NA_UREN;
    }).sort((x, y) => x.start - y.start).slice(0, proef ? 3 : 8);
    zichtbaar.forEach(i => {
      const d = dagStart(i.start);
      i.wanneer = d === vandaag ? 'Vandaag' : d === morgen ? 'Morgen' : i.start.toLocaleDateString('nl-NL', { weekday: 'long', day: 'numeric', month: 'long' });
      // Het weer: alleen waar de thuisvereniging beslist (wedstrijden thuis en trainingen bij Union)
      i.oordeel = i.thuis && !i.afgelast && verwachting.length ? moment(i.start, i.einde, verwachting, ctx) : null;
      // Het vertrek: alleen als de auto naar Union gaat
      if (i.thuis && ritje && !i.afgelast) {
        i.aankomst = i.aankomst ?? new Date(i.start.getTime() - (i.soort === 'Wedstrijd' ? AANKOMST_WEDSTRIJD : AANKOMST_TRAINING) * 60000);
        const dichtbij = i.start.getTime() - nu.getTime() < 3 * 3600000;
        const reis = dichtbij ? ritje.reis : ritje.normaal;
        i.vertrek = new Date(i.aankomst.getTime() - (reis + MARGE_MIN) * 60000);
        i.reis = Math.round(reis);
        i.actueel = dichtbij;
      }
    });
    return { teams: teams.map(t => t.attributes.team), items: zichtbaar };
  };

  // De melding voor het dashboard: de eerstvolgende training of wedstrijd van de persoon als titel en regel. null als er niets te melden is.
  const melding = (states, opties) => {
    const item = persoon(states, opties).items[0];
    if (!item) return null;
    const tijd = d => d.toLocaleTimeString('nl-NL', { hour: '2-digit', minute: '2-digit' });
    const naam = opties.naam ?? opties.persoon;
    const wanneer = item.wanneer === 'Vandaag' ? '' : item.wanneer === 'Morgen' ? 'morgen ' : `${item.start.toLocaleDateString('nl-NL', { weekday: 'short', day: 'numeric', month: 'short' })} `;
    const titel = `${naam}: ${item.soort === 'Wedstrijd' ? `wedstrijd tegen ${item.tegen}` : 'training'} ${wanneer}${tijd(item.start)}`;
    const delen = [];
    if (item.afgelast) delen.push('afgelast');
    else {
      const laat = item.vertrek && item.vertrek.getTime() <= Date.now() && item.start.getTime() > Date.now();
      if (item.vertrek) delen.push(laat ? 'nu vertrekken' : `vertrek ${tijd(item.vertrek)}`);
      else if (item.aankomst) delen.push(`verzamelen ${tijd(item.aankomst)}`);
      if (item.oordeel && item.oordeel.niveau > 0) delen.push(`${item.oordeel.niveau === 2 ? 'waarschijnlijk afgelast' : 'let op het weer'} (${item.oordeel.punten.map(p => p.kort).join(', ')})`);
      else if (item.oordeel) delen.push('weer is geen probleem');
      if (!item.thuis) delen.push('uit, de thuisclub beslist');
    }
    const niveau = item.afgelast ? 2 : item.oordeel?.niveau ?? 0;
    return { item, titel, label: [veldNaam(item.veld), ...delen].filter(Boolean).join(' · '), kleur: item.afgelast || niveau === 2 ? '255, 107, 107' : niveau === 1 ? '255, 176, 96' : '255, 122, 156', niveau };
  };

  // Wedstrijden van vandaag (die nog moeten beginnen) en morgen waar het weer reden is om af te keuren: voor de melding "let op, misschien afgelastingen"
  // (senioren; de jeugd tot en met O18 telt niet mee). Geeft null als er niets te melden is.
  const risico = (states, nu = new Date()) => {
    const verwachting = (states['sensor.weer_verwachting_hockey']?.attributes.forecast?.length ? states['sensor.weer_verwachting_hockey'] : states['sensor.weer_verwachting_per_uur'])?.attributes.forecast ?? [];
    if (!verwachting.length) return null;
    const ctx = omstandigheden(states), sleutel = d => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
    const vandaag = sleutel(nu), morgen = vandaag + 86400000;
    const kandidaten = (states['sensor.union_thuiswedstrijden']?.attributes.matches ?? [])
      .filter(m => m.start && !m.cancelled && !isJeugd(m)).map(m => ({ m, start: new Date(m.start) }))
      .filter(x => x.start > nu && [vandaag, morgen].includes(sleutel(x.start)))
      .map(x => ({ ...x, dag: sleutel(x.start) === vandaag ? 'vandaag' : 'morgen', g: moment(x.start, null, verwachting, ctx) })).filter(x => x.g);
    for (const dag of ['vandaag', 'morgen']) {
      const rij = kandidaten.filter(x => x.dag === dag), kans = rij.filter(x => x.g.niveau > 0);
      if (!kans.length) continue;
      const hard = kans.filter(x => x.g.niveau === 2).length;
      const redenen = [...new Set(kans.flatMap(x => x.g.punten.map(p => p.kort)))];
      const keuringen = kans.map(x => keuring(x.m.time)).filter(Boolean).sort();
      return { dag, aantal: kans.length, van: rij.length, hard, niveau: hard ? 2 : 1, redenen, keuring: keuringen[0] ?? null,
        titel: `Let op: ${dag} ${hard ? 'waarschijnlijk' : 'misschien'} afgelastingen`,
        label: `${kans.length} van de ${rij.length} thuiswedstrijden van de senioren (${redenen.join(', ')})${keuringen[0] ? `. Eerste keuring ${keuringen[0]} uur` : ''}` };
    }
    return null;
  };

  // Heeft het team van de persoon vandaag gewonnen? (uit de uitslagen van de integratie.) proef = test: de laatste winst, ook als die niet van vandaag is.
  const winst = (states, { persoon: slug, naam, proef = false, nu = new Date() }) => {
    const p = n => String(n).padStart(2, '0'), vandaag = `${nu.getFullYear()}-${p(nu.getMonth() + 1)}-${p(nu.getDate())}`;
    for (const team of teamsVan(states, `person.${slug}`)) {
      const r = (team.attributes.results ?? []).find(x => x.outcome === 'W' && (proef || x.date === vandaag));
      if (r) return { titel: `${naam ?? slug} heeft gewonnen!`, label: `${team.attributes.team}: ${r.home_team} ${r.score_home} – ${r.score_away} ${r.away_team}`, resultaat: r };
    }
    return null;
  };

  window.huisHockey = { winst, risico,  REGELS, NIVEAUS, toets, dag, moment, keuring, punten, tijdlijn, uitleg, velden, procedure, uur, esc, uitUur, periode, persoon, melding, omstandigheden, span, veldNaam, isJeugd, MARGE_MIN };
  window.dispatchEvent(new Event('huis-hockey-klaar'));
})();
