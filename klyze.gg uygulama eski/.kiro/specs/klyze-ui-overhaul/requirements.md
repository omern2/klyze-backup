# Requirements Document

## Introduction

Bu doküman, Klyze.gg masaüstü uygulamasının (WPF/C#) kapsamlı görsel ve işlevsel güncellemesini tanımlar. Güncelleme; monokrom renk teması, yeniden tasarlanmış üst bar, Türkçe karakter düzeltmeleri, ana sayfaya cihaz seçimi bölümü eklenmesi ve uygulama genelinde akıcı animasyonlar içermektedir.

## Glossary

- **Uygulama**: Klyze.gg WPF masaüstü uygulaması
- **TopBar**: Pencerenin en üstündeki başlık çubuğu bölgesi
- **Sidebar**: Sol taraftaki dikey navigasyon paneli
- **NavBtn**: Sidebar içindeki navigasyon düğmeleri
- **HomePage**: Ana sayfa içerik alanı
- **DeviceCard**: Cihaz markası seçim kutucuğu
- **DeviceSelector**: Ana sayfadaki cihaz seçim bölümü
- **Glassmorphism**: Cam efekti görünümü (yarı saydam, bulanık arka plan, ince kenarlık)
- **StaggeredAnimation**: Elemanların sırayla, belirli gecikmelerle sahneye girmesi
- **MonokromTema**: Yalnızca siyah, gri ve beyaz tonlarından oluşan renk paleti
- **AccentRed**: Mevcut kırmızı vurgu rengi (#FF4655) — kaldırılacak
- **AccentGreen**: Mevcut yeşil vurgu rengi (#00D26A) — kaldırılacak
- **ResourceDictionary**: WPF kaynak sözlüğü, paylaşılan stil ve renk tanımları
- **Encoding_Hatası**: Türkçe karakterlerin bozuk görünmesi (örn. "Ã¼" yerine "ü")

---

## Requirements

### Requirement 1: Monokrom Renk Teması

**User Story:** Bir kullanıcı olarak, uygulamanın tüm renkli aksanlardan arındırılmış, siyah-gri-beyaz tonlarından oluşan monokrom bir görünüme sahip olmasını istiyorum; böylece uygulama daha şık ve profesyonel görünsün.

#### Acceptance Criteria

1. THE Uygulama SHALL tüm renk tanımlarını (ResourceDictionary içindeki AccentRed #FF4655 ve AccentGreen #00D26A dahil) monokrom eşdeğerleriyle değiştirmeli; birincil vurgu rengi olarak #FFFFFF veya #E0E0E0 kullanmalıdır.
2. THE Uygulama SHALL hiçbir UI bileşeninde renkli gradient, parlak ton veya renkli arka plan içermemelidir; marka öğeleri ve durum göstergeleri dahil tüm bileşenler katı monokrom temaya uymalıdır.
3. WHEN bir NavBtn aktif duruma geçtiğinde, THE Sidebar SHALL aktif durumu kırmızı kenarlık (#FF4655) yerine beyaz veya açık gri (#E0E0E0) sol kenarlık ile göstermelidir.
4. THE Uygulama SHALL tüm düğme stillerini (BigRedBtn, RedBtn, RedSlider dahil) monokrom karşılıklarıyla güncellemeli; birincil düğmeler koyu gri (#2A2A2A) arka plan ve beyaz metin kullanmalıdır.
5. WHEN bir bileşen devre dışı (disabled) durumda olduğunda, THE Uygulama SHALL devre dışı durumu renk yerine opaklık azaltma (%40 opacity) ile göstermelidir.
6. THE Uygulama SHALL uyarı ve bilgi kutucuklarındaki renkli kenarlıkları (#FF4655, #FF9900) nötr gri tonlarıyla (#3A3A3A) değiştirmelidir.

---

### Requirement 2: Üst Bar Yeniden Tasarımı

**User Story:** Bir kullanıcı olarak, üst barın ortada uygulama ikonu ve adıyla, sağda pencere kontrol düğmeleriyle ve en üstte ince bir çizgiyle yeniden tasarlanmasını istiyorum; böylece uygulama daha temiz ve modern görünsün.

#### Acceptance Criteria

1. THE TopBar SHALL pencerenin en üstünde 1px kalınlığında yatay bir ayırıcı çizgi (#2A2A2A renk) içermelidir.
2. THE TopBar SHALL tam ortasında uygulama ikonunu (32x32 piksel) ve hemen altında "Klyze" yazısını (12px, beyaz, normal ağırlık) içermelidir.
3. THE TopBar SHALL sağ üst köşesinde küçültme (−) ve kapatma (×) ikonlarını içermelidir; her ikon 40x40 piksel tıklama alanına sahip olmalı ve hover durumunda arka plan #252525 olmalıdır.
4. THE TopBar SHALL mevcut GameSelector ComboBox bileşenini içermemelidir; oyun seçici kaldırılmalı veya Ayarlar sayfasına taşınmalıdır.
5. WHEN kullanıcı TopBar üzerine sol tıklayıp sürüklediğinde, THE Uygulama SHALL pencereyi taşımalıdır (DragMove işlevi korunmalıdır).
6. THE TopBar SHALL yüksekliği 50px olmalı ve arka planı #1A1A1A olmalıdır.

---

### Requirement 3: Türkçe Karakter ve Yazı Düzeltmeleri

**User Story:** Bir kullanıcı olarak, uygulamadaki tüm metinlerin doğru Türkçe karakterlerle ve düzgün görünümde gösterilmesini istiyorum; böylece uygulama profesyonel ve güvenilir görünsün.

#### Acceptance Criteria

1. THE Uygulama SHALL tüm XAML dosyalarında bozuk encoding içeren kaynak referanslarını (örn. `"Ã¼Ã§ Ã§izgi.png"`, `"ajan seÃ§imi.png"`, `"yazÄ± spam.png"`, `"uyarÄ±.png"`) doğru UTF-8 kodlamalı dosya adlarıyla değiştirmelidir.
2. THE Uygulama SHALL tüm TextBlock ve Run elementlerindeki bozuk encoding metinlerini (örn. `"KarÅŸÄ±lama"`, `"Ã–zellikler"`, `"HÄ±zlÄ± BaÅŸlangÄ±Ã§"`) doğru Türkçe metinlerle değiştirmelidir.
3. THE Uygulama SHALL tüm metin içeriklerinde Türkçe karakterleri (ş, ğ, ü, ö, ç, ı, İ, Ş, Ğ, Ü, Ö, Ç) doğru biçimde göstermelidir.
4. WHEN bir TextBlock içeriği kapsayıcı genişliğini aştığında, THE Uygulama SHALL metni `TextTrimming="CharacterEllipsis"` ile kesmeli ve taşmayı önlemelidir.
5. THE Uygulama SHALL tüm XAML dosyalarının başında UTF-8 encoding bildirimi içermelidir.
6. THE Uygulama SHALL görsel olmayan (placeholder) metin içermeyen tüm boş TextBlock elementlerini kaldırmalıdır.

---

### Requirement 4: Ana Sayfa — Cihaz Seçimi Bölümü

**User Story:** Bir kullanıcı olarak, ana sayfada klavye, monitör ve fare markamı seçebilmek istiyorum; böylece uygulama hangi donanımı kullandığımı bilsin ve buna göre özelleştirilmiş deneyim sunabilsin.

#### Acceptance Criteria

1. THE HomePage SHALL üç ayrı cihaz kategorisi bölümü içermelidir: "Klavye", "Monitör" ve "Fare"; her bölüm kendi başlığı ve marka kutucukları ızgarasıyla gösterilmelidir.
2. THE DeviceSelector SHALL klavye kategorisinde şu markaları içermelidir: Corsair, Logitech, Razer, SteelSeries, Keychron, HyperX, ASUS ROG, Ducky.
3. THE DeviceSelector SHALL monitör kategorisinde şu markaları içermelidir: ASUS, AOC, LG, Samsung, BenQ, MSI, Gigabyte, Dell.
4. THE DeviceSelector SHALL fare kategorisinde şu markaları içermelidir: Logitech, Razer, SteelSeries, Zowie, Pulsar, Endgame Gear, HyperX, Corsair.
5. THE DeviceCard SHALL glassmorphism efekti uygulamalıdır: yarı saydam arka plan (#1A1A1A, %60 opaklık), 1px beyaz kenarlık (%15 opaklık), `CornerRadius="10"` ve arka plan bulanıklığı.
6. WHEN kullanıcı bir DeviceCard'a tıkladığında, THE DeviceSelector SHALL o kartı seçili duruma geçirmeli; seçili kart beyaz kenarlık (#FFFFFF, 1.5px) ve hafif beyaz arka plan (#FFFFFF, %8 opaklık) ile gösterilmelidir.
7. WHEN kullanıcı seçili bir DeviceCard'a tekrar tıkladığında, THE DeviceSelector SHALL o kartın seçimini kaldırmalıdır (toggle davranışı).
8. THE DeviceSelector SHALL aynı kategoride birden fazla markanın eş zamanlı seçimine izin vermelidir.
9. THE DeviceCard SHALL şimdilik yalnızca marka adını (TextBlock, 12px, beyaz) içermelidir; ikon alanı boş bırakılmalıdır.
10. THE DeviceSelector SHALL seçilen marka bilgilerini uygulama oturumu boyunca bellekte tutmalıdır.

---

### Requirement 5: Uygulama Geneli Animasyonlar

**User Story:** Bir kullanıcı olarak, uygulamanın tüm etkileşimlerinde akıcı animasyonlar görmek istiyorum; böylece uygulama canlı, modern ve kullanıcı dostu hissettirsin.

#### Acceptance Criteria

1. WHEN bir sayfa görünür hale geldiğinde, THE Uygulama SHALL sayfadaki elemanları yukarıdan aşağıya doğru sırayla (staggered) sahneye sokmalıdır; her eleman bir öncekinden 60ms sonra, 300ms süre ve `CubicEase EasingMode=EaseOut` ile `TranslateTransform` (Y: -20px → 0px) ve `Opacity` (0 → 1) animasyonu uygulamalıdır.
2. WHEN kullanıcı bir düğmeye (Button) tıkladığında, THE Uygulama SHALL düğmeye hafif basılma hissi vermek için `ScaleTransform` animasyonu uygulamalıdır (1.0 → 0.95, 80ms, `QuadraticEase EasingMode=EaseIn`).
3. WHEN kullanıcı bir DeviceCard üzerine geldiğinde (MouseEnter), THE Uygulama SHALL kartı hafifçe yukarı kaldırmalıdır (`TranslateTransform` Y: 0 → -4px, 150ms) ve gölge efektini artırmalıdır (`DropShadowEffect BlurRadius`: 4 → 12, 150ms).
4. WHEN kullanıcı bir DeviceCard'dan uzaklaştığında (MouseLeave), THE Uygulama SHALL kartı orijinal konumuna döndürmelidir (150ms, `CubicEase EasingMode=EaseOut`).
5. WHEN kullanıcı Sidebar'daki bir NavBtn'a tıkladığında, THE Uygulama SHALL sayfa geçişini `Opacity` animasyonu ile gerçekleştirmelidir (0 → 1, 200ms).
6. THE Uygulama SHALL tüm animasyonları WPF `Storyboard` ve `DoubleAnimation` mekanizmaları ile uygulamalıdır; üçüncü taraf kütüphane gerektirmemelidir.
7. WHILE bir animasyon oynatılırken, THE Uygulama SHALL kullanıcı etkileşimini engellememeli; animasyonlar arka planda çalışmalıdır.

---

### Requirement 6: Sidebar Navigasyon Güncellemesi

**User Story:** Bir kullanıcı olarak, sidebar navigasyonunun monokrom tema ile uyumlu ve animasyonlu olmasını istiyorum; böylece navigasyon deneyimi tutarlı ve akıcı olsun.

#### Acceptance Criteria

1. THE Sidebar SHALL aktif NavBtn'ı kırmızı (#FF4655) kenarlık yerine beyaz (#FFFFFF) 2px sol kenarlık ve #252525 arka plan ile göstermelidir.
2. WHEN kullanıcı bir NavBtn üzerine geldiğinde (hover), THE Sidebar SHALL arka planı #1E1E1E'den #252525'e 150ms sürede geçirmelidir.
3. THE Sidebar SHALL genişletilmiş modda (expanded) NavBtn etiketlerini (TextBlock) göstermeli; daraltılmış modda (collapsed) yalnızca ikonları göstermelidir.
4. WHEN Sidebar genişletildiğinde veya daraltıldığında, THE Sidebar SHALL genişlik değişimini `DoubleAnimation` ile 200ms sürede animasyonlu olarak gerçekleştirmelidir.
5. THE Sidebar SHALL tüm ikon kaynaklarını doğru dosya adlarıyla (Türkçe karakter içermeyen, ASCII uyumlu) referans etmelidir.S
