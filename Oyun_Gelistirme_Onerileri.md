# Oyun Gelistirme Onerileri (Oynanis + Oyuncu Bagliligi)

Bu dokuman, mevcut repo yapisi ve sistemleri uzerinden oyunun oynanis derinligini
artiracak ve oyuncuyu oyuna daha uzun sure baglayacak gelistirme onerilerini
toplu halde sunar. Oneriler, mevcut sistemlerin dogal uzantilari olarak
tasarlanmistir ve her biri oynanis akisini daha anlamli kararlara, risk/odul
dengesine ve uzun vadeli ilerlemeye baglamayi hedefler.

## Mevcut oynanis omurgasi (ozet)
- Telefon gorev teklifi akisi uzerinden teslimat kabul
- Pickup -> teslimat hedefi -> odul/ceza ve progresyon
- NavigationService merkezli rota rehberligi ve minimap
- Quest sistemi ile kalici gorev durumu ve odul hesaplari
- Para/XP/level ile uzun vadeli ilerleme

Bu omurga dogru. Gelistirmeler, omurgayi bozmak yerine yukaridan derinlik
ekleyecek sekilde kurgulanmali.

## Oyuncuyu oyuna baglayan temel direkler
- Anlamli secim: kisa yol mu guvenli yol mu, hiz mi dikkat mi, para mi itibar mi
- Net hedefler: kisa, orta, uzun vadeli hedeflerin bir arada olmasi
- Surekli “bir gorev daha” hissi: mini risk/odul tetikleyicileri
- Kalici ilerleme: arac, yetenek, itibar ve sozlesmeler
- Dunyanin canliligi: dinamik olaylar ve beklenmedik durumlar

## Oynanisi dogrudan etkileyen gelistirmeler

### 1) Teslimat cesitliligi + risk/odul
Mevcut teslimat akisini zenginlestirir, oyuncuya karar alanlari acar.
- Zaman pencereli teslimatlar (dar zaman, genis zaman)
- Kargo hassasiyeti (sarsinti, hiz, fren cezasi)
- Zincir teslimatlar (aynı bolgede birden fazla drop)
- Opsiyonel yan hedef: “ekstra bahsis icin ara durak”
- Hata toleransi: “kismasiz basari” yerine kademeli odul

Beklenen etkiler:
- Oyuncu, “en kisa yol” disinda farkli stratejilere yonelir
- Ayni harita uzerinde farkli oynanis davranislari dogar

### 2) Itibar ve sirket iliskileri (Company akisi ile bagli)
Mevcut CompanyPageUI ve teslimat ekonomisine baglanabilir.
- Firma bazli itibar puani (hizli teslimat, hasarsiz teslimat, seri)
- Itibar seviyeleriyle yeni teslimat tipleri/acilislar
- “Sozlesmeli” teslimatlar (belirli sure boyunca firmanin isleri)

Beklenen etkiler:
- Uzun vadeli hedefler ve “neden tekrar oynuyorum” cevabi

### 3) Arac sinifi, ekipman ve bakim
PlayerVehicleManager ve arac secimiyle birlikte ilerler.
- Arac sinifina gore kargo kapasitesi / hiz / manevra
- Moduler yukseltmeler (fren, lastik, motor, surus stabilitesi)
- Basit bakim/fuel loop’u (tum loop’u bozmayan, karar yaratan)

Beklenen etkiler:
- “Bir sonraki hedefim su araci almak” motivasyonu
- Risk/odul: hizli arac = daha az kapasite

### 4) Dinamik dunya olaylari
Traffic ve Weather sistemleriyle dogal entegrasyon.
- Yol calismasi, trafik kazasi, tikanma
- Hava kosulu ile etkilesen yol tutusu
- Acil teslimat (kisa sureli, yuksek odul)

Beklenen etkiler:
- Her teslimat farkli hissedilir
- Oyuncu rota secimini daha bilincli yapar

### 5) Rota secimi ve rehberlik iyilestirmeleri
NavigationService/MinimapUI/WorldRouteRenderer uzerinde dusunulur.
- Rota alternatifleri (hizli/guvenli/ucuz)
- Riskli bolgeler (trafik yogunlugu, dar sokak)
- Rota onizleme: ekstra sure ama daha az ceza

Beklenen etkiler:
- Oynanis “otomatik surus”tan cikarak stratejiye kayar

### 6) Progresyonu anlamli secimlere baglama
PlayerProgressionManager + DriverProgressionSystem uzerinden.
- Yetenek dallari: “Guvenli surus”, “Hiz odakli”, “Ekonomi odakli”
- Her dalin teslimat davranisina somut etkisi
- Seviye atlama esnasinda kalici secimler

Beklenen etkiler:
- Oyuncu kendi stilini belirler, meta ilerleme anlam kazanir

### 7) Mini challenge ve gunluk hedefler
Quest/Progression katmani uzerinden takip edilebilir.
- Gunluk 3 hedef (ornek: “Hasarsiz 2 teslimat”)
- Haftalik teslimat serisi
- Basari rozetleri ve kucuk bonuslar

Beklenen etkiler:
- Geri donus motivasyonu ve kisa hedefler

### 8) Geri bildirim ve anlatim
UI katmaninda (Quest UI, HUD) kucuk ama etkili iyilestirmeler.
- Gorev sonunda kisa performans ozeti
- “Neden ceza aldım?” net aciklama
- Kargo durumunu anlik gosterim (hasar, gecikme riski)

Beklenen etkiler:
- Oyuncu sistemleri anlar, “adil” hisseder

## Uygulama notlari (mevcut sistemlere baglantilar)
- DeliveryManager + QuestManager birlikte ele alinmali
- NavigationService degisince Minimap/EdgeIndicator/WorldRouteRenderer birlikte test edilmeli
- CompanyPageUI ve PlayerVehicleManager, arac ilerlemesi icin ana giris noktasi
- SaveManager + QuestDatabaseAutoSync degisikliklerde birlikte dusunulmeli
- GlobalUiCoordinator ile UI hiyerarsisi ve persist davranisi korunmali

## Onceliklendirme onerisi

### Kisa vadeli (1-2 sprint)
- Teslimat cesitliligi (zaman penceresi + kargo hassasiyeti)
- Gorev performans ozetleri
- Rota alternatifleri (en azindan hizli/guvenli)

### Orta vadeli (3-5 sprint)
- Itibar sistemi ve firma bazli acilislar
- Arac yukseltme ve sinif farklari
- Dinamik dunya olaylari (trafik ve hava ile temel etki)

### Uzun vadeli
- Sozlesmeli teslimatlar ve uzun sureli hedef zincirleri
- Metaya bagli yeni oyun modlari (challenge mode, time trial)
- Daha derin ekonomi simulasyonu

## Basari kriterleri (oyuncu bagliligi)
- Ortalama oturum suresi artisi
- Gunluk geri donus orani
- Teslimat basina karar sayisi (rota, risk, hedef)
- Arac/yetenek acilisina ulasip ulasmama oranlari

Bu oneriler, mevcut sistemleri bozmadan oyuncu davranisini degistirecek
katmanlar ekler: daha anlamli secimler, net hedefler ve kalici ilerleme.
