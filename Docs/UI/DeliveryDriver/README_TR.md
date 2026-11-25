# DELIVERY DRIVER — KAPSAMLI UI TASARIM PAKETİ

**Sürüm:** 1.0 · **Dil:** Türkçe · **Hedef:** Unity / UGUI / TextMeshPro · **Hazırlanan ekran:** 52 · **UI bileşeni:** 46 · **vektör ikon:** 49

Bu dosya, paylaştığınız `Delivery Driver` Unity UI/HUD sistem envanterine göre oluşturulmuş, oyununuza uyarlanabilir **tasarım ve görsel varlık paketidir**. Önceki konuşmadaki ana menü, sürüş, filo ve erişilebilirlik konseptlerini görsel referans olarak korur. Bütün ekranlar, semboller ve ortak parçalarda turuncu / koyu lacivert / turkuaz / nane yeşili görsel sistem kullanılır.

## Klasörler

| Klasör | İçerik |
| --- | --- |
| `01_Screens/SVG` | 52 adet **düzenlenebilir** tam ekran UI konsepti (1600×900, vektör SVG). |
| `01_Screens/PNG` | Aynı ekranların 1600×900 PNG önizlemeleri. |
| `Backgrounds/city_backdrop.svg` | Ana menü, pause ve loading ekranlarında kullanılan metinsiz şehir arka planının SVG kaynağı. Runtime PNG `Assets/Resources/UI/DeliveryDriver/Backgrounds` altında. |
| `02_Components/SVG` | 46 bileşenin düzenlenebilir vektör kaynakları; PNG'ler `Assets/Resources/UI/DeliveryDriver/Components` altında. |
| `03_Icons/SVG` | 49 ikonun düzenlenebilir vektör kaynakları; PNG'ler `Assets/Resources/UI/DeliveryDriver/Icons` altında. |
| `Assets/Scripts/UI/Kit` | Renk token'ları ve isteğe bağlı UI yardımcıları. |
| `Assets/Editor/UI/DDUITextureImporter.cs` | İlk importta Sprite ve 9-slice ayarlarını otomatik uygular. |
| `05_Docs` | Tasarım token'ları, C# eşleştirme manifesti, kullanım yönergeleri, komponent manifesti ve üretici kaynak kod. |
| `06_Reference_Concepts` | Önceki görüşmede oluşturulan 4 yüksek detaylı görsel konsept. |
| `07_Preview` | İnternet gerektirmeyen görsel katalog, kontak sayfaları. |

## Kullanım

1. **`07_Preview/index.html`** dosyasını tarayıcıda açın.
2. Üstteki kategorilerden ekran seçin; görseli açarak tasarımı inceleyin.
3. Ekran düzenini düzenlemek için SVG'yi Inkscape, Illustrator veya Figma'ya aktarın. Tam ekran PNG'ler referanstır; oyun build'ine dahil edilmez.
4. Runtime sprite'lar `Assets/Resources/UI/DeliveryDriver/Components`, `Icons` ve `Backgrounds` altında hazırdır. Ana menü, pause, loading, minimap, speedometer, fuel HUD, balance HUD, fuel modalı, confirmation dialog, smartphone bildirimleri ve ikonları uygun görselleri yükler.
5. Unity ilk importta Sprite ve 9-slice ayarlarını uygular. Var olan importları yeniden ayarlamak için **Tools → Delivery Driver UI → Configure Sprites** menüsünü kullanın.
6. `05_Docs/screen_manifest.csv` ve `05_Docs/unity_mapping.md` diğer ekranları geliştirirken görsel referans sunar. Bu ekranların tamamı henüz çalışan prefab veya etkileşimli UI değildir.

## Kapsanan ekran grupları

- **Menüler:** 11 ekran — Ana Menü, Duraklatma, Genel Ayarlar, Grafik Ayarları, Ses Ayarları, Tuş Atamaları, Oynanış Ayarları, Erişilebilirlik, Kayıt Yükleme, Yükleme, Yapımcılar
- **Sürüş HUD:** 8 ekran — Ana Sürüş HUD, Hız Göstergesi, Geri Görüş, Düşük Yakıt Uyarısı, Yakıt İkmali, Çarpışma Hasarı, Harita Sınırı, Güvenli Konuma Dönüş
- **Navigasyon:** 3 ekran — Mini Harita, Pusula, Tam Harita (öneri)
- **Görevler:** 7 ekran — Aktif Görev, Görev Teklifi, Görev Panosu, Görev Başarısı, Görev Başarısızlığı, Görev İstatistikleri, Öğretici
- **Şirket & Filo:** 5 ekran — Şirket Kurulumu, Araçlarım, Araç Galerisi, Araç Yükseltme, Şirket Profili
- **Akıllı Telefon:** 6 ekran — Telefon Ana Ekranı, Kurye Express, QuickPay, Garajım & Filo, Mesajlar, Telefon Bildirimleri
- **İlerleme:** 4 ekran — Bakiye Değişimi, Seviye Atlama, Yetenek Ağacı, Başarım
- **Dünya & Sistem:** 8 ekran — Bildirim Kuyruğu, Onay Diyaloğu, Bilgi İpucu, Mahalle Adı, Dünya Teslimat İşareti, Sinematik HUD, Renk Körlüğü Kontrolü, TR/EN Yerelleştirme

## Kapsamın sınırları

- **Üretime hazır çalışan prefab sistemi değildir.** Tasarımlar UI yerleşimi ve görsel varlık paketidir; tıklanabilir eylemler, oyun mantığı, animasyon zamanlayıcıları, RenderTexture kamera/mini harita, kayıt ve teslimat verileri projedeki C# scriptlerine ayrıca bağlanmalıdır.
- Paylaşılan dosya **scriptlerin özet envanteridir**, Unity projesinin çalışan kaynak kodu değildir. Bu nedenle referans atamaları ve gerçek hiyerarşileri doğrudan doğrulanmadı. `screen_manifest` dosyasındaki bağlantılar belirtilen sınıf adlarına göre hazırlanmıştır.
- Ekrandaki şirket isimleri, miktarlar, yüzdeler, hız, tarih ve diğer rakamlar **yer tutucu örnek veridir**; gerçek oyun değerleri değildir.
- Görsel arayüz mockup'larına dahil gölgeli şehir/garaj sahneleri konsept arka planıdır. Gerçek oyun sahneleri yerine kullanılması zorunlu değildir.
- Dört adet referans konsept yalnızca görsel yönlendirme içindir; diğer ekranlarda bağımsız düzenlenebilir SVG katmanları bulunur.
- SVG doğrudan Unity UGUI `Image` bileşeninde her Unity sürümünde desteklenmeyebilir. PNG sprite kullanın ya da projeye uygun Vector Graphics paketini kurun.
- PNG `Image` bileşenlerinde `Sliced` türünü özellikle buton/kart/panel arka planlarında tercih edin. Bileşen atlası için önerilen kenarlıklar `component_manifest.json` içindedir.

## Kaynak & lisans

Bu paket, kullanıcı isteği üzerine hazırlanan özgün konsept varlıklardan oluşur. Önceki oturumda kullanıcı için üretilen 4 görsel de yalnızca bu projenin konsept referansı olarak eklenmiştir. Hiçbir font dosyası dahil edilmemiştir. Unity veya üçüncü taraf marka görselleri için proje sahibinin ayrıca hak değerlendirmesi yapması önerilir.
