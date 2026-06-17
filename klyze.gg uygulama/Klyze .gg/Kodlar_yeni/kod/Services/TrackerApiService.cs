using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    /// <summary>
    /// Tracker.gg Valorant API entegrasyonu.
    /// Endpoint: https://public-api.tracker.gg/v2/valorant/standard/
    /// </summary>
    public class TrackerApiService : IDisposable
    {
        private readonly HttpClient _http;
        // standard endpoint kullan
        private const string BaseUrl = "https://public-api.tracker.gg/v2/valorant/standard";
        private const int TimeoutSeconds = 15;

        // ─── LRU Önbellek ────────────────────────────────────────────────────────
        private readonly Dictionary<string, (TrackerPlayerProfile Profile, DateTime FetchedAt)> _cache = new();
        private readonly LinkedList<string> _cacheOrder = new();
        private const int MaxCacheSize = 50;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        public TrackerApiService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("Tracker.gg API anahtarı boş olamaz.", nameof(apiKey));

            _http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };
            _http.DefaultRequestHeaders.Add("TRN-Api-Key", apiKey);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ─── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Oyuncu profilini ve istatistiklerini getirir.
        /// Önbellekte geçerli kayıt varsa API çağrısı yapmaz.
        /// </summary>
        public async Task<TrackerPlayerProfile> GetPlayerProfileAsync(
            string gameName,
            string tagLine,
            CancellationToken ct = default)
        {
            var riotId = $"{gameName}#{tagLine}";
            var cacheKey = riotId.ToLowerInvariant();

            if (TryGetFromCache(cacheKey, out var cached))
            {
                cached.FromCache = true;
                return cached;
            }

            // standard endpoint: /standard/profile/riot/{name}%23{tag}
            var encoded = Uri.EscapeDataString(riotId);
            var profileUrl = $"{BaseUrl}/profile/riot/{encoded}";

            var profileResp = await GetAsync<TrackerStandardProfileResponse>(profileUrl, ct);

            if (profileResp?.Data == null)
                throw new TrackerApiException("Oyuncu bulunamadı veya profil gizli.");

            var profile = BuildProfileFromStandard(riotId, gameName, tagLine, profileResp.Data);
            AddToCache(cacheKey, profile);
            return profile;
        }

        /// <summary>
        /// Daha fazla maç yükler (sayfalama) — ileride kullanılmak üzere hazır.
        /// </summary>
        public async Task<List<TrackerMatchSummary>> GetMoreMatchesAsync(
            string gameName,
            string tagLine,
            string nextCursor,
            CancellationToken ct = default)
        {
            // standard endpoint'te maç geçmişi ayrı bir endpoint
            // şimdilik boş liste döndür
            return new List<TrackerMatchSummary>();
        }

        // ─── HTTP Helper ─────────────────────────────────────────────────────────

        private async Task<T> GetAsync<T>(string url, CancellationToken ct)
        {
            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(url, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TrackerApiException("İstek zaman aşımına uğradı (10 saniye). Lütfen tekrar deneyin.");
            }
            catch (HttpRequestException ex)
            {
                throw new TrackerApiException($"Ağ hatası: {ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                HandleHttpError(response.StatusCode, body);
            }

            var json = await response.Content.ReadAsStringAsync();
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (JsonException ex)
            {
                LoggingService.Error("TrackerApiService", $"JSON ayrıştırma hatası: {url}", ex);
                throw new TrackerApiException("Sunucu yanıtı işlenemedi.");
            }
        }

        private static void HandleHttpError(System.Net.HttpStatusCode code, string body)
        {
            switch ((int)code)
            {
                case 404:
                    throw new TrackerApiException("Oyuncu bulunamadı. Kullanıcı adı ve etiketi kontrol edin.");
                case 429:
                    throw new TrackerApiException("İstek limiti aşıldı. Lütfen bekleyin.");
                case 401:
                case 403:
                    throw new TrackerApiException("API anahtarı geçersiz veya yetkisiz.");
                case >= 500:
                    throw new TrackerApiException("Sunucu hatası oluştu. Lütfen tekrar deneyin.");
                default:
                    throw new TrackerApiException($"API hatası ({(int)code}). Lütfen tekrar deneyin.");
            }
        }

        // ─── Veri Dönüşümü — standard endpoint field path'leri ──────────────────

        /// <summary>
        /// standard endpoint yanıtından profil oluşturur.
        /// data.segments[0] = overview (rank, winPct, kd...)
        /// data.segments[1] = en çok oynanan ajan
        /// data.platformInfo.platformUserHandle = oyuncu adı
        /// data.platformInfo.additionalParameters.platformRegion = bölge
        /// </summary>
        private static TrackerPlayerProfile BuildProfileFromStandard(
            string riotId, string gameName, string tagLine,
            TrackerStandardData data)
        {
            var profile = new TrackerPlayerProfile
            {
                RiotId = riotId,
                GameName = data.PlatformInfo?.PlatformUserHandle?.Split('#')[0] ?? gameName,
                TagLine = tagLine,
                AvatarUrl = data.PlatformInfo?.AvatarUrl,
                FetchedAt = DateTime.Now
            };

            // segments[0] = overview
            var overview = data.Segments?.FirstOrDefault(s => s.Type == "overview");
            if (overview?.Stats != null)
            {
                // Rütbe: data.segments[0].stats.rank.metadata.tierName
                var rankStat = overview.Stats.GetValueOrDefault("rank");
                profile.CurrentRankName = rankStat?.Metadata?.TierName
                                       ?? rankStat?.DisplayValue
                                       ?? "";
                profile.CurrentRankIconUrl = rankStat?.Metadata?.IconUrl;

                // RR: data.segments[0].stats.rankScore.value
                profile.CurrentRankRating = (int)(overview.Stats.GetValueOrDefault("rankScore")?.Value ?? 0);

                // Kazanma oranı: data.segments[0].stats.matchesWinPct.value
                profile.WinRate = overview.Stats.GetValueOrDefault("matchesWinPct")?.Value ?? 0;

                // KD
                profile.KdRatio = overview.Stats.GetValueOrDefault("kDRatio")?.Value ?? 0;

                // ACS
                profile.Acs = overview.Stats.GetValueOrDefault("scorePerRound")?.Value ?? 0;

                // HS%
                profile.HeadshotPct = overview.Stats.GetValueOrDefault("headshotsPercentage")?.Value ?? 0;

                // Toplam maç
                profile.TotalMatches = (int)(overview.Stats.GetValueOrDefault("matchesPlayed")?.Value ?? 0);

                // Hasar/tur
                profile.DamagePerRound = overview.Stats.GetValueOrDefault("damagePerRound")?.Value ?? 0;

                // Hesap seviyesi
                profile.AccountLevel = (int)(overview.Stats.GetValueOrDefault("level")?.Value ?? 0);

                // Peak rank
                var peakRank = overview.Stats.GetValueOrDefault("peakRank");
                if (peakRank != null)
                {
                    profile.PeakRankName = peakRank.Metadata?.TierName ?? peakRank.DisplayValue;
                    profile.PeakRankIconUrl = peakRank.Metadata?.IconUrl;
                }
            }

            // En çok oynanan ajan: segments[1].metadata.name
            var agentSegments = data.Segments?
                .Where(s => s.Type == "agent" && s.Stats != null)
                .ToList() ?? new List<TrackerSegment>();

            profile.TopAgents = agentSegments
                .Select(s => new TrackerAgentStat
                {
                    AgentName = s.Metadata?.Name ?? "",
                    AgentImageUrl = s.Metadata?.ImageUrl ?? s.Metadata?.PortraitImageUrl,
                    MatchesPlayed = (int)(s.Stats.GetValueOrDefault("matchesPlayed")?.Value ?? 0),
                    WinRate = s.Stats.GetValueOrDefault("matchesWinPct")?.Value ?? 0,
                    KdRatio = s.Stats.GetValueOrDefault("kDRatio")?.Value ?? 0,
                    Acs = s.Stats.GetValueOrDefault("scorePerRound")?.Value ?? 0,
                    HeadshotPct = s.Stats.GetValueOrDefault("headshotsPercentage")?.Value ?? 0,
                    DamagePerRound = s.Stats.GetValueOrDefault("damagePerRound")?.Value ?? 0
                })
                .Where(a => a.MatchesPlayed > 0)
                .OrderByDescending(a => a.MatchesPlayed)
                .Take(5)
                .ToList();

            return profile;
        }

        // ─── LRU Önbellek Yönetimi ───────────────────────────────────────────────

        private bool TryGetFromCache(string key, out TrackerPlayerProfile profile)
        {
            profile = null;
            if (!_cache.TryGetValue(key, out var entry)) return false;
            if (DateTime.Now - entry.FetchedAt > CacheTtl)
            {
                _cache.Remove(key);
                _cacheOrder.Remove(key);
                return false;
            }
            // LRU: en son kullanılanı başa taşı
            _cacheOrder.Remove(key);
            _cacheOrder.AddFirst(key);
            profile = entry.Profile;
            return true;
        }

        private void AddToCache(string key, TrackerPlayerProfile profile)
        {
            if (_cache.ContainsKey(key))
            {
                _cacheOrder.Remove(key);
            }
            else if (_cache.Count >= MaxCacheSize)
            {
                // En eski kaydı sil (LRU)
                var oldest = _cacheOrder.Last?.Value;
                if (oldest != null)
                {
                    _cache.Remove(oldest);
                    _cacheOrder.RemoveLast();
                }
            }

            _cache[key] = (profile, DateTime.Now);
            _cacheOrder.AddFirst(key);
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }

    /// <summary>Tracker.gg API'ye özgü hata sınıfı</summary>
    public class TrackerApiException : Exception
    {
        public TrackerApiException(string message) : base(message) { }
    }
}
