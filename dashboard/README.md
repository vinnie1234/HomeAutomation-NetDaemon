# Huis-dashboard

YAML-dashboard voor Home Assistant.

## Mappen

| Pad | Inhoud |
|---|---|
| `dashboard.yaml` | Startpunt: templates en de lijst met pagina's |
| `views/` | Eén bestand per pagina |
| `popups/` | Pop-ups (Bubble Card): de kamers, thema, pakketten, taken en taak met plek; opgenomen in `views/home.yaml` (en `taak` ook in Huis en op de persoonlijke pagina's) |
| `templates/button_card_templates.yaml` | Gedeelde stijl en gedrag van tegels (kamer, persoon, actie, info) |
| `navbar.yaml` | Navigatiebalk, opgenomen in elke pagina |
| `cards/` | Gedeelde kaarten en lijsten: `agenda.yaml` (agenda en taken), `auto.yaml`, `todo_*.yaml` (de takenlijsten en de boodschappenlijst), `boodschappen_knop.yaml` (tegel op Huis), `taak_knop.yaml`, `boodschappen_gedaan_knop.yaml` (knop op Home, een uur na Boodschappen AH), `kerstmuziek_knop.yaml` (knop op Home, alleen zolang de kerstlampjes er zijn), `camera_woonkamer.yaml`, `power_sources.yaml` (de apparaten van "Grootste verbruikers nu"), ... |
| `ha/template.yaml` | Template-sensoren voor `/config/template.yaml`: verwachting per uur, brieven van deze week, of NetDaemon online is en de open issues en pull requests van de GitHub-repositories |
| `themes/glass_home.yaml` | Thema "Glass Home" |

## Benodigd uit HACS

Bubble Card, Mushroom, button-card, card-mod, Navbar Card, Universal Remote Card, auto-entities,
ApexCharts Card, mini-graph-card, clock-weather-card, upcoming-media-card, tabbed-card,
Gaming Status Cards, Calendar & Tasks Card en Kiosk Mode.

Verder in Home Assistant: de integraties Fully Kiosk Browser (de tablet), Stellantis (de auto) en Local To-do, met de
takenlijsten `todo.dagelijks` (heet "Samen"), `todo.vincent` en `todo.carleen`, plus de aparte boodschappenlijst `todo.boodschappen` (niet bij de taken of de agenda; wie de zone Boodschappen AH binnenkomt krijgt de open items per WhatsApp).

## Installeren (eenmalig)

1. Kopieer deze map naar Home Assistant als `/config/dashboards/huis/`
   (via de Samba-share: `\\192.168.50.189\config\dashboards\huis`).
2. Kopieer `themes/glass_home.yaml` naar `/config/themes/`, `www/idle-home.js` en `www/netdaemon-activiteit.js` naar `/config/www/huis/`, en de map `www/clouds` naar `/config/www/huis/clouds`
   (de wolken van de weerkaart; in het dashboard is dat `/local/huis/clouds/`). Bestaat `/config/www` nog niet,
   herstart HA dan na het aanmaken. De wolken zijn gemaakt met `tools/generate_clouds.py` (numpy en Pillow).
3. Voeg toe aan `configuration.yaml` (sla over wat er al staat):

   ```yaml
   frontend:
     themes: !include_dir_merge_named themes
     # Kiosk Mode al laden voordat het dashboard opent, anders verschijnt de bovenbalk soms toch
     extra_module_url:
       - /hacsfiles/kiosk-mode/kiosk-mode.js
       # card-mod vóór de kaarten laden, anders waarschuwt hij over prestaties
       - /hacsfiles/lovelace-card-mod/card-mod.js
       # Na 30 seconden niets doen terug naar Home (www/idle-home.js); ?v= ophogen na een wijziging
       - /local/huis/idle-home.js?v=3
       # De kaart NetDaemon activiteit (pagina Activiteit onder Beheer, www/netdaemon-activiteit.js); ?v= ophogen na een wijziging
       - /local/huis/netdaemon-activiteit.js?v=1

   lovelace:
     dashboards:
       dashboard-huis:
         mode: yaml
         title: Huis
         icon: mdi:home-variant
         show_in_sidebar: true
         filename: dashboards/huis/dashboard.yaml
   ```

4. Voeg de template-sensoren uit `ha/template.yaml` toe aan `/config/template.yaml` (in `configuration.yaml`:
   `template: !include template.yaml`). Staat er al een bestand, zet dan alleen de ontbrekende blokken erbij:
   - de verwachting per uur voor de strook op Weer (`Weer verwachting per uur`),
   - `Brieven deze week`: de PostNL-brieven van de laatste 7 dagen, voor het aantal en de scans,
   - `NetDaemon online`: uit als het levensteken van NetDaemon (elke minuut) een paar minuten wegblijft; het dashboard
     toont dan een melding met een herstartknop,
   - `Git open items`: de open issues en pull requests van alle repositories van de integratie GitHub, samen (de state) en per
     repository met sterren, forks, de laatste release en de laatste commit (het attribuut `repos`). Een repository erbij toevoegen
     in de integratie is genoeg; herlaad daarna de template-entiteiten, dan telt hij mee.
   - `NetDaemon MQTT werkt`: uit als het MQTT-levensteken van NetDaemon verouderd is. De sensoren die NetDaemon zelf aanmaakt
     lopen via MQTT, en als de broker de login weigert stopt dat publiceren zonder foutmelding. Het dashboard toont dan een
     oranje melding.

   Herlaad daarna de template-entiteiten (Ontwikkelhulpmiddelen → YAML).

5. Herstart Home Assistant. "Huis" staat daarna in de zijbalk.

De pagina's zetten zelf het thema "Glass Home", je hoeft je profiel niet aan te passen.

## Bijwerken

Kopieer de gewijzigde bestanden opnieuw naar `/config/dashboards/huis/` en laat Home Assistant ze opnieuw inlezen: een
YAML-dashboard wordt onthouden tot dat gebeurt, een gewone browser-refresh is dus niet genoeg. Dat kan met
rechtsboven ⋮ → Vernieuwen, of (Kiosk Mode verbergt dat menu) met Beheer → Dashboard → "Ververs dit scherm". Herstarten is alleen nodig na wijzigingen in `configuration.yaml`.
Na een wijziging aan het thema: Ontwikkelhulpmiddelen → YAML → Thema's herladen.

## Helpers

NetDaemon maakt de helpers zelf aan bij het opstarten (`automation/Configuration/HelperDefinitions.cs`), zoals het
werkrooster, de thema-keuze en het formulier van "Taak met plek". Het dashboard werkt dus pas helemaal nadat NetDaemon
een keer is gestart.

## Bediening

- Kamertegel: **tik** opent de pop-up, **lang drukken** zet alle lampen van die kamer aan of uit.
- Pop-up: lamp **schuiven** = helderheid, **tik op het icoon** = aan/uit. Omlaag swipen sluit.
- Meldingen bovenaan verschijnen alleen als ze relevant zijn; tik erop om naar de juiste pagina te gaan.
- Een bolletje bij een pagina in het menu (Pixel, Huis, Weer en verkeer, Vincent) betekent dat er daar iets aandacht nodig heeft.
  Bovenaan die pagina staat dan "Aandacht nodig: ..." met de reden; elke reden van een bolletje staat daar ook. Het bolletje bij
  Huis verdwijnt op dat apparaat zodra je de pagina opent, tot er iets anders te melden is.
- Een afgelaste training of wedstrijd van Carleen krijgt een eigen rode melding op Home ("... is afgelast"), en staat dan niet
  meer in de gewone hockeymelding met vertrek en weer. Tik op de melding en bevestig om hem te verbergen (per apparaat
  onthouden). De melding "Let op, misschien afgelastingen" telt afgelaste wedstrijden niet mee, en meldt "morgen" pas vanaf 06:00 (daarvoor alleen wat vandaag speelt: wie na middernacht nog op is, zit in zijn gevoel nog in de dag ervoor). Alles staat ook op de pagina Hockey.
- De labels naast de weerkaart (pakketten, brieven, taken): tik opent een pop-up met de details. Het thema-label
  linksboven opent de periode van het thema en de feestdagen.
- Agenda en taken in één kaart: kleur per agenda of lijst (blauw Vincent, roze Carleen, paars samen); taken afvinken en
  toevoegen kan er direct in.
- "Taak met plek": kies voor wie, wat, de plek en of er een WhatsApp-bericht moet komen als je daar aankomt.
- Autokaart: status, tank, locatie en beurt; knoppen om te vergrendelen, ontgrendelen (met bevestiging) en de lichten.
- Luchtontvochtiger (heeft geen vaste ruimte, dus bij Huis onder Wasmachine): ring met de luchtvochtigheid, tik opent de bediening
  (aan of uit, doel, stand Continu, Auto of Slaap, snelheid, timer, ionisator en kinderslot). Op Home staat een chip zolang hij aan staat.
  Is de tank vol of meldt hij een storing, dan staat er een melding op Home, een bolletje bij Huis en een regel in de samenvatting van Huis.
  De entiteiten heten `*.luchtontvochtiger*` (het apparaat komt uit de integratie xtend_tuya). Gaat Vincent slapen, dan zet NetDaemon (SleepManager) hem
  op de stand Slaap als hij aan staat en onthoudt de stand die hij had; zijn Vincent en Carleen allebei weer wakker (beide slaapschakelaars uit), dan gaat
  hij terug naar die stand. Een ontvochtiger die uit staat, al op Slaap stond of intussen op een andere stand is gezet, blijft zoals hij is.
- Ramen (bij Huis, onder Luchtontvochtiger): één kaart "Ramen" met het advies, tik opent de pop-up. Er zijn geen raamsensoren: bij
  de twee ramen in de woonkamer (bij de tv en bij de tafel) en het raam in de slaapkamer druk je als het raam open gaat en (met bevestiging) als het dicht is. De pop-up toont het advies met
  de redenen (buiten en binnen, vocht in g/m³, wind, regen, donker), per raam sinds wanneer het open staat, en hoe lang de ramen vandaag en de
  laatste dagen open stonden. Staat een raam open, dan is er één melding op Home voor alle open ramen (tik opent de pop-up met alle ramen), een bolletje bij Huis en een
  regel in de samenvatting van Huis; NetDaemon stuurt een telefoonmelding bij regen, een waarschuwing, harde wind, donker of als het te lang open
  staat. Het advies: regen, een waarschuwing, windstoten vanaf 50 km/u of donker (zon onder de horizon, bij elke temperatuur: bij 10 graden of
  warmer staat erbij dat er beestjes binnenkomen) is "Liever dicht", en dat gaat voor de rest; zijn Vincent en Carleen allebei weg, dan is het
  advies "Niemand thuis" (luchten heeft dan geen zin, en een raam dat dan open staat is rood en geeft een telefoonmelding); buiten 90% vochtig of meer of niet droger dan
  binnen is "Weinig nut" (laat de ontvochtiger werken); anders "Luchten kan", en bij 5 graden of kouder "Kort luchten" (10 minuten). De redenen om
  dicht te houden staan op één plek (`WindowAdvice.Risks`), het advies en de telefoonmeldingen gebruiken dezelfde lijst. De grenzen staan in
  `automation/Helpers/WindowAdvice.cs`, de app in `automation/apps/General/Household/WindowMonitor.cs`.
- Stroomprijs (Energie → Prijs, onder Historie): de grafiek van de laatste 7 dagen. Tik erop voor de pop-up met Dag, Week of Maand, de pijlen om
  terug (en weer vooruit) te gaan in de tijd, "Terug naar nu", en aanwijzen of aantikken van de grafiek voor de prijs op dat uur. Eronder staan het
  gemiddelde, de laagste en de hoogste prijs van de periode (met de verandering ten opzichte van de vorige) en bij Dag de prijzen van de komende uren
  als stippellijn. Een maand toont per dag het gemiddelde met de laagste en de hoogste prijs als band. De prijzen komen uit de langetermijn-
  statistieken van `sensor.anwb_electricity_all_in_price_current`; het element `huis-prijs-historie` staat in `www/prijs-historie.js` (kopieer het naar
  `/config/www/huis/`; het wordt door de kaart zelf geladen, `?v=` in de template `price_history` ophogen na een wijziging).
- Git (pagina Vincent, onder "Waar is..."): per GitHub-repository de open issues en pull requests, de samengevoegde pull requests, sterren, forks, kijkers,
  de laatste release en de laatste commit, met bovenaan de totalen. Groen als er niets openstaat, oranje bij open issues, paars bij open pull requests;
  elk getal is een link naar die lijst op GitHub. Staat er in een repository iets open, dan is er een melding op Home (alleen voor Vincent; tik opent dit
  overzicht), een bolletje bij Vincent in het menu en een regel bovenaan zijn pagina. De gegevens komen uit de integratie GitHub (die ververst zelf
  om de paar minuten) via de sensor `sensor.git_open_items`.
- Rode melding "NetDaemon reageert niet": tik herstart de add-on (alleen met een beheerdersaccount).
- Oranje melding "NetDaemon kan zijn sensoren niet meer bijwerken": NetDaemon draait, maar publiceert niets meer via MQTT.
  Controleer dan de MQTT-gebruiker en het wachtwoord in `appsettings.json` van NetDaemon (de gebruikersnaam in Home
  Assistant is `netdeamon`).
- Beheer → Dashboard: "Ververs dit scherm" laat Home Assistant de dashboardbestanden opnieuw inlezen, gooit de cache van de
  browser weg en laadt de pagina opnieuw (op de tablet is er geen verversknop); "Ververs de tablet" doet dat op de tablet
  vanaf een ander apparaat. Beheer staat ook in het menu van
  de tablet.
