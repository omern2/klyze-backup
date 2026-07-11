# Klyze Kurulum Rehberi

## Gereksinimler
- Windows 10 veya 11 (64-bit)
- [.NET 8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (yüklü değilse kurulum sırasında uyarılır)

## Yeni Kurulum
1. **KlyzeSetup.exe**'yi [GitHub Releases](https://github.com/omern2/klyze-releases/releases) sayfasından indir
2. Yönetici olarak çalıştır (`Sağ tık → Yönetici olarak çalıştır`)
3. Kurulum dizinini seç (varsayılan: `C:\Program Files\Klyze`)
4. Kurulum tamamlandığında masaüstü kısayolu oluşturulur

## Güncelleme (Mevcut Kullanıcılar)
- Uygulama açıkken otomatik olarak yeni sürümü kontrol eder
- Bildirim geldiğinde **Güncelle** butonuna tıkla
- İndirme tamamlanınca uygulama kapanır, güncellenir ve yeniden açılır

## Manuel Güncelleme
1. **klyze_update.zip**'i [GitHub Releases](https://github.com/omern2/klyze-releases/releases) sayfasından indir
2. `C:\Program Files\Klyze\` dizinindeki tüm dosyaları zip içindekilerle değiştir
3. Uygulamayı yeniden başlat

## Kaldırma
- `C:\Program Files\Klyze\` dizinini sil
- Masaüstü kısayolunu sil
- `%LOCALAPPDATA%\Klyze\` dizinini sil (ayarlar ve önbellek)

## Sıkça Sorulan Sorular

**S: Uygulama açılmıyor, hata veriyor?**
C: .NET 8 Runtime'ın yüklü olduğundan emin ol. Yönetici olarak çalıştırmayı dene.

**S: Güncelleme bildirimi gelmiyor?**
C: İnternet bağlantını kontrol et. Güvenlik duvarı/firewall uygulamanın bağlantısını engelliyor olabilir.

**S: Henrik API'den veri gelmiyor?**
C: Henrik Dev API anahtarı Firebase üzerinden otomatik yüklenir. İnternet bağlantısı gereklidir.
