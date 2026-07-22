using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    public class ProfileDataService
    {
        private readonly string _baseUrl = "https://wshbwkgujaspnflnwnwx.supabase.co";
        private readonly string _anonKey = "sb_publishable_1_eY31wnWDkYY6DQ6masNw_IQZpm5Gi";
        private readonly HttpClient _http;
        private static readonly HttpClient _henrikHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private const string HenrikBase = "https://api.henrikdev.xyz";

        public ProfileDataService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        private string AccessToken()
        {
            return App.Supabase?.CurrentIdToken ?? "";
        }

        private HttpRequestMessage MakeRequest(HttpMethod method, string path, string body = null)
        {
            var req = new HttpRequestMessage(method, $"{_baseUrl}{path}");
            req.Headers.TryAddWithoutValidation("apikey", _anonKey);
            var token = AccessToken();
            if (!string.IsNullOrEmpty(token))
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
            if (body != null)
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return req;
        }

        public async Task<List<ProfileData>> GetAllAsync()
        {
            try
            {
                using var req = MakeRequest(HttpMethod.Get, "/rest/v1/active_users?select=*&order=last_seen.desc");
                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return new List<ProfileData>();
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null" || json == "[]") return new List<ProfileData>();
                var arr = JArray.Parse(json);
                var simdi = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var result = new List<ProfileData>();
                foreach (var item in arr)
                {
                    var o = (JObject)item;
                    var lastSeenStr = o["last_seen"]?.ToString();
                    long lastSeen = 0;
                    if (!string.IsNullOrEmpty(lastSeenStr) && DateTimeOffset.TryParse(lastSeenStr, out var dt))
                        lastSeen = dt.ToUnixTimeSeconds();
                    if (simdi - lastSeen > 86400) continue;

                    result.Add(new ProfileData
                    {
                        Uid = o["uid"]?.ToString(),
                        Name = o["oyuncu_adi"]?.ToString() ?? "",
                        Tag = o["tag"]?.ToString() ?? "",
                        Elo = o["elo"]?.ToObject<int>() ?? 0,
                        CurrentTier = o["current_tier"]?.ToObject<int>() ?? 0,
                        CardSmallUrl = o["card_small_url"]?.ToString() ?? "",
                        Rank = o["rutbe"]?.ToString() ?? "",
                        LastSeenAt = lastSeen
                    });
                }

                // app_config'ten card_small_url/current_tier/rutbe oku (cache)
                var uids = result.Where(p => !string.IsNullOrEmpty(p.Uid)).Select(p => p.Uid).Distinct().ToList();
                if (uids.Count > 0)
                {
                    try
                    {
                        using var cfgReq = MakeRequest(HttpMethod.Get, "/rest/v1/app_config?select=key,value");
                        var cfgResp = await _http.SendAsync(cfgReq);
                        if (cfgResp.IsSuccessStatusCode)
                        {
                            var cfgJson = await cfgResp.Content.ReadAsStringAsync();
                            if (!string.IsNullOrEmpty(cfgJson) && cfgJson != "null" && cfgJson != "[]")
                            {
                                var cfgArr = JArray.Parse(cfgJson);
                                var cardMap = new Dictionary<string, string>();
                                var tierMap = new Dictionary<string, int>();
                                var rankMap = new Dictionary<string, string>();
                                foreach (var c in cfgArr.OfType<JObject>())
                                {
                                    var key = c["key"]?.ToString() ?? "";
                                    var val = c["value"]?.ToString() ?? "";
                                    if (key.StartsWith("card_")) cardMap[key.Substring(5)] = val;
                                    else if (key.StartsWith("tier_") && int.TryParse(val, out var t)) tierMap[key.Substring(5)] = t;
                                    else if (key.StartsWith("rank_")) rankMap[key.Substring(5)] = val;
                                }
                                foreach (var e in result)
                                {
                                    if (string.IsNullOrEmpty(e.Uid)) continue;
                                    if (string.IsNullOrEmpty(e.CardSmallUrl) && cardMap.TryGetValue(e.Uid, out var url)) e.CardSmallUrl = url;
                                    if (e.CurrentTier == 0 && tierMap.TryGetValue(e.Uid, out var t)) e.CurrentTier = t;
                                    if (string.IsNullOrEmpty(e.Rank) && rankMap.TryGetValue(e.Uid, out var r)) e.Rank = r;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // Hâlâ eksik olan card URL'leri Henrik Dev API'den paralel çek ve app_config'e kaydet
                var eksikler = result.Where(e => string.IsNullOrEmpty(e.CardSmallUrl) 
                    && !string.IsNullOrEmpty(e.Name) && !string.IsNullOrEmpty(e.Tag)).ToList();
                if (eksikler.Count > 0)
                {
                    var saveTasks = eksikler.Select(async e =>
                    {
                        try
                        {
                            var cardUrl = await FetchCardFromHenrikAsync(e.Name, e.Tag);
                            if (!string.IsNullOrEmpty(cardUrl))
                            {
                                e.CardSmallUrl = cardUrl;
                                await SaveCardToAppConfigAsync(e.Uid, cardUrl, e.CurrentTier, e.Rank);
                            }
                        }
                        catch { }
                    });
                    await Task.WhenAll(saveTasks);
                }

                result = result
                    .GroupBy(p => new { p.Name, p.Tag })
                    .Select(g => g.OrderByDescending(p => p.Elo).First())
                    .ToList();

                return result;
            }
            catch { return new List<ProfileData>(); }
        }

        private async Task SaveCardToAppConfigAsync(string uid, string cardUrl, int tier, string rank)
        {
            try
            {
                var metaList = new JArray();
                metaList.Add(new JObject { ["key"] = $"card_{uid}", ["value"] = cardUrl ?? "" });
                if (tier > 0)
                    metaList.Add(new JObject { ["key"] = $"tier_{uid}", ["value"] = tier.ToString() });
                if (!string.IsNullOrEmpty(rank))
                    metaList.Add(new JObject { ["key"] = $"rank_{uid}", ["value"] = rank });
                using var req = MakeRequest(HttpMethod.Post, "/rest/v1/app_config",
                    metaList.ToString(Formatting.None));
                req.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");
                await _http.SendAsync(req);
            }
            catch { }
        }

        private async Task<string> FetchCardFromHenrikAsync(string name, string tag)
        {
            var key = ApiKeyProvider.HenrikDevKey;
            if (string.IsNullOrEmpty(key)) return null;
            var url = $"{HenrikBase}/valorant/v1/account/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Authorization", key);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var resp = await _henrikHttp.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var root = JObject.Parse(json);
            var status = root["status"]?.Value<int>() ?? 0;
            if (status != 200) return null;
            return root["data"]?["card"]?["small"]?.ToString();
        }

        public async Task<HashSet<string>> GetOnlineUidsAsync()
        {
            try
            {
                using var req = MakeRequest(HttpMethod.Get, "/rest/v1/active_users?select=uid,last_seen");
                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return new HashSet<string>();
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null" || json == "[]") return new HashSet<string>();
                var arr = JArray.Parse(json);
                var simdi = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var online = new HashSet<string>();
                foreach (var item in arr)
                {
                    var o = (JObject)item;
                    var lastSeenStr = o["last_seen"]?.ToString();
                    if (string.IsNullOrEmpty(lastSeenStr)) continue;
                    if (DateTimeOffset.TryParse(lastSeenStr, out var dt))
                    {
                        if (simdi - dt.ToUnixTimeSeconds() <= 120)
                            online.Add(o["uid"]?.ToString());
                    }
                }
                return online;
            }
            catch { return new HashSet<string>(); }
        }

        public async Task<ProfileData> GetAsync()
        {
            try
            {
                var uid = ProfilUid();
                if (string.IsNullOrEmpty(uid)) return null;
                using var req = MakeRequest(HttpMethod.Get, $"/rest/v1/profile_data?uid=eq.{uid}&limit=1");
                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null" || json == "[]") return null;
                var arr = JArray.Parse(json);
                if (arr.Count == 0) return null;
                var o = (JObject)arr[0];
                return new ProfileData
                {
                    Uid = o["uid"]?.ToString(),
                    Streak = o["streak"]?.ToObject<int>() ?? 0,
                    ActivityScore = o["activityScore"]?.ToObject<int>() ?? 0,
                    Level = o["level"]?.ToObject<int>() ?? 1,
                    LevelXp = o["levelXp"]?.ToObject<int>() ?? 0,
                    LevelProgress = o["levelProgress"]?.ToObject<double>() ?? 0,
                    LastLoginAt = o["lastLoginAt"]?.ToObject<long>() ?? 0,
                    LastSeenAt = o["lastSeenAt"]?.ToObject<long>() ?? 0,
                    CreatedAt = o["createdAt"]?.ToObject<long>() ?? 0,
                    UpdatedAt = o["updatedAt"]?.ToObject<long>() ?? 0
                };
            }
            catch { return null; }
        }

        public async Task<bool> UpdateLoginAsync()
        {
            try
            {
                var uid = ProfilUid();
                if (string.IsNullOrEmpty(uid)) return false;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var existing = await GetAsync();
                var streak = 1;
                var activityBonus = 1;

                if (existing != null)
                {
                    var lastLogin = existing.LastLoginAt;
                    var lastDt = DateTimeOffset.FromUnixTimeSeconds(lastLogin);
                    var nowDt = DateTimeOffset.UtcNow;

                    if (lastDt.Date == nowDt.Date)
                    {
                        streak = existing.Streak;
                        activityBonus = 1;
                    }
                    else if (lastDt.Date == nowDt.Date.AddDays(-1))
                    {
                        streak = existing.Streak + 1;
                        activityBonus = 2;
                    }
                    else
                    {
                        streak = 1;
                        activityBonus = 2;
                    }

                    var updatePayload = new JObject
                    {
                        ["streak"] = streak,
                        ["lastLoginAt"] = now,
                        ["lastSeenAt"] = now,
                        ["activityScore"] = existing.ActivityScore + activityBonus,
                        ["updatedAt"] = now
                    };
                    if (existing.LevelXp >= 100)
                    {
                        updatePayload["level"] = existing.Level + 1;
                        updatePayload["levelXp"] = 0;
                        updatePayload["levelProgress"] = 0.0;
                    }
                    else
                    {
                        updatePayload["level"] = existing.Level;
                        updatePayload["levelXp"] = existing.LevelXp;
                        updatePayload["levelProgress"] = existing.LevelProgress;
                    }

                    using var req = MakeRequest(HttpMethod.Patch, $"/rest/v1/profile_data?uid=eq.{uid}",
                        updatePayload.ToString(Formatting.None));
                    req.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
                    var resp = await _http.SendAsync(req);
                    return resp.IsSuccessStatusCode;
                }

                var newData = new JObject
                {
                    ["uid"] = uid,
                    ["streak"] = streak,
                    ["activityScore"] = activityBonus,
                    ["lastLoginAt"] = now,
                    ["lastSeenAt"] = now,
                    ["createdAt"] = now,
                    ["updatedAt"] = now,
                    ["level"] = 1,
                    ["levelXp"] = 0,
                    ["levelProgress"] = 0.0
                };
                using var req2 = MakeRequest(HttpMethod.Post, "/rest/v1/profile_data",
                    newData.ToString(Formatting.None));
                req2.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");
                var resp2 = await _http.SendAsync(req2);
                return resp2.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task UpdateProfileDataAsync(string uid, UserProfile profil)
        {
            try
            {
                if (string.IsNullOrEmpty(uid) || profil == null || string.IsNullOrEmpty(profil.OyuncuAdi))
                {
                    uid = ProfilUid();
                    if (string.IsNullOrEmpty(uid)) return;
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var data = new JObject
                {
                    ["uid"] = uid,
                    ["name"] = profil.OyuncuAdi ?? "",
                    ["tag"] = profil.Tag ?? "",
                    ["rank"] = profil.Rutbe ?? "",
                    ["elo"] = profil.Elo,
                    ["currentTier"] = profil.CurrentTier,
                    ["profileImage"] = profil.CardSmallUrl ?? "",
                    ["cardSmallUrl"] = profil.CardSmallUrl ?? "",
                    ["favoriteAgent"] = profil.EnCokOynadigiAjan ?? "",
                    ["favoriteWeapon"] = profil.EnCokKullandigiSilah ?? "",
                    ["puuid"] = profil.Puuid ?? "",
                    ["bolge"] = profil.Bolge ?? "",
                    ["hesapSeviyesi"] = profil.HesapSeviyesi,
                    ["kazanmaOrani"] = profil.KazanmaOrani,
                    ["kdOrani"] = profil.KdOrani,
                    ["acs"] = profil.Acs,
                    ["updatedAt"] = now
                };

                using var req = MakeRequest(HttpMethod.Post, "/rest/v1/profile_data",
                    data.ToString(Formatting.None));
                req.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");
                await _http.SendAsync(req);
            }
            catch { }
        }

        private static string ProfilUid()
        {
            var profil = App.MainVM?.UserService?.GetProfile();
            if (profil != null && !string.IsNullOrEmpty(profil.GoogleUid))
                return profil.GoogleUid;
            return App.Supabase?.LocalId ?? "";
        }

    }

    public class ActiveUserEntry
    {
        public string Name { get; set; }
        public string Tag { get; set; }
        public string Rutbe { get; set; }
        public int Elo { get; set; }
        public int CurrentTier { get; set; }
        public string CardSmallUrl { get; set; }
        public string EnCokOynadigiAjan { get; set; }
        public string EnCokKullandigiSilah { get; set; }
        public double KazanmaOrani { get; set; }
        public double KdOrani { get; set; }
        public double Acs { get; set; }
        public long LastSeen { get; set; }
    }
}
