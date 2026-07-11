using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValorantAutoClicker.Helpers;
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
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Klyze", "data");
            Directory.CreateDirectory(dir);
            _userJsonPath = Path.Combine(dir, "user.json");
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
                _cachedProfile = SafeJson.Deserialize<UserProfile>(json);
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

        private static readonly HttpClient _riotHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        /// <summary>
        /// Profili Henrik Dev API'den günceller, başarısız olursa resmi Riot API'ye düşer.
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
                // HenrikDev başarısız → resmi Riot API'ye düş
                if (ex.Message.Contains("bulunamadı") && !string.IsNullOrEmpty(ApiKeyProvider.RiotApiKey))
                {
                    try
                    {
                        return await RiotApiFallbackAsync(oyuncuAdi, tag);
                    }
                    catch { }
                }
                return (false, ex.Message, null);
            }
            catch (Exception ex)
            {
                return (false, $"Bağlantı hatası: {ex.Message}", null);
            }
        }

        private async Task<(bool basarili, string hata, UserProfile profil)> RiotApiFallbackAsync(
            string name, string tag)
        {
            var url = $"https://europe.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("X-Riot-Token", ApiKeyProvider.RiotApiKey);

            var resp = await _riotHttp.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new Exception("Hesap bulunamadı. Kullanıcı adı ve tag'i kontrol edin.");
                if ((int)resp.StatusCode == 429)
                    throw new Exception("Çok fazla istek. Lütfen bekleyin.");
                throw new Exception($"API hatası: {(int)resp.StatusCode}");
            }

            var json = await resp.Content.ReadAsStringAsync();
            var data = JObject.Parse(json);

            var profil = new UserProfile
            {
                OyuncuAdi = data["gameName"]?.ToString() ?? name,
                Tag = data["tagLine"]?.ToString() ?? tag,
                Bolge = "eu",
                SonGuncelleme = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            SaveProfile(profil);
            return (true, null, profil);
        }
    }
}
