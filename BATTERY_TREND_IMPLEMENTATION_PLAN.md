# Battery Trend Analysis Implementation Plan

## 📋 **Huidige Status: ALMOST COMPLETE** 
**Laatste update:** 2025-07-07 23:10

## 🎯 **Doelstellingen**
1. ✅ BatteryTrendAnalyzer app maken (KLAAR - volledig sync, tijd 23:00)
2. ✅ Records/Models naar Models/Battery/ verplaatsen (KLAAR)
3. ✅ EntityManager service maken voor entiteit beheer (KLAAR - volledig sync)
4. ✅ Entities automatisch aanmaken zonder duplicaten (KLAAR)
5. ✅ Dashboard configuratie maken met ApexCharts (KLAAR)
6. ⏳ Integratie testen en valideren
7. ⏳ Deze planning file verwijderen bij voltooiing

---

## 📝 **Gedetailleerde Taken**

### **Stap 1: EntityManager Service** ✅
**Status:** VOLTOOID
**Bestand:** `automation/Core/EntityManager.cs`

**Wat is er gedaan:**
- [x] EntityManager service gemaakt (volledig sync) ✅
- [x] Dubbele entities voorkomen (check of entity al bestaat) ✅
- [x] Interface gemaakt voor dependency injection ✅
- [x] Service geregistreerd in DI container ✅
- [x] Namespace conflict opgelost (Core i.p.v. Services) ✅

**Afhankelijkheden:** Geen

---

### **Stap 2: Models & Records uitplaatsen** ✅
**Status:** VOLTOOID
**Bestanden:** 
- `automation/Models/Battery/BatteryDeviceInfo.cs`
- `automation/Models/Battery/BatteryTrendResult.cs`

**Wat is er gedaan:**
- [x] BatteryDeviceInfo record aangemaakt in Models/Battery/ ✅
- [x] BatteryTrendResult record aangemaakt in Models/Battery/ ✅
- [x] Battery submapje aangemaakt ✅
- [x] Using statements toegevoegd in BatteryTrendAnalyzer ✅
- [x] Proper namespace conventie gebruikt (Automation.Models.Battery) ✅

**Afhankelijkheden:** Geen

---

### **Stap 3: BatteryTrendAnalyzer aanpassen** ✅  
**Status:** VOLTOOID
**Bestand:** `automation/apps/General/BatteryTrendAnalyzer.cs`

**Wat is er gedaan:**
- [x] Tijd ingesteld op 23:00 ✅
- [x] Volledig opnieuw gemaakt zonder async ✅
- [x] Using statements toegevoegd voor Models.Battery ✅
- [x] EntityManager dependency toegevoegd ✅
- [x] Error handling geïmplementeerd ✅
- [x] Uitgebreide logging toegevoegd ✅
- [x] Alle entity creation en state updates volledig sync ✅
- [x] Proactive notifications geïmplementeerd ✅

**Afhankelijkheden:** Models uitgeplaatst ✅, EntityManager service ✅

---

### **Stap 4: Entity Creation Logic** ✅
**Status:** GEÏNTEGREERD IN BATTERYTRENDANALYZER
**Bestand:** `automation/apps/General/BatteryTrendAnalyzer.cs`

**Wat is er gedaan:**
- [x] Entity creation geïntegreerd in BatteryTrendAnalyzer ✅
- [x] Template systeem voor entity naming geïmplementeerd ✅
- [x] Bulk entity creation voor alle devices ✅
- [x] Duplicate detection via EntityManager ✅
- [x] System-wide entities aangemaakt ✅

**Afhankelijkheden:** EntityManager service ✅

---

### **Stap 5: Dashboard Configuratie** ✅
**Status:** VOLTOOID  
**Bestand:** `BATTERY_DASHBOARD_CONFIG.yaml`

**Wat is er gedaan:**
- [x] ApexCharts configuratie gemaakt ✅
- [x] Bestaande battery sectie replacement config ✅
- [x] Trend grafieken toegevoegd ✅
- [x] Health status overzicht ✅
- [x] Shopping list sectie ✅
- [x] Maintenance calendar ✅
- [x] Multi-series charts voor discharge rates ✅
- [x] Interactive tooltips en annotations ✅

**Afhankelijkheden:** Werkende entities

---

### **Stap 6: Testing & Validatie** ⏳
**Status:** Nog niet gestart

**Wat moet er gebeuren:**
- [ ] Unit tests maken voor EntityManager
- [ ] Integration tests voor BatteryTrendAnalyzer  
- [ ] Mock data voor testing
- [ ] Performance testing

**Afhankelijkheden:** Alle vorige stappen

---

### **Stap 7: Cleanup** ⏳
**Status:** Nog niet gestart

**Wat moet er gebeuren:**
- [ ] Deze planning file verwijderen
- [ ] Code review en optimalisatie
- [ ] Documentatie bijwerken
- [ ] ARCHITECTURE_REVIEW.md updaten

**Afhankelijkheden:** Succesvolle implementatie

---

## 🔧 **Technische Details**

### **Entity Naming Convention:**
```
sensor.{device_name}_battery_discharge_rate
sensor.{device_name}_battery_days_remaining  
sensor.{device_name}_battery_age_days
sensor.{device_name}_battery_health_status
sensor.{device_name}_battery_replacement_date

// System wide
sensor.battery_devices_critical
sensor.battery_devices_warning  
sensor.next_battery_replacement
sensor.batteries_to_buy
```

### **Expected Battery Life Database:**
```csharp
"aa" => 365 dagen      // Motion sensors
"aaa" => 180 dagen     // Remotes
"cr2032" => 730 dagen  // Coin cells
"cr2450" => 1095 dagen // Large coin cells  
"9v" => 270 dagen      // 9V batteries
```

### **Health Status Logic:**
- **Critical:** ≤10% OR ≤7 dagen
- **Warning:** ≤25% OR ≤30 dagen  
- **Good:** ≤90 dagen remaining
- **Degrading:** Performing 30% worse than expected
- **Excellent:** > 90 dagen + normal performance

---

## 🚨 **Belangrijke Aandachtspunten**

### **Entity Duplication Prevention:**
- Check `HaContext.GetState()` voor bestaande entities
- Gebruik `CreateAsync()` met proper error handling
- Log creation successes en failures

### **Performance Optimizations:**
- Bulk entity operations waar mogelijk
- Cache entity lists tussen runs
- Minimale Home Assistant API calls

### **Error Scenarios:**
- Missing battery_type entities → Skip device
- Missing last_replaced entities → Skip device  
- Invalid date parsing → Use fallback date
- API timeouts → Retry logic

---

## 📊 **Dashboard Requirements**

### **Vervang huidige battery sectie met:**
1. **Health Overview Cards** - Critical/Warning counts
2. **Trend Graphs** - ApexCharts voor discharge rates
3. **Timeline View** - Replacement schedule  
4. **Shopping List** - Batteries needed per type
5. **Device Details** - Expandable per-device info

### **ApexCharts Features:**
- Multi-series line charts voor trends
- Gradient fills voor visual appeal  
- Annotations voor replacement dates
- Responsive design voor mobile
- Interactive tooltips met device info

---

## 🔄 **Volgende Sessie Acties**

**Wanneer je deze file krijgt:**
1. Lees deze volledige planning
2. Check huidige status van elke stap
3. Begin bij eerste incomplete stap
4. Update deze file met voortgang
5. Commit regelmatig met duidelijke messages

**Prioriteit volgorde:**
1. Models/Records naar juiste mappen structuur
2. EntityManager service (foundation)
3. BatteryTrendAnalyzer updates  
4. Entity creation testing
5. Integration testing
6. Cleanup en documentatie

## 📁 **Code Organisatie Eisen**

### **Models & Records Structuur:**
```
automation/Models/
├── Battery/
│   ├── BatteryDeviceInfo.cs
│   ├── BatteryTrendResult.cs
│   └── BatteryHealthStatus.cs (indien nodig)
├── NotificationModel.cs (bestaand)
├── PersonModel.cs (bestaand)
└── ... (andere bestaande models)
```

### **Namespace Conventies:**
```csharp
namespace Automation.Models.Battery;

public record BatteryDeviceInfo
{
    // Record definitie
}
```

### **Using Statements:**
```csharp
using Automation.Models.Battery;
// In de BatteryTrendAnalyzer.cs
```

**❗ BELANGRIJK:** Alle records en models MOETEN uit de app files gehaald worden en in de juiste Models mappenstructuur geplaatst worden!

---

**💡 REMINDER:** Vergeet niet om deze file te verwijderen wanneer alles klaar is!