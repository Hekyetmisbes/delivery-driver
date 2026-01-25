# Route Visualizer Plugin - Kullanım Kılavuzu

## Özellikler

Bu plugin, EasyRoads3D yolları üzerinde NPC arabaların takip edeceği rotaları görselleştirmenizi sağlar.

### 1. Route Visualizer Window

**Açmak için:** Unity menüsünden `Tools > Traffic System > Route Visualizer`

**Özellikler:**
- ✅ Yol rotalarını çizgilerle gösterir
- ✅ Waypoint'leri küre şeklinde işaretler
- ✅ Waypoint'lerin yön vektörlerini gösterir (mavi oklar)
- ✅ Yol bağlantılarını (intersection'lar) sarı çizgilerle gösterir
- ✅ Renk, kalınlık ve boyut ayarları

**Nasıl Kullanılır:**

1. Window'u açın: `Tools > Traffic System > Route Visualizer`
2. "Find RoadGraphBuilder in Scene" butonuna tıklayın (otomatik bulur)
3. Görselleştirme ayarlarını yapın:
   - Show Waypoints: Waypoint kürelerini göster/gizle
   - Show Connections: Yol bağlantılarını göster/gizle
   - Route Color: Rota çizgisi rengi
   - Line Width: Çizgi kalınlığı
   - Waypoint Marker Size: Waypoint küre boyutu
4. **"Create Route Visualization"** butonuna tıklayın
5. Hierarchy'de "NPC_Route_Visualization" objesi oluşacak

**Silmek için:**
- "Clear Route Visualization" butonuna tıklayın
- Veya Hierarchy'den "NPC_Route_Visualization" objesini silin

---

### 2. Scene View Gizmos

Oyun çalışmasa bile Scene view'da sürekli rota çizgilerini gösterir.

**Açma/Kapama:**
`Tools > Traffic System > Toggle Route Gizmos`

**Renk Kodları:**
- 🟢 **Yeşil Çizgiler:** Ana rota yolu
- 🔵 **Mavi Çizgiler:** Waypoint yön vektörleri (arabalar bu yöne gider)
- 🔴 **Cyan Noktalar:** Waypoint pozisyonları
- 🟡 **Sarı Kesikli Çizgiler:** Yol bağlantıları (intersection'lar)

**Label'lar:**
Her 5 waypoint'te bir etiket gösterir:
```
Default Road 001
WP 10
```

---

## Kurulum Sonrası Kontrol

1. Unity Editor'ü yeniden başlatın
2. Menüde `Tools > Traffic System` sekmesini kontrol edin
3. Şunları görmelisiniz:
   - ✅ Route Visualizer
   - ✅ Toggle Route Gizmos

---

## Sorun Giderme

### "No RoadGraphBuilder found" hatası
**Çözüm:**
- Road Network GameObject'ine RoadGraphBuilder component'i eklenmiş mi kontrol edin
- Build road graph yapılmış mı kontrol edin

### Gizmos görünmüyor
**Çözüm:**
- Scene view'ın sağ üst köşesinde "Gizmos" butonunun açık olduğundan emin olun
- `Tools > Traffic System > Toggle Route Gizmos` ile açın

### Rotalar yol dışında görünüyor
**Çözüm:**
- RoadGraphBuilder'ın road graph'ı doğru build ettiğinden emin olun
- Console'da "[RoadGraphBuilder] Built road graph: X segments" mesajını kontrol edin

---

## İpuçları

💡 **Route Visualization vs Scene Gizmos:**
- **Route Visualization:** Play mode'da da görünür, kalıcı GameObject'ler oluşturur
- **Scene Gizmos:** Sadece Scene view'da görünür, performanslı, oyun build'ine dahil olmaz

💡 **Debug İçin:**
1. Scene Gizmos ile genel bakış
2. Route Visualization ile detaylı inceleme
3. NPC spawn olunca rotaları takip edip etmediğini kontrol et

💡 **Performans:**
- Çok fazla waypoint varsa Scene Gizmos performansı düşürebilir
- O zaman Toggle Route Gizmos ile kapatın
- Sadece ihtiyaç duyduğunuzda Create Route Visualization kullanın
