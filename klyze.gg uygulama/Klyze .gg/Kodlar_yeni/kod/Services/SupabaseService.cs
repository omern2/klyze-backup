using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Helpers;

namespace ValorantAutoClicker.Services
{
    public class SupabaseService : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl = "https://wshbwkgujaspnflnwnwx.supabase.co";
        private readonly string _anonKey = "sb_publishable_1_eY31wnWDkYY6DQ6masNw_IQZpm5Gi";

        private string _accessToken;
        private string _refreshToken;
        private string _userUid;

        public string LocalId => _userUid;
        public string CurrentIdToken => _accessToken ?? "";
        public string CurrentRefreshToken => _refreshToken ?? "";

        private Dictionary<string, string> AuthHeaders(bool withUser = false)
        {
            var h = new Dictionary<string, string>
            {
                ["apikey"] = _anonKey,
                ["Content-Type"] = "application/json"
            };
            if (withUser && !string.IsNullOrEmpty(_accessToken))
                h["Authorization"] = $"Bearer {_accessToken}";
            return h;
        }

        private async Task<T> GetAsync<T>(string path, bool withAuth = false)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}{path}");
            foreach (var kv in AuthHeaders(withAuth))
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return default;
            if (json == "null" || json == "[]") return default;
            return JsonConvert.DeserializeObject<T>(json);
        }

        private async Task<bool> PutAsync(string path, object body, bool withAuth = false)
        {
            var json = JsonConvert.SerializeObject(body);
            using var req = new HttpRequestMessage(HttpMethod.Put, $"{_baseUrl}{path}");
            foreach (var kv in AuthHeaders(withAuth))
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        private async Task<bool> PostAsync(string path, object body, bool withAuth = false)
        {
            var json = JsonConvert.SerializeObject(body);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{path}");
            foreach (var kv in AuthHeaders(withAuth))
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        private async Task<string> PostWithResultAsync(string path, object body, bool withAuth = false)
        {
            var responseBody = await SendPostWithResultAsync(path, body, withAuth);
            if (responseBody != null && responseBody.Contains("JWT expired"))
            {
                LoggingService.Info("Supabase", "JWT expired — attempting token refresh");
                if (await RefreshTokenAsync())
                {
                    responseBody = await SendPostWithResultAsync(path, body, withAuth);
                }
            }
            return responseBody;
        }

        private async Task<string> SendPostWithResultAsync(string path, object body, bool withAuth = false)
        {
            var json = JsonConvert.SerializeObject(body);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{path}");
            foreach (var kv in AuthHeaders(withAuth))
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            req.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            var responseBody = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                LoggingService.Warning("Supabase", $"PostWithResultAsync {path} returned {resp.StatusCode}: {responseBody}");
            return responseBody;
        }

        private async Task<bool> PatchAsync(string path, object body, bool withAuth = false)
        {
            var json = JsonConvert.SerializeObject(body);
            using var req = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}{path}");
            foreach (var kv in AuthHeaders(withAuth))
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        private async Task<bool> DeleteAsync(string path, bool withAuth = false)
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"{_baseUrl}{path}");
            foreach (var kv in AuthHeaders(withAuth))
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public SupabaseService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        // â”€â”€â”€ Auth â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public void SetAuthState(string accessToken, string uid, string refreshToken = null)
        {
            _accessToken = accessToken;
            _userUid = uid;
            _refreshToken = refreshToken;
            if (!string.IsNullOrEmpty(_accessToken))
            {
                try
                {
                    var parts = _accessToken.Split('.');
                    if (parts.Length >= 2)
                    {
                        var p = parts[1].Replace('-', '+').Replace('_', '/');
                        switch (p.Length % 4) { case 2: p += "=="; break; case 3: p += "="; break; }
                        var bytes = Convert.FromBase64String(p);
                        var payload = Encoding.UTF8.GetString(bytes);
                        var j = JObject.Parse(payload);
                        _userUid = j["sub"]?.ToString() ?? _userUid;
                    }
                }
                catch { }
            }
        }

        public async Task<bool> RefreshTokenAsync()
        {
            if (string.IsNullOrEmpty(_refreshToken))
            {
                LoggingService.Warning("Supabase", "RefreshTokenAsync: no refresh token available");
                return false;
            }
            try
            {
                var body = new { refresh_token = _refreshToken };
                var json = await PostWithResultAsync("/auth/v1/token?grant_type=refresh_token", body);
                var data = JObject.Parse(json);
                if (data["access_token"] == null)
                {
                    LoggingService.Warning("Supabase", $"RefreshTokenAsync failed: {json}");
                    return false;
                }
                _accessToken = data["access_token"]?.ToString();
                _refreshToken = data["refresh_token"]?.ToString() ?? _refreshToken;
                _userUid = data["user"]?["id"]?.ToString() ?? _userUid;
                LoggingService.Info("Supabase", "Token refreshed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Supabase", "RefreshTokenAsync error", ex);
                return false;
            }
        }

        public void ClearAuth()
        {
            _accessToken = null;
            _userUid = null;
            _refreshToken = null;
        }

        /// <summary>
        /// Google ID token'Ä± Supabase session'a Ã§evirir.
        /// POST {baseUrl}/auth/v1/token?grant_type=id_token
        /// </summary>
        public async Task<SupabaseSession> SignInWithGoogleAsync(string googleIdToken)
        {
            try
            {
                var body = new { id_token = googleIdToken, provider = "google" };
                var json = await PostWithResultAsync("/auth/v1/token?grant_type=id_token", body);
                var data = JObject.Parse(json);
                if (data["access_token"] == null)
                {
                    LoggingService.Warning("Supabase", $"SignInWithGoogle failed: {json}");
                    return null;
                }
                var session = new SupabaseSession
                {
                    AccessToken = data["access_token"]?.ToString(),
                    RefreshToken = data["refresh_token"]?.ToString(),
                    ExpiresIn = data["expires_in"]?.ToObject<int>() ?? 3600,
                    UserId = data["user"]?["id"]?.ToString() ?? _userUid,
                    Email = data["user"]?["email"]?.ToString()
                };
                SetAuthState(session.AccessToken, session.UserId, session.RefreshToken);
                LoggingService.Info("Supabase", $"Google sign-in success: uid={session.UserId}");
                return session;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Supabase", "SignInWithGoogleAsync error", ex);
                return null;
            }
        }

        // â”€â”€â”€ Profiles â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<string> GetUserProfileAsync(string uid)
        {
            try
            {
                var result = await GetAsync<JArray>($"/rest/v1/profiles?id=eq.{uid}&select=*", withAuth: true);
                if (result == null || result.Count == 0) return null;
                var p = result[0] as JObject;
                if (p == null) return null;
                // Map Supabase columns back to Firebase-compatible JSON keys
                var mapped = new JObject
                {
                    ["oyuncuAdi"] = p["oyuncu_adi"],
                    ["tag"] = p["tag"],
                    ["puuid"] = p["puuid"],
                    ["bolge"] = p["bolge"],
                    ["elo"] = p["elo"],
                    ["currentTier"] = p["current_tier"],
                    ["rutbePuani"] = p["rutbe_puani"],
                    ["rutbe"] = p["rutbe"],
                    ["cardSmallUrl"] = p["card_small_url"],
                    ["hesapSeviyesi"] = p["hesap_seviyesi"],
                    ["kazanmaOrani"] = p["kazanma_orani"],
                    ["enCokOynadigiAjan"] = p["en_cok_oynadigi_ajan"],
                    ["enCokKullandigiSilah"] = p["en_cok_kullandigi_silah"],
                    ["kdOrani"] = p["kd_orani"],
                    ["acs"] = p["acs"],
                    ["email"] = p["email"],
                    ["googleUid"] = p["google_uid"],
                    ["sonGuncelleme"] = p["son_guncelleme"]
                };
                return mapped.ToString(Formatting.None);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Supabase", "GetUserProfileAsync error", ex);
                return null;
            }
        }

        public async Task<bool> SaveUserProfileAsync(string uid, object profile)
        {
            try
            {
                var json = profile is JObject j ? j : JObject.FromObject(profile);
                var body = new JObject
                {
                    ["id"] = uid,
                    ["oyuncu_adi"] = json["oyuncuAdi"] ?? json["oyuncu_adi"] ?? "",
                    ["tag"] = json["tag"] ?? "",
                    ["puuid"] = json["puuid"] ?? "",
                    ["bolge"] = json["bolge"] ?? "eu",
                    ["elo"] = json["elo"]?.ToObject<int>() ?? 0,
                    ["current_tier"] = json["currentTier"]?.ToObject<int>() ?? 0,
                    ["rutbe_puani"] = json["rutbePuani"]?.ToObject<int>() ?? 0,
                    ["rutbe"] = json["rutbe"] ?? "",
                    ["card_small_url"] = json["cardSmallUrl"]?.ToString() ?? "",
                    ["hesap_seviyesi"] = json["hesapSeviyesi"]?.ToObject<int>() ?? 0,
                    ["kazanma_orani"] = json["kazanmaOrani"]?.ToObject<double>() ?? 0.0,
                    ["en_cok_oynadigi_ajan"] = json["enCokOynadigiAjan"]?.ToString() ?? "",
                    ["en_cok_kullandigi_silah"] = json["enCokKullandigiSilah"]?.ToString() ?? "",
                    ["kd_orani"] = json["kdOrani"]?.ToObject<double>() ?? 0.0,
                    ["acs"] = json["acs"]?.ToObject<double>() ?? 0.0,
                    ["email"] = json["email"] ?? "",
                    ["google_uid"] = json["googleUid"] ?? uid,
                    ["son_guncelleme"] = json["sonGuncelleme"]?.ToObject<long>() ?? 0
                };
                // Upsert: POST with Prefer: resolution=merge-duplicates
                var bodyStr = body.ToString(Formatting.None);
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/rest/v1/profiles");
                foreach (var kv in AuthHeaders(withUser: true))
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                req.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");
                req.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Supabase", "SaveUserProfileAsync error", ex);
                return false;
            }
        }

        // â”€â”€â”€ Bans â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static long ParseTimestampLong(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return 0;
            if (token.Type == JTokenType.Integer) return token.ToObject<long>();
            var str = token.ToString();
            if (string.IsNullOrEmpty(str)) return 0;
            if (long.TryParse(str, out var l)) return l;
            if (DateTimeOffset.TryParse(str, out var dto)) return dto.ToUnixTimeSeconds();
            return 0;
        }

        public async Task<Dictionary<string, BanModel>> GetAllBansAsync()
        {
            try
            {
                var result = await GetAsync<JArray>("/rest/v1/bans?select=*&aktif=eq.true");
                if (result == null) return new Dictionary<string, BanModel>();
                var dict = new Dictionary<string, BanModel>();
                foreach (var item in result)
                {
                    var o = item as JObject;
                    if (o == null) continue;
                    var puuid = o["puuid"]?.ToString();
                    if (string.IsNullOrEmpty(puuid)) continue;
                    var aktif = o["aktif"]?.ToObject<bool>() ?? true;
                    var bitis = ParseTimestampLong(o["bitis"]);
                    dict[puuid] = new BanModel
                    {
                        Puuid = puuid,
                        OyuncuAdi = o["oyuncu_adi"]?.ToString(),
                        Tag = o["tag"]?.ToString(),
                        Sebep = o["sebep"]?.ToString(),
                        Baslangic = ParseTimestampLong(o["baslangic"]),
                        Bitis = bitis,
                        Aktif = aktif,
                        Banli = aktif,
                        Sure = bitis
                    };
                }
                return dict;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Supabase", "GetAllBansAsync error", ex);
                return new Dictionary<string, BanModel>();
            }
        }

        public async Task<BanModel> GetBanAsync(string puuid)
        {
            var result = await GetAsync<JArray>($"/rest/v1/bans?puuid=eq.{puuid}&limit=1");
            if (result == null || result.Count == 0) return null;
            var o = result[0] as JObject;
            if (o == null) return null;
            var aktif = o["aktif"]?.ToObject<bool>() ?? true;
            var bitis = ParseTimestampLong(o["bitis"]);
            return new BanModel
            {
                Puuid = o["puuid"]?.ToString(),
                OyuncuAdi = o["oyuncu_adi"]?.ToString(),
                Tag = o["tag"]?.ToString(),
                Sebep = o["sebep"]?.ToString(),
                Baslangic = ParseTimestampLong(o["baslangic"]),
                Bitis = bitis,
                Aktif = aktif,
                Banli = aktif,
                Sure = bitis
            };
        }

        public async Task<bool> SaveBanAsync(string puuid, BanModel ban)
        {
            try
            {
                var body = new JObject
                {
                    ["puuid"] = puuid,
                    ["oyuncu_adi"] = ban.OyuncuAdi ?? "",
                    ["tag"] = ban.Tag ?? "",
                    ["sebep"] = ban.Sebep ?? "",
                    ["aktif"] = ban.Aktif
                };
                if (ban.Bitis > 0)
                    body["bitis"] = ban.Bitis;

                using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/rest/v1/bans");
                foreach (var kv in AuthHeaders())
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                req.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");
                req.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // â”€â”€â”€ Reports â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<bool> SaveReportAsync(object reportData, string uid = null)
        {
            try
            {
                var json = JsonConvert.SerializeObject(reportData);
                var body = new JObject
                {
                    ["uid"] = uid ?? _userUid ?? "anonymous",
                    ["rapor"] = json
                };
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/rest/v1/reports");
                foreach (var kv in AuthHeaders())
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                req.Headers.TryAddWithoutValidation("Prefer", "return=representation");
                req.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // â”€â”€â”€ Config â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<Dictionary<string, string>> GetConfigAsync()
        {
            try
            {
                var result = await GetAsync<JArray>("/rest/v1/app_config?select=key,value");
                if (result == null) return new Dictionary<string, string>();
                return result.OfType<JObject>().ToDictionary(
                    o => o["key"]?.ToString() ?? "",
                    o => o["value"]?.ToString() ?? "");
            }
            catch { return new Dictionary<string, string>(); }
        }

        // â”€â”€â”€ Notifications â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<AppBildirim> GetBildirimAsync()
        {
            try
            {
                var result = await GetAsync<JArray>("/rest/v1/notifications?select=*&aktif=eq.true&limit=1");
                if (result == null || result.Count == 0) return null;
                var o = result[0] as JObject;
                if (o == null) return null;
                return new AppBildirim
                {
                    Id = o["id"]?.ToString(),
                    Mesaj = o["mesaj"]?.ToString(),
                    Baslik = o["baslik"]?.ToString(),
                    Tip = o["tip"]?.ToString() ?? "info"
                };
            }
            catch { return null; }
        }

        // â”€â”€â”€ Updates â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<AppGuncelleme> GuncellemeKontrolFirestoreAsync()
        {
            try
            {
                var result = await GetAsync<JArray>("/rest/v1/app_updates?select=*&order=id.desc&limit=1");
                if (result == null || result.Count == 0) return null;
                var o = result[0] as JObject;
                if (o == null) return null;
                return new AppGuncelleme
                {
                    Version = o["version"]?.ToString(),
                    DownloadUrl = o["download_url"]?.ToString(),
                    Changelog = o["changelog"]?.ToString(),
                    Zorunlu = o["zorunlu"]?.ToObject<bool>() ?? false
                };
            }
            catch { return null; }
        }

        // â”€â”€â”€ Lobbies â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public event Action<List<FirestoreLobi>> LobilerGuncellendi;
        public event Action<FirestoreLobi> LobbyGuncellendi;

        private CancellationTokenSource _lobbyCts;
        private CancellationTokenSource _singleLobbyCts;

        public async Task<string> CreateLobbyAsync(string gameMode, int maxPlayers, string lobiKodu,
            string olusturanAdi, string olusturanTag, string olusturanPuuid, int olusturanElo, int olusturanTier)
        {
            var body = new JObject
            {
                ["olusturan_uid"] = _userUid ?? "",
                ["lobi_kodu"] = lobiKodu,
                ["oyun_modu"] = gameMode,
                ["max_players"] = maxPlayers,
                ["mevcut_oyuncu"] = 1,
                ["durum"] = "waiting"
            };
            var json = await PostWithResultAsync("/rest/v1/lobbies", body);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var arr = JArray.Parse(json);
                return arr[0]?["id"]?.ToString();
            }
            catch { return null; }
        }

        public async Task<string> LobiOlusturAsync(FirestoreLobi lobi)
        {
            var body = new JObject
            {
                ["olusturan_uid"] = lobi.OlusturanUid ?? _userUid ?? "",
                ["lobi_kodu"] = lobi.LobiKodu,
                ["oyun_modu"] = lobi.OyunModu,
                ["max_players"] = lobi.MaxPlayers,
                ["mevcut_oyuncu"] = lobi.MevcutOyuncu,
                ["durum"] = lobi.Durum ?? "waiting",
                ["host_name"] = lobi.HostName ?? "",
                ["host_tag"] = lobi.HostTag ?? "",
                ["host_elo"] = lobi.HostElo,
                ["host_tier"] = lobi.HostTier,
                ["host_rank"] = lobi.HostRank ?? "",
                ["host_card_url"] = lobi.HostCardUrl ?? "",
                ["group_code"] = lobi.GroupCode ?? "",
                ["region"] = lobi.Region ?? "eu",
                ["status"] = lobi.Status ?? "waiting",
                ["game_mode"] = lobi.GameMode ?? "",
                ["min_rank_tier"] = lobi.MinRankTier,
                ["max_rank_tier"] = lobi.MaxRankTier,
                ["players"] = JToken.FromObject(lobi.Players ?? new List<LobbyPlayer>())
            };
            var json = await PostWithResultAsync("/rest/v1/lobbies", body, withAuth: true);
            if (string.IsNullOrEmpty(json))
            {
                LoggingService.Warning("Supabase", "LobiOlusturAsync: empty response");
                return null;
            }
            try
            {
                var arr = JArray.Parse(json);
                return arr[0]?["id"]?.ToString();
            }
            catch (Exception ex)
            {
                LoggingService.Error("Supabase", $"LobiOlusturAsync parse error — response: {json}", ex);
                return null;
            }
        }

        public async Task<bool> LobiSilAsync(string lobbyId)
        {
            return await DeleteAsync($"/rest/v1/lobbies?id=eq.{lobbyId}", withAuth: true);
        }

        public async Task<List<FirestoreLobi>> GetWaitingLobilerAsync()
        {
            var result = await GetAsync<JArray>("/rest/v1/lobbies?select=*&durum=eq.waiting&order=created_at.desc");
            if (result == null) return new List<FirestoreLobi>();
            return result.OfType<JObject>().Select(o => MapToFirestoreLobi(o)).ToList();
        }

        public async Task<FirestoreLobi> LobiGetirAsync(string lobbyId)
        {
            var result = await GetAsync<JArray>($"/rest/v1/lobbies?id=eq.{lobbyId}&limit=1", withAuth: true);
            if (result == null || result.Count == 0) return null;
            var o = result[0] as JObject;
            if (o == null) return null;
            return MapToFirestoreLobi(o);
        }

        private static FirestoreLobi MapToFirestoreLobi(JObject o)
        {
            return new FirestoreLobi
            {
                Id = o["id"]?.ToString() ?? "",
                OlusturanUid = o["olusturan_uid"]?.ToString() ?? "",
                LobiKodu = o["lobi_kodu"]?.ToString() ?? "",
                OyunModu = o["oyun_modu"]?.ToString() ?? "",
                MaxPlayers = o["max_players"]?.ToObject<int>() ?? 5,
                MevcutOyuncu = o["mevcut_oyuncu"]?.ToObject<int>() ?? 0,
                Durum = o["durum"]?.ToString() ?? "waiting",
                HostName = o["host_name"]?.ToString() ?? "",
                HostTag = o["host_tag"]?.ToString() ?? "",
                HostElo = o["host_elo"]?.ToObject<int>() ?? 0,
                HostTier = o["host_tier"]?.ToObject<int>() ?? 0,
                HostRank = o["host_rank"]?.ToString() ?? "",
                HostCardUrl = o["host_card_url"]?.ToString() ?? "",
                GroupCode = o["group_code"]?.ToString() ?? "",
                Region = o["region"]?.ToString() ?? "eu",
                Status = o["status"]?.ToString() ?? "waiting",
                GameMode = o["game_mode"]?.ToString() ?? "",
                MinRankTier = o["min_rank_tier"]?.ToObject<int>() ?? 0,
                MaxRankTier = o["max_rank_tier"]?.ToObject<int>() ?? 0,
                Players = o["players"] != null ? (o["players"].ToObject<List<LobbyPlayer>>() ?? new List<LobbyPlayer>()) : new List<LobbyPlayer>(),
                CreatedAt = TokenToLong(o["created_at"]),
                ExpiresAt = TokenToLong(o["expires_at"])
            };
        }

        private static long TokenToLong(JToken token)
        {
            if (token == null) return 0;
            if (token.Type == JTokenType.Integer) return token.ToObject<long>();
            if (token.Type == JTokenType.Date)
            {
                var dt = token.ToObject<DateTime>();
                return new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();
            }
            return 0;
        }

        public async Task LobiGuncelleAsync(string lobbyId, object data)
        {
            await PatchAsync($"/rest/v1/lobbies?id=eq.{lobbyId}", data, withAuth: true);
        }

        public async Task CleanExpiredLobbiesAsync()
        {
            // Supabase'de expires_at var, DB tarafÄ±nda temizlenebilir
            await DeleteAsync("/rest/v1/lobbies?expires_at=lt.now()");
        }

        public void StartListening()
        {
            _lobbyCts?.Cancel();
            _lobbyCts = new CancellationTokenSource();
            _ = LobbyPollLoopAsync(_lobbyCts.Token);
        }

        public void StopListening()
        {
            _lobbyCts?.Cancel();
        }

        public void StartLobbyListener(string lobbyId)
        {
            _singleLobbyCts?.Cancel();
            _singleLobbyCts = new CancellationTokenSource();
            _ = SingleLobbyPollLoopAsync(lobbyId, _singleLobbyCts.Token);
        }

        public void StopLobbyListener()
        {
            _singleLobbyCts?.Cancel();
        }

        private async Task LobbyPollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, ct);
                    var lobbies = await GetWaitingLobilerAsync();
                    LobilerGuncellendi?.Invoke(lobbies);
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        private async Task SingleLobbyPollLoopAsync(string lobbyId, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2000, ct);
                    var lobby = await LobiGetirAsync(lobbyId);
                    if (lobby != null)
                        LobbyGuncellendi?.Invoke(lobby);
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        // â”€â”€â”€ Matchmaking Queue â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task QueueEkleAsync(string localId, string name, string tag, int elo, string bolge)
        {
            var body = new JObject
            {
                ["uid"] = localId,
                ["oyuncu_adi"] = name,
                ["tag"] = tag,
                ["elo"] = elo,
                ["bolge"] = bolge ?? "eu"
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/rest/v1/matchmaking_queue");
            foreach (var kv in AuthHeaders(withUser: true))
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            req.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            await _http.SendAsync(req);
        }

        public async Task QueueKaldirAsync(string localId)
        {
            await DeleteAsync($"/rest/v1/matchmaking_queue?uid=eq.{localId}", withAuth: true);
        }

        public async Task<List<QueueOyuncu>> QueueGetirAsync(string excludeLocalId = "")
        {
            var result = await GetAsync<JArray>("/rest/v1/matchmaking_queue?select=*");
            if (result == null) return new List<QueueOyuncu>();
            return result.OfType<JObject>()
                .Where(o => o["uid"]?.ToString() != excludeLocalId)
                .Select(o => new QueueOyuncu
                {
                    Uid = o["uid"]?.ToString(),
                    OyuncuAdi = o["oyuncu_adi"]?.ToString(),
                    Tag = o["tag"]?.ToString(),
                    Elo = o["elo"]?.ToObject<int>() ?? 0,
                    Bolge = o["bolge"]?.ToString() ?? "eu"
                }).ToList();
        }

        // â”€â”€â”€ Rooms â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<string> OdaOlusturAsync(string oyuncu1Uid, string oyuncu2Uid,
            string oyuncu1Adi, string oyuncu2Adi, string oyuncu1Tag, string oyuncu2Tag, string grupKodu)
        {
            var body = new JObject
            {
                ["oyuncu1_uid"] = oyuncu1Uid,
                ["oyuncu2_uid"] = oyuncu2Uid,
                ["oyuncu1_adi"] = oyuncu1Adi,
                ["oyuncu2_adi"] = oyuncu2Adi,
                ["oyuncu1_tag"] = oyuncu1Tag,
                ["oyuncu2_tag"] = oyuncu2Tag,
                ["grup_kodu"] = grupKodu,
                ["durum"] = "bekliyor"
            };
            var json = await PostWithResultAsync("/rest/v1/rooms", body);
            if (string.IsNullOrEmpty(json)) return null;
            try { return JArray.Parse(json)?[0]?["id"]?.ToString(); }
            catch { return null; }
        }

        public async Task OdaGrupKoduGuncelleAsync(string roomId, string groupCode)
        {
            await PatchAsync($"/rest/v1/rooms?id=eq.{roomId}", new { grup_kodu = groupCode }, withAuth: true);
        }

        public async Task OdaSilAsync(string roomId)
        {
            await DeleteAsync($"/rest/v1/rooms?id=eq.{roomId}", withAuth: true);
        }

        public async Task<Oda> OdaGetirAsync(string roomId)
        {
            var result = await GetAsync<JArray>($"/rest/v1/rooms?id=eq.{roomId}&limit=1");
            if (result == null || result.Count == 0) return null;
            var o = result[0] as JObject;
            if (o == null) return null;
            return new Oda
            {
                Id = o["id"]?.ToString(),
                Oyuncu1Uid = o["oyuncu1_uid"]?.ToString(),
                Oyuncu2Uid = o["oyuncu2_uid"]?.ToString(),
                Oyuncu1Adi = o["oyuncu1_adi"]?.ToString(),
                Oyuncu2Adi = o["oyuncu2_adi"]?.ToString(),
                Oyuncu1Tag = o["oyuncu1_tag"]?.ToString(),
                Oyuncu2Tag = o["oyuncu2_tag"]?.ToString(),
                GrupKodu = o["grup_kodu"]?.ToString(),
                Durum = o["durum"]?.ToString() ?? "bekliyor"
            };
        }

        public async Task<Oda> OyuncuAktifOdaGetirAsync(string localId)
        {
            var result = await GetAsync<JArray>($"/rest/v1/rooms?or=(oyuncu1_uid.eq.{localId},oyuncu2_uid.eq.{localId})&durum=neq.closed&limit=1");
            if (result == null || result.Count == 0) return null;
            var o = result[0] as JObject;
            if (o == null) return null;
            return new Oda
            {
                Id = o["id"]?.ToString(),
                Oyuncu1Uid = o["oyuncu1_uid"]?.ToString(),
                Oyuncu2Uid = o["oyuncu2_uid"]?.ToString(),
                Oyuncu1Adi = o["oyuncu1_adi"]?.ToString(),
                Oyuncu2Adi = o["oyuncu2_adi"]?.ToString(),
                Oyuncu1Tag = o["oyuncu1_tag"]?.ToString(),
                Oyuncu2Tag = o["oyuncu2_tag"]?.ToString(),
                GrupKodu = o["grup_kodu"]?.ToString(),
                Durum = o["durum"]?.ToString() ?? "bekliyor"
            };
        }

        // â”€â”€â”€ Active Users / Heartbeat â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private CancellationTokenSource _heartbeatCts;

        public void StartHeartbeat(UserProfile profil)
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts = new CancellationTokenSource();
            _ = HeartbeatLoopAsync(profil, _heartbeatCts.Token);
        }

        public void StopHeartbeat()
        {
            _heartbeatCts?.Cancel();
        }

        private async Task HeartbeatLoopAsync(UserProfile profil, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(30000, ct);
                    var body = new JObject
                    {
                        ["uid"] = _userUid ?? profil.GoogleUid ?? "unknown",
                        ["oyuncu_adi"] = profil.OyuncuAdi ?? "",
                        ["tag"] = profil.Tag ?? "",
                        ["puuid"] = profil.Puuid ?? "",
                        ["elo"] = profil.Elo,
                        ["last_seen"] = DateTime.UtcNow.ToString("o"),
                        ["current_tier"] = profil.CurrentTier,
                        ["card_small_url"] = profil.CardSmallUrl ?? "",
                        ["rutbe"] = profil.Rutbe ?? ""
                    };
                    await UpsertActiveUserInternalAsync(body);
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        public async Task<List<AktifKullanici>> AktifKullanicilariGetirAsync()
        {
            var result = await GetAsync<JArray>("/rest/v1/active_users?select=*&order=last_seen.desc");
            if (result == null) return new List<AktifKullanici>();
            return result.OfType<JObject>().Select(o => new AktifKullanici
            {
                Uid = o["uid"]?.ToString(),
                OyuncuAdi = o["oyuncu_adi"]?.ToString(),
                Tag = o["tag"]?.ToString(),
                Puuid = o["puuid"]?.ToString(),
                Elo = o["elo"]?.ToObject<int>() ?? 0,
                LastSeen = o["last_seen"]?.ToString()
            }).ToList();
        }

        public async Task<bool> KullaniciProfiliGuncelleAsync(string localId, UserProfile profil)
        {
            using var req = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}/rest/v1/profiles?id=eq.{localId}");
            foreach (var kv in AuthHeaders(withUser: true))
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            var body = new JObject { ["son_guncelleme"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            req.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        // â”€â”€â”€ Profile Data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<string> GetProfileDataAsync(string uid)
        {
            var result = await GetAsync<JArray>($"/rest/v1/profile_data?uid=eq.{uid}&limit=1", withAuth: true);
            if (result == null || result.Count == 0) return null;
            var o = result[0] as JObject;
            return o?.ToString(Formatting.None);
        }

        public async Task<bool> UpdateProfileDataAsync(string uid, string email, string dataJson = "{}")
        {
            var body = new JObject
            {
                ["uid"] = uid,
                ["email"] = email ?? "",
                ["data"] = string.IsNullOrEmpty(dataJson) ? "{}" : dataJson,
                ["last_login"] = DateTime.UtcNow.ToString("o")
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/rest/v1/profile_data");
            foreach (var kv in AuthHeaders(withUser: true))
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            req.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        // Google Users

        public async Task<bool> UpsertGoogleUserAsync(GoogleUser user)
        {
            try
            {
                var body = new JObject
                {
                    ["supabase_uid"] = user.SupabaseUid,
                    ["email"] = user.Email ?? "",
                    ["display_name"] = user.DisplayName ?? "",
                    ["picture"] = user.Picture ?? "",
                    ["google_id"] = user.GoogleId ?? "",
                    ["access_token"] = user.AccessToken ?? "",
                    ["refresh_token"] = user.RefreshToken ?? "",
                    ["oyuncu_adi"] = user.OyuncuAdi ?? "",
                    ["tag"] = user.Tag ?? ""
                };
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/rest/v1/google_users");
                foreach (var kv in AuthHeaders(withUser: true))
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                req.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");
                req.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Supabase", "UpsertGoogleUserAsync error", ex);
                return false;
            }
        }

        public async Task<bool> UpdateGoogleUserRiotIdAsync(string supabaseUid, string oyuncuAdi, string tag)
        {
            try
            {
                var body = new JObject
                {
                    ["oyuncu_adi"] = oyuncuAdi ?? "",
                    ["tag"] = tag ?? ""
                };
                return await PatchAsync($"/rest/v1/google_users?supabase_uid=eq.{supabaseUid}", body, withAuth: true);
            }
            catch { return false; }
        }

        public async Task<GoogleUser> GetGoogleUserAsync(string supabaseUid)
        {
            try
            {
                LoggingService.Info("Supabase", $"GetGoogleUserAsync: fetching supabase_uid={supabaseUid}");
                var result = await GetAsync<JArray>($"/rest/v1/google_users?supabase_uid=eq.{supabaseUid}&limit=1", withAuth: true);
                LoggingService.Info("Supabase", $"GetGoogleUserAsync: result={(result == null ? "null" : $"count={result.Count}")}");
                if (result == null || result.Count == 0)
                {
                    LoggingService.Warning("Supabase", "GetGoogleUserAsync: no rows found");
                    return null;
                }
                var o = result[0] as JObject;
                if (o == null) return null;
                return new GoogleUser
                {
                    SupabaseUid = o["supabase_uid"]?.ToString() ?? "",
                    Email = o["email"]?.ToString() ?? "",
                    DisplayName = o["display_name"]?.ToString() ?? "",
                    Picture = o["picture"]?.ToString() ?? "",
                    OyuncuAdi = o["oyuncu_adi"]?.ToString() ?? "",
                    Tag = o["tag"]?.ToString() ?? "",
                    GoogleId = o["google_id"]?.ToString() ?? "",
                    AccessToken = o["access_token"]?.ToString() ?? "",
                    RefreshToken = o["refresh_token"]?.ToString() ?? ""
                };
            }
            catch { return null; }
        }

        public async Task<bool> UpsertActiveUserAsync(JObject body)
        {
            try
            {
                return await UpsertActiveUserInternalAsync(body);
            }
            catch { return false; }
        }

        private async Task<bool> UpsertActiveUserInternalAsync(JObject body, bool withUser = true)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/rest/v1/active_users");
            foreach (var kv in AuthHeaders(withUser: true))
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            req.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        // â”€â”€â”€ Online Status â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task SetOnlineAsync(bool isOnline)
        {
            var body = new JObject
            {
                ["uid"] = _userUid ?? "unknown",
                ["is_online"] = isOnline,
                ["last_seen"] = DateTime.UtcNow.ToString("o")
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/rest/v1/online_status");
            foreach (var kv in AuthHeaders(withUser: true))
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            req.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            await _http.SendAsync(req);
        }

        // â”€â”€â”€ Ping â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<bool> PingAsync()
        {
            try
            {
                var result = await GetAsync<JArray>("/rest/v1/app_updates?limit=1&select=id");
                return result != null;
            }
            catch { return false; }
        }

        // â”€â”€â”€ Cleanup â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public void Dispose()
        {
            _lobbyCts?.Cancel();
            _lobbyCts?.Dispose();
            _singleLobbyCts?.Cancel();
            _singleLobbyCts?.Dispose();
            _heartbeatCts?.Cancel();
            _heartbeatCts?.Dispose();
            _http?.Dispose();
        }
    }

    // â”€â”€â”€ Models â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public class SupabaseSession
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public string UserId { get; set; }
        public string Email { get; set; }
    }

    public class AppGuncelleme
    {
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string Changelog { get; set; }
        public bool Zorunlu { get; set; }
        public string Notes { get; set; }
        public string DosyaUrl { get; set; }
        public string DirectUrl { get; set; }
        public string Date { get; set; }
    }

    public class AppBildirim
    {
        public string Id { get; set; }
        public string Baslik { get; set; }
        public string Mesaj { get; set; }
        public string Tip { get; set; }
        public bool Aktif { get; set; }
    }

    public class LobbyPlayer
    {
        public string Name { get; set; } = "";
        public string Tag { get; set; } = "";
        public int Elo { get; set; }
        public int Tier { get; set; }
        public string Rank { get; set; } = "";
        public string CardUrl { get; set; } = "";
    }

    public class FirestoreLobi
    {
        public string Id { get; set; } = "";
        public string OlusturanUid { get; set; } = "";
        public string LobiKodu { get; set; } = "";
        public string OyunModu { get; set; } = "";
        public int MaxPlayers { get; set; }
        public int MevcutOyuncu { get; set; }
        public string Durum { get; set; } = "";

        public string HostName { get; set; } = "";
        public string HostTag { get; set; } = "";
        public int HostElo { get; set; }
        public int HostTier { get; set; }
        public string HostRank { get; set; } = "";
        public string HostCardUrl { get; set; } = "";
        public string GroupCode { get; set; } = "";
        public string Region { get; set; } = "eu";
        public string Status { get; set; } = "waiting";
        public string GameMode { get; set; } = "";
        public int MinRankTier { get; set; }
        public int MaxRankTier { get; set; }
        public long CreatedAt { get; set; }
        public long ExpiresAt { get; set; }
        public List<LobbyPlayer> Players { get; set; } = new();
    }

    public class QueueOyuncu
    {
        public string Uid { get; set; } = "";
        public string OyuncuAdi { get; set; } = "";
        public string Tag { get; set; } = "";
        public int Elo { get; set; }
        public string Bolge { get; set; } = "";

        public string LocalId { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public string PlayerTag { get; set; } = "";
        public int Tier { get; set; }
        public string Region { get; set; } = "eu";
        public long CreatedAt { get; set; }
    }

    public class Oda
    {
        public string Id { get; set; } = "";
        public string Oyuncu1Uid { get; set; } = "";
        public string Oyuncu2Uid { get; set; } = "";
        public string Oyuncu1Adi { get; set; } = "";
        public string Oyuncu2Adi { get; set; } = "";
        public string Oyuncu1Tag { get; set; } = "";
        public string Oyuncu2Tag { get; set; } = "";
        public string GrupKodu { get; set; } = "";

        public string Player1LocalId { get; set; } = "";
        public string Player1Name { get; set; } = "";
        public string Player1Tag { get; set; } = "";
        public int Player1Elo { get; set; }
        public int Player1Tier { get; set; }
        public string Player1CardUrl { get; set; } = "";
        public string Player2LocalId { get; set; } = "";
        public string Player2Name { get; set; } = "";
        public string Player2Tag { get; set; } = "";
        public int Player2Elo { get; set; }
        public int Player2Tier { get; set; }
        public string Player2CardUrl { get; set; } = "";
        public string Durum { get; set; } = "matched";
        public string Status { get; set; } = "matched";
        public string GroupCode { get; set; } = "";
        public long CreatedAt { get; set; }
    }

    public class AktifKullanici
    {
        public string Uid { get; set; } = "";
        public string OyuncuAdi { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Puuid { get; set; } = "";
        public int Elo { get; set; }
        public string LastSeen { get; set; } = "";
    }
}



