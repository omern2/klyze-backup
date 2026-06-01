# Yorum Sistemi Bugfix Requirements

## Giriş

Klyze.gg sitesinde mevcut yorum sistemi kullanıcı arayüzünde görünüyor ancak yorumlar Firebase veritabanına kaydedilmiyor. Kullanıcılar giriş yaptıktan sonra yorum yazıp yıldız puanı verebiliyor, ancak bu veriler sadece tarayıcı belleğinde (JavaScript `state` objesi) saklanıyor. Sayfa yenilendiğinde tüm yorumlar kayboluyor ve diğer kullanıcılar bu yorumları göremiyor. Bu bug, iki farklı `submitReview()` fonksiyonunun çakışmasından kaynaklanıyor: eski fonksiyon sadece local state'e ekliyor, yeni Firebase entegrasyonlu fonksiyon ise çağrılmıyor.

## Bug Analizi

### 1. Mevcut Davranış (Hatalı)

1.1 WHEN kullanıcı giriş yaptıktan sonra yorum yazar ve "Gönder" butonuna tıklarsa THEN yorum sadece tarayıcı belleğinde (`state.reviews` array) saklanır ve Firebase veritabanına kaydedilmez

1.2 WHEN kullanıcı yorum gönderdikten sonra sayfayı yenilerse THEN tüm yorumlar kaybolur ve sadece hardcoded örnek yorumlar görünür

1.3 WHEN bir kullanıcı yorum yazdığında THEN diğer kullanıcılar bu yorumu göremez çünkü veri Firebase'e kaydedilmemiştir

1.4 WHEN kullanıcı yıldız puanı verirse THEN bu puan ortalamaya dahil edilir ancak sayfa yenilendiğinde ortalama eski haline döner

1.5 WHEN `submitReview()` fonksiyonu çağrıldığında THEN eski fonksiyon (satır 2114) çalışır ve Firebase'e kaydetme işlemi yapan yeni fonksiyon (satır 2313) çağrılmaz

### 2. Beklenen Davranış (Doğru)

2.1 WHEN kullanıcı giriş yaptıktan sonra yorum yazar ve "Gönder" butonuna tıklarsa THEN yorum Firebase Realtime Database'deki `reviews` node'una `push()` metodu ile kaydedilmelidir

2.2 WHEN kullanıcı yorum gönderdikten sonra sayfayı yenilerse THEN yorumlar Firebase'den yüklenmeli ve kalıcı olarak görünmelidir

2.3 WHEN bir kullanıcı yorum yazdığında THEN tüm kullanıcılar bu yorumu gerçek zamanlı olarak görebilmelidir (Firebase `onValue` listener sayesinde)

2.4 WHEN kullanıcı yıldız puanı verirse THEN bu puan Firebase'e kaydedilmeli ve ortalama puan tüm kullanıcılar için doğru hesaplanmalıdır

2.5 WHEN `submitReview()` fonksiyonu çağrıldığında THEN Firebase entegrasyonlu `window.submitReview` fonksiyonu çalışmalı ve yorum veritabanına kaydedilmelidir

2.6 WHEN yorum başarıyla kaydedildiğinde THEN kullanıcıya "Yorumunuz eklendi! 🎉" mesajı gösterilmeli ve form temizlenmelidir

### 3. Değişmemesi Gereken Davranışlar (Regresyon Önleme)

3.1 WHEN kullanıcı giriş yapmadan yorum yazmaya çalışırsa THEN "Yorum yapmak için giriş yapmalısınız!" uyarısı gösterilmeye DEVAM ETMELİDİR

3.2 WHEN kullanıcı yıldız seçmeden yorum göndermeye çalışırsa THEN "Lütfen yıldız verin!" uyarısı gösterilmeye DEVAM ETMELİDİR

3.3 WHEN kullanıcı boş yorum metni ile göndermeye çalışırsa THEN "Lütfen yorum yazın!" uyarısı gösterilmeye DEVAM ETMELİDİR

3.4 WHEN yorumlar Firebase'den yüklendiğinde THEN mevcut yorum kartı tasarımı (avatar, isim, tarih, yıldızlar, metin, beğeni butonu) aynı şekilde görünmeye DEVAM ETMELİDİR

3.5 WHEN kullanıcı profil avatarına tıklarsa THEN dropdown menü açılmaya ve Google giriş/çıkış işlemleri çalışmaya DEVAM ETMELİDİR

3.6 WHEN yorumlar yüklendiğinde THEN ortalama puan ve toplam yorum sayısı doğru hesaplanmaya DEVAM ETMELİDİR

3.7 WHEN kullanıcı yorum gönderdiğinde THEN yorum formu temizlenmeli ve yıldız seçimi sıfırlanmaya DEVAM ETMELİDİR

3.8 WHEN Firebase'den yorumlar yüklendiğinde THEN yorumlar tarih sırasına göre (en yeni üstte) görünmeye DEVAM ETMELİDİR
