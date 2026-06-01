# 🔐 Google Giriş Sistemi Kurulum

## ✅ Oluşturulan Dosyalar:
- `profile.html` - Profil sayfası (Google giriş)
- Ana sayfaya "Profil" linki eklendi

## 🚀 Firebase Authentication Kurulumu:

### 1️⃣ Firebase Console'da Authentication Aktif Et

1. https://console.firebase.google.com/ git
2. Projenizi seçin: **klyzegg**
3. Sol menüden **"Build"** > **"Authentication"** tıkla
4. **"Get started"** tıkla
5. **"Sign-in method"** sekmesine git
6. **"Google"** seç
7. **"Enable"** (Etkinleştir) switch'ini AÇ
8. **"Save"** tıkla

### 2️⃣ Firebase Config Bilgilerini Al

1. Firebase Console'da sol üstteki **⚙️ (Ayarlar)** > **"Project settings"** tıkla
2. Aşağı kaydır, **"Your apps"** bölümünde **"</>"** (Web) ikonu tıkla
3. App nickname: `klyze-web` yaz
4. **"Register app"** tıkla
5. Çıkan `firebaseConfig` kodunu KOPYALA

Şöyle bir şey göreceksin:
```javascript
const firebaseConfig = {
  apiKey: "AIzaSyXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
  authDomain: "klyzegg.firebaseapp.com",
  databaseURL: "https://klyzegg-default-rtdb.firebaseio.com",
  projectId: "klyzegg",
  storageBucket: "klyzegg.appspot.com",
  messagingSenderId: "123456789012",
  appId: "1:123456789012:web:abcdef123456789"
};
```

### 3️⃣ Config'i profile.html'e Yapıştır

`profile.html` dosyasını aç, **yaklaşık 150. satırı** bul:

```javascript
const firebaseConfig = {
  apiKey: "BURAYA-API-KEY-GELECEK",
  ...
};
```

Firebase Console'dan kopyaladığın config ile DEĞİŞTİR.

### 4️⃣ Test Et

1. `profile.html` dosyasını tarayıcıda aç
2. **"Google ile Giriş Yap"** tıkla
3. Google hesabını seç
4. Profil bilgilerin görünecek!

## 🌐 Netlify'a Yükleme

Netlify'a yüklerken `profile.html` dosyasını da ekle:

✅ Yüklenecek dosyalar:
- `klyze.html`
- `profile.html` ← YENİ
- `klyzeee.png`
- `klyzesetup-3.2.1 (1).exe`
- `gizlilik.html`
- `kullanim-sartlari.html`
- `iletisim.html`
- `hakkimizda.html`
- `netlify.toml`

## 🎯 Özellikler:

✅ Google ile tek tıkla giriş
✅ Profil fotoğrafı gösterimi
✅ Kullanıcı adı ve email
✅ Çıkış yapma
✅ Otomatik oturum yönetimi
✅ Responsive tasarım

## 🔒 Güvenlik:

- Firebase Authentication otomatik güvenlik sağlar
- Token'lar otomatik yönetilir
- HTTPS zorunlu (Netlify otomatik sağlar)

## 📝 Sonraki Adımlar:

1. Firebase Console'da Authentication'ı aktif et
2. Config bilgilerini `profile.html`'e yapıştır
3. Test et
4. Netlify'a yükle

## ❓ Sorun Giderme:

### "Firebase config hatası"
- Config bilgilerini doğru kopyaladığınızdan emin olun
- Tırnak işaretlerini kontrol edin

### "Google giriş çalışmıyor"
- Firebase Console'da Google provider'ın aktif olduğunu kontrol edin
- Tarayıcı popup'larına izin verin

### "Localhost'ta çalışmıyor"
- Firebase otomatik localhost'u destekler
- Eğer sorun varsa Firebase Console > Authentication > Settings > Authorized domains kontrol edin

## 🎉 Tamamlandı!

Artık kullanıcılar Google hesaplarıyla giriş yapabilir!
