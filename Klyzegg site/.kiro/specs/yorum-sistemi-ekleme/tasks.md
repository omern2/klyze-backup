# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Yorumlar Firebase'e Kaydedilmiyor
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: For deterministic bugs, scope the property to the concrete failing case(s) to ensure reproducibility
  - Test that when a logged-in user submits a review (with stars selected and text entered), the review is saved to Firebase Realtime Database `reviews` node
  - The test assertions should match the Expected Behavior Properties from design: review saved to Firebase, toast message shown, form cleared
  - Run test on UNFIXED code (with old submitReview function at line 2114)
  - **EXPECTED OUTCOME**: Test FAILS (this is correct - it proves the bug exists)
  - Document counterexamples found: review not saved to Firebase, only added to local state
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Validasyon ve UI Davranışları Korunmalı
  - **IMPORTANT**: Follow observation-first methodology
  - Observe behavior on UNFIXED code for non-buggy inputs (not logged in, no stars selected, empty text)
  - Write property-based tests capturing observed behavior patterns from Preservation Requirements:
    - Test 1: Not logged in → "Yorum yapmak için giriş yapmalısınız!" warning shown
    - Test 2: No stars selected → "Lütfen yıldız verin!" warning shown
    - Test 3: Empty text → "Lütfen yorum yazın!" warning shown
    - Test 4: Form clearing after successful submission
    - Test 5: Review card UI design (avatar, name, date, stars, text, like button)
    - Test 6: Reviews loaded from Firebase in date order (newest first)
  - Property-based testing generates many test cases for stronger guarantees
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

- [x] 3. Fix for yorum sistemi Firebase entegrasyonu

  - [x] 3.1 Eski submitReview fonksiyonunu kaldır
    - Satır 2114-2133 arasındaki eski `function submitReview()` fonksiyonunu tamamen sil
    - Bu fonksiyon sadece local state'e ekliyor, Firebase'e kaydetmiyor
    - _Bug_Condition: isBugCondition(input) where input.userLoggedIn = true AND input.starsSelected > 0 AND input.reviewText.length > 0 AND input.submitButtonClicked = true AND calledFunction = "submitReview()" (satır 2114)_
    - _Expected_Behavior: Yorum Firebase Realtime Database'deki `reviews` node'una `push()` ile kaydedilmeli, "Yorumunuz eklendi! 🎉" mesajı gösterilmeli, form temizlenmeli_
    - _Preservation: Validasyon mantığı (giriş kontrolü, yıldız kontrolü, boş metin kontrolü), UI tasarımı, Firebase'den yorum yükleme işlevselliği korunmalı_
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

  - [x] 3.2 HTML event handler'ını güncelle
    - Satır 1634'teki yorum gönder butonlarının `onclick="submitReview()"` attribute'ünü `onclick="window.submitReview()"` olarak değiştir
    - Bu, yeni Firebase entegrasyonlu fonksiyonu çağıracak
    - İki adet duplicate buton var, ikisini de güncelle
    - _Bug_Condition: HTML event handler eski fonksiyonu çağırıyor_
    - _Expected_Behavior: HTML event handler yeni Firebase fonksiyonunu çağırmalı_
    - _Preservation: Buton tasarımı ve diğer event handler'lar korunmalı_
    - _Requirements: 2.5, 2.6_

  - [x] 3.3 Validasyon mesajlarını güncelle (opsiyonel iyileştirme)
    - Yeni `window.submitReview` fonksiyonundaki `alert()` çağrılarını mevcut `showToast()` fonksiyonu ile değiştir
    - `alert('Yorum yazmak için giriş yapmalısınız!')` → `showToast('Yorum yapmak için giriş yapmalısınız!')`
    - `alert('Lütfen yıldız verin!')` → `showToast('Lütfen yıldız verin!')`
    - `alert('Lütfen yorum yazın!')` → `showToast('Lütfen yorum yazın!')`
    - `alert('Yorumunuz eklendi! 🎉')` → `showToast('Yorumunuz eklendi! 🎉')`
    - `alert('Hata: ' + error.message)` → `showToast('Hata: ' + error.message)`
    - _Preservation: Mesaj gösterme mekanizması tutarlı olmalı_
    - _Requirements: 2.6, 3.1, 3.2, 3.3_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Yorumlar Firebase'e Kaydedilir
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - Validasyon ve UI Davranışları Korundu
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
  - Verify the fix works end-to-end: login → write review → submit → refresh page → review still visible
  - Verify Firebase Console shows the new review in `reviews` node
  - Verify multi-user scenario: one user submits review → another user sees it
