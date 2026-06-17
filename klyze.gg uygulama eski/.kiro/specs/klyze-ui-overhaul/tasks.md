# Implementation Plan: Klyze UI Overhaul

## Overview

Bu plan, Klyze.gg WPF/C# uygulamasının kapsamlı UI güncellemesini adım adım uygular. Sırasıyla: monokrom renk teması, üst bar yeniden tasarımı, Türkçe karakter düzeltmeleri, ana sayfa cihaz seçimi bölümü ve uygulama geneli animasyonlar hayata geçirilir. Her adım bir öncekinin üzerine inşa edilir; tüm değişiklikler mevcut `MainWindow.xaml`, `MainWindow.xaml.cs` ve `ViewModels/MainViewModel.cs` dosyaları üzerinde yapılır; iki yeni dosya eklenir.

---

## Tasks

- [ ] 1. Monokrom Renk Teması — ResourceDictionary ve Stil Güncellemeleri
  - [ ] 1.1 `MainWindow.xaml` içindeki `Window.Resources` bölümünü güncelle
    - `AccentRed` (#FF4655) ve `AccentGreen` (#00D26A) kaynaklarını kaldır
    - `AccentWhite` (#FFFFFF) ve `AccentLightGray` (#E0E0E0) kaynaklarını ekle
    - `SidebarBg` rengini `#141414` olarak güncelle
    - `BigRedBtn` stilini `BigPrimaryBtn` olarak yeniden adlandır; arka plan `#2A2A2A`, metin `#FFFFFF`, hover `#3A3A3A`
    - `RedBtn` stilini `PrimaryBtn` olarak yeniden adlandır; arka plan `#2A2A2A`, metin `#FFFFFF`
    - `RedSlider` stilini `MonoSlider` olarak yeniden adlandır; aktif track `#FFFFFF`, pasif track `#3A3A3A`
    - Tüm XAML içindeki `{StaticResource AccentRed}` ve `{StaticResource AccentGreen}` referanslarını monokrom karşılıklarıyla değiştir
    - Uyarı/bilgi kutucuklarındaki renkli kenarlıkları (#FF4655, #FF9900) `#3A3A3A` ile değiştir
    - Devre dışı (disabled) durumları renk yerine `Opacity="0.4"` ile göster
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6_

  - [ ]* 1.2 Property testi yaz — Monokrom Renk Invariantı
    - **Property 1: Monokrom Renk Invariantı**
    - `MainWindow.xaml` içindeki tüm `SolidColorBrush` kaynaklarını parse et; her renk için `|R-G| <= 10 && |G-B| <= 10 && |R-B| <= 10` doğrula
    - `#FF4655` ve `#00D26A` değerlerinin bulunmadığını doğrula
    - FsCheck ile 100 iterasyon
    - **Validates: Requirements 1.2**

- [ ] 2. Üst Bar (TopBar) Yeniden Tasarımı
  - [ ] 2.1 `MainWindow.xaml` içindeki TopBar Border'ını yeniden yaz
    - `GameSelector` ComboBox'ı kaldır (veya yorum satırına al)
    - Yüksekliği 60px'den 50px'e düşür, arka plan `#1A1A1A`
    - En üste 1px `#2A2A2A` kenarlık ekle (`BorderThickness="0,1,0,0"`)
    - Ortaya `StackPanel` (Vertical): `Image` (uygulama_logusu.png, 32×32) + `TextBlock` "Klyze" (12px, beyaz, normal ağırlık)
    - Sağa pencere kontrol düğmeleri: küçültme (−) ve kapatma (×), her biri 40×40px tıklama alanı, hover arka plan `#252525`
    - `Header_MouseLeftButtonDown` event handler'ını koru (DragMove)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [ ] 2.2 `MainWindow.xaml.cs` içinde `GameSelector_SelectionChanged` handler'ını yorum satırına al
    - `VM.CurrentGame` property'sini `MainViewModel.cs` içinde koru
    - _Requirements: 2.4_

- [ ] 3. Türkçe Karakter ve Encoding Düzeltmeleri
  - [ ] 3.1 `MainWindow.xaml` dosyasının başına UTF-8 encoding bildirimi ekle
    - İlk satır: `<?xml version="1.0" encoding="utf-8"?>`
    - _Requirements: 3.5_

  - [ ] 3.2 Bozuk dosya adı referanslarını düzelt
    - `"Ã¼Ã§ Ã§izgi.png"` → `"uc_cizgi.png"`
    - `"ajan seÃ§imi.png"` → `"ajan_secimi.png"`
    - `"yazÄ± spam.png"` → `"yazi_spam.png"`
    - `"uyarÄ±.png"` → `"uyari.png"`
    - `"Ana_sayfa.png"` → `"home.png"` (mevcut dosya)
    - Tüm `Image.Source` attribute değerlerini ASCII uyumlu dosya adlarıyla güncelle
    - _Requirements: 3.1, 6.5_

  - [ ] 3.3 Bozuk TextBlock metin içeriklerini düzelt
    - `"KarÅŸÄ±lama"` → `"Karşılama"`, `"Ã–zellikler"` → `"Özellikler"`, `"HÄ±zlÄ± BaÅŸlangÄ±Ã§"` → `"Hızlı Başlangıç"` ve benzeri tüm bozuk metinleri doğru Türkçe karşılıklarıyla değiştir
    - Taşan metinlere `TextTrimming="CharacterEllipsis"` ekle
    - Boş (placeholder olmayan) TextBlock elementlerini kaldır
    - _Requirements: 3.2, 3.3, 3.4, 3.6_

  - [ ]* 3.4 Property testi yaz — XAML Mojibake Yokluğu
    - **Property 3: XAML String Değerlerinde Mojibake Bulunmaması**
    - `MainWindow.xaml` dosyasını oku; tüm attribute değerlerini ve text içeriklerini extract et
    - `"Ã"`, `"Å"`, `"Ä"`, `"â€"`, `"Ä±"` pattern'larının hiçbirinde bulunmadığını doğrula
    - FsCheck ile 100 iterasyon
    - **Validates: Requirements 3.1, 3.2, 6.5**

- [ ] 4. Checkpoint — Temel Değişiklikler
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 5. Sidebar Navigasyon Güncellemesi
  - [ ] 5.1 `MainWindow.xaml.cs` içindeki `SetNavActive` metodunu güncelle
    - Aktif durum: `Background = #252525`, `BorderBrush = Colors.White`, `BorderThickness = new Thickness(2, 0, 0, 0)`
    - Pasif durum: `Background = Transparent`, `BorderBrush = Transparent`, `BorderThickness = new Thickness(0)`
    - _Requirements: 1.3, 6.1_

  - [ ] 5.2 `MainWindow.xaml` içindeki `NavBtn` stilini güncelle
    - Hover arka planını `#252525` olarak koru (150ms geçiş için EventTrigger eklenecek — animasyon adımında)
    - Daraltılmış/genişletilmiş mod TextBlock görünürlüğünü koru
    - _Requirements: 6.2, 6.3_

  - [ ]* 5.3 Birim testi yaz — SetNavActive
    - `SetNavActive(btn, true)` → `BorderBrush` beyaz, `Background` #252525 olduğunu doğrula
    - `SetNavActive(btn, false)` → `BorderBrush` Transparent olduğunu doğrula
    - xUnit ile test et
    - **Validates: Requirements 1.3, 6.1**

  - [ ]* 5.4 Property testi yaz — NavBtn Aktif Durum Kenarlık Rengi
    - **Property 2: NavBtn Aktif Durum Kenarlık Rengi**
    - `SetNavActive(btn, active)` için `bool` parametresi üzerinde FsCheck property testi
    - Aktif: `BorderBrush` beyaz veya açık gri; pasif: Transparent
    - **Validates: Requirements 1.3, 6.1**

- [ ] 6. DeviceSelectionModel ve HomeViewModel Oluşturma
  - [ ] 6.1 `Models/DeviceSelectionModel.cs` dosyasını oluştur
    - `SelectedKeyboards`, `SelectedMonitors`, `SelectedMice` (`List<string>`) property'lerini tanımla
    - Namespace: `ValorantAutoClicker.Models`
    - _Requirements: 4.1, 4.10_

  - [ ] 6.2 `ViewModels/HomeViewModel.cs` dosyasını oluştur
    - `KeyboardBrands`, `MonitorBrands`, `MouseBrands` statik dizilerini tanımla (Req 4.2–4.4'teki markalar)
    - `DeviceBrandItem` partial class'ını oluştur: `Name` (string), `IsSelected` (ObservableProperty)
    - `ObservableCollection<DeviceBrandItem>` koleksiyonlarını oluştur
    - `ToggleDeviceCommand` (`IRelayCommand<DeviceBrandItem>`) ve `ToggleDevice` metodunu uygula
    - `SyncToModel()` ile `DeviceSelectionModel`'i güncelle
    - `GetSelection()` metodunu ekle
    - `ToggleDevice(null)` için null check ekle
    - Namespace: `ValorantAutoClicker.ViewModels`
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.6, 4.7, 4.8, 4.10_

  - [ ] 6.3 `ViewModels/MainViewModel.cs` dosyasına `HomeVM` property'si ekle
    - `public HomeViewModel HomeVM { get; }` property'si ekle
    - Constructor içinde `HomeVM = new HomeViewModel();` başlat
    - _Requirements: 4.1_

  - [ ]* 6.4 Birim testi yaz — HomeViewModel
    - `KeyboardBrands`, `MonitorBrands`, `MouseBrands` dizilerinin beklenen değerleri içerdiğini doğrula
    - `ToggleDevice(item)` → `item.IsSelected = true` olduğunu doğrula
    - `GetSelection()` seçili item'ları doğru yansıttığını doğrula
    - `ToggleDevice(null)` → exception fırlatmadığını doğrula
    - xUnit ile test et
    - _Requirements: 4.2, 4.3, 4.4, 4.6, 4.10_

  - [ ]* 6.5 Property testi yaz — DeviceCard Toggle Round-Trip
    - **Property 4: DeviceCard Toggle Round-Trip**
    - `ToggleDevice` iki kez çağrıldığında `IsSelected` başlangıç değerine döndüğünü doğrula
    - `string brandName` ve `bool initialState` parametreleri üzerinde FsCheck property testi
    - **Validates: Requirements 4.6, 4.7, 4.10**

  - [ ]* 6.6 Property testi yaz — Çoklu Seçim Bağımsızlığı
    - **Property 5: Çoklu Cihaz Seçimi Bağımsızlığı**
    - Bir item toggle edildiğinde diğer item'ların `IsSelected` durumunun değişmediğini doğrula
    - `int targetIndex` parametresi üzerinde FsCheck property testi; `KeyboardItems` koleksiyonu üzerinde çalış
    - **Validates: Requirements 4.8**

- [ ] 7. Ana Sayfa — DeviceSelector XAML Bileşeni
  - [ ] 7.1 `MainWindow.xaml` içine `DeviceCard` stilini ekle
    - `Window.Resources` içine `Style x:Key="DeviceCard" TargetType="Border"` ekle
    - Glassmorphism: `Background="#1A1A1A"`, `Opacity="0.6"`, `BorderBrush="#26FFFFFF"` (1px), `CornerRadius="10"`, `Padding="12,10"`, `Cursor="Hand"`
    - Seçili durum için ayrı stil veya trigger: `BorderBrush="#FFFFFF"`, `BorderThickness="1.5"`, `Background="#14FFFFFF"`
    - _Requirements: 4.5, 4.6_

  - [ ] 7.2 `MainWindow.xaml` içindeki `HomePage` Grid'ine DeviceSelector bölümünü ekle
    - "Cihazlarınız" başlıklı ana `Border` kartı ekle
    - Üç kategori bölümü: "⌨ Klavye", "🖥 Monitör", "🖱 Fare"
    - Her kategori için `UniformGrid` veya `WrapPanel` ile marka kartları ızgarası (4 sütun)
    - Her `DeviceCard` içinde marka adını gösteren `TextBlock` (12px, beyaz)
    - `ItemsControl` ile `HomeVM.KeyboardItems`, `HomeVM.MonitorItems`, `HomeVM.MouseItems` koleksiyonlarına bind et
    - `ToggleDeviceCommand` ile tıklama bağlantısı kur
    - `IsSelected` property'sine göre seçili/pasif görünümü için `DataTrigger` ekle
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9_

- [ ] 8. Checkpoint — Cihaz Seçimi
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Animasyonlar
  - [ ] 9.1 `MainWindow.xaml.cs` içine `PlayStaggeredEntry(Panel container)` metodunu ekle
    - Her child için `TranslateTransform(0, -20)` ve `Opacity = 0` başlangıç değerleri ata
    - `index * 60ms` gecikme, 300ms süre, `CubicEase EasingMode=EaseOut`
    - Y: -20 → 0 ve Opacity: 0 → 1 animasyonları
    - `Children.Cast<UIElement>().ToList()` ile snapshot al (koleksiyon değişim koruması)
    - _Requirements: 5.1, 5.6, 5.7_

  - [ ] 9.2 `MainWindow.xaml.cs` içine `PlayPageTransition(Grid newPage)` metodunu ekle
    - `newPage.Opacity = 0`, `newPage.Visibility = Visible`
    - `DoubleAnimation(0, 1, 200ms)` ile opacity animasyonu
    - `OnPageChanged` metodunu bu metodu kullanacak şekilde güncelle
    - _Requirements: 5.5, 5.6, 5.7_

  - [ ] 9.3 `MainWindow.xaml` içindeki `BigPrimaryBtn` ve `PrimaryBtn` stillerine button press scale animasyonu ekle
    - `ControlTemplate.Triggers` içine `EventTrigger RoutedEvent="Button.PreviewMouseLeftButtonDown"` ekle
    - `ScaleTransform` ile ScaleX ve ScaleY: 1.0 → 0.95, 80ms, `QuadraticEase EasingMode=EaseIn`
    - `EventTrigger RoutedEvent="Button.PreviewMouseLeftButtonUp"` ile 1.0'a geri dön
    - `RenderTransformOrigin="0.5,0.5"` ayarla
    - _Requirements: 5.2, 5.6_

  - [ ] 9.4 `MainWindow.xaml` içindeki `DeviceCard` stiline hover lift animasyonu ekle
    - `ControlTemplate` veya `Style.Triggers` içine `EventTrigger RoutedEvent="Border.MouseEnter"` ekle
    - `TranslateTransform` Y: 0 → -4px, 150ms
    - `DropShadowEffect BlurRadius`: 4 → 12, 150ms
    - `EventTrigger RoutedEvent="Border.MouseLeave"` ile orijinal konuma dön (150ms, `CubicEase EasingMode=EaseOut`)
    - _Requirements: 5.3, 5.4, 5.6_

  - [ ] 9.5 `MainWindow.xaml` içindeki `NavBtn` stiline hover arka plan geçiş animasyonu ekle
    - `EventTrigger RoutedEvent="Button.MouseEnter"` ile arka plan `#1E1E1E` → `#252525`, 150ms
    - `EventTrigger RoutedEvent="Button.MouseLeave"` ile geri dön
    - _Requirements: 6.2_

  - [ ] 9.6 `MainWindow.xaml` içindeki Sidebar genişletme/daraltma animasyonunu güncelle
    - `ToggleSidebar_Click` handler'ında `DoubleAnimation` ile `SidebarCol.Width` değişimini 200ms'de animasyonlu yap
    - _Requirements: 6.4_

  - [ ]* 9.7 Birim testi yaz — Animasyon Parametreleri
    - `PlayStaggeredEntry` → her child için `BeginTime = index * 60ms`, `Duration = 300ms` olduğunu doğrula
    - `PlayPageTransition` → Opacity animasyonu 0→1, 200ms olduğunu doğrula
    - xUnit ile test et
    - _Requirements: 5.1, 5.5_

- [ ] 10. Final Checkpoint — Tüm Testler
  - Ensure all tests pass, ask the user if questions arise.

---

## Notes

- `*` ile işaretli görevler isteğe bağlıdır; MVP için atlanabilir.
- Her görev belirli gereksinimlere referans verir (izlenebilirlik için).
- Checkpoint'ler artımlı doğrulama sağlar.
- Property testleri FsCheck (C# desteği olan .NET PBT kütüphanesi) ile yazılır.
- Birim testleri xUnit ile yazılır.
- Animasyonlar yalnızca WPF yerleşik `Storyboard`/`DoubleAnimation` mekanizması kullanır; üçüncü taraf kütüphane gerekmez.
- `DeviceSelectionModel` oturum belleğinde tutulur, diske yazılmaz.
- `GameSelector_SelectionChanged` handler derleme hatası oluşmaması için silinmez, yorum satırına alınır.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "3.1", "6.1"] },
    { "id": 1, "tasks": ["2.1", "3.2", "3.3", "6.2"] },
    { "id": 2, "tasks": ["1.2", "2.2", "3.4", "5.1", "6.3"] },
    { "id": 3, "tasks": ["5.2", "6.4", "7.1"] },
    { "id": 4, "tasks": ["5.3", "5.4", "6.5", "6.6", "7.2"] },
    { "id": 5, "tasks": ["9.1", "9.2", "9.3", "9.4", "9.5", "9.6"] },
    { "id": 6, "tasks": ["9.7"] }
  ]
}
```
