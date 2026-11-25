# Unity entegrasyon rehberi

## Klasör yapısı

```
Assets/Editor/UI/DDUITextureImporter.cs
Assets/Scripts/UI/Kit/           # DDUITheme, DDUIFactory ve isteğe bağlı yardımcılar
Assets/Resources/UI/DeliveryDriver/
  Components/                    # 46 PNG Sprite
  Icons/                         # 49 PNG Sprite
  Backgrounds/                   # Metinsiz şehir arka planı
Docs/UI/DeliveryDriver/          # Tam ekran konseptler, SVG kaynaklar ve katalog
```

`01_Screens` PNG'leri *tam ekran tasarım referansıdır*: Tam ekran PNG'yi Canvas'ın tek RawImage/Image nesnesi olarak koymak düğmeleri işlevsel hale getirmez. UI'ı gerçekten kurmak için ekranın parçalara ayrılan `02_Components`/`03_Icons` varlıklarını kullanın.

## Canvas önerisi
- Render Mode: **Screen Space - Overlay** (ya da mevcut Canvas sisteminize göre Screen Space - Camera)
- CanvasScaler: **Scale With Screen Size**
- Reference Resolution: **1600 × 900**
- Screen Match Mode: **Match Width Or Height**, Match = **0.5**
- Safe Area: sağ / sol ≥40, üst ≥30, alt ≥45 referans piksel
- TMP: Türkçe karakterleri içeren proje font atlasınızı kullanın. Bu paket telifli font veya font ikilisi içermez.
- Sorting sırası (örnek): BaseHUD 0; Map 100; Phone 200; Modal 300; Notification 400; Fade 500. Gerçek `GlobalUiCoordinator` politikanızı koruyun.

## Script bağlama stratejisi
1. HUD panellerini `GameplayHUD` bölümlerine referanslayın.
2. `SpeedometerUI`: hız/saat sayısal TMP ve segment görüntülerini bağlayın; P/R/N/1–6 vites grafiğini mevcut davranışta tutun.
3. `ReverseCameraHUD`: varlık `reverse_camera.svg` yalnızca UI yerleşimi. Gerçek görüntü için `RawImage` ve mevcut `RenderTexture` kullanın; geri viteste etkinleştirin.
4. `MinimapUI`: çerçeve sprite `minimap_frame.png`; içerik `RawImage` RenderTexture ve rota overlay olarak ayrı GameObject.
5. `FuelHudUI`: doluluk `Image.fillAmount`; kritik uyarı renk/animasyonu; `FuelStationUI` modal ile ayrı ayrı bağlayın.
6. `ActiveQuestUI`: süre, mesafe ve hassas kargo dayanıklılığı örnek metinlerini oyun değerlerine taşıyın.
7. `SmartphoneUI`: `phone_shell.png` çerçeve, uygulamalar ayrı panel; toast için `toast_default.png`.
8. `FleetManagementUI`: Araçlarım / Galeri / Yükseltme sekmelerini gerçek araç verisiyle oluşturun.
9. `SettingsMenuUI`: kullanıcının mevcut input rebinding, dil ve hız birimi ayarlarını koruyun.
10. `AccessibilityManager` + `LocalizationTable`: palet semantiği ve tr-TR / en-US metin uzunluğu kontrollerini arayüzde uygulayın.

## 9-slice örneği

1. `Assets/Resources/UI/DeliveryDriver/Components/panel_medium.png` seçin.
2. Texture Type = **Sprite (2D and UI)**, Mesh Type = **Full Rect**.
3. Sprite Editor → Border Left/Right/Top/Bottom yaklaşık **20 px**.
4. UI Image → Image Type = **Sliced**, Preserve Aspect Ratio isteğe bağlı. Bu import ayarı `DDUITextureImporter` tarafından otomatik uygulanır.
5. Düz bar, ikon veya cihaz görsellerine 9-slice uygulamayın.

## Etkileşim ve erişilebilirlik kontrol listesi
- Mouse hover, keyboard focus, gamepad navigation, disabled ve active durumları
- En az 44×44 eşdeğer hedef alanı ve açık seçili öğe sınırı
- Semantik durumlar yalnızca renkle değil ikon + yazı ile anlatılmalı
- Renk körlüğü: protanopia / deuteranopia / tritanopia durumlarını proje servisinizle bağlayın
- Animasyonlarda azaltılmış hareket ayarını dikkate alın
- Kargo hasarı, yakıt uyarısı, teslimat süre sayacı ekran okuyucu/altyazı metniyle desteklenmeli
- SceneTransitionManager fade katmanını modal/tooltip/notification katmanlarının en üstünde tutun

## Önemli
Bu pakette bulunan C# yardımcıları **örnektir** ve Unity'de derlenerek test edilmemiştir; gerçek proje sınıfları, ad alanları ve Unity sürümüne göre uyarlamak gerekir. Otomatik çalışan prefab veya gerçek sahne içe aktarması olduğunu varsaymayın.

## Örnek presenter ve ekran anahtarlama
- `DDUIHUDPresenter.cs`: Oyun mantığını değiştirmeden verileri `TextMeshProUGUI` ve `Image.fillAmount` elemanlarına yansıtmak için isteğe bağlı yardımcıdır.
- `DDUIScreenSwitcher.cs`: telefon uygulamaları, filo sekmeleri ve ayar ekranları için basit panel görünürlüğü yönetimi sağlar.
- `DDUIFactory.cs`: runtime panel / TMP metin / buton oluşturma yardımcı fonksiyonlarıdır.
**Mevcut script sınıflarının yerini almazlar.**
