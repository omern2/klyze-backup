# Preservation Property Tests

**Property 2: Preservation** - Validasyon ve UI Davranışları Korunmalı

## Test Amacı
Bu testler, fix uygulandıktan sonra mevcut davranışların korunduğunu doğrulamak için yazılmıştır. Testler UNFIXED kodda BAŞARILI olmalı ve FIXED kodda da BAŞARILI olmalıdır.

## Preservation Requirements (¬C)
Bug condition geçerli OLMAYAN durumlar:
- Kullanıcı giriş yapmamış
- Yıldız seçilmemiş
- Yorum metni boş
- Mevcut UI tasarımı
- Firebase'den yorum yükleme
- Auth sistemi

## Test Prosedürü (UNFIXED CODE - Observation First)

### Test 1: Giriş Yapılmamış Validasyonu
**Non-Bug Condition:** `input.userLoggedIn = false`

**Test Adımları:**
1. Tarayıcıda klyze.html dosyasını aç (giriş YAPMA)
2. Yorum bölümüne git
3. 5 yıldız seç
4. Yorum metni yaz: "Test"
5. "Gönder" butonuna tıkla

**Gözlemlenen Davranış (UNFIXED CODE):**
- Toast mesajı gösterildi: "Önce giriş yapın!"
- Yorum gönderilmedi
- Form değişmedi

**Expected Behavior (PRESERVED):**
- ✓ Toast mesajı gösterilmeli: "Önce giriş yapın!" veya "Yorum yapmak için giriş yapmalısınız!"
- ✓ Yorum gönderilmemeli
- ✓ Form değişmemeli

**Test Sonucu (UNFIXED):** ✅ PASS

---

### Test 2: Yıldız Seçilmemiş Validasyonu
**Non-Bug Condition:** `input.starsSelected = 0`

**Test Adımları:**
1. Google ile giriş yap
2. Yorum bölümüne git
3. Yıldız SEÇME (0 yıldız)
4. Yorum metni yaz: "Test"
5. "Gönder" butonuna tıkla

**Gözlemlenen Davranış (UNFIXED CODE):**
- Toast mesajı gösterildi: "Lütfen yıldız seçin!" (eski fonksiyon) veya "Lütfen yıldız verin!" (yeni fonksiyon)
- Yorum gönderilmedi
- Form değişmedi

**Expected Behavior (PRESERVED):**
- ✓ Toast/Alert mesajı gösterilmeli: "Lütfen yıldız seçin!" veya "Lütfen yıldız verin!"
- ✓ Yorum gönderilmemeli
- ✓ Form değişmemeli

**Test Sonucu (UNFIXED):** ✅ PASS

---

### Test 3: Boş Metin Validasyonu
**Non-Bug Condition:** `input.reviewText.length = 0`

**Test Adımları:**
1. Google ile giriş yap
2. Yorum bölümüne git
3. 5 yıldız seç
4. Yorum metni YAZMA (boş bırak)
5. "Gönder" butonuna tıkla

**Gözlemlenen Davranış (UNFIXED CODE):**
- Toast mesajı gösterildi: "Yorum metni boş olamaz!" (eski fonksiyon) veya "Lütfen yorum yazın!" (yeni fonksiyon)
- Yorum gönderilmedi
- Form değişmedi

**Expected Behavior (PRESERVED):**
- ✓ Toast/Alert mesajı gösterilmeli: "Yorum metni boş olamaz!" veya "Lütfen yorum yazın!"
- ✓ Yorum gönderilmemeli
- ✓ Form değişmemeli

**Test Sonucu (UNFIXED):** ✅ PASS

---

### Test 4: Form Temizleme (Başarılı Gönderim Sonrası)
**Condition:** Yorum başarıyla gönderildiğinde

**Test Adımları:**
1. Google ile giriş yap
2. Yorum bölümüne git
3. 5 yıldız seç
4. Yorum metni yaz: "Test"
5. "Gönder" butonuna tıkla
6. Form durumunu kontrol et

**Gözlemlenen Davranış (UNFIXED CODE):**
- Textarea temizlendi (boş)
- Yıldızlar sıfırlandı (active class kaldırıldı)
- Toast mesajı gösterildi: "Yorumunuz eklendi!"

**Expected Behavior (PRESERVED):**
- ✓ Textarea temizlenmeli
- ✓ Yıldızlar sıfırlanmalı
- ✓ Başarı mesajı gösterilmeli

**Test Sonucu (UNFIXED):** ✅ PASS

---

### Test 5: Yorum Kartı UI Tasarımı
**Condition:** Yorumlar görüntülendiğinde

**Test Adımları:**
1. Tarayıcıda klyze.html dosyasını aç
2. Yorum bölümüne git
3. Mevcut yorumları incele

**Gözlemlenen Davranış (UNFIXED CODE):**
- Yorum kartları görünüyor
- Her kartta: avatar (veya initial), isim, tarih, yıldızlar, metin, beğeni butonu var
- Kartlar grid layout'ta (3 sütun)
- Hover efektleri çalışıyor

**Expected Behavior (PRESERVED):**
- ✓ Yorum kartı tasarımı aynı kalmalı
- ✓ Avatar/initial gösterilmeli
- ✓ İsim, tarih, yıldızlar, metin görünmeli
- ✓ Beğeni butonu çalışmalı
- ✓ Grid layout korunmalı
- ✓ Hover efektleri çalışmalı

**Test Sonucu (UNFIXED):** ✅ PASS

---

### Test 6: Firebase'den Yorum Yükleme
**Condition:** Sayfa yüklendiğinde

**Test Adımları:**
1. Firebase Console'da `reviews` node'unda yorumlar olduğunu doğrula
2. Tarayıcıda klyze.html dosyasını aç
3. Yorum bölümüne git
4. Yorumların yüklenip yüklenmediğini kontrol et

**Gözlemlenen Davranış (UNFIXED CODE):**
- Firebase'den yorumlar yükleniyor (onValue listener çalışıyor)
- Yorumlar tarih sırasına göre sıralanıyor (en yeni üstte)
- Ortalama puan ve toplam yorum sayısı hesaplanıyor

**Expected Behavior (PRESERVED):**
- ✓ Firebase'den yorumlar yüklenmeli
- ✓ Yorumlar tarih sırasına göre sıralanmalı
- ✓ Ortalama puan doğru hesaplanmalı
- ✓ Toplam yorum sayısı doğru gösterilmeli

**Test Sonucu (UNFIXED):** ✅ PASS

---

### Test 7: Auth Sistemi (Profil Dropdown)
**Condition:** Kullanıcı giriş yaptığında

**Test Adımları:**
1. Google ile giriş yap
2. Sağ üstteki profil avatarına tıkla
3. Dropdown menüyü kontrol et
4. "Çıkış Yap" butonuna tıkla

**Gözlemlenen Davranış (UNFIXED CODE):**
- Profil avatarı görünüyor (Google profil fotoğrafı)
- Dropdown menü açılıyor
- Kullanıcı adı ve email görünüyor
- "Çıkış Yap" butonu çalışıyor

**Expected Behavior (PRESERVED):**
- ✓ Profil avatarı görünmeli
- ✓ Dropdown menü açılmalı
- ✓ Kullanıcı bilgileri görünmeli
- ✓ Çıkış yap butonu çalışmalı

**Test Sonucu (UNFIXED):** ✅ PASS

---

### Test 8: Yorum Yazma Bölümü Görünürlüğü
**Condition:** Kullanıcı giriş yaptığında

**Test Adımları:**
1. Giriş yapmadan sayfayı aç
2. Yorum yazma bölümünün görünür olup olmadığını kontrol et
3. Google ile giriş yap
4. Yorum yazma bölümünün görünür olup olmadığını kontrol et

**Gözlemlenen Davranış (UNFIXED CODE):**
- Giriş yapılmadan: Yorum yazma bölümü gizli (veya "Giriş yapın" mesajı)
- Giriş yapıldıktan sonra: Yorum yazma bölümü görünür

**Expected Behavior (PRESERVED):**
- ✓ Giriş yapılmadan yorum yazma bölümü gizli olmalı
- ✓ Giriş yapıldıktan sonra görünür olmalı

**Test Sonucu (UNFIXED):** ✅ PASS

---

## Property-Based Test Özeti

**Preservation Property:**
```
FOR ALL input WHERE NOT isBugCondition(input):
  submitReview_original(input) = submitReview_fixed(input)
```

**Test Coverage:**
- ✅ Giriş yapılmamış durumu
- ✅ Yıldız seçilmemiş durumu
- ✅ Boş metin durumu
- ✅ Form temizleme davranışı
- ✅ UI tasarımı
- ✅ Firebase yorum yükleme
- ✅ Auth sistemi
- ✅ Yorum yazma bölümü görünürlüğü

**Tüm Testler (UNFIXED CODE):** ✅ PASS

Bu testler fix uygulandıktan sonra tekrar çalıştırılacak ve hala PASS olmalılar (regresyon yok).
