# Delivery Driver

Unity tabanli bir acik dunya teslimat oyunu prototipi. Proje, surus fizigi, gorev/quest sistemi, NPC trafik AI, mahalle bolgeleri, minimap ve performans optimizasyonu gibi birden fazla sistemi tek bir sahnede birlestirir.

## Icindekiler
- [Proje Ozeti](#proje-ozeti)
- [Temel Ozellikler](#temel-ozellikler)
- [Teknoloji ve Surumler](#teknoloji-ve-surumler)
- [Kurulum](#kurulum)
- [Hizli Baslangic](#hizli-baslangic)
- [Kontroller](#kontroller)
- [Oynanis Dongusu](#oynanis-dongusu)
- [Sistem Mimarisi](#sistem-mimarisi)
- [Veri, Kayit ve Veritabani](#veri-kayit-ve-veritabani)
- [Klasor Yapisi](#klasor-yapisi)
- [Editor ve Debug Araclari](#editor-ve-debug-araclari)
- [Performans Test Araclari](#performans-test-araclari)
- [Build Alma](#build-alma)
- [Sorun Giderme](#sorun-giderme)
- [Ek Dokumanlar](#ek-dokumanlar)

## Proje Ozeti
Bu proje su hedefleri olan bir teslimat simulasyonu uzerine kurulu:

- Oyuncu, sehir icinde paket teslimat gorevleri alir.
- Gorevler farkli tiplerde uretilir (standart, zamanli, kirilgan, multi-stop).
- Gorev odulleri; mesafe, sure baskisi, kargo tipi, risk ve surus performansina gore hesaplanir.
- Trafik sistemi, yol grafi uzerinden NPC araclari surer ve davranis optimizasyonu uygular.
- Progresyon sistemi para, XP, level, basarimlar ve hafif bir skill tree yapisi sunar.

Proje aktif gelistirme asamasindadir; roadmap dosyasinda tamamlanan ve planlanan adimlar tutulur.

## Temel Ozellikler

### 1) Surus ve Arac Sistemi
- `CarController` ile wheel collider tabanli surus fizigi
- Hiz-duyarli direksiyon
- El freni / drift puanlama
- Sert fren algilama (quest ceza/feedback entegrasyonu)
- Geri viteste reverse kamera HUD destegi

### 2) Teslimat ve Gorev Sistemi
- `DeliveryManager` ile runtime teslimat olusturma
- Telefon benzeri gorev teklifi UI (`PhoneMissionUI`)
- Pickup ve delivery indicatorlari
- Mahalle bazli hedef secimi ve tekrar azaltma kurallari
- Minimap objective marker + edge indicator
- Speedometer HUD

### 3) Quest Altyapisi
- `QuestManager` ile aktif/available/completed quest akisi
- Streak, bonus ve ceza hesaplari
- Daily challenge uretimi
- Kargo hasar takibi ve basarisizlik nedenleri
- Quest marker pooling

### 4) Progresyon
- `PlayerProgressionManager` ile para/XP/level yonetimi
- Basarimlar
- Detayli oyuncu istatistikleri
- `DriverProgressionSystem` ile skill tree:
  - FuelEfficiency
  - CargoDurability
  - RouteAssist

### 5) Trafik AI ve Sehir Sistemleri
- `RoadGraphBuilder` ile EasyRoads3D + SimplePoly yollarindan yol grafigi uretimi
- `NpcSpawner` ile dagitik NPC spawn ve pooling
- `NpcCarAgent` ile path-following, obstacle avoidance, lane davranisi
- `TrafficSimulationOptimizer` ile mesafeye dayali update throttling
- `WeatherManager` ile hava durumunun trafik davranisina etkisi

### 6) Optimizasyon ve Streaming
- `WorldChunkManager` + `WorldChunk` ile halka (near/mid/far) bazli chunk yonetimi
- `HLODGroup` + `HLODProxy` ile uzak mesafe proxy gecisleri
- `PerformanceOptimizationManager` ile layer culling + kalite ayari
- `RuntimeOptimizationBootstrap` ile gecikmeli sistem bootstrapping

### 7) UI Katmani
- Delivery UI
- Quest UI (active list, completion/failure, statistics)
- Pause/Settings/Save-Load UI
- Skill tree UI
- Mahalle giris bildirimi UI

## Teknoloji ve Surumler
- **Unity Editor**: `6000.3.9f1`
- **Render Pipeline**: URP varliklari mevcut (Settings klasoru)
- **Input**: Unity Input System (`com.unity.inputsystem`)
- **Kamera**: Cinemachine paketi yuklu (`com.unity.cinemachine`)
- **Veritabani**: SQLite (schema/seed + runtime sync)
- **Dil**: C#

### Paketler (manifest'ten)
- `com.unity.inputsystem` 1.18.0
- `com.unity.cinemachine` 3.1.5
- `com.unity.feature.worldbuilding` 1.0.1
- Unity core module paketleri

## Kurulum

### Gereksinimler
- Unity Hub
- Unity `6000.3.9f1`
- Windows ortaminda acilis onerilir (repo bu ortamda gelistirildi)

### Adimlar
1. Repoyu klonlayin:
   ```bash
   git clone <repo-url>
   ```
2. Unity Hub > **Add** ile proje klasorunu secin.
3. Unity Hub, projeyi `6000.3.9f1` ile acsin.
4. Ilk import tamamlanana kadar bekleyin.

## Hizli Baslangic
1. `Assets/Scenes/SampleScene.unity` sahnesini acin.
2. Sahne ilk acilista su sistemleri bulundurmalidir:
   - QuestManager
   - PlayerProgressionManager
   - SaveManager
   - DeliveryManager
   - RoadGraphBuilder
   - NpcSpawner
3. Play'e basin.
4. Baslangicta telefon gorev teklifi UI gelirse gorevi kabul ederek donguyu baslatin.

## Kontroller

| Aksiyon | Klavye | Gamepad | Not |
|---|---|---|---|
| Araci sur (ileri/geri/yon) | `WASD` veya Ok tuslari | Sol analog | `InputSystem_Actions` icinde `Move` |
| El freni / drift | `Space` | South button (A/X) | `CarController`, `Jump` action'i kullanir |
| Pause menusu | `Esc` | - | `PauseMenuUI` |
| Skill tree paneli | `K` | - | `ProgressionSkillTreeUI` |
| Quest debug menusu | `F1` | - | Sadece Editor/Development build |
| Memory overlay | `F3` | - | `MemoryProfiler` |
| Force GC | `F4` | - | Memory profiler acikken |
| Memory CSV export | `F5` | - | `BenchmarkResults/memory_profile.csv` |

Not: Input action asset icinde `Interact (E)`, `Sprint (LeftShift)`, `Previous (1)`, `Next (2)` gibi ek actionlar da tanimli.

## Oynanis Dongusu
1. Oyuncuya telefon gorev teklifi gelir.
2. Gorev kabul edilince pickup noktasi olusur.
3. Oyuncu pickup bolgesine yaklasinca paket alinmis sayilir.
4. Delivery noktasi/rotasi aktif olur (multi-stop destekli).
5. Gorev sonunda:
   - Odul/ceza hesaplanir
   - XP/para eklenir
   - Progresyon ve istatistikler guncellenir
   - Quest kaydi save + (aktifse) SQLite tarafina senkronlanir

## Sistem Mimarisi

### Cekirdek Oyun
- `Assets/Scripts/CarController.cs`
- `Assets/Scripts/CameraFollow.cs`
- `Assets/Scripts/DeliveryManager.cs`
- `Assets/Scripts/DeliveryBox.cs`
- `Assets/Scripts/DeliveryUI.cs`

### Quest + Progresyon
- `Assets/Scripts/Quest/QuestManager.cs`
- `Assets/Scripts/Quest/QuestData.cs`
- `Assets/Scripts/Quest/QuestDatabase.cs`
- `Assets/Scripts/Quest/PlayerProgressionManager.cs`
- `Assets/Scripts/Quest/DriverProgressionSystem.cs`
- `Assets/Scripts/Quest/UI/*`

### Trafik + Yol
- `Assets/Scripts/RoadGraphBuilder.cs`
- `Assets/Scripts/RoadGraphTypes.cs`
- `Assets/Scripts/NpcSpawner.cs`
- `Assets/Scripts/NpcCarAgent.cs`
- `Assets/Scripts/TrafficSimulationOptimizer.cs`
- `Assets/Scripts/WeatherManager.cs`

### Performans
- `Assets/Scripts/PerformanceOptimizationManager.cs`
- `Assets/Scripts/WorldChunkManager.cs`
- `Assets/Scripts/WorldChunk.cs`
- `Assets/Scripts/HLODGroup.cs`
- `Assets/Scripts/HLODProxy.cs`
- `Assets/Scripts/RuntimeOptimizationBootstrap.cs`

## Veri, Kayit ve Veritabani

### Save Sistemi (JSON)
- Yonetici: `SaveManager`
- Dosya: `savegame.json`
- Konum: `Application.persistentDataPath`
- Auto-save: varsayilan acik (300 sn)

Kaydedilen ana alanlar:
- Oyuncu para/XP/level
- Quest durumu ve aktif gorevler
- Skill tree durumlari
- Oyun istatistikleri

### SQLite Quest Veritabani
- Bootstrap: `QuestDatabaseBootstrap`
- Service: `QuestDatabaseService`
- Auto Sync: `QuestDatabaseAutoSync`
- DB dosyasi: `quest.db` (`Application.persistentDataPath`)
- SQL kaynaklari:
  - `Assets/StreamingAssets/Database/schema.sql`
  - `Assets/StreamingAssets/Database/seed.sql`

Notlar:
- `Mono.Data.Sqlite` provider gerekli.
- `Assets/Plugins/x86_64/sqlite3.dll` repo icinde bulunur.

## Klasor Yapisi
```text
Assets/
  Scenes/                     Ana oynanis sahnesi (SampleScene)
  Scripts/                    Tum oyun, UI, AI ve optimizasyon scriptleri
    Quest/                    Quest/progresyon/save/UI alt sistemi
    Neighborhood/             Mahalle bolge ve UI sistemi
    Performance/              Benchmark, memory profiler, regression detector
    Editor/                   Editor window ve setup toollari
  Resources/                  QuestDatabase ve CargoLibrary gibi runtime yuklenen assetler
  StreamingAssets/Database/   SQLite schema ve seed SQL dosyalari
  Database/                   SQL referans dosyalari
  Prefabs/                    Quest/NPC prefableri
  Plugins/x86_64/             Native sqlite3 dll
ProjectSettings/              Unity proje ayarlari
Packages/                     Unity package manifest ve lock
```

## Editor ve Debug Araclari
- `QuestUISetup`: Quest UI elemanlarini sahnede otomatik kurar.
- `QuestSystemValidator`: Quest baglantilarini runtime validate eder.
- `ChunkSetupTool`, `HLODSetupTool`: dunya optimizasyon kurulumu.
- `NpcPrefabCreator`, `NpcPrefabFixer`: NPC prefab yardimcilari.
- `RouteVisualizerEditor`: yol/rota gorsellestirme araclari.

## Performans Test Araclari
- `PerformanceBenchmark`: otomatik kamera rotasinda FPS/CPU/RAM olcumu.
- `PerformanceRegressionDetector`: baseline'a gore performans gerilemesi takibi.
- `MemoryProfiler`: runtime memory snapshot/leak detection + CSV export.

Benchmark ciktilari varsayilan olarak proje kokundeki `BenchmarkResults/` klasorune yazilir.

## Build Alma
1. `File > Build Settings` acin.
2. `Assets/Scenes/SampleScene.unity` sahnesinin listede oldugunu kontrol edin.
3. Hedef platformu secin (PC onerilir).
4. Build alin.

Not: Mobil hedeflerde kalite, input ve performans ayarlari icin ek tuning gerekir.

## Sorun Giderme

### 1) Gorev UI gorunmuyor
- Sahneye bos bir obje ekleyip `QuestUISetup` component'i takin.
- Play modunda setup'in tamamlanmasini bekleyin.
- Alternatif: `Assets/Scripts/Quest/UI/UI_KURULUM_TALIMATLARI.txt` dosyasini izleyin.

### 2) NPC araclar spawn olmuyor
- `RoadGraphBuilder` grafigi olusturuyor mu kontrol edin.
- `NpcSpawner` icinde `roadGraphBuilder` referansini dogrulayin.

### 3) SQLite baglanmiyor
- Console'da `Mono.Data.Sqlite not available` hatasi var mi bakin.
- `sqlite3.dll` dosyasinin plugin import ayarlarini kontrol edin.

### 4) Save/Load calismiyor
- `SaveManager` sahnede aktif mi kontrol edin.
- `Application.persistentDataPath` altinda `savegame.json` olusuyor mu bakin.

## Ek Dokumanlar
- `DEVELOPMENT_ROADMAP.md`: Gelistirme hedefleri ve sprint durumlari
- `QUEST_DATABASE_GUIDE.md`: Quest veritabani modeli
- `Assets/Scripts/Editor/ROUTE_VISUALIZER_README.md`: Rota araclari dokumani
- `Assets/Scripts/Quest/UI/UI_KURULUM_TALIMATLARI.txt`: UI kurulum adimlari

---
Bu README, repo durumu baz alinarak hazirlanmistir. Yeni sistemler eklendikce ilgili bolumlerin guncellenmesi onerilir.

