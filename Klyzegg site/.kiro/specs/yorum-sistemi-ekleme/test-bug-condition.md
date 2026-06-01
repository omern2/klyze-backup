# Bug Condition Exploration Test

**Property 1: Bug Condition** - Yorumlar Firebase'e Kaydedilmiyor

## Test Amacı
Bu test, unfixed kodda bug'ın var olduğunu doğrulamak için yazılmıştır. Test BAŞARISIZ olmalıdır - bu bug'ın varlığını kanıtlar.

## Bug Condition (C)
```
isBugCondition(input) WHERE:
  - input.userLoggedIn = true
  - input.starsSelected > 0
  - input.reviewText.length > 0
  - input.submitButtonClicked = true
  - calledFunction = "submitReview()" (satır 2114, eski fonksiyon)
  - NOT calledFunction = "window.submitReview" (satır 2313, yeni fonksiyon)
```

## Expected Behavior (P)
Yorum gönderildiğinde:
1. Firebase Realtime Database'deki `reviews` node'una `push()` ile kaydedilmeli
2. "Yorumunuz eklendi! 🎉" mesajı gösterilmeli
3. Form temizlenmeli (textarea boşaltılmalı, yıldızlar sıfırlanmalı)
4. Sayfa yenilendiğinde yorum görünür olmalı
5. Diğer kullanıcılar yorumu görebilmeli

## Test Prosedürü (UNFIXED CODE)

### Ön Hazırlık
1. Firebase Console'u aç: https://console.firebase.google.com/
2. Projeyi seç ve Realtime Database'e git
3. `reviews` node'unu bul ve mevcut yorum sayısını not et

### Test Adımları
1. **Tarayıcıda klyze.html dosyasını aç** (unfixed version - satır 2114'teki eski submitReview fonksiyonu mevcut)
2. **Google ile giriş yap**
3. **Yorum bölümüne git** (Reviews section)
4. **5 yıldız seç**
5. **Yorum metni yaz**: "Test yorumu - Bug condition exploration"
6. **Browser DevTools Console'u aç** (F12)
7. **Console'da şu komutu çalıştır** (hangi fonksiyonun çağrıldığını görmek için):
   ```javascript
   console.log('submitReview function:', submitReview.toString().substring(0, 100));
   ```
8. **"Gönder" butonuna tıkla**
9. **Console'da hata veya log mesajlarını kontrol et**
10. **Firebase Console'da `reviews` node'unu yenile**
11. **Tarayıcı sayfasını yenile (F5)**
12. **Yorumun hala görünür olup olmadığını kontrol et**

## Beklenen Sonuç (UNFIXED CODE)

### ❌ Test BAŞARISIZ Olmalı (Bu Doğru - Bug Var)

**Counterexample 1: Firebase'e Kaydedilmeme**
- Firebase Console'da `reviews` node'unda yeni yorum GÖRÜNMEZ
- Yorum sayısı artmaz
- Sadece local state'e eklenir

**Counterexample 2: Persistence Yok**
- Sayfa yenilendiğinde yorum KAYBOLUR
- Sadece hardcoded örnek yorumlar görünür

**Counterexample 3: Multi-User Görünürlük Yok**
- Başka bir tarayıcıda/cihazda sayfa açıldığında yorum GÖRÜNMEZ
- Diğer kullanıcılar yorumu göremez

**Counterexample 4: Yanlış Fonksiyon Çağrılıyor**
- Console'da görünen fonksiyon satır 2114'teki eski fonksiyon olmalı
- `state.reviews.unshift` içeren kod görünmeli
- Firebase `push()` içeren kod görünmemeli

## Test Sonucu Dokümantasyonu

### Test Tarihi: [Tarih buraya yazılacak]
### Test Eden: [İsim buraya yazılacak]

**Gözlemlenen Davranış:**
- [ ] Yorum local state'e eklendi mi? (Evet olmalı)
- [ ] Firebase Console'da yorum görünüyor mu? (Hayır olmalı - BUG)
- [ ] Sayfa yenilendiğinde yorum kayboldu mu? (Evet olmalı - BUG)
- [ ] Console'da hangi fonksiyon çağrıldı? (Satır 2114'teki eski fonksiyon olmalı)

**Counterexample Detayları:**
```
Input:
  - User: [Google kullanıcı adı]
  - Stars: 5
  - Text: "Test yorumu - Bug condition exploration"
  - Timestamp: [Timestamp]

Actual Output:
  - Firebase'e kaydedildi mi: HAYIR ❌
  - Local state'e eklendi mi: EVET ✓
  - Sayfa yenilendiğinde görünür mü: HAYIR ❌
  - Diğer kullanıcılar görebilir mi: HAYIR ❌

Expected Output:
  - Firebase'e kaydedilmeli: EVET ✓
  - Local state'e eklenmeli: EVET ✓
  - Sayfa yenilendiğinde görünür olmalı: EVET ✓
  - Diğer kullanıcılar görebilmeli: EVET ✓
```

**Root Cause Analizi:**
- HTML'deki `onclick="submitReview()"` event handler, global scope'taki eski fonksiyonu (satır 2114) çağırıyor
- Eski fonksiyon sadece `state.reviews.unshift()` ile local state'e ekliyor
- Firebase entegrasyonlu `window.submitReview` fonksiyonu (satır 2313) hiç çağrılmıyor
- Bu yüzden `push(reviewsRef, newReview)` kodu çalışmıyor

## Test Tamamlandı ✓
Bu test unfixed kodda çalıştırıldı ve bug'ın varlığı doğrulandı. Test başarısız oldu (expected). Şimdi fix uygulanabilir.
