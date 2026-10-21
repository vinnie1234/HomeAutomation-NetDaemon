# Huis-dashboard

YAML-dashboard voor Home Assistant. Ontwerp en paginaplan: zie `../DASHBOARD_PLAN.md`.

## Mappen

| Pad | Inhoud |
|---|---|
| `dashboard.yaml` | Startpunt: templates en de lijst met pagina's |
| `views/` | Eén bestand per pagina |
| `popups/` | Kamer-pop-ups (Bubble Card), opgenomen in `views/home.yaml` |
| `templates/button_card_templates.yaml` | Gedeelde stijl en gedrag van tegels (kamer, persoon, actie, info) |
| `navbar.yaml` | Navigatiebalk, opgenomen in elke pagina |
| `cards/` | Gedeelde kaarten en lijsten, zoals `power_sources.yaml` (de apparaten van "Grootste verbruikers nu") |
| `themes/glass_home.yaml` | Thema "Glass Home" |

## Benodigd uit HACS

Bubble Card, Mushroom, button-card, card-mod, Navbar Card, Universal Remote Card, auto-entities,
ApexCharts Card, mini-graph-card, clock-weather-card, upcoming-media-card, tabbed-card,
Gaming Status Cards, Week Planner Card en Kiosk Mode.

## Installeren (eenmalig)

1. Kopieer deze map naar Home Assistant als `/config/dashboards/huis/`
   (via de Samba-share: `\\192.168.50.189\config\dashboards\huis`).
2. Kopieer `themes/glass_home.yaml` naar `/config/themes/`, en de map `www/clouds` naar `/config/www/huis/clouds`
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

   lovelace:
     dashboards:
       dashboard-huis:
         mode: yaml
         title: Huis
         icon: mdi:home-variant
         show_in_sidebar: true
         filename: dashboards/huis/dashboard.yaml
   ```

4. Voeg de template-sensor voor de verwachting per uur toe (voor de strook op Weer). Staat er al een
   `template:` in `configuration.yaml`, zet dit blok dan als extra item in die lijst:

   ```yaml
   template:
     - trigger:
         - trigger: time_pattern
           minutes: 5
         - trigger: homeassistant
           event: start
       action:
         - action: weather.get_forecasts
           data:
             type: hourly
           target:
             entity_id: weather.weer_thuis
           response_variable: verwachting
       sensor:
         - name: Weer verwachting per uur
           unique_id: weer_verwachting_per_uur
           icon: mdi:weather-partly-cloudy
           state: "{{ now().isoformat() }}"
           attributes:
             forecast: "{{ verwachting['weather.weer_thuis'].forecast[:48] }}"
   ```

5. Herstart Home Assistant. "Huis" staat daarna in de zijbalk.

De pagina's zetten zelf het thema "Glass Home", je hoeft je profiel niet aan te passen.

## Bijwerken

Kopieer de gewijzigde bestanden opnieuw naar `/config/dashboards/huis/` en ververs de pagina
(rechtsboven ⋮ → Vernieuwen). Herstarten is alleen nodig na wijzigingen in `configuration.yaml`.
Na een wijziging aan het thema: Ontwikkelhulpmiddelen → YAML → Thema's herladen.

## Bediening

- Kamertegel: **tik** opent de pop-up, **lang drukken** zet alle lampen van die kamer aan of uit.
- Pop-up: lamp **schuiven** = helderheid, **tik op het icoon** = aan/uit. Omlaag swipen sluit.
- Meldingen bovenaan verschijnen alleen als ze relevant zijn; tik erop om naar de juiste pagina te gaan.
