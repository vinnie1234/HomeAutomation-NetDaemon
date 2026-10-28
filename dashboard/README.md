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
| `cards/` | Gedeelde kaarten en lijsten: `agenda.yaml` (agenda en taken), `auto.yaml`, `todo_*.yaml` (de drie takenlijsten), `taak_knop.yaml`, `camera_woonkamer.yaml`, `power_sources.yaml` (de apparaten van "Grootste verbruikers nu"), ... |
| `ha/template.yaml` | Template-sensoren voor `/config/template.yaml`: verwachting per uur, brieven van deze week en of NetDaemon online is |
| `themes/glass_home.yaml` | Thema "Glass Home" |

## Benodigd uit HACS

Bubble Card, Mushroom, button-card, card-mod, Navbar Card, Universal Remote Card, auto-entities,
ApexCharts Card, mini-graph-card, clock-weather-card, upcoming-media-card, tabbed-card,
Gaming Status Cards, Calendar & Tasks Card en Kiosk Mode.

Verder in Home Assistant: de integraties Fully Kiosk Browser (de tablet), Stellantis (de auto) en Local To-do, met de
takenlijsten `todo.dagelijks` (heet "Samen"), `todo.vincent` en `todo.carleen`.

## Installeren (eenmalig)

1. Kopieer deze map naar Home Assistant als `/config/dashboards/huis/`
   (via de Samba-share: `\\192.168.50.189\config\dashboards\huis`).
2. Kopieer `themes/glass_home.yaml` naar `/config/themes/`, `www/idle-home.js` naar `/config/www/huis/`, en de map `www/clouds` naar `/config/www/huis/clouds`
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
       - /local/huis/idle-home.js?v=1

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
   - `NetDaemon MQTT werkt`: uit als het MQTT-levensteken van NetDaemon verouderd is. De sensoren die NetDaemon zelf aanmaakt
     lopen via MQTT, en als de broker de login weigert stopt dat publiceren zonder foutmelding. Het dashboard toont dan een
     oranje melding.

   Herlaad daarna de template-entiteiten (Ontwikkelhulpmiddelen → YAML).

5. Herstart Home Assistant. "Huis" staat daarna in de zijbalk.

De pagina's zetten zelf het thema "Glass Home", je hoeft je profiel niet aan te passen.

## Bijwerken

Kopieer de gewijzigde bestanden opnieuw naar `/config/dashboards/huis/` en ververs de pagina
(rechtsboven ⋮ → Vernieuwen). Herstarten is alleen nodig na wijzigingen in `configuration.yaml`.
Na een wijziging aan het thema: Ontwikkelhulpmiddelen → YAML → Thema's herladen.

## Helpers

NetDaemon maakt de helpers zelf aan bij het opstarten (`automation/Configuration/HelperDefinitions.cs`), zoals het
werkrooster, de thema-keuze en het formulier van "Taak met plek". Het dashboard werkt dus pas helemaal nadat NetDaemon
een keer is gestart.

## Bediening

- Kamertegel: **tik** opent de pop-up, **lang drukken** zet alle lampen van die kamer aan of uit.
- Pop-up: lamp **schuiven** = helderheid, **tik op het icoon** = aan/uit. Omlaag swipen sluit.
- Meldingen bovenaan verschijnen alleen als ze relevant zijn; tik erop om naar de juiste pagina te gaan.
- De labels naast de weerkaart (pakketten, brieven, taken): tik opent een pop-up met de details. Het thema-label
  linksboven opent de periode van het thema en de feestdagen.
- Agenda en taken in één kaart: kleur per agenda of lijst (blauw Vincent, roze Carleen, paars samen); taken afvinken en
  toevoegen kan er direct in.
- "Taak met plek": kies voor wie, wat, de plek en of er een WhatsApp-bericht moet komen als je daar aankomt.
- Autokaart: status, tank, locatie en beurt; knoppen om te vergrendelen, ontgrendelen (met bevestiging) en de lichten.
- Rode melding "NetDaemon reageert niet": tik herstart de add-on (alleen met een beheerdersaccount).
- Oranje melding "NetDaemon kan zijn sensoren niet meer bijwerken": NetDaemon draait, maar publiceert niets meer via MQTT.
  Controleer dan de MQTT-gebruiker en het wachtwoord in `appsettings.json` van NetDaemon (de gebruikersnaam in Home
  Assistant is `netdeamon`).
- Beheer → Dashboard: "Ververs dit scherm" gooit de cache van de browser weg en laadt de pagina opnieuw (op de tablet is er
  geen verversknop); "Ververs de tablet" doet dat op de tablet vanaf een ander apparaat. Beheer staat ook in het menu van
  de tablet.
