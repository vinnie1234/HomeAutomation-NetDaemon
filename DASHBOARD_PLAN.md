# Dashboard plan – "Huis van Vincent & Carleen"

Status: **gebouwd** (fase 1, alle pagina's) in `dashboard/`. Fase 2: 3D-plattegrond. Alle entity-id's hieronder komen uit `automation/HomeAssistantGenerated.cs`.

## 1. Concept

- **Stijl:** donker, glas-effect (blur, halftransparante kaarten), grote afgeronde hoeken, veel lucht.
  De achtergrond verloopt mee met `input_select.housemodeselect` (Morning → warm oranje, Day → helder blauw,
  Evening → paars/amber, Night → bijna zwart). Zo voelt het dashboard elk moment van de dag anders.
- **Levende kaarten:** lamptegels gloeien in de *echte* kleur en helderheid van de lamp, de stofzuiger-icoon
  beweegt als Jaap rijdt, de PS5-kaart toont de box-art van het spel dat nu gespeeld wordt.
- **Rust door context:** het overzicht toont alleen wat er nú toe doet. Meldingen (afval morgen, wasmachine
  klaar, kattenbak fout, batterij kritiek) verschijnen alleen als ze actief zijn.
- **Bediening:** onderaan op elke pagina een vaste navigatiebalk (op desktop/tablet een zijbalk). Kamers zijn
  **pop-ups** (`#woonkamer`, `#slaapkamer`, …) in plaats van losse pagina's: één tik op een kamertegel, de pop-up
  schuift omhoog, swipe omlaag om te sluiten. Snel én overzichtelijk.
- **Layout:** Home Assistant *Sections*-view, zodat het vanzelf schaalt van telefoon (1 kolom) naar tablet
  en desktop (3-4 kolommen).
- **Opslag:** alles als YAML in deze repo (`dashboard/`), zodat het versiebeheerd is.

## 2. Benodigde HACS-onderdelen

| Onderdeel | Waarvoor | Status |
|---|---|---|
| **Bubble Card** | Kamer-pop-ups, mooie sliders, horizontale knoppenrij | Nieuw |
| **Mushroom Cards** | Chips (statusregel), compacte entiteitkaarten | Nieuw |
| **button-card** | Lamp-/kamertegels met eigen kleur, gloed en animaties | Nieuw |
| **card-mod** | Glas-effect, animaties, kleine stijlaanpassingen | Al in gebruik |
| **ApexCharts Card** | Energieprijs per uur, verbruik, gewicht Pixel | Al in gebruik |
| **Navbar Card** | Vaste navigatie onderaan / zijbalk op desktop | Nieuw |
| **Universal Remote Card** | Volwaardige afstandsbediening voor Samsung TV en Google TV Streamer | Nieuw |
| **auto-entities** | Automatische lijsten (lampen die aan staan, batterijen die bijna leeg zijn) | Nieuw |

Thema: ik maak zelf een thema `themes/glass_home.yaml`, zodat er geen extra afhankelijkheid nodig is.

## 3. Navigatie

```
[ Home ]  [ Energie ]  [ Media ]  [ Pixel ]  [ Huishouden ]  [ Weer ]  [ Systeem* ]
                                                                         * alleen voor Vincent (visible: user)
Kamer-pop-ups (vanaf Home, of via een chip):
#woonkamer  #keuken  #slaapkamer  #badkamer  #gang  #berging  #buiten
```

## 4. Pagina's

### 4.1 Home (overzicht)

**Hero-blok (bovenaan, volle breedte)**
- Begroeting op basis van huismodus ("Goedenavond Vincent"), grote klok, datum.
- Avatars van `person.vincent_maarschalkerweerd` en `person.carleen`: thuis/weg, slaap-icoon bij
  `input_boolean.sleepingvincent` / `sleepingcarleen`.
- Weer: `weather.weer_thuis`, temperatuur `sensor.weer_thuis_temperatuur`, min/max vandaag. Tik → Weer.

**Statusregel (Mushroom-chips, alleen zichtbaar als ze relevant zijn)**
| Chip | Bron | Tik → |
|---|---|---|
| Afval vandaag/morgen | `sensor.afval_vandaag`, `sensor.afval_morgen` | Huishouden |
| Regen verwacht | `binary_sensor.weer_thuis_neerslag_verwacht` | Weer |
| Weerwaarschuwing | `binary_sensor.weer_thuis_waarschuwing_actief` | Weer |
| Wasmachine bezig / klaar (+ resttijd) | `sensor.wasmachine_washer_machine_state`, `..._completion_time` | Huishouden |
| Jaap is aan het zuigen / bak vol | `vacuum.jaap`, `binary_sensor.jaap_bin_full` | Huishouden |
| Kattenbak fout / voer bijna op | `sensor.petsnowy_litterbox_errors`, `sensor.zedar_food_storage_status` | Pixel |
| Batterijen kritiek | `sensor.battery_devices_critical` | Huishouden |
| Integratieproblemen | `sensor.active_issues` | Systeem |
| Stroom nu goedkoop/duur | `sensor.energyzero_today_energy_current_hour_price` | Energie |
| Huismodus / Vakantie / Weg | `input_select.housemodeselect`, `input_boolean.holliday`, `input_boolean.away` | – |

**Kamertegels (grid)** – per kamer: icoon, aantal lampen aan, temperatuur (als beschikbaar).
Tegel gloeit in de kleur van de lampen die aan staan. Tik = pop-up, lang drukken = alles aan/uit.

**Snelle scènes** – Film (`scene.woonkamer_movie_2`), TV kijken (`scene.tv_kijken`), Date night
(`input_boolean.datenight`), Alles uit.

**Energie-mini** – huidig vermogen `sensor.p1_meter_vermogen`, kosten vandaag (dynamic energy cost sensor),
een strip van 24 gekleurde uurblokjes (groen → rood) met het huidige uur gemarkeerd.

**Pixel-mini** – laatst gevoerd, voer in de bak, status kattenbak, fontein aan/uit. Tik → Pixel.

**Now playing (alleen als er iets speelt)** – volle breedte met wazige achtergrond van de hoes:
PS5-game (`image.vinnie12349491_now_playing`), Steam (`image.vinnie1234_now_playing`), Spotify of TV.

### 4.2 Kamer-pop-ups

Elke pop-up heeft dezelfde opbouw (zo is het voorspelbaar):
1. Kopregel: naam, temperatuur, knop "alles uit".
2. Lampen als grote sliders (Bubble Card): helderheid schuiven, tik = aan/uit, lang drukken = kleur.
3. Scènes van de kamer als knoppenrij.
4. Automatisering: schakelaar "Automatische verlichting" (`input_boolean.disablelightautomation*`) en
   bewegingssensor-status.

| Pop-up | Lampen | Scènes / extra |
|---|---|---|
| `#woonkamer` | `light.plafond_woonkamer`, `light.bank`, `light.hanglamp`, `light.hue_filament_bulb_1/2`, `light.hue_play_links/midden/rechts`, `light.tv_light` | `select.woonkamer_woonkamer_scene`, scènes morning/day/evening/night/movie, `binary_sensor.motionwoonkamer` |
| `#keuken` | `light.lampen_keuken`, `light.koelkast` | Keukenspeaker (`media_player` keuken), `input_boolean.netdaemon_kitchen_music_on_motion` |
| `#slaapkamer` | `light.plafondslaapkamer`, `light.nachtkastje`, `light.bureau`, `light.bureau_2` | `select.slaapkamer_slaapkamer_scene`, rolgordijn `cover.rollerblind_0003`, slaapknoppen |
| `#badkamer` | `light.plafond_badkamer`, `light.badkamer_spiegel` | `select.badkamer_badkamer_scene`, `input_boolean.douchen`, temperatuur, tandenborstel (`sensor.smart_series_4000_97ae_*`) |
| `#gang` | `light.gang`, `light.gang_sigaret`, `light.hal`, `light.hal_2` | `select.gang_gang_scene`, temperatuur, beweging |
| `#berging` | `light.berging`, `light.berging_2` | temperatuur, beweging |
| `#buiten` | `light.buiten_deur` | zon op/onder |

> Woonkamer en slaapkamer hebben geen temperatuursensor in HA; die tegels tonen dan alleen de lampen.

### 4.3 Energie

- **Prijsgrafiek vandaag + morgen** (ApexCharts): staafjes per uur, kleur op prijs, verticale lijn op "nu",
  gemiddelde als stippellijn. Kaarten ernaast: nu, volgend uur, goedkoopste uur, duurste uur
  (`sensor.energyzero_today_*` en/of `sensor.anwb_electricity_all_in_price_*`).
- **Live verbruik**: grote meter `sensor.p1_meter_vermogen`, grafiek laatste 24 uur.
- **Kosten** dag/week/maand/jaar: `sensor.gang_p1_meter_..._active_daily/weekly/monthly/yearly_energy_cost`.
- **Sluipverbruik**: `sensor.all_standby_power`.
- **Zonne-opbrengst** (alleen als het van toepassing is): `sensor.power_production_now`,
  `sensor.energy_production_today/tomorrow`.
- **Water**: `sensor.watermeter_..._active_water_usage`, totaal en kosten.
- **Advies**: "Goedkoopste moment om de wasmachine te starten: 13:00–15:00".

### 4.4 Media (TV, PS5, Google TV Streamer)

- **Bronkeuze bovenaan** als grote knoppen: TV · PS5 · Google TV · Spotify. Eén tik zet de juiste combinatie aan
  (TV aan, juiste HDMI via `select.sync_box_hdmi_input`, soundbar aan) via een script.
- **Afstandsbediening** (Universal Remote Card): D-pad, terug, home, volume, kanalen; schakelt mee met de
  gekozen bron (`remote.tv` voor Samsung, `remote.tv_in_woonkamer` voor de Google TV Streamer).
- **PS5**: aan/uit `switch.ps5_vincent_power`, huidig spel `sensor.ps5_vincent_activity` met box-art,
  PS Plus status.
- **Hue Sync Box (ambilight)**: aan/uit `switch.sync_box_light_sync`, modus `select.sync_box_sync_mode`
  (video/game/muziek), intensiteit `select.sync_box_intensity`, helderheid `number.sync_box_brightness`.
- **Soundbar**: volume slider, bron.
- **Wat kijken we?**: Trakt "volgende aflevering" (`sensor.trakt_next_to_watch_only_aired`) en
  "binnenkort" (`sensor.trakt_upcoming_shows`) als posterrij.
- **Speakers**: groepen (`media_player.huis`, `media_player.hele_huis`) met Spotify-bediening.

### 4.5 Pixel (de kat)

Drie kolommen, elk met een duidelijke "alles OK / actie nodig"-status bovenaan.

**Eten (Zedar A600)**
- Voorraad in de bak `sensor.zedar_food_storage_status`, `sensor.pixelhavefood`.
- Vandaag gegeten `input_number.pixeltotalamountfeedday` (ring-meter) en totaal ooit.
- Tijdlijn van de 4 vaste voedermomenten (`input_datetime.pixelfeed*time` + `input_number.pixelfeed*amount`)
  met vinkjes voor wat al geweest is.
- Laatste automatische en handmatige voeding.
- Knoppen: **Nu voeren** (`input_button.feedcat`), **Volgende eerder geven**
  (`input_button.pixelgivenextfeedeary`), **Volgende overslaan** (`input_boolean.pixelskipnextautofeed`).

**Kattenbak (PetSnowy)**
- Status `sensor.snow_self_cleaning_litter_box_status` / `sensor.petsnowy_litterbox_status`, fouten.
- **Gewicht van Pixel** als grafiek over 30 dagen (`sensor.snow_self_cleaning_litter_box_gewicht_van_kat`) –
  vroege waarschuwing voor gezondheid.
- Bezoeken vandaag, duur, aantal schoonmaakbeurten en legingen (counters), filter resterend.
- Knoppen: **Schoonmaken**, **Legen**, "Stofzuiger na kattenbak overslaan" (`input_boolean.skipvaccumlitterbox`).

**Water (PetSnowy fontein)**
- Aan/uit `switch.petsnowy_fountain_ison`, modus `select.petsnowy_fountain_mode`.
- Dagen tot schoonmaken / filter vervangen als voortgangsbalken.

### 4.6 Huishouden

- **Jaap (stofzuiger)**: status met animatie, batterij, bak vol, tank / dock-tank, missies.
  Knoppen per ruimte: woonkamer, bank, gang, slaapkamer, kattenbak (`input_button.vacuumclean*`).
- **Wasmachine**: status, resttijd als aftellende ring, energie, "buiten ophangen?" (`input_boolean.laundryhangingoutside`).
- **Afval**: komende ophaaldagen (`sensor.afval_*`), hoe vaak buiten gezet (`sensor.garbage_placed_*`).
- **Takenlijst**: `todo.dagelijks`.
- **Agenda**: `calendar.gezin`, `calendar.carleen_vincent`, verjaardagen.
- **Batterijen**: kritiek/waarschuwing, volgende vervanging, boodschappenlijst (`sensor.batteries_to_buy`),
  automatische lijst van apparaten onder 25%.

### 4.7 Weer

- Groot weerblok met verwachting per uur en per dag (`weather.weer_thuis`).
- **Buienradar** live: `camera.neerslagradar`.
- Wind (richting als kompas), luchtvochtigheid, luchtdruk, zicht, zonneschijn.
- UV-index en veilige tijd in de zon, pollen (bomen, gras, onkruid), luchtkwaliteit, maanfase, zon op/onder.
- Weerwaarschuwingen als rode banner wanneer actief.

### 4.8 Systeem (alleen zichtbaar voor Vincent)

- NetDaemon: alle `input_boolean.netdaemon_*` app-schakelaars, herstartknop.
- Updates beschikbaar, back-up status (`sensor.backup_*`), integratieproblemen.
- Netwerk: internet, speedtest (Cloudflare), ping, router uptime.
- Raspberry Pi: CPU, geheugen, schijf, temperatuur.
- PC: aan/uit, vergrendelen, CPU/GPU-belasting en temperatuur.
- Afwezigheid: werkdagen (`input_boolean.officeday_*`), auto-away uitzetten, vakantie.

## 5. Beslissingen (genomen)

- **A. Energie per kamer:** voorlopig niet. De Energie-pagina toont alleen het totaal van het huis.
- **B. Apparaat:** telefoon en pc. Mobiel eerst (1 kolom, navigatie onderaan), op de pc 3-4 kolommen met
  een zijbalk. Geen Kiosk Mode nodig.
- **C. Plattegrond:** ja, als fase 2 (3D-render, kamers lichten op als de lampen aan staan).
- **D. Gebruikers:** Carleen ziet hetzelfde dashboard; alleen de Systeem-pagina is verborgen voor haar.

## 6. Aanpak

1. HACS-onderdelen installeren (jij), thema toevoegen.
2. Bouwen in `dashboard/`: `dashboard.yaml`, `views/*.yaml`, `popups/*.yaml`, `templates/` (button-card
   templates voor lamp- en kamertegels, zodat alles uit één bron komt).
3. Volgorde: thema + navigatie → Home + kamer-pop-ups → Pixel → Media → Energie → Huishouden → Weer → Systeem.
4. Per pagina samen testen op telefoon en pc en bijschaven.
5. Fase 2: 3D-plattegrond.

Nog te controleren in HA: welke attributen de prijssensoren hebben (prijzen per uur voor de grafiek) en welke
media_player bij de keukenspeaker hoort.
