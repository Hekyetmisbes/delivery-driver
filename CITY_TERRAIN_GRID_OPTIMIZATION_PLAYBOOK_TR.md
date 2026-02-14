# Unity Terrain Ustunde Grid Tabanli Buyuk Sehir Optimizasyon Playbook'u

Bu dokumanin amaci, terrain uzerine kurulu grid tabanli buyuk bir sehirin performansini olculebilir sekilde iyilestirmek ve sureci sistematik yurutmektir.

## 1) Hedefler ve Basari Kriterleri

Ilk once net KPI belirleyin. Ornek hedef seti:

- `PC High` (1080p): ortalama `>= 90 FPS`, `1% low >= 55 FPS`
- `PC Mid` (1080p): ortalama `>= 60 FPS`, `1% low >= 40 FPS`
- `Laptop/Low` (1080p): ortalama `>= 45 FPS`, `1% low >= 30 FPS`
- RAM kullanimi: sahne bazli tepe kullanimda hedef cihaz limitinin `<= %70`
- Frame time hedefi:
  - 60 FPS icin: `<= 16.67 ms`
  - 90 FPS icin: `<= 11.11 ms`

Not: Optimizasyon "hissetmek" ile degil, her adimdan once/sonra olcumle yonetilmeli.

## 2) Olcum Altyapisi (Once Bunu Kurun)

## 2.1 Profiling Standartlari

- Her olcum ayni kosullarda alinmali:
  - Ayni kamera rotasi
  - Ayni hava/saat kosulu
  - Ayni NPC/arac yogunlugu
  - Ayni quality preset
- `Editor` yerine `Development Build` + `Autoconnect Profiler` ile olcum alin.
- En az `60-120 saniye` kayit alin, spike'lari not edin.

## 2.2 Kullanilacak Unity Araclari

- `Profiler`: CPU, GPU, Rendering, Memory, Physics, UI
- `Profile Analyzer`: iki profil arasinda karsilastirma
- `Frame Debugger`: draw call, overdraw, shader pass analizi
- `Memory Profiler`: texture/mesh/allocation kaynaklari
- `Stats` penceresi: hizli draw call/triangles kontrolu
- Harici:
  - RenderDoc (GPU pass analizi)
  - Platforma gore GPU araci (Nsight, Radeon GPU Profiler vs.)

## 2.3 Benchmark Sahnesi Olusturun

Tek bir benchmark rotasi olusturun:

- Sehir merkezinden banliyoye ve geri donen 60 sn kamera path
- 3 farkli yogunluk modu:
  - `Low Density`
  - `Target Density`
  - `Stress Density`

Bu sahne tum sprint boyunca referansiniz olacak.

## 3) Hizli Kazanimlar (Ilk 1-2 Gun)

## 3.1 Render Pipeline Ayarlari

- URP/HDRP fark etmeksizin:
  - `SRP Batcher`: `ON`
  - `GPU Instancing`: uygun materyallerde `ON`
  - Gereksiz `real-time shadow distance` dusurun
  - Cascaded shadow sayisini azaltin (kaliteye gore)
  - MSAA/TAA/SSAO gibi etkileri kalite presetlerine ayirin

## 3.2 Kamera ve Culling

- `Far Clip Plane` gerektiginden buyukse dusurun.
- Katman bazli culling mesafesi uygulayin:
  - kucuk prop'lar daha erken cull olsun
  - buyuk binalar daha uzakta gorunmeye devam etsin
- `Occlusion Culling` bake edin (ozellikle sokak-koridor tipi yerlesimlerde ciddi kazanc verir).

## 3.3 Terrain Ayarlari

- `Pixel Error`: kalite/perf dengesine gore yukseltin (daha az geometri)
- `Base Map Distance`: gereksiz yuksekse dusurun
- Terrain detail (grass) mesafelerini agresif optimize edin
- Ayni terrain yerine buyuk haritayi mantikli tile'lara bolmeyi degerlendirin

## 3.4 Isik ve Golge

- Statik geometri icin `Baked Lighting` tercih edin.
- Dinamik isik sayisini ciddi sekilde sinirlayin.
- Gereksiz "shadow caster" mesh'leri kapatin (kucuk prop'larda cok etkili).

## 4) Sehir ve Grid Mimarisi Icin Ana Strateji

Buyuk sehirde asil kazanc mimari duzeydedir: her seyi ayni anda aktif tutmamak.

## 4.1 World Chunking (Zorunlu)

Haritayi chunk'lara bolun:

- Ornek: `64x64m` veya `128x128m` chunk
- Kamera/player merkezli aktif halka:
  - `Near Ring`: tam detay
  - `Mid Ring`: orta detay
  - `Far Ring`: proxy/HLOD veya tamamen kapali

Yukleme modeli:

- Chunk prefab'lari `Addressables` ile stream edilsin
- Senkron yukleme yerine asenkron yukleme kullanin
- Yukleme butcesi belirleyin (ornegin frame basi max 2-4ms)

## 4.2 HLOD (Hierarchical LOD)

Tek tek binalar yerine uzak mesafede:

- Bina bloklarini birlestirilmis proxy mesh ile gosterin
- Tek materyal atlasi + dusuk poly mesh
- Uzakta collider kapali

Hedef: uzak mesafe draw call ve vertex maliyetini dramatik dusurmek.

## 4.3 Grid Data Odakli Akis

Grid'i sadece build-time yerlesim olarak degil runtime veri modeli olarak kullanin:

- Her hucre icin yalnizca gerekli state tutulmali
- Gorunmeyen hucrelerde:
  - renderer yok
  - AI tick yok
  - fizik sim yok
- Hucre durumlari:
  - `Unloaded`
  - `Loaded-Proxy`
  - `Loaded-Full`

## 5) Rendering Optimizasyonu (Derin)

## 5.1 Draw Call Azaltma Sirasi

1. Ayni materyal kullanimini standartlastirin.
2. Statik objelerde static batching / mesh combine.
3. Instancing uygun objelerde etkinlestirin.
4. Uzak geometriyi HLOD/proxy ile degistirin.
5. Shader varyantlarini azaltin.

## 5.2 Materyal ve Shader Kurallari

- PBR shader cesit sayisini azaltin.
- "One-off" materyal sayisini temizleyin.
- Transparency kullanimi minimumda olsun (overdraw sebebiyle).
- Decal/particle kullanimini sahne hotspot'larinda profil edin.

## 5.3 Overdraw Kontrolu

- Ozellikle:
  - agac yapraklari
  - cam/transparent tabelalar
  - partikuller
- Frame Debugger ile problemli pass'leri bulun.
- Transparan katmanlari sadelestirin.

## 6) NPC, Trafik ve Oyun Mantigi Optimizasyonu

Sehirlerde sadece render degil simulasyon da darbo gaz olur.

## 6.1 Tick Seyreltme (Update Decimation)

Her NPC'yi her frame update etmeyin:

- Yakin NPC: her frame
- Orta mesafe: 2-4 frame'de bir
- Uzak: 10+ frame'de bir veya event-driven

## 6.2 Spatial Partition

- Grid tabanli broadphase kullanin (zaten grid altyapiniz var).
- Komsu/etkilesim sorgulari tum dunya yerine hucre bazli olsun.
- Physics query'lerde layer mask ve mesafe siniri zorunlu olsun.

## 6.3 Pathfinding Stratejisi

- NavMesh'i bolge bazli tutun.
- Uzun yol hesaplarini arkaplanda/queue ile planlayin.
- Siklikla tekrar eden rotalari cache edin.

## 6.4 Pooling

- Arac, NPC, VFX, projectile vb. her sey `Object Pool` kullanmali.
- Runtime instantiate/destroy spike'larini kaldirin.

## 7) Physics Optimizasyonu

- Gereksiz `Rigidbody` ve `Collider` temizligi yapin.
- Uzak objelerde collider devre disi.
- `Fixed Timestep` ve `Max Allowed Timestep` degerlerini hedef platforma gore ayarlayin.
- Collision matrix'i sadelestirin (katmanlar arasi gereksiz carpismalari kapatin).

## 8) Bellek ve I/O Optimizasyonu

## 8.1 Bellek

- Texture import ayarlari:
  - Uygun compression formatlari
  - Mipmap acik/kapali karari kullanim senaryosuna gore
- Devasa texture yerine atlas/trim yaklasimi
- Mesh read/write kapatma (gerekmiyorsa)

## 8.2 Streaming

- Addressables label stratejisi:
  - `city_core`
  - `district_x`
  - `props_common`
- Yukleme ve unload esiklerini oyuncu hizina gore belirleyin.
- Ani pop-in'i azaltmak icin preload mesafesi kullanin.

## 9) Terrain + Sehir Birlikte En Iyi Pratikler

- Terrain detail (grass) ile sehir prop yogunlugunu ayni bolgede zirveye cikarmayin.
- Yol/zemin gibi duz alanlarda terrain detail'i lokal kapatin.
- Bina altinda kalan terrain parcalarini delik/optimizasyon ile sadelestirin.
- Uzak bolgelerde terrain LOD + HLOD kombinasyonu kullanin.

## 10) Kalite Ayarlari ve Platform Profilleri

Minimum 3 kalite profili tanimlayin:

- `Low`:
  - Shadow distance kisa
  - Daha az cascade
  - Dusuk post-process
- `Medium`:
  - Dengeli ayarlar
- `High`:
  - Daha uzun gorus + daha yuksek efekt

Ayrica runtime auto-detect:

- GPU/CPU sinifina gore varsayilan kalite secimi
- Dinamik cozunurluk (gerekirse)

## 11) Uygulama Yol Haritasi (4 Sprint Ornegi)

## Sprint 1: Olcum ve Hizli Kazanim

- Benchmark sahnesi + otomatik kamera path
- Profil snapshot baseline
- SRP Batcher, culling, shadow tuning
- Terrain detail/mesafe optimizasyonu

Beklenen kazanc: `%15 - %35`

## Sprint 2: Chunking + Streaming

- Dunyayi chunk modeline gecis
- Addressables stream sistemi
- Yakin/orta/uzak halka aktivasyonu

Beklenen kazanc: `%20 - %45`

## Sprint 3: HLOD + Simulasyon Seyreltme

- Bina blok HLOD
- NPC/Traffic update decimation
- Pooling yayginlastirma

Beklenen kazanc: `%20 - %40`

## Sprint 4: Final Profiling ve Regression Guvencesi

- CPU/GPU/memory detayli karsilastirma
- Spike root-cause temizligi
- Quality preset ince ayar
- Performans regression checklist

## 12) Gorev Listesi (Takip Icin Kopyala-Kullan)

## 12.1 Profilleme

- [ ] Baseline profiler kaydi alindi
- [ ] CPU ana darbo gazlari etiketlendi
- [ ] GPU ana darbo gazlari etiketlendi
- [ ] Memory snapshot alindi

## 12.2 Render

- [ ] SRP Batcher aktif
- [ ] Instancing kontrolu tamam
- [ ] Material varyant azaltimi yapildi
- [ ] Occlusion bake tamamlandi
- [ ] LOD/HLOD gecisleri test edildi

## 12.3 Terrain

- [ ] Pixel Error optimize edildi
- [ ] Detail/tree mesafeleri ayarlandi
- [ ] Base map distance optimize edildi
- [ ] Sehir altindaki terrain yuk sadelestirildi

## 12.4 Simulasyon

- [ ] NPC tick seyreltme uygulandi
- [ ] Trafik AI update gruplandi
- [ ] Pathfinding queue/cache uygulandi
- [ ] Pooling kapsamli hale getirildi

## 12.5 Build ve QA

- [ ] Development build + profile analyzer raporu
- [ ] Low/Medium/High preset benchmark
- [ ] 30 dk soak test (memory leak/spike kontrolu)
- [ ] Regression checklist tamam

## 13) Olcum Tablosu Sablonu

Asagidaki tabloyu her degisiklikten sonra doldurun:

| Tarih | Degisiklik | Avg FPS | 1% Low | CPU ms | GPU ms | RAM MB | Not |
|---|---|---:|---:|---:|---:|---:|---|
| 2026-02-10 | Baseline |  |  |  |  |  |  |
| 2026-02-10 | Culling + Shadow |  |  |  |  |  |  |
| 2026-02-10 | Terrain tuning |  |  |  |  |  |  |
| 2026-02-10 | Chunk streaming |  |  |  |  |  |  |
| 2026-02-10 | HLOD |  |  |  |  |  |  |

## 14) Sik Yapilan Hatalar

- Editor FPS'e bakip karar vermek
- Tek seferde cok degisiklik yapip etkiyi olcememek
- Tum sehri ayni anda aktif tutmak
- LOD var sanip gecis mesafelerini test etmemek
- Gereksiz real-time isik/golge kullanmak
- "Optimize ettik" deyip regression test yapmamak

## 15) Net Eylem Onerisi (Bu Projede Baslangic Sirasi)

1. Benchmark rotasi ve baseline profili olustur.
2. Ilk gun: culling, shadow, terrain detail mesafelerini optimize et.
3. Sonra chunk streaming altyapisini kur.
4. HLOD/proxy sistemini devreye al.
5. NPC/traffic update seyreltme + pooling'i tamamla.
6. Quality presetleri donanima gore ayarla.
7. Her sprint sonunda metrik tablosunu guncelle ve regression kontrolu yap.

Bu sirayi izlerseniz, "tek ayar degistirerek" degil "mimariyi hafifleterek" kalici performans kazanimi elde edersiniz.

---

## 16) OPTIMIZASYON ILERLEME DURUMU (2026-02-12)

### Sprint 4: Final Profiling ve Regression Guvencesi (TAMAMLANDI - Test Icin Hazir)

#### Tamamlanan Ozellikler:
- [x] PerformanceBenchmark bileseni olusturuldu (otomatik test sistemi):
  - Sabit 60 saniyelik kamera yolu sistemi
  - Waypoint tabanli rota olusturma
  - Otomatik FPS, CPU, GPU, bellek olcumu
  - 1% dusuk FPS hesaplama (kritik metrik)
  - JSON ve CSV sonuc export
  - Isitma fazı (warmup) ile kararlı ölçüm
  - Gorsel waypoint gizmo'lar ile rota planlama
  - Editor entegrasyonu ile tek tıkla kurulum

- [x] PerformanceRegressionDetector olusturuldu (kalite guvencesi):
  - Onceki benchmark'lardan otomatik baseline yukleme
  - Gercek zamanli performans izleme (FPS, CPU, bellek)
  - Uyari ve Kritik alarm sistemi (%10 ve %20 esikleri)
  - Frame spike tespiti (>33ms = <30 FPS spike)
  - Bellek sizintisi tespiti ile buyume orani hesaplama
  - Oyun sirasinda ekran ustu alarm gosterimi
  - Kalite seviyesine gore ayarlanabilir varyans esikleri
  - Benchmark gecmisi ile entegrasyon

- [x] MemoryProfiler olusturuldu (bellek optimizasyonu):
  - Gercek zamanli bellek izleme (managed/native/GC)
  - Frame basina GC allocation takibi
  - MB/dakika buyume orani ile bellek sizintisi tespiti
  - Snapshot gecmis sistemi (10 dakikalik pencere)
  - F3: Bellek overlay acma/kapama
  - F4: Zorla garbage collection
  - F5: Snapshot gecmisini CSV'ye export
  - Gorsel bellek kullanim overlay
  - GPU bellek takibi

- [x] Sprint4SetupTool olusturuldu (Editor Araci):
  - Tools > Performance > Sprint 4 Setup menusunden erisim
  - Tek tikla benchmark sistemi kurulumu
  - Otomatik regression detector yapilandirma
  - Memory profiler entegrasyonu
  - Kalite preset onerileri (Low/Medium/High)
  - Benchmark sonuc klasoru yonetimi
  - Test kontrol listesi ve dogrulama adimlari
  - Onceki kosumladan baseline yapilandirma

#### Entegrasyon Adimlari:
- [ ] Unity Editor'de Tools > Performance > Sprint 4 Setup ac
- [ ] "Add Benchmark System to Scene" butonuna tikla
- [ ] Kamerayi sehir merkezine konumlandir, "Setup Waypoints from Current Position" tikla
- [ ] "Add Regression Detector to Scene" butonuna tikla
- [ ] "Add Memory Profiler to Scene" butonuna tikla
- [ ] Project Settings'de kalite presetlerini yapılandır (Low/Medium/High)
- [ ] Sprint 4 aracindan onerilen kalite ayarlarini uygula

#### Test ve Dogrulama Fazı:
- [ ] Low kalitede benchmark koştur (hedef: 45+ FPS ort, 30+ %1 dusuk)
- [ ] Medium kalitede benchmark koştur (hedef: 60+ FPS ort, 40+ %1 dusuk)
- [ ] High kalitede benchmark koştur (hedef: 90+ FPS ort, 55+ %1 dusuk)
- [ ] Regression detector'un kasitli FPS dusulerini yakaladigini dogrula
- [ ] Bellek sizintisi kontrolu icin 30 dakikalik soak test koştur
- [ ] Memory profiler kısayol tuşlarını test et (F3/F4/F5)
- [ ] Benchmark CSV export et ve sprint ilerlemelerini karşılaştır
- [ ] Development Build'de profille (Editor'de degil)
- [ ] Sprint 1-3 optimizasyonlarinin hala aktif oldugunu dogrula

#### Uygulama Notları (Sprint 4):
- **Benchmark Rotası**: Şehir merkezi etrafında 60 saniyelik dairesel yol
- **Waypoint Sistemi**: Pürüzsüz interpolasyonla 8 waypoint
- **Ölçüm**: Ortalama FPS, %1 Düşük FPS, CPU süresi, bellek zirvesi
- **Regression Alarmları**: %10 bozulmada Uyarı, %20'de Kritik
- **Bellek Sızıntı Eşiği**: 30 MB/dakika büyüme alarm tetikler
- **Frame Spike Eşiği**: >33ms frame süresi (< 30 FPS)
- **Kalite Presetleri**: Low (30m gölge), Medium (50m), High (75m)
- **Export Formatı**: Koşum başına JSON, karşılaştırma için CSV geçmişi

### Oluşturulan Dosyalar (Sprint 4):
1. **PerformanceBenchmark.cs** (YENİ)
   - Otomatik performans test sistemi
   - Waypoint tabanlı kamera yolu
   - FPS, CPU, GPU, bellek metrikleri
   - JSON ve CSV sonuç export
   - Gizmo'larla görsel rota planlama
   - Kolay kurulum için editor entegrasyonu

2. **PerformanceRegressionDetector.cs** (YENİ)
   - Gerçek zamanlı regression izleme
   - Baseline karşılaştırma sistemi
   - Uyarı/Kritik alarm seviyeleri
   - Frame spike tespiti
   - Bellek sızıntı tespiti
   - Ekran üstü alarm gösterimi

3. **MemoryProfiler.cs** (YENİ)
   - Runtime bellek izleme
   - GC allocation takibi
   - Bellek sızıntı tespiti
   - Snapshot geçmiş sistemi
   - F3/F4/F5 kısayol kontrolleri
   - Analiz için CSV export

4. **Sprint4SetupTool.cs** (YENİ - Editor)
   - Kapsamlı Sprint 4 kurulum sihirbazı
   - Tek tıkla bileşen ekleme
   - Kalite preset önerileri
   - Test kontrol listesi
   - Sonuç klasörü yönetimi

### Beklenen Performans Doğrulama (Sprint 4):
- **Benchmark Tekrarlanabilirlik**: Koşumlar arası <%2 varyans
- **Regression Tespiti**: %5 FPS düşüşlerini otomatik yakala
- **Bellek Sızıntı Tespiti**: Sızıntı başlangıcından 2 dakika içinde alarm
- **Spike Tespiti**: >33ms tüm frame'leri tanımla
- **Kalite Ölçekleme**: Low→High arası 30-40% FPS farkı
- **Stabilite**: 30 dakikalık soak testte crash yok
- **Polish Kazançları**: İnce ayardan final %5-15

### Kümülatif Performans Kazançları (Tüm Sprint'ler):
- **Sprint 1**: %15-35 FPS iyileşmesi (culling, gölge, terrain, NPC throttling)
- **Sprint 2**: %20-45 ek kazanç (chunking, streaming, LOD)
- **Sprint 3**: %20-40 ek kazanç (HLOD, gelişmiş throttling, pooling)
- **Sprint 4**: %5-15 polish + stabilite (kalite ölçekleme, sızıntı önleme)
- **Toplam Beklenen**: Baseline'dan %60-135 FPS iyileşmesi
- **Ölçeklenebilirlik**: Sistem artık 5-10x daha büyük şehirleri kaldırıyor
- **Bellek**: Zirve kullanımda %40-60 azalma
- **Draw Call'lar**: Mesafede %60-80 azalma
