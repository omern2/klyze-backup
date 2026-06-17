using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    /// <summary>
    /// Henrik Dev Valorant API entegrasyonu.
    /// Docs: https://docs.henrikdev.xyz/valorant
    /// Account: GET /valorant/v1/account/{name}/{tag}
    /// MMR:     GET /valorant/v2/mmr/{region}/{name}/{tag}
    /// </summary>
    public class HenrikApiService : IDisposable
    {
        private const string BaseUrl = "https://api.henrikdev.xyz";

        private readonly HttpClient _http;

        public HenrikApiService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            if (!string.IsNullOrEmpty(ApiKeyProvider.HenrikDevKey))
                _http.DefaultRequestHeaders.Add("Authorization", ApiKeyProvider.HenrikDevKey);
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ─── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Oyuncunun hesap bilgilerini çeker (bölge, seviye, kart).
        /// Endpoint: /valorant/v1/account/{name}/{tag}
        /// </summary>
        public async Task<HenrikAccountData> GetAccountAsync(
            string name, string tag, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/valorant/v1/account/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}";
            var json = await GetJsonAsync(url, ct);
            var root = JObject.Parse(json);

            CheckStatus(root, url);

            var data = root["data"];
            return new HenrikAccountData
            {
                Puuid        = data?["puuid"]?.ToString() ?? "",
                Name         = data?["name"]?.ToString() ?? name,
                Tag          = data?["tag"]?.ToString() ?? tag,
                Region       = data?["region"]?.ToString() ?? "eu",
                AccountLevel = data?["account_level"]?.Value<int>() ?? 0,
                CardSmallUrl = data?["card"]?["small"]?.ToString() ?? ""
            };
        }

        /// <summary>
        /// Oyuncunun MMR / rütbe bilgilerini çeker.
        /// Endpoint: /valorant/v2/mmr/{region}/{name}/{tag}
        /// </summary>
        public async Task<HenrikMmrData> GetMmrAsync(
            string region, string name, string tag, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/valorant/v2/mmr/{region}/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}";
            var json = await GetJsonAsync(url, ct);
            var root = JObject.Parse(json);

            CheckStatus(root, url);

            var data    = root["data"];
            var current = data?["current_data"];

            return new HenrikMmrData
            {
                CurrentTier         = current?["currenttier"]?.Value<int>() ?? 0,
                CurrentTierPatched  = current?["currenttierpatched"]?.ToString() ?? "",
                RankingInTier       = current?["ranking_in_tier"]?.Value<int>() ?? 0,
                MmrChangeToLastGame = current?["mmr_change_to_last_game"]?.Value<int>() ?? 0,
                Elo                 = current?["elo"]?.Value<int>() ?? 0,
                HighestRankPatched  = data?["highest_rank"]?["patched_tier"]?.ToString() ?? ""
            };
        }

        /// <summary>
        /// Hesap + MMR birleştirip UserProfile döndürür.
        /// Bölgeyi account endpoint'ten alır, sonra MMR çeker.
        /// </summary>
        public async Task<UserProfile> GetFullProfileAsync(
            string name, string tag, CancellationToken ct = default)
        {
            // 1. Hesap bilgisi (bölge için)
            var account = await GetAccountAsync(name, tag, ct);

            // 2. MMR bilgisi
            HenrikMmrData mmr = null;
            try
            {
                mmr = await GetMmrAsync(account.Region, name, tag, ct);
            }
            catch
            {
                // MMR alınamazsa profil yine de oluşturulur
            }

            return new UserProfile
            {
                OyuncuAdi          = account.Name,
                Tag                = account.Tag,
                Bolge              = account.Region,
                HesapSeviyesi      = account.AccountLevel,
                CardSmallUrl       = account.CardSmallUrl,
                Rutbe              = mmr?.CurrentTierPatched ?? "",
                RutbePuani         = mmr?.RankingInTier ?? 0,
                CurrentTier        = mmr?.CurrentTier ?? 0,
                KazanmaOrani       = 0,   // Henrik v2 MMR'da winrate yok, ayrı endpoint gerekir
                EnCokOynadigiAjan  = "",  // Ayrı endpoint gerekir
                KdOrani            = 0,
                Acs                = 0,
                SonGuncelleme      = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        // ─── HTTP ────────────────────────────────────────────────────────────────

        private async Task<string> GetJsonAsync(string url, CancellationToken ct)
        {
            if (!_http.DefaultRequestHeaders.Contains("Authorization") && !string.IsNullOrEmpty(ApiKeyProvider.HenrikDevKey))
                _http.DefaultRequestHeaders.Add("Authorization", ApiKeyProvider.HenrikDevKey);

            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(url, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new HenrikApiException("İstek zaman aşımına uğradı. Lütfen tekrar deneyin.");
            }
            catch (HttpRequestException ex)
            {
                throw new HenrikApiException($"Ağ hatası: {ex.Message}");
            }

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                HandleHttpError(response.StatusCode, body);

            return body;
        }

        private static void CheckStatus(JObject root, string url)
        {
            var status = root["status"]?.Value<int>() ?? 200;
            if (status == 200) return;

            var msg = root["errors"]?[0]?["message"]?.ToString()
                   ?? root["message"]?.ToString()
                   ?? $"API hatası ({status})";

            switch (status)
            {
                case 404: throw new HenrikApiException("Hesap bulunamadı. Kullanıcı adı ve tag'i kontrol edin.");
                case 429: throw new HenrikApiException("Çok fazla istek gönderildi. Lütfen bekleyin.");
                case 401:
                case 403: throw new HenrikApiException("API anahtarı geçersiz.");
                default:  throw new HenrikApiException($"API hatası ({status}): {msg}");
            }
        }

        private static void HandleHttpError(System.Net.HttpStatusCode code, string body)
        {
            switch ((int)code)
            {
                case 404: throw new HenrikApiException("Hesap bulunamadı. Kullanıcı adı ve tag'i kontrol edin.");
                case 429: throw new HenrikApiException("Çok fazla istek gönderildi. Lütfen bekleyin.");
                case 401:
                case 403: throw new HenrikApiException("API anahtarı geçersiz veya yetkisiz.");
                case >= 500: throw new HenrikApiException("Sunucu hatası. Lütfen tekrar deneyin.");
                default:  throw new HenrikApiException($"Bağlantı hatası ({(int)code}).");
            }
        }

        public void Dispose() => _http?.Dispose();
    }

    // ─── Response Modelleri ──────────────────────────────────────────────────────

    public class HenrikAccountData
    {
        public string Puuid        { get; set; }
        public string Name         { get; set; }
        public string Tag          { get; set; }
        public string Region       { get; set; }
        public int    AccountLevel { get; set; }
        public string CardSmallUrl { get; set; }
    }

    public class HenrikMmrData
    {
        public int    CurrentTier            { get; set; }
        public string CurrentTierPatched     { get; set; }
        public int    RankingInTier          { get; set; }
        public int    MmrChangeToLastGame    { get; set; }
        public int    Elo                    { get; set; }
        public string HighestRankPatched     { get; set; }
    }

    public class HenrikApiException : Exception
    {
        public HenrikApiException(string message) : base(message) { }
    }
}
