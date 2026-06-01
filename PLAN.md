# Plan: Per-persoon aanwezigheid (Vincent weg / Carleen weg / beide weg)

## Doel
Aparte aanwezigheidssensoren voor Vincent en Carleen introduceren, zodat het systeem
onderscheid maakt tussen drie scenario's — **Vincent weg**, **Carleen weg** en **beide weg** —
en per scenario passende acties uitvoert.

## Vastgelegde beslissingen
- **Sensoren:** twee HA-helpers, handmatig door Vincent aangemaakt:
  `input_boolean.vincent_away` en `input_boolean.carleen_away`.
- **`input_boolean.away` = beide weg (afgeleid):** `away = vincent_away && carleen_away`.
  Bestaande code die op `away` leunt blijft werken.
- **Acties verschillen per scenario.**
- **Notificaties:** wél een melding wanneer **Vincent** weggaat; **geen** melding voor Carleen.
- **Hue-knop in de hal:** betekent **iedereen weg** (forceert beide weg).
- **`IsNightMode`-fix wordt in deze klus meegenomen** (woonkamer niet in nightmode louter
  omdat Carleen slaapt).

---

## Huidige situatie (waarom dit nodig is)
- Er is nu maar één presence-begrip: `input_boolean.away`.
- `Vincent.IsHome` is afgeleid van `away` (`automation/Models/Persons/VincentModel.cs:20`),
  dus "Vincent" en "het huis" zijn nu hetzelfde concept.
- `Carleen.IsHome` komt los van `person.carleen` (`automation/Models/Persons/CarleenModel.cs:20`).
- `away` wordt verspreid handmatig aan/uit gezet:
  - `automation/apps/General/AwayManager.cs` (`VincentHomeHandler`, `AutoAway`, `ExecuteAwayActions`)
  - `automation/apps/Rooms/Hall/HallLightOnMovement.cs:123-126`
  - `automation/apps/General/SleepManager.cs:123`
- `IsNightMode` (`automation/apps/BaseApp.cs:53`):
  `Vincent.IsSleeping || (Carleen.IsHome && Carleen.IsSleeping)` — zet de woonkamer in
  nachtmodus zodra Carleen slaapt, ook als Vincent wakker en actief is.

---

## Stap 1 — HA helpers (door Vincent)
Aanmaken in Home Assistant:
- `input_boolean.vincent_away`
- `input_boolean.carleen_away`

Daarna `dotnet tool run nd-codegen` draaien zodat ze in `HomeAssistantGenerated.cs` verschijnen.

## Stap 2 — Centrale presence-enum
Nieuw bestand `automation/Enum/PresenceScenario.cs`:
```csharp
public enum PresenceScenario { BothHome, VincentAwayOnly, CarleenAwayOnly, BothAway }
```
Maakt de actiematrix expliciet en testbaar, los van de `away`-boolean.

## Stap 3 — PersonModels ontkoppelen van `away`
- **`VincentModel.cs`** — `IsHome` afleiden van `input_boolean.vincent_away` (geïnverteerd),
  niet meer van `away`.
- **`CarleenModel.cs`** — `IsHome` afleiden van `input_boolean.carleen_away` (geïnverteerd).

> Gedragswijziging: alle bestaande `Vincent.IsHome`-gebruikers gaan op de nieuwe bron werken.
> Na te lopen: `Alarm.cs:48`, `HallLightOnMovement`, `BathRoomLights.cs:224`, `BaseApp` (IsNightMode).

## Stap 4 — Nieuwe `PresenceManager` app
Nieuw: `automation/apps/General/PresenceManager.cs` (inherit `BaseApp`).
Verantwoordelijk voor het **zetten** van de per-persoon-booleans en het afleiden van `away`:

1. **Per-persoon away zetten** op basis van `person.*` + de auto-away distance-logica
   (verplaatst vanuit `AwayManager.AutoAway`):
   - `person.vincent → home` ⇒ `vincent_away` uit; weg + `away_from` ⇒ aan.
   - idem voor Carleen (`person.carleen` / `device_tracker.carleen_mobiel`).
2. **`away` afleiden** (de "beide weg"-regel, op één centrale plek):
   ```
   away = vincent_away && carleen_away
   ```
3. **Scenario bepalen** → `PresenceScenario`; bij wijziging de bijbehorende actie triggeren (stap 5).

## Stap 5 — Actiematrix per scenario

| Scenario | Acties |
|---|---|
| **Beide thuis** | Normale automatisering; niets uitzetten. |
| **Vincent weg, Carleen thuis** | Geen lampen/tv uit. **Melding naar Vincent** ("Tot ziens" / op kantoordag vóór 09:00 "Werkse Vincent"). Carleen stuurt woonkamer-/nightmode-logica. |
| **Carleen weg, Vincent thuis** | Geen lampen/tv uit. **Geen melding.** `IsNightMode` valt terug op alleen Vincent. |
| **Beide weg** | Echte away-acties: `Light.TurnAllOff()`, tv + soundbar uit, alarm-condities. (Melding is al verstuurd op het moment dat Vincent als laatste wegging.) |

De huidige inhoud van `ExecuteAwayActions` (`AwayManager.cs:187-210`) verhuist hiernaartoe:
- De Vincent-vertrekmelding hangt aan de overgang **naar `vincent_away`**, niet aan `away`.
- De `Light.TurnAllOff()` / tv / soundbar hangen aan de overgang **naar `BothAway`**.

## Stap 6 — `AwayManager` afslanken
Behoudt de **welcome-home state machine** (Returning → WelcomingHome → Home), maar:
- `AutoAway` en `VincentHomeHandler` (het zetten van `away`) verhuizen naar `PresenceManager`.
- `ExecuteAwayActions`-inhoud verhuist naar de scenario-matrix.
- De Carleen-slaap-onderdrukking (`AwayManager.cs:189-195`) + `ScheduleCarleenWakeUp` blijven,
  maar haken op het nieuwe presence-model.

## Stap 7 — `IsNightMode`-fix (woonkamer)
`BaseApp.cs:53` opsplitsen zodat de woonkamer niet in nachtmodus gaat puur omdat Carleen slaapt.
Voorstel:
- `IsNightMode` (huisbreed/persoonlijk) blijft `Vincent.IsSleeping || (Carleen.IsHome && Carleen.IsSleeping)`.
- Nieuwe property voor ruimtes waar alleen Vincents slaap telt, bv.
  `protected bool IsVincentNightMode => Vincent.IsSleeping;`
- `LivingRoomLights.cs:34,40` gaat `IsVincentNightMode` gebruiken i.p.v. `IsNightMode`,
  zodat de woonkamer blijft reageren als Vincent wakker is terwijl Carleen slaapt.
- Slaapkamer-nabije ruimtes (hal, badkamer) houden het bestaande gedrag — die hebben al
  een office-day-uitzondering (`HallLightOnMovement`, `BathRoomLights`).

## Stap 8 — Losse plekken bijwerken
- **`HallLightOnMovement.cs:123-126`** (Hue-knop): ombouwen naar **iedereen weg** —
  zet zowel `vincent_away` als `carleen_away` aan (i.p.v. direct `away`). Tweede druk /
  thuiskomst zet ze weer uit.
- **`SleepManager.cs:123`** (`away` uit bij wakker worden): controleren/aanpassen zodat dit
  via de per-persoon-booleans loopt.

## Stap 9 — Tests
- `TestAutomation/Apps/General/PresenceManagerTests.cs` (nieuw): tabel-test over de vier
  scenario's, de `away`-afleiding en de Vincent-vertrekmelding.
- Bestaande `AwayManager`-tests bijwerken voor de afgeslankte verantwoordelijkheid.
- `VincentModel`/`CarleenModel`-tests: `IsHome` volgt nu de nieuwe booleans.
- Test voor `IsVincentNightMode` + woonkamergedrag (Vincent wakker / Carleen slaapt).

---

## Volgorde van uitvoeren
1. Vincent: helpers in HA + `nd-codegen`.
2. Enum + PersonModels ontkoppelen.
3. `PresenceManager` (zetten + afleiden + matrix).
4. `AwayManager` afslanken.
5. Losse plekken (Hall-knop, Sleep) + `IsNightMode`-fix.
6. Tests.

## Blokkade
Stap 2+ kunnen pas zodra de HA-helpers bestaan en `nd-codegen` is gedraaid
(`Entities.InputBoolean.VincentAway` / `CarleenAway` moeten gegenereerd zijn).
