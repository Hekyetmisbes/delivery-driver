# Delivery Driver

**Delivery Driver**, Unity (Unity 6 / `6000.3.x`) ile geliştirilen açık dünya araç sürüşü, teslimat simülasyonu, şirket/filo yönetimi ve sinematik tanıtım araçlarını birleştiren kapsamlı bir oyun projesidir.

Proje; gerçekçi WheelCollider tabanlı araç fiziği, spline tabanlı otonom NPC trafik yapısı, akıllı telefon üzerinden görev teklifleri, çok kademeli teslimat/quest mekanikleri, SQLite ve JSON hibrit veri kalıcılığı ve tanıtım/sosyal medya için **16:9 Trailer & 9:16 Instagram Reels 60 FPS Video Kayıt & Yönetmen Sistemi** içerir.

---

## 📋 İçindekiler
- [Proje Özeti ve Vizyon](#proje-özeti-ve-vizyon)
- [Temel Sistemler](#temel-sistemler)
  - [1. Sürüş ve Araç Fiziği](#1-sürüş-ve-araç-fiziği)
  - [2. Görev ve Teslimat Döngüsü](#2-görev-ve-teslimat-döngüsü)
  - [3. Şirket, Filo ve Yakıt Sistemi](#3-şirket-filo-ve-yakıt-sistemi)
  - [4. Spline Tabanlı Akıllı Trafik AI](#4-spline-tabanlı-akıllı-trafik-ai)
  - [5. Sinematik Çekim ve Yönetmen Sistemi (Reels & Trailer)](#5-sinematik-çekim-ve-yönetmen-sistemi-reels--trailer)
  - [6. Navigasyon, Minimap ve HUD](#6-navigasyon-minimap-ve-hud)
  - [7. Progresyon ve Skill Tree](#7-progresyon-ve-skill-tree)
  - [8. Veri Kalıcılığı (JSON & SQLite)](#8-veri-kalıcılığı-json--sqlite)
- [Teknoloji ve Paketler](#teknoloji-ve-paketler)
- [Sahneler ve Başlatma (Bootstrap Mimarisi)](#sahneler-ve-başlatma-bootstrap-mimarisi)
- [Klavye ve Gamepad Kontrolleri](#klavye-ve-gamepad-kontrolleri)
- [Klasör Yapısı](#klasör-yapısı)
- [Editor ve Araçlar](#editor-ve-araçlar)
- [Build Alma](#build-alma)

---

## Proje Özeti ve Vizyon
Oyuncu, kurye olarak başladığı kariyerinde şehir içinde çeşitli kargo ve teslimat görevleri alır:
* **Görev Çeşitliliği:** Standart, zamana duyarlı (ekspres), kırılgan kargo, soğuk zincir ve çok duraklı (multi-stop) teslimatlar.
* **Sürüş Değerlendirmesi:** Hız, sert fren, kargo hasarı, viraj alma ve drift performansına göre dinamik prim ve ceza hesapları.
* **Şirket Büyümesi:** Minivan ile başlayıp kamyon/tır filosu kurma, araç parçalarını yükseltme ve benzin istasyonlarından yakıt ikmali yapma.
* **Tanıtım & Medya Üretimi:** Oyun içinden tek tuşla 9:16 Instagram Reels/TikTok veya 16:9 YouTube/Steam tanıtım videoları için çok açılı, otomatik yönetmen kesimli ve akıcı 60 FPS MP4 video kaydetme.

---

## Temel Sistemler

### 1. Sürüş ve Araç Fiziği
* **`CarController`:** WheelCollider tabanlı, hıza duyarlı direksiyon açısı, motor torku, el freni, drift algılama ve sert fren olayları.
* **`CameraFollow` & `VehicleCameraAnchors`:** Araç boyutuna (van vs. kamyon/tır) göre otomatik pozisyon alan, geri viteste açısını kilitleyen ve `ReverseCameraHUD` ile ayna/geri görüş sunan dinamik kamera.
* **Görsel Efektler:** `VehicleExhaustSmoke` ile egzoz dumanı, `TruckWheelVisuals` ile çok akslı tır tekerlek senkronizasyonu.

### 2. Görev ve Teslimat Döngüsü
* **`DeliveryManager`:** Sahnedeki teslimat akışını yönetir; harita üzerinde mahalle ve yol verilerine göre pickup/dropoff noktaları üretir.
* **`PhoneMissionUI` / `SmartphoneUI`:** Görevler doğrudan oyuncunun sanal akıllı telefonuna bildirim/çağrı olarak gelir (Kabul / Reddet akışı zorunludur).
* **`QuestManager`:** Görevlerin otorite merkezidir. Kargo tipi (ağır, kırılgan, değerli), rota tamamlama, streak bonusları, zaman kısıtları ve hasar durumunu takip eder.

### 3. Şirket, Filo ve Yakıt Sistemi
* **`CompanyPageUI` & `PlayerVehicleManager`:** Minivan, Panelvan ve Ağır Vasıta (Kamyon/Tır) araçları arasında geçiş, filo satın alımı ve araç yönetimi.
* **`FleetManagementUI` & `FleetInteractionPoint`:** Şirket garajında araçları inceleme ve yükseltme (`VehicleUpgradeData`).
* **`FuelSystem` & `FuelStationZone`:** Araçların yakıt tüketimi, benzin istasyonlarında interaktif yakıt ikmali (`FuelStationUI`) ve ekranda yakıt HUD'ı (`FuelHudUI`).

### 4. Spline Tabanlı Akıllı Trafik AI
* **`RoadNetworkManager`:** Şehrin yol ağını `SplineRoad` eğrileri ve `RoadIntersection` kavşakları üzerinden temsil eden modern otorite.
* **`SplinePathfinder`:** A* algoritması ile kavşaklar ve spline yollar üzerinden rotalama.
* **`NpcCarAgent`:** Pure Pursuit direksiyon algoritması ile pürüzsüz viraj alma, engel algılama, şerit takibi ve kavşak geçiş öncelikleri.
* **`NpcSpawner` & `NpcRecovery`:** Oyuncu etrafında dinamik NPC araç havuzlama ve takılan araçları kurtarma mantığı.

### 5. Sinematik Çekim ve Yönetmen Sistemi (Reels & Trailer)
* **`CinematicCameraController`:** 7 farklı profesyonel sinematik kamera açısı (Kaput, Jant/Drift, Kapı Profili, Alçak Egzoz, Drone/Helikopter, Akıllı Yol Kenarı Geçişi ve Takip).
* **Format Desteği:** Tek tuşla **16:9 Trailer (1920x1080)** ve **9:16 Dikey Instagram Reels (1080x1920)** kadrajları arasında geçiş.
* **🎬 `CinematicDirector`:** Sen aracı sürerken 3-5 saniyede bir kamera açılarını otomatik olarak değiştiren akıllı yönetmen modu (hıza ve drifte duyarlı kesimler).
* **`CinematicRecorderController` (Unity Recorder `5.1.7`):** Oyun içi FPS düşse bile kare kare render alarak **sıfır frame-drop, tereyağı gibi akıcı 60 FPS MP4** dosyasını doğrudan `Recordings/Reels/` veya `Recordings/Trailers/` dizinlerine kaydeder.
* **Temiz Çekim (Clean Footage):** Kayıt esnasında oyun HUD'ını otomatik gizleyerek tertemiz trailer çıktısı alma.

### 6. Navigasyon, Minimap ve HUD
* **`NavigationService`:** Tüm yönlendirme servisinin tekil kaynağı (Single Source of Truth).
* **`MinimapUI` & `CompassUI`:** Rota çizgisi, hedef ikonları, dinamik zoom ve yol katmanı.
* **`GlobalUiCoordinator`:** Sahne geçişlerinde kaybolmayan persistant global Canvas mimarisi.
* **`SpeedometerUI`:** Ayarlar menüsünden değiştirilebilir **KMH / MPH** hız göstergesi.
* **`BalanceHudUI`:** Güncel bakiye, kazanılan paralar ve animasyonlu gelir bildirimleri.

### 7. Progresyon ve Skill Tree
* **`PlayerProgressionManager`:** Seviye, XP, para, teslimat istatistikleri ve başarımlar.
* **`DriverProgressionSystem`:** Sürücü yetenek ağacı (Yakıt Verimliliği, Kargo Dayanıklılığı, Navigasyon Kolaylığı vb.).

### 8. Veri Kalıcılığı (JSON & SQLite)
* **JSON (`SaveManager`):** `Application.persistentDataPath/savegame.json` üzerinde oyuncu seviyesi, parası, kilit açılımları ve tamamlanan görevler.
* **PlayerPrefs (`GameSettings`):** Ses, grafik kalitesi, çözünürlük, hedef FPS, hız birimi (KMH/MPH), dil, erişilebilirlik ve UI ölçeği.
* **SQLite (`QuestDatabaseService`):** `quest.db` veritabanı üzerinden ayrıntılı teslimat geçmişi, şirket verileri ve görev kütüphanesi senkronizasyonu (`schema.sql` / `seed.sql`).

---

## Teknoloji ve Paketler
* **Unity Versiyonu:** `6000.3.9f1` / `6000.3.12f1` (Unity 6)
* **Render Pipeline:** Universal Render Pipeline (URP)
* **Girdi Sistemi:** New Input System (`com.unity.inputsystem: 1.19.0`)
* **Kamera:** Unity Cinemachine (`com.unity.cinemachine: 3.1.6`)
* **Video Kayıt:** Unity Recorder (`com.unity.recorder: 5.1.7`)
* **Zaman Çizelgesi:** Unity Timeline (`com.unity.timeline: 1.8.11`)
* **Veritabanı:** Mono.Data.Sqlite (`Assets/Plugins/x86_64/sqlite3.dll`)

---

## Sahneler ve Başlatma (Bootstrap Mimarisi)

Build ayarlarında yalnızca iki sahne etkindir:
1. **`Assets/Scenes/MainMenu.unity`:** Tamamen kod tabanlı (`MainMenuRuntimeUI`) inşa edilen, ayarlar ve profil menüsünü barındıran başlangıç sahnesi.
2. **`Assets/Scenes/Game.unity`:** Açık dünya teslimat ortamı (Git LFS takip edilir).

> [!NOTE]
> Projede runtime bootstrap sistemleri kullanılır (`CinematicCaptureInstaller`, `GameSceneCompanyPageInstaller`, `FuelSystemInstaller`, `GlobalUiCoordinator`, `QuestDatabaseBootstrap` vb.). Birçok sistem sahne yüklendiğinde kendisini otomatik olarak oluşturur; sahne üzerinde gereksiz statik nesne çoğaltılmasından kaçınılmalıdır.

---

## Klavye ve Gamepad Kontrolleri

### Sürüş ve Oynanış
| Aksiyon | Klavye | Gamepad | Açıklama |
|---|---|---|---|
| **Sürüş (Gaz/Fren/Direksiyon)** | `WASD` / Ok Tuşları | Sol Analog | `Move` Action |
| **El Freni / Drift** | `Space` | South Button (A/X) | Wheel collider fren kilidi |
| **Akıllı Telefon (Görevler)** | `Tab` / `T` | D-Pad Up | Görev teklifleri ve kargo detayları |
| **Etkileşim (İstasyon / Şirket)** | `E` | West Button (X/Square) | Yakıt alma veya garaj etkileşimi |
| **Yetenek Ağacı (Skill Tree)** | `K` | - | Sürücü gelişim menüsü |
| **Duraklatma / Ayarlar** | `Esc` | Start Button | `PauseMenuUI` |

### 🎬 Sinematik Çekim & Video Kayıt (Reels & Trailer)
| Tuş | Fonksiyon | Açıklama |
|---|---|---|
| **`1` - `7`** | **Kamera Açıları** | Kaput, Jant, Profil, Egzoz, Drone, Ralli Geçişi, Takip |
| **`F7`** | **Otomatik Yönetmen (Director)** | 3-5 saniyede bir kamera açılarını otomatik keser |
| **`F8`** | **Format Değiştir** | 16:9 Trailer ↔ 9:16 Instagram Reels/TikTok geçişi |
| **`F9`** | **60 FPS Video Kaydı** | MP4 video kaydını başlatır / durdurur (`Recordings/`) |
| **`F10`** | **Ağır Çekim (Slow-Mo)** | Drift/viraj anları için 0.25x ağır çekim |
| **`H`** | **Sinematik HUD** | Ekrandaki durum panelini gizler / gösterir |

### Debug ve Profiler (Development)
| Tuş | Fonksiyon |
|---|---|
| `F1` | Quest Debug Menüsü |
| `F3` | Bellek & RAM Profiler Göstergesi |
| `F4` | Manuel Garbage Collection (GC.Collect) |
| `F5` | Bellek Verilerini CSV Olarak Dışa Aktar |

---

## Klasör Yapısı

```text
Assets/
  Art/                        Materyaller, dokular ve çevre kaplamaları
  Editor/
    Build/                    Build ve derleme otomasyonları
    Cinematic/                CinematicCaptureWindow ve RecorderController
    City/                     Şehir inşa ve düzenleme araçları
    Optimization/             Optimizasyon ve chunk yardımcıları
    Traffic/                  Spline yol düzenleyicileri ve trafik araçları
  Prefabs/
    NPCs/                     Trafik araç prefableri
    Quest/                    Görev markerları ve teslimat kutuları
    Vehicle/                  Oyuncu araçları (Minivan, LorryCargo vb.)
  Resources/                  CargoLibrary, QuestDatabase ve UI varlıkları
  Scenes/
    MainMenu.unity            Kod tabanlı ana menü sahnesi
    Game.unity                Açık dünya oynanış sahnesi (Git LFS)
  Scripts/
    City/                     Mahalle bölgeleri ve sokak isimlendirmeleri
    Company/                  Şirket sayfası, filo yönetimi ve araç seçimi
    Core/                     Ayarlar, JSON save ve SQLite veritabanı
    Gameplay/
      Camera/                 CameraFollow, ReverseCameraHUD ve Cinematic/
      Delivery/               DeliveryManager, PhoneMissionUI, DeliveryBox
      Vehicle/                CarController, FuelSystem, Anchors, Smoke
    Navigation/               NavigationService, ObjectiveMarker, RouteRenderer
    Optimization/             WorldChunkManager, HLODGroup, AdvancedObjectPool
    Performance/              MemoryProfiler ve performans benchmarkları
    Quest/                    QuestManager, PlayerProgression, SkillTree, UI/
    Traffic/                  Spline yollar, RoadNetworkManager, NpcCarAgent
    UI/                       GlobalUiCoordinator, HUD, Menus, Minimap, Core/
  Settings/                   URP ve New Input System ayarları
  StreamingAssets/Database/   SQLite schema.sql ve seed.sql
  Plugins/x86_64/             sqlite3.dll
```

---

## Editor ve Araçlar
* **Sinematik Stüdyo:** `Delivery Driver > Cinematic Capture & Director` menüsünden video kayıt, format ve açı kontrol penceresi.
* **Trafik & Yol:** `Delivery Driver > Traffic` üzerinden Spline yol ve kavşak düzenleme yardımcıları.
* **Şehir & Çevre:** `Delivery Driver > City` üzerinden bölge ve bina yerleşim araçları.
* **Performans Benchmark:** `BenchmarkResults/` klasörüne otomatik FPS, CPU ve RAM dökümü alan test modülü.

---

## Build Alma
1. `File > Build Profiles` veya `Build Settings` penceresini açın.
2. Sahne sıralamasının şu şekilde olduğundan emin olun:
   * `Assets/Scenes/MainMenu.unity` (0)
   * `Assets/Scenes/Game.unity` (1)
3. Platform olarak **Windows (x86_64)** seçin.
4. **Build** butonuna basın.

---
*Son güncelleme: Eylül 2026 - Delivery Driver Geliştirme Ekibi*
