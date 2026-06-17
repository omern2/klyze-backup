using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    /// <summary>
    /// Kullanıcı oturum yönetimi — data/user.json okuma/yazma.
    /// Giriş durumu, profil verisi ve çıkış işlemleri buradan yönetilir.
    /// </summary>
    public class UserService
    {
        private readonly string _userJsonPath;
        private UserProfile _cachedProfile;

        public UserService()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var dataDir = Path.Combine(dir, "data");
            Directory.CreateDirectory(dataDir);
            _userJsonPath = Path.Combine(dataDir, "user.json");
        }

        // ─── Giriş Durumu ────────────────────────────────────────────────────────

        /// <summary>
        /// Kullanıcı daha önce giriş yapmış mı? (user.json var ve geçerli mi)
        /// </summary>
        public bool GirisYapilmisMi()
        {
            var profile = GetProfile();
            return profile?.GecerliMi == true;
        }

        /// <summary>
        /// Mevcut kullanıcı profilini döndürür. Yoksa null.
        /// </summary>
        public UserProfile GetProfile()
        {
            if (_cachedProfile != null) return _cachedProfile;

            try
            {
                if (!File.Exists(_userJsonPath)) return null;
                var json = File.ReadAllText(_userJsonPath);
                _cachedProfile = JsonConvert.DeserializeObject<UserProfile>(json);
                return _cachedProfile?.GecerliMi == true ? _cachedProfile : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Profili kaydeder ve önbelleği günceller.
        /// </summary>
        public void SaveProfile(UserProfile profile)
        {
            try
            {
                var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
                File.WriteAllText(_userJsonPath, json);
                _cachedProfile = profile;
            }
            catch { }
        }

        /// <summary>
        /// Çıkış yap — user.json sil, önbelleği temizle.
        /// </summary>
        public void CikisYap()
        {
            try
            {
                if (File.Exists(_userJsonPath))
                    File.Delete(_userJsonPath);
                _cachedProfile = null;
            }
            catch { }
        }

        /// <summary>
        /// Profili Henrik Dev API'den günceller ve kaydeder.
        /// </summary>
        public async Task<(bool basarili, string hata, UserProfile profil)> GirisVeGuncelleAsync(
            string oyuncuAdi,
            string tag,
            HenrikApiService henrikApi)
        {
            try
            {
                var profil = await henrikApi.GetFullProfileAsync(oyuncuAdi, tag);
                SaveProfile(profil);
                return (true, null, profil);
            }
            catch (HenrikApiException ex)
            {
                return (false, ex.Message, null);
            }
            catch (Exception ex)
            {
                return (false, $"Bağlantı hatası: {ex.Message}", null);
            }
        }
    }
}
