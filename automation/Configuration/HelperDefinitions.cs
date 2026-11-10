using Automation.apps.General.System;
using Automation.apps.General.Work;
using Automation.Models.Helpers;

namespace Automation.Configuration;

/// <summary>
/// The helpers (input_boolean, input_number, counter, ...) the automations need. <see cref="HelperManager"/>
/// creates the ones that are missing in Home Assistant when NetDaemon starts; existing helpers are left as they are, so
/// changes made in the Home Assistant UI stay. Home Assistant derives the id from the name when it creates a helper.
/// </summary>
public static class HelperDefinitions
{
    /// <summary>All helpers, grouped per domain.</summary>
    public static readonly IReadOnlyList<HelperDefinition> All =
    [
        // Medicijnen van Pixel (PixelMedication): voorraad in pillen (prednison per kwart), de knoppen "gegeven" en de herinneringstijd
        Number("pixel_prednison_voorraad", "Pixel prednison voorraad", min: 0, max: 500, step: 0.25, mode: "box", unit: "pillen", icon: "mdi:pill"),
        Number("pixel_antibraak_voorraad", "Pixel antibraak voorraad", min: 0, max: 500, step: 0.5, mode: "box", unit: "pillen", icon: "mdi:pill-multiple"),
        Button("pixel_prednison_gegeven", "Pixel prednison gegeven", icon: "mdi:pill"),
        Button("pixel_antibraak_gegeven", "Pixel antibraak gegeven", icon: "mdi:pill-multiple"),
        Button("pixel_medicijn_ongedaan", "Pixel medicijn ongedaan", icon: "mdi:undo"),
        Time("pixel_prednison_tijd", "Pixel prednison tijd", "20:00:00"),
        // De prijs van een doosje en hoeveel pillen erin zitten (factuur 21-10-2026), voor de kosten per pil en tot nu toe
        Number("pixel_prednison_doosprijs", "Pixel prednison doosprijs", min: 0, max: 1000, step: 0.01, mode: "box", unit: "€", icon: "mdi:cash", start: 7.85),
        Number("pixel_prednison_doosgrootte", "Pixel prednison doosgrootte", min: 1, max: 500, step: 1, mode: "box", unit: "pillen", icon: "mdi:package-variant", start: 21),
        Number("pixel_antibraak_doosprijs", "Pixel antibraak doosprijs", min: 0, max: 1000, step: 0.01, mode: "box", unit: "€", icon: "mdi:cash", start: 20.30),
        Number("pixel_antibraak_doosgrootte", "Pixel antibraak doosgrootte", min: 1, max: 500, step: 1, mode: "box", unit: "pillen", icon: "mdi:package-variant", start: 4),

        // Werkrooster per persoon (WorkSchedule). Vincent neemt zijn oude instellingen over; Carleen en het weekend beginnen op Vrij.
        WorkDay("werkdag_vincent_maandag", "Werkdag Vincent maandag", copyFrom: "input_boolean.officeday_monday"),
        WorkDay("werkdag_vincent_dinsdag", "Werkdag Vincent dinsdag", copyFrom: "input_boolean.officeday_tuesday"),
        WorkDay("werkdag_vincent_woensdag", "Werkdag Vincent woensdag", copyFrom: "input_boolean.officeday_wednesday"),
        WorkDay("werkdag_vincent_donderdag", "Werkdag Vincent donderdag", copyFrom: "input_boolean.officeday_thursday"),
        WorkDay("werkdag_vincent_vrijdag", "Werkdag Vincent vrijdag", copyFrom: "input_boolean.officeday_friday"),
        WorkDay("werkdag_vincent_zaterdag", "Werkdag Vincent zaterdag"),
        WorkDay("werkdag_vincent_zondag", "Werkdag Vincent zondag"),
        // Brieven die op het dashboard als ontvangen zijn afgevinkt (de eerste 8 tekens van hun id, met komma's ertussen)
        Text("brieven_ontvangen", "Brieven ontvangen", min: 0, max: 255, mode: "text", icon: "mdi:email-check-outline"),
        Boolean("vrij_vincent", "Vrij Vincent", icon: "mdi:beach", copyFrom: "input_boolean.holliday"),
        Time("opstaan_kantoor_vincent", "Opstaan kantoor Vincent", "07:15:00", copyFrom: "input_datetime.daytimeoffice"),
        Time("opstaan_thuiswerk_vincent", "Opstaan thuiswerk Vincent", "08:15:00", copyFrom: "input_datetime.daytimehomework"),
        Time("werk_start_vincent", "Werk start Vincent", "08:30:00"),
        Time("werk_eind_vincent", "Werk eind Vincent", "17:00:00"),
        WorkDay("werkdag_carleen_maandag", "Werkdag Carleen maandag"),
        WorkDay("werkdag_carleen_dinsdag", "Werkdag Carleen dinsdag"),
        WorkDay("werkdag_carleen_woensdag", "Werkdag Carleen woensdag"),
        WorkDay("werkdag_carleen_donderdag", "Werkdag Carleen donderdag"),
        WorkDay("werkdag_carleen_vrijdag", "Werkdag Carleen vrijdag"),
        WorkDay("werkdag_carleen_zaterdag", "Werkdag Carleen zaterdag"),
        WorkDay("werkdag_carleen_zondag", "Werkdag Carleen zondag"),
        Boolean("vrij_carleen", "Vrij Carleen", icon: "mdi:beach"),
        Time("opstaan_kantoor_carleen", "Opstaan kantoor Carleen", "07:15:00"),
        Time("opstaan_thuiswerk_carleen", "Opstaan thuiswerk Carleen", "08:15:00"),
        Time("werk_start_carleen", "Werk start Carleen", "08:30:00"),
        Time("werk_eind_carleen", "Werk eind Carleen", "17:00:00"),

        // Verkeersbericht per persoon (CommuteReport): of het WhatsApp-bericht komt, en hoe laat voor de reis naar kantoor en de reis naar huis
        Boolean("verkeer_bericht_vincent", "Verkeer bericht Vincent", icon: "mdi:car-clock", start: true),
        Time("verkeer_heen_vincent", "Verkeer heen Vincent", "07:50:00"),
        Time("verkeer_terug_vincent", "Verkeer terug Vincent", "17:15:00"),
        Boolean("verkeer_bericht_carleen", "Verkeer bericht Carleen", icon: "mdi:car-clock", start: true),
        Time("verkeer_heen_carleen", "Verkeer heen Carleen", "07:50:00"),
        Time("verkeer_terug_carleen", "Verkeer terug Carleen", "17:15:00"),

        // Muziek per persoon (SpotifyChoice, SpotifyAccounts): de playlist per ruimte en het Spotify-account; Spotcast speelt af
        .. SpotifyHelpers("vincent", "Vincent"),
        .. SpotifyHelpers("carleen", "Carleen"),
        // Uren die Vincent zelf toevoegt aan zijn werktijd (WorkTimeTracker, het venster Werktijd op zijn pagina)
        Number("werktijd_extra_minuten", "Werktijd extra minuten", min: -720, max: 720, step: 5, mode: "box", unit: "min", icon: "mdi:timer-plus-outline", start: 0),
        Select("werktijd_extra_plek", "Werktijd extra plek", ["Thuis", "Kantoor", "Elders"], icon: "mdi:map-marker-outline"),
        DateTime("werktijd_extra_datum", "Werktijd extra datum", hasDate: true, hasTime: false, icon: "mdi:calendar"),
        Button("werktijd_extra_toevoegen", "Werktijd extra toevoegen", icon: "mdi:plus-circle-outline"),
        Select("spotify_standaard_persoon", "Spotify standaard persoon", ["Vincent", "Carleen"], icon: "mdi:account-switch"),

        // Kantoortijden en vervoer per persoon (OfficeTimes, CommuteReport)
        .. OfficeHelpers("vincent", "Vincent"),
        .. OfficeHelpers("carleen", "Carleen"),

        // input_boolean
        Boolean("away", "Away", icon: "mdi:shield-lock"),
        Boolean("awaycarleen", "AwayCarleen", icon: "mdi:shield-lock"),
        Boolean("awayvincent", "AwayVincent", icon: "mdi:shield-lock"),
        Boolean("cleanpetsnowy", "CleanPetSnowy"),
        Boolean("datenight", "DateNight", icon: "far:heart"),
        Boolean("disableautoaway", "DisableAutoAway"),
        Boolean("disablelightautomationbathroom", "DisableLightAutomationBathroom", icon: "mdi:sync-off"),
        Boolean("disablelightautomationbedroom", "DisableLightAutomationBedRoom", icon: "mdi:sync-off"),
        Boolean("disablelightautomationgeneral", "DisableLightAutomationGeneral", icon: "mdi:sync-off"),
        Boolean("disablelightautomationhall", "DisableLightAutomationHall", icon: "mdi:sync-off"),
        Boolean("disablelightautomationlivingroom", "DisableLightAutomationLivingRoom", icon: "mdi:sync-off"),
        Boolean("disablereset", "DisableReset", icon: "mdi:sync-off"),
        Boolean("douchen", "Douchen", icon: "mdi:shower-head"),
        Boolean("emptypetsnowy", "EmptyPetSnowy"),
        Boolean("feedcat", "FeedCat"),
        Boolean("laundryhangingoutside", "LaundryHangingOutside", icon: "mdi:washing-machine"),
        Boolean("netdaemon_alarm", "netdaemon_alarm"),
        Boolean("netdaemon_auto_update_app", "netdaemon_auto_update_app"),
        Boolean("netdaemon_away_manager", "netdaemon_away_manager"),
        Boolean("netdaemon_bath_room_lights", "netdaemon_bath_room_lights"),
        Boolean("netdaemon_battery_monitoring", "netdaemon_battery_monitoring"),
        Boolean("netdaemon_bed_room_lights", "netdaemon_bed_room_lights"),
        Boolean("netdaemon_cat", "netdaemon_cat"),
        Boolean("netdaemon_cat_vomit_tracker", "netdaemon_cat_vomit_tracker"),
        Boolean("netdaemon_coc_monitoring", "netdaemon_coc_monitoring"),
        Boolean("netdaemon_fun_app", "netdaemon_fun_app"),
        Boolean("netdaemon_gaming", "netdaemon_gaming"),
        Boolean("netdaemon_google_assistant_button_translate", "netdaemon_google_assistant_button_translate"),
        Boolean("netdaemon_hall_light_on_movement", "netdaemon_hall_light_on_movement"),
        Boolean("netdaemon_holiday_manager", "netdaemon_holiday_manager"),
        Boolean("netdaemon_home_work_energy_tracker", "netdaemon_home_work_energy_tracker"),
        Boolean("netdaemon_house_state_manager", "netdaemon_house_state_manager"),
        Boolean("netdaemon_integration_monitor", "netdaemon_integration_monitor"),
        Boolean("netdaemon_kitchen_music_on_motion", "netdaemon_kitchen_music_on_motion"),
        Boolean("netdaemon_laundry", "netdaemon_laundry"),
        Boolean("netdaemon_living_room_lights", "netdaemon_living_room_lights"),
        Boolean("netdaemon_net_daemon", "netdaemon_net_daemon"),
        Boolean("netdaemon_parcel_notifications", "netdaemon_parcel_notifications"),
        Boolean("netdaemon_pc_manager", "netdaemon_pc_manager"),
        Boolean("netdaemon_power_estimator", "netdaemon_power_estimator"),
        Boolean("netdaemon_presence_manager", "netdaemon_presence_manager"),
        Boolean("netdaemon_presence_simulation", "netdaemon_presence_simulation"),
        Boolean("netdaemon_reset", "netdaemon_reset"),
        Boolean("netdaemon_save_in_state", "netdaemon_save_in_state"),
        Boolean("netdaemon_sleep_manager", "netdaemon_sleep_manager"),
        Boolean("netdaemon_tv", "netdaemon_tv"),
        Boolean("netdaemon_vacuum", "netdaemon_vacuum"),
        Boolean("netdaemon_whats_app_assistant", "netdaemon_whats_app_assistant"),
        Boolean("onvacation", "OnVacation ", icon: "mdi:wallet-travel"),
        Boolean("pixelgivenextfeedeary", "PixelGiveNextFeedEary"),
        Boolean("restartnetdaemon", "RestartNetDaemon"),
        Boolean("skipvaccumlitterbox", "SkipVaccumLitterbox", icon: "mdi:robot-vacuum-variant-off"),
        Boolean("sleeping", "SleepingVincent", icon: "mdi:sleep"),
        Boolean("sleepingcarleen", "SleepingCarleen", icon: "mdi:sleep"),
        Boolean("start_friends", "Start Friends"),
        Boolean("start_pc", "Start PC"),
        Boolean("working", "Working", icon: "mdi:briefcase"),
        Boolean("workingcarleen", "WorkingCarleen", icon: "mdi:briefcase"),
        Boolean("workingvincent", "WorkingVincent", icon: "mdi:briefcase"),
        Boolean("zedarskipnextautofeed", "PixelSkipNextAutoFeed", icon: "mdi:food-drumstick-off-outline"),

        // input_button
        Button("cleanpetsnowy", "CleanPetSnowy", icon: "mdi:spray-bottle"),
        Button("emptypetsnowy", "EmptyPetSnowy", icon: "mdi:delete-empty-outline"),
        Button("feedcat", "FeedCat", icon: "mdi:cat"),
        Button("restartnetdeamon", "RestartNetDaemon"),
        Button("start_friends", "Start Friends"),
        Button("start_kerstmuziek", "Start Kerstmuziek", icon: "mdi:music-note"),
        Button("start_pc", "Start PC", icon: "mdi:desktop-classic"),
        Button("startnewyear", "StartNewYear", icon: "mdi:firework"),
        Button("vacuumcleanbank", "VacuumCleanBank", icon: "mdi:robot-vacuum"),
        Button("vacuumcleangang", "VacuumCleanGang", icon: "mdi:robot-vacuum"),
        Button("vacuumcleankattenbak", "VacuumCleanKattenbak", icon: "mdi:robot-vacuum"),
        Button("vacuumcleanslaapkamer", "VacuumCleanSlaapkamer", icon: "mdi:robot-vacuum"),
        Button("vacuumcleanwoonkamer", "VacuumCleanWoonkamer", icon: "mdi:robot-vacuum"),
        // Sends the two commute messages (to the office and home) right away
        Button("verkeertestbericht", "VerkeerTestbericht", icon: "mdi:car-clock"),
        Button("zedargivenextfeedeary", "PixelGiveNextFeedEary", icon: "mdi:food-drumstick"),

        // input_number
        Number("bathroomlightdaytime", "BathroomLightDayTime", min: 1, max: 50, step: 1, mode: "box", icon: "mdi:clock-time-eight"),
        Number("bathroomlightnighttime", "BathroomLightNightTime", min: 1, max: 50, step: 1, mode: "box", icon: "mdi:clock-time-eight"),
        Number("cleaningpetsnowytime", "CleaningPetSnowyTime", min: 0, max: 1.0000000000000002e+17, step: 1, mode: "box"),
        Number("halllightdaytime", "HallLightDayTime", min: 1, max: 50, step: 1, mode: "box", icon: "mdi:clock-time-eight"),
        Number("halllightnighttime", "HallLightNightTime", min: 1, max: 50, step: 1, mode: "box", icon: "mdi:clock-time-eight"),
        Number("peuken_pakjes", "Peuken Pakjes", min: 0, max: 1000, step: 1, mode: "box"),
        Number("pixelinpetsnowytime", "PixelInPetSnowyTime", min: 0, max: 1.0000000000000002e+17, step: 1, mode: "box"),
        Number("pixelnumberofmanualfood", "PixelNumberOfManualFood", min: 0, max: 10, step: 1, mode: "slider", icon: "mdi:food-drumstick"),
        Number("zedarfeedfirstamound", "PixelFeedFirstAmount", min: 0, max: 10, step: 1, mode: "box", icon: "fas:drumstick-bite"),
        Number("zedarfeedfourthamound", "PixelFeedFourthAmount", min: 0, max: 10, step: 1, mode: "box", icon: "fas:drumstick-bite"),
        Number("zedarfeedsecondamound", "PixelFeedSecondAmount", min: 0, max: 10, step: 1, mode: "box", icon: "fas:drumstick-bite"),
        Number("zedarfeedthirdamound", "PixelFeedThirdAmount", min: 0, max: 10, step: 1, mode: "box", icon: "fas:drumstick-bite"),
        Number("zedarlastamountautomationfeed", "PixelLastAmountAutomationFeed", min: 0, max: 1.0000000000000003e+18, step: 1, mode: "slider"),
        Number("zedarlastamountmanualfeed", "PixelLastAmountManualFeed", min: 0, max: 100, step: 1, mode: "box"),
        Number("zedartotalamountfeedalltime", "PixelTotalAmountFeedAllTime", min: 0, max: 1.0000000000000003e+21, step: 1, mode: "box"),
        Number("zedartotalamountfeedday", "PixelTotalAmountFeedDay", min: 0, max: 100, step: 1, mode: "box"),

        // Taak met plek (TodoZoneReminders): het formulier van de pop-up #taak. De opties van todo_zone houdt de app zelf bij.
        Select("todo_lijst", "todo_lijst", options: ["Vincent", "Carleen", "Samen"], icon: "mdi:account-multiple-outline"),
        Select("todo_zone", "todo_zone", options: ["Geen plek"], icon: "mdi:map-marker-outline"),
        Text("todo_tekst", "todo_tekst", min: 0, max: 255, icon: "mdi:checkbox-marked-circle-outline"),
        Boolean("todo_stuur_bericht", "todo_stuur_bericht", icon: "mdi:whatsapp"),
        Button("todo_toevoegen", "todo_toevoegen", icon: "mdi:plus"),
        // Boodschappenlijst (GroceryList): alles afvinken, en of de knop daarvoor op Home staat
        Button("boodschappen_alles_gehaald", "boodschappen_alles_gehaald", icon: "mdi:cart-check"),
        Boolean("boodschappen_afvinken", "boodschappen_afvinken", icon: "mdi:cart-check"),
        // Aan als de portemonnee na twee keer zoeken nog niet thuis is (WalletMonitor): dan komt de melding op Home
        Boolean("portemonnee_melding", "portemonnee_melding", icon: "mdi:wallet-outline"),

        // input_select
        Select("housemodeselect", "HouseModeSelect", options: ["Morning", "Day", "Evening", "Night"], icon: "mdi:home-alert"),
        Select("tijd_van_de_dag", "Tijd van de dag", options: ["Ochtend", "Middag", "Avond", "Nacht", "Automatisch"]),
        Select("water_usage_swelector", "Water Usage Selector", options: ["Default", "WC Klein", "WC Groot", "Afwas", "Douchen", "Wasmachine", "Anders", "Kraan"]),
        Select("weerkaart_thema", "weerkaart_thema", options: ["Automatisch", "Lente", "Zomer", "Herfst", "Winter", "Pasen", "Nieuwjaar", "Kerst", "Sinterklaas", "Halloween", "Verjaardag Vincent", "Verjaardag Carleen", "Verjaardag Pixel", "Jubileum", "Valentijn", "Koningsdag", "Dodenherdenking", "Bevrijdingsdag"], icon: "mdi:weather-hurricane"),
        // Testen van de weerkaart: zet hem in een situatie (weer, een weerwaarschuwing, muziek en tv, de was, het afval, Pixel bij de deur, post en pakketjes, bezorgbusjes, een gemist pakketje, niemand thuis, op vakantie, een vliegtuig)
        Select("weerkaart_scenario", "Weerkaart scenario", options: ["Automatisch", "Heerlijk weer", "Lichte mist", "Dichte mist", "Hagel", "Harde wind", "Vorst", "Hitte", "Regenboog", "Regen", "Onweer", "Sneeuw", "Mist", "Muziek en tv", "Was buiten", "Afval buiten", "Pixel bij de deur", "Stofzuiger aan", "Pixel gekke 10 minuten", "Auto komt thuis", "Auto rijdt weg", "Auto staat stil", "Post en pakketjes", "Busje in de verte", "Busje komt aan", "Busje bezorgt", "Busje rijdt weg", "Drie busjes", "Pakketje gemist", "Pakketje bij afhaalpunt", "Niemand thuis", "Op vakantie", "Op vakantie met oppas", "Vliegtuig", "Code geel", "Code oranje", "Code rood"], icon: "mdi:test-tube"),

        // input_datetime
        // Verjaardagen en ons jubileum (de weerkaart viert feest); de datums vul je zelf in, ze staan bewust niet in deze openbare code
        DateTime("verjaardag_vincent", "Verjaardag Vincent", hasDate: true, hasTime: false, icon: "mdi:cake-variant-outline"),
        DateTime("verjaardag_carleen", "Verjaardag Carleen", hasDate: true, hasTime: false, icon: "mdi:cake-variant-outline"),
        DateTime("jubileum", "Jubileum", hasDate: true, hasTime: false, icon: "mdi:heart-outline"),
        DateTime("verjaardag_pixel", "Verjaardag Pixel", hasDate: true, hasTime: false, icon: "mdi:cat"),
        DateTime("netdaemon_heartbeat", "NetDaemon heartbeat", hasDate: true, hasTime: true, icon: "mdi:heart-pulse"),
        DateTime("daytimeweekend", "DaytimeWeekend", hasDate: false, hasTime: true, icon: "mdi:clock-digital"),
        DateTime("nighttimeweekdays", "NighttimeWeekdays", hasDate: false, hasTime: true, icon: "mdi:clock-digital"),
        DateTime("nighttimeweekends", "NighttimeWeekends", hasDate: false, hasTime: true, icon: "mdi:clock-digital"),
        DateTime("zedarfeedfirsttime", "PixelFeedFirstTime", hasDate: false, hasTime: true, icon: "mdi:food-drumstick"),
        DateTime("zedarfeedfourthtime", "PixelFeedFourthTime", hasDate: false, hasTime: true, icon: "mdi:food-drumstick"),
        DateTime("zedarfeedsecondtime", "PixelFeedSecondTime", hasDate: false, hasTime: true, icon: "mdi:food-drumstick"),
        DateTime("zedarfeedthirdtime", "PixelFeedThirdTime", hasDate: false, hasTime: true, icon: "mdi:food-drumstick"),
        DateTime("zedarlastautomatedfeed", "PixelLastAutomatedFeed", hasDate: true, hasTime: true, icon: "mdi:clock-time-eight"),
        DateTime("zedarlastmanualfeed", "PixelLastManualFeed", hasDate: true, hasTime: true, icon: "mdi:clock-time-eight"),
        // When Pixel last used the litter box: the dashboard header lets him run his "zoomies" for 10 minutes after
        DateTime("pixellastlitterboxvisit", "PixelLastLitterboxVisit", hasDate: true, hasTime: true, icon: "mdi:cat"),

        // input_text
        Text("lastwaterusageguess", "LastWaterUsageGuess", min: 0, max: 100, mode: "text"),
        Text("lastwaterusageinliter", "LastWaterUsageInLiterSaved", min: 0, max: 100, mode: "text"),
        Text("lastwaterusageinliteractual", "LastWaterUsageInLiterActual", min: 0, max: 100, mode: "text"),

        // counter
        Counter("petsnowylitterboxemptying", "PetSnowyLitterBoxEmptying", initial: 0, step: 1, restore: true),
        Counter("petsnowylitterboxpixelinit", "PetSnowyLitterBoxPixelInIt", initial: 0, step: 1, minimum: 0, restore: true),
        Counter("petsnowylittleboxcleaning", "PetSnowyLittleBoxCleaning", initial: 0, step: 1, restore: true),
        Counter("pixel_gekotst", "Pixel Gekotst", initial: 0, step: 1, minimum: 0, maximum: 9999999999999, restore: true, icon: "mdi:cat")
    ];

    /// <summary>
    /// What <see cref="OfficeTimes"/> keeps up to date for a person: the time to be at the office (ten minutes before the
    /// start) and the time to leave the office (ten minutes after the end), for the next office day. A 9292 route can use them as
    /// its arrival or departure time. Besides these, a choice of car or public transport for every day of the week.
    /// </summary>
    private static IEnumerable<HelperDefinition> SpotifyHelpers(string person, string name) =>
    [
        Text($"spotify_douche_{person}", $"Spotify douche {name}", min: 0, max: 255, mode: "text", icon: "mdi:shower"),
        Text($"spotify_keuken_{person}", $"Spotify keuken {name}", min: 0, max: 255, mode: "text", icon: "mdi:silverware-fork-knife"),
        Text($"spotify_radio_{person}", $"Spotify radio {name}", min: 0, max: 255, mode: "text", icon: "mdi:radio"),
        Text($"spotify_kerst_{person}", $"Spotify kerst {name}", min: 0, max: 255, mode: "text", icon: "mdi:pine-tree"),
        // Of de automatiseringen de muziek van deze ruimte zelf starten (douche bij het douchen, keuken bij beweging); aan als er niets is ingesteld
        Boolean($"spotify_auto_douche_{person}", $"Spotify auto douche {name}", icon: "mdi:shower", start: true),
        Boolean($"spotify_auto_keuken_{person}", $"Spotify auto keuken {name}", icon: "mdi:silverware-fork-knife", start: true),
        Boolean($"spotify_auto_radio_{person}", $"Spotify auto radio {name}", icon: "mdi:radio", start: true),
        // De keuzes komen van SpotifyAccounts (de accounts die Spotcast heeft); zonder account speelt Spotcast op zijn standaardaccount
        Select($"spotify_account_{person}", $"Spotify account {name}", [Helpers.SpotifyChoice.NoAccount], icon: "mdi:account-music"),
        Button($"spotify_koppelen_{person}", $"Spotify koppelen {name}", icon: "mdi:link-variant-plus")
    ];

    private static IEnumerable<HelperDefinition> OfficeHelpers(string person, string name) =>
    [
        DateTime($"kantoor_aankomst_{person}", $"Kantoor aankomst {name}", hasDate: true, hasTime: true, icon: "mdi:office-building-marker"),
        DateTime($"kantoor_vertrek_{person}", $"Kantoor vertrek {name}", hasDate: true, hasTime: true, icon: "mdi:office-building-marker-outline"),
        DateTime($"thuis_vertrek_{person}", $"Thuis vertrek {name}", hasDate: true, hasTime: true, icon: "mdi:home-export-outline"),
        DateTime($"thuis_aankomst_{person}", $"Thuis aankomst {name}", hasDate: true, hasTime: true, icon: "mdi:home-import-outline"),
        // Wat je zelf instelt: haal je het werk uit de agenda, dan komen de tijden daaruit; zo niet, dan stel je hier in hoe laat je 's ochtends
        // op kantoor wilt zijn (of van huis vertrekt) en hoe laat je 's avonds van kantoor vertrekt (of thuis wilt zijn)
        // Marge als het werk uit de agenda komt: zo veel minuten eerder op kantoor, en zo veel minuten na het einde van kantoor weg
        Number($"reis_marge_voor_{person}", $"Reis marge voor {name}", min: 0, max: 120, step: 5, mode: "box", unit: "min", icon: "mdi:timer-sand", start: 10),
        Number($"reis_marge_na_{person}", $"Reis marge na {name}", min: 0, max: 120, step: 5, mode: "box", unit: "min", icon: "mdi:timer-sand-complete", start: 10),
        // De trein of bus die je kiest in het venster van een OV-reis (het vertrek volgens de dienstregeling, "jjjj-mm-dd uu:mm"); leeg is de eerste reis
        // Aan als de persoon niet in de trein stapte die hij moest nemen (zie CommuteReport): de route is dan niet meer nodig
        Boolean($"reis_niet_{person}_work", $"Reis niet {name} work", icon: "mdi:train-variant"),
        Boolean($"reis_niet_{person}_home", $"Reis niet {name} home", icon: "mdi:train-variant"),
        // De eerstvolgende afspraak van vandaag die nog niet voorbij is ("begin|eind|titel", begin en eind als "jjjj-mm-dd uu:mm"), voor de overige routes op Home
        // Aan zodra de persoon voor die afspraak op het station van Nijmegen aankwam: de overige routes verdwijnen dan van Home
        // Aan zodra de overige routes van de persoon op Home komen: zes uur voor de afspraak, en alleen als de persoon dan thuis is. Ze blijven daarna staan.
        Boolean($"afspraak_route_{person}", $"Afspraak route {name}", icon: "mdi:map-marker-path"),
        Boolean($"afspraak_aangekomen_{person}", $"Afspraak aangekomen {name}", icon: "mdi:map-marker-check"),
        Text($"afspraak_{person}", $"Afspraak {name}", min: 0, max: 100, mode: "text", icon: "mdi:calendar-clock"),
        // De afspraak waarvoor de overige routes met "Niet tonen" zijn verborgen: een andere afspraak toont ze weer
        Text($"afspraak_verborgen_{person}", $"Afspraak verborgen {name}", min: 0, max: 100, mode: "text", icon: "mdi:eye-off-outline"),
        // Welke overige route "Stuur door" doorstuurt (de sensor); leeg is de route van de dag
        Text($"reis_doorsturen_route_{person}", $"Reis doorsturen route {name}", min: 0, max: 100, mode: "text", icon: "mdi:share-variant"),
        // De route naar huis zoals hij was toen de persoon de zone van het werk verliet (CommuteReport): reistijd, vertraging en aankomst blijven dan staan
        Text($"reis_vast_{person}", $"Reis vast {name}", min: 0, max: 255, mode: "text", icon: "mdi:pin-outline"),
        Text($"reis_keuze_{person}_work",$"Reis keuze {name} work", min: 0, max: 40, mode: "text", icon: "mdi:train"),
        Text($"reis_keuze_{person}_home", $"Reis keuze {name} home", min: 0, max: 40, mode: "text", icon: "mdi:train"),
        Button($"reis_doorsturen_{person}", $"Reis doorsturen {name}", icon: "mdi:share-variant"),
        Button($"reis_doorsturen_zelf_{person}", $"Reis doorsturen zelf {name}", icon: "mdi:send"),
        Button($"sync_agenda_{person}", $"Sync agenda {name}", icon: "mdi:calendar-sync"),
        Boolean($"werk_uit_agenda_{person}", $"Werk uit agenda {name}", icon: "mdi:calendar-sync", start: person == "carleen"),
        Select($"reis_ochtend_soort_{person}", $"Reis ochtend soort {name}", [Models.Persons.OfficeTravel.MorningArrive, Models.Persons.OfficeTravel.MorningDepart], icon: "mdi:weather-sunset-up"),
        Time($"reis_ochtend_tijd_{person}", $"Reis ochtend tijd {name}", "08:20:00"),
        Select($"reis_avond_soort_{person}", $"Reis avond soort {name}", [Models.Persons.OfficeTravel.EveningLeave, Models.Persons.OfficeTravel.EveningArrive], icon: "mdi:weather-sunset-down"),
        Time($"reis_avond_tijd_{person}", $"Reis avond tijd {name}", "17:10:00"),
        // Wat voor vandaag is gekozen op het dashboard (met de auto in plaats van het OV, of andersom); 's nachts weer volgens het schema (CommuteReport)
        Select($"vervoer_vandaag_{person}", $"Vervoer vandaag {name}",
            [Models.Persons.WorkSchedule.FollowSchedule, Models.Persons.WorkSchedule.Car, Models.Persons.WorkSchedule.PublicTransport], icon: "mdi:swap-horizontal"),
        .. Models.Persons.WorkSchedule.DayNames.Values.Select(day =>
            Select($"vervoer_{person}_{day}", $"Vervoer {name} {day}", [Models.Persons.WorkSchedule.Car, Models.Persons.WorkSchedule.PublicTransport], icon: "mdi:car-clock"))
    ];

    private static HelperDefinition Boolean(string id, string name, string? icon = null, string? copyFrom = null, bool? start = null) =>
        new("input_boolean", id, name, Settings(("icon", icon)), copyFrom, StartValue: start == null ? null : start.Value ? "on" : "off");

    private static HelperDefinition Button(string id, string name, string? icon = null) =>
        new("input_button", id, name, Settings(("icon", icon)));

    private static HelperDefinition Number(string id, string name, double min, double max, double? step = null,
        string? mode = null, string? unit = null, string? icon = null, double? start = null) =>
        new("input_number", id, name, Settings(("min", min), ("max", max), ("step", step), ("mode", mode),
            ("unit_of_measurement", unit), ("icon", icon)), StartValue: start?.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static HelperDefinition Select(string id, string name, IReadOnlyList<string> options, string? icon = null) =>
        new("input_select", id, name, Settings(("options", options), ("icon", icon)));

    private static HelperDefinition DateTime(string id, string name, bool hasDate, bool hasTime, string? icon = null) =>
        new("input_datetime", id, name, Settings(("has_date", hasDate), ("has_time", hasTime), ("icon", icon)));

    /// <summary>A time of day (input_datetime without a date), starting at <paramref name="start"/> (<c>HH:mm:ss</c>).</summary>
    private static HelperDefinition Time(string id, string name, string start, string? copyFrom = null) =>
        new("input_datetime", id, name, Settings(("has_date", false), ("has_time", true), ("icon", "mdi:clock-outline")), copyFrom,
            StartValue: start);

    /// <summary>
    /// A weekday of someone's work schedule: Vrij, Thuis or Kantoor (see <see cref="Models.Persons.WorkSchedule"/>). Copying
    /// from an old office day boolean turns on into Kantoor and off into Thuis. A new one starts at Vrij, the first option.
    /// </summary>
    private static HelperDefinition WorkDay(string id, string name, string? copyFrom = null) =>
        new("input_select", id, name,
            Settings(("options", new[] { Models.Persons.WorkSchedule.Off, Models.Persons.WorkSchedule.Home, Models.Persons.WorkSchedule.Office }),
                ("icon", "mdi:briefcase-variant-outline")),
            copyFrom,
            new Dictionary<string, string> { ["on"] = Models.Persons.WorkSchedule.Office, ["off"] = Models.Persons.WorkSchedule.Home });

    private static HelperDefinition Text(string id, string name, int? min = null, int? max = null, string? mode = null,
        string? pattern = null, string? icon = null) =>
        new("input_text", id, name, Settings(("min", min), ("max", max), ("mode", mode), ("pattern", pattern), ("icon", icon)));

    private static HelperDefinition Counter(string id, string name, long? initial = null, long? step = null, long? minimum = null,
        long? maximum = null, bool? restore = null, string? icon = null) =>
        new("counter", id, name, Settings(("initial", initial), ("step", step), ("minimum", minimum), ("maximum", maximum),
            ("restore", restore), ("icon", icon)));

    /// <summary>The settings that are given, without the ones left empty (Home Assistant then uses its default).</summary>
    private static IReadOnlyDictionary<string, object> Settings(params (string Key, object? Value)[] settings) =>
        settings.Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value!);
}
