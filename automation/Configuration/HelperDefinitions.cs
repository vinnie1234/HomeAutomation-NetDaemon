using Automation.Models.Helpers;

namespace Automation.Configuration;

/// <summary>
/// The helpers (input_boolean, input_number, counter, ...) the automations need. <see cref="apps.General.HelperManager"/>
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
        Number("pixel_antibraak_voorraad", "Pixel antibraak voorraad", min: 0, max: 500, step: 1, mode: "box", unit: "pillen", icon: "mdi:pill-multiple"),
        Button("pixel_prednison_gegeven", "Pixel prednison gegeven", icon: "mdi:pill"),
        Button("pixel_antibraak_gegeven", "Pixel antibraak gegeven", icon: "mdi:pill-multiple"),
        Time("pixel_prednison_tijd", "Pixel prednison tijd", "20:00:00"),

        // Werkrooster per persoon (WorkSchedule). Vincent neemt zijn oude instellingen over; Carleen en het weekend beginnen op Vrij.
        WorkDay("werkdag_vincent_maandag", "Werkdag Vincent maandag", copyFrom: "input_boolean.officeday_monday"),
        WorkDay("werkdag_vincent_dinsdag", "Werkdag Vincent dinsdag", copyFrom: "input_boolean.officeday_tuesday"),
        WorkDay("werkdag_vincent_woensdag", "Werkdag Vincent woensdag", copyFrom: "input_boolean.officeday_wednesday"),
        WorkDay("werkdag_vincent_donderdag", "Werkdag Vincent donderdag", copyFrom: "input_boolean.officeday_thursday"),
        WorkDay("werkdag_vincent_vrijdag", "Werkdag Vincent vrijdag", copyFrom: "input_boolean.officeday_friday"),
        WorkDay("werkdag_vincent_zaterdag", "Werkdag Vincent zaterdag"),
        WorkDay("werkdag_vincent_zondag", "Werkdag Vincent zondag"),
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
        Boolean("vincent_pc_afsluiten", "PC Afsluiten"),
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
        Button("start_pc", "Start PC", icon: "mdi:desktop-classic"),
        Button("startnewyear", "StartNewYear", icon: "mdi:firework"),
        Button("vacuumcleanbank", "VacuumCleanBank", icon: "mdi:robot-vacuum"),
        Button("vacuumcleangang", "VacuumCleanGang", icon: "mdi:robot-vacuum"),
        Button("vacuumcleankattenbak", "VacuumCleanKattenbak", icon: "mdi:robot-vacuum"),
        Button("vacuumcleanslaapkamer", "VacuumCleanSlaapkamer", icon: "mdi:robot-vacuum"),
        Button("vacuumcleanwoonkamer", "VacuumCleanWoonkamer", icon: "mdi:robot-vacuum"),
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

        // input_select
        Select("housemodeselect", "HouseModeSelect", options: ["Morning", "Day", "Evening", "Night"], icon: "mdi:home-alert"),
        Select("tijd_van_de_dag", "Tijd van de dag", options: ["Ochtend", "Middag", "Avond", "Nacht", "Automatisch"]),
        Select("water_usage_swelector", "Water Usage Selector", options: ["Default", "WC Klein", "WC Groot", "Afwas", "Douchen", "Wasmachine", "Anders", "Kraan"]),
        Select("weerkaart_thema", "weerkaart_thema", options: ["Automatisch", "Lente", "Zomer", "Herfst", "Winter", "Pasen", "Nieuwjaar", "Kerst", "Sinterklaas"], icon: "mdi:weather-hurricane"),

        // input_datetime
        DateTime("daytimeweekend", "DaytimeWeekend", hasDate: false, hasTime: true, icon: "mdi:clock-digital"),
        DateTime("nighttimeweekdays", "NighttimeWeekdays", hasDate: false, hasTime: true, icon: "mdi:clock-digital"),
        DateTime("nighttimeweekends", "NighttimeWeekends", hasDate: false, hasTime: true, icon: "mdi:clock-digital"),
        DateTime("zedarfeedfirsttime", "PixelFeedFirstTime", hasDate: false, hasTime: true, icon: "mdi:food-drumstick"),
        DateTime("zedarfeedfourthtime", "PixelFeedFourthTime", hasDate: false, hasTime: true, icon: "mdi:food-drumstick"),
        DateTime("zedarfeedsecondtime", "PixelFeedSecondTime", hasDate: false, hasTime: true, icon: "mdi:food-drumstick"),
        DateTime("zedarfeedthirdtime", "PixelFeedThirdTime", hasDate: false, hasTime: true, icon: "mdi:food-drumstick"),
        DateTime("zedarlastautomatedfeed", "PixelLastAutomatedFeed", hasDate: true, hasTime: true, icon: "mdi:clock-time-eight"),
        DateTime("zedarlastmanualfeed", "PixelLastManualFeed", hasDate: true, hasTime: true, icon: "mdi:clock-time-eight"),

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

    private static HelperDefinition Boolean(string id, string name, string? icon = null, string? copyFrom = null) =>
        new("input_boolean", id, name, Settings(("icon", icon)), copyFrom);

    private static HelperDefinition Button(string id, string name, string? icon = null) =>
        new("input_button", id, name, Settings(("icon", icon)));

    private static HelperDefinition Number(string id, string name, double min, double max, double? step = null,
        string? mode = null, string? unit = null, string? icon = null) =>
        new("input_number", id, name, Settings(("min", min), ("max", max), ("step", step), ("mode", mode),
            ("unit_of_measurement", unit), ("icon", icon)));

    private static HelperDefinition Select(string id, string name, IReadOnlyList<string> options, string? icon = null) =>
        new("input_select", id, name, Settings(("options", options), ("icon", icon)));

    private static HelperDefinition DateTime(string id, string name, bool hasDate, bool hasTime, string? icon = null) =>
        new("input_datetime", id, name, Settings(("has_date", hasDate), ("has_time", hasTime), ("icon", icon)));

    /// <summary>A time of day (input_datetime without a date), starting at <paramref name="initial"/> (<c>HH:mm:ss</c>).</summary>
    private static HelperDefinition Time(string id, string name, string initial, string? copyFrom = null) =>
        new("input_datetime", id, name, Settings(("has_date", false), ("has_time", true), ("initial", initial), ("icon", "mdi:clock-outline")), copyFrom);

    /// <summary>
    /// A weekday of someone's work schedule: Vrij, Thuis or Kantoor (see <see cref="Models.Persons.WorkSchedule"/>). Copying
    /// from an old office day boolean turns on into Kantoor and off into Thuis.
    /// </summary>
    private static HelperDefinition WorkDay(string id, string name, string? copyFrom = null) =>
        new("input_select", id, name,
            Settings(("options", new[] { Models.Persons.WorkSchedule.Off, Models.Persons.WorkSchedule.Home, Models.Persons.WorkSchedule.Office }),
                ("initial", Models.Persons.WorkSchedule.Off), ("icon", "mdi:briefcase-variant-outline")),
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
