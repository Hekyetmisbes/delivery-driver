# Delivery Driver - Geliştirme Yol Haritası

Bu roadmap, mevcut sistemleri (teslimat döngüsü, quest altyapısı, minimap, mahalle yapısı, trafik AI, hava durumu, kayıt sistemi) daha oynanabilir, uzun ömürlü ve dengeli hale getirmek için hazırlanmıştır.

## 0) Hedefler
- [ ] İlk 10 dakikada oyuncunun oyunu öğrenip en az 2 teslimatı başarıyla tamamlaması.
- [ ] 30-45 dakikalık bir oturumda sürekli yeni hedef/ödül hissinin korunması.
- [ ] Erken oyun, orta oyun ve geç oyun arasında net ilerleme farkının hissedilmesi.
- [ ] Performansın orta seviye sistemlerde stabil kalması (trafik + UI + minimap birlikte çalışırken).

## 1) Çekirdek Oynanış Döngüsünü Güçlendirme (En Yüksek Öncelik)
Not: Bu bölüm "oynanabilirlik" etkisi en yüksek olan görevleri içerir.

### 1.1 Teslimat çeşitliliği
- [x] **Teslimat türleri:** Standart, zamanlı, hassas kargo, çok duraklı teslimat.
- [x] **Görev koşulları:** Trafik yoğun saatte bonus, gece teslimat bonusu, yağmurda risk primi.
- [x] **Başarısızlık nedenleri:** Süre aşımı, kargo hasarı, yanlış bölgeye teslim.
- [x] **Tekrarı kırma:** Aynı mahalleye arka arkaya görev verilmesini sınırlayan kural.

### 1.2 Ödül ve ceza dengesi
- [x] **Ücret formülü:** Mesafe + süre baskısı + kargo zorluğu + mahalle risk çarpanı.
- [x] **Seri bonusu:** Aralıksız başarılı teslimatlarda artan çarpan.
- [x] **Ceza sistemi:** Çarpışma, gecikme, kargo hasarı için kademeli para kesintisi.
- [x] **Şeffaf görev ekranı:** Göreve başlamadan tahmini kazanç/ceza bilgisi.

### 1.3 Oyuncu geri bildirimi (feedback)
- [x] **Anlık durum paneli:** Kargo durumu, kalan süre, tahmini rota süresi.
- [x] **Sürüş geri bildirimi:** Sert fren/çarpışma anında kısa uyarı ve puan etkisi.
- [x] **Teslimat sonucu ekranı:** Neden başarılı/başarısız olduğunun net dökümü.

## 2) İlerleme Sistemi (Para, Seviye, Kilit Açma)
### 2.1 Oyuncu progresyonu
- [x] **Sürücü seviyesi:** Teslimat performansına göre XP.
- [x] **Seviye ödülleri:** Yeni görev türü, yeni bölge, yeni araç parçası açılması.
- [x] **Yetenek ağacı (hafif):** Yakıt tasarrufu, kargo dayanıklılığı, rota okuma yardımı.

### 2.2 Garaj ve araç geliştirme
- [ ] **Garaj UI:** Motor, fren, yol tutuş, süspansiyon yükseltmeleri.
- [ ] **Görsel özelleştirme:** Renk, jant, plaka, araç içi küçük kozmetik.
- [ ] **Maliyet eğrisi:** İlk yükseltmeler ucuz, üst seviye yükseltmeler anlamlı pahalı.
- [ ] **Denge:** Araç güçlendikçe görev zorluğunun hafif artması.

### 2.3 Ekonomi sağlığı
- [ ] **Enflasyon kontrolü:** Oyun ilerledikçe kazanç artışı ile fiyat artışının dengelenmesi.
- [ ] **Onarım/yakıt giderleri:** Para harcama kanalları ile ekonominin canlı tutulması.
- [ ] **Haftalık rapor ekranı:** Gelir-gider, en karlı mahalle, başarısızlık oranı.

## 3) Dünya ve Sistem Derinliği
### 3.1 Trafik ve şehir canlılığı
- [ ] **Yaya sistemi (hafif sürüm):** Kaldırım hareketi + kontrollü yaya geçidi davranışı.
- [ ] **Özel araçlar:** Ambulans/itfaiye araçlarına yol verme mekaniği.
- [ ] **Olay sistemi:** Kaza, yol çalışması, geçici rota kapanması.

### 3.2 Çevre etkileri
- [ ] **Hava durumunun oynanışa etkisi:** Yağmurda fren mesafesi artışı, sisde görüş düşüşü.
- [ ] **Gün/gece etkisi:** Gece teslimatında görüş azalırken ödeme artışı.
- [ ] **Yol tipi etkisi:** Islak/asfalt/toprak zeminde farklı tutuş değerleri.

### 3.3 Araç durumu
- [ ] **Yakıt sistemi:** Yakıt tüketimi + istasyonlarda dolum.
- [ ] **Araç hasarı:** Çarpışmaya bağlı performans kaybı.
- [ ] **Bakım noktaları:** Tamir/lastik değişimi ile risk yönetimi.

## 4) UX ve UI İyileştirmeleri
Not: Minimap ve quest UI zaten mevcut; odak noktası kalite artırımıdır.

- [ ] **HUD sadeleştirme:** Hız, vites, yakıt, görev durumu tek bakışta okunur olmalı.
- [ ] **Harita kullanılabilirliği:** Minimap zoom seviyesi, hedef pin görünürlüğü.
- [ ] **GPS rota kalitesi:** Yol grafına bağlı daha güvenilir rota çizimi.
- [ ] **Erişilebilirlik:** Renk körlüğü modu, yazı boyutu seçimi, kontrast seçenekleri.
- [ ] **Kontrol özelleştirme:** Tuş yeniden atama ve gamepad hassasiyet ayarı.

## 5) Görev İçeriği ve Uzun Vadeli Tutundurma
- [ ] **Haftalık challenge sistemi:** "3 teslimatı hasarsız tamamla" gibi hedefler.
- [ ] **Başarımlar:** Hızlı teslimat, hasarsız seri, mahalle uzmanı vb.
- [ ] **Dinamik etkinlikler:** Belirli saatlerde daha yüksek ödüllü görev havuzu.
- [ ] **Koleksiyon sistemi:** Nadir kargo türleri ve istatistik tamamlama hedefleri.

## 6) Teknik Sağlamlık ve Üretim Kalitesi
### 6.1 Test ve dengeleme
- [ ] **Görev dengeleme araçları:** Görev üretim parametrelerini canlı test ekranı.
- [ ] **Playtest checklist:** 10, 20, 40 dakikalık oturumlar için kontrol listesi.
- [ ] **Telemetri (lokal):** Ortalama teslimat süresi, başarısızlık nedenleri, kaza yoğunluğu.

### 6.2 Performans
- [ ] **NPC yoğunluk ölçekleme:** Donanım seviyesine göre trafik yoğunluğu ayarı.
- [ ] **UI maliyet ölçümü:** Quest/minimap/HUD güncelleme frekansı optimizasyonu.
- [ ] **Spawn güvenliği:** Dünya chunk geçişlerinde obje spawn/despawn stabilitesi.

### 6.3 Veri ve kayıt sistemi
- [ ] **Versiyonlu save yapısı:** Güncelleme sonrası kayıtların bozulmaması.
- [ ] **Hata toleransı:** Eksik veri durumunda güvenli fallback.
- [ ] **Yedek kayıt slotu:** Otomatik geri dönüş noktası.

## 7) 8 Haftalık Uygulama Planı
### Faz 1 (Hafta 1-2) - Oynanabilir çekirdek
- [x] Teslimat türlerini artır.
- [x] Ödül/ceza formülünü kur.
- [x] Sonuç ekranını ve net geri bildirimi ekle.

### Faz 2 (Hafta 3-4) - Progresyon ve ekonomi
- [x] Seviye + XP sistemi.
- [ ] Garaj yükseltmeleri (ilk sürüm).
- [ ] Gelir-gider dengesini ayarla.

### Faz 3 (Hafta 5-6) - Dünya derinliği
- [ ] Hava etkilerini sürüşe bağla.
- [ ] Yakıt + hasar + bakım üçlüsünü ekle.
- [ ] Olay sistemi (kaza/yol kapanması) prototipi.

### Faz 4 (Hafta 7-8) - Parlatma ve stabilizasyon
- [ ] HUD/UX kalite geçişi.
- [ ] Telemetriye göre dengeleme.
- [ ] Performans ve save güvenliği iyileştirmeleri.

## 8) "Bitti" Kriterleri (Definition of Done)
- [ ] Yeni oyuncu 10 dakika içinde temel döngüyü öğreniyor.
- [ ] Oyuncu en az 3 farklı görev türünü tek oturumda deneyimleyebiliyor.
- [ ] Garaj yükseltmeleri sürüşte hissedilir fark yaratıyor.
- [ ] Başarısız görev nedenleri oyuncuya açık biçimde gösteriliyor.
- [ ] FPS düşüşleri kritik sahnelerde kabul edilebilir seviyede kalıyor.
- [ ] Kayıt yükleme sonrası görev/progresyon tutarlılığı korunuyor.
