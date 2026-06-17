using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    public class AnalizService : IDisposable
    {
        private const string BaseUrl = "https://api.henrikdev.xyz";

        private readonly HttpClient _http;
        private readonly RiotLiveMatchService _riotLiveMatch;

        public AnalizService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            if (!string.IsNullOrEmpty(ApiKeyProvider.HenrikDevKey))
                _http.DefaultRequestHeaders.Add("Authorization", ApiKeyProvider.HenrikDevKey);
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            _riotLiveMatch = new RiotLiveMatchService();
        }

        // ─── Maç Geçmişi ──────────────────────────────────────────────────────────

        public async Task<List<AnalizMac>> GetMacGecmisiAsync(
            string region, string name, string tag,
            CancellationToken ct = default)
        {
            var (maclar, _) = await GetMacGecmisiSayfaliAsync(region, name, tag, 1, ct);
            return maclar;
        }

        public async Task<(List<AnalizMac> Maclar, bool HasMore)> GetMacGecmisiSayfaliAsync(
            string region, string name, string tag,
            int page, CancellationToken ct = default)
        {
            int start = (page - 1) * 10;
            var url = $"{BaseUrl}/valorant/v4/matches/{region}/pc" +
                      $"/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}?size=20&start={start}";

            var root = await GetJsonAsync(url, ct);
            var data = root["data"] as JArray;
            if (data == null || data.Count == 0) return (new List<AnalizMac>(), false);

            var result = new List<AnalizMac>();
            foreach (var mac in data)
            {
                try
                {
                    var parsed = ParseMacVerisi(mac, name, tag);
                    if (parsed != null) result.Add(parsed);
                }
                catch { }
            }

            bool hasMore = data.Count >= 10;
            return (result, hasMore);
        }

        // ─── Tüm maç geçmişini sayfalayarak çek (page-based) ─────────────────────
        public async Task<List<AnalizMac>> GetTumMacGecmisiAsync(
            string region, string name, string tag,
            CancellationToken ct = default, int maxPages = 100)
        {
            return await GetTumMacGecmisiCachedAsync(region, name, tag, ct, maxPages);
        }

        // ─── Önbellekli (Cached) Maç Geçmişi ──────────────────────────────────
        // İlk yüklemede API'den çeker ve diske kaydeder.
        // Sonraki açılışlarda sadece yeni maçları çeker, önbelleği günceller.

        private string CacheDir
        {
            get
            {
                var exeDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
                return Path.Combine(exeDir, "data");
            }
        }

        private string HesapSuffix(string name, string tag) =>
            $"_{Uri.EscapeDataString(name)}_{Uri.EscapeDataString(tag)}";

        private string MatchCachePath(string name, string tag) =>
            Path.Combine(CacheDir, $"match_cache{HesapSuffix(name, tag)}.json");

        private string MmrCachePath(string name, string tag) =>
            Path.Combine(CacheDir, $"mmr_cache{HesapSuffix(name, tag)}.json");

        public List<AnalizMac> LoadMatchCache(string name, string tag)
        {
            try
            {
                var path = MatchCachePath(name, tag);
                if (!File.Exists(path)) return new List<AnalizMac>();
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<AnalizMac>>(json)
                       ?? new List<AnalizMac>();
            }
            catch { return new List<AnalizMac>(); }
        }

        public void SaveMatchCache(List<AnalizMac> matches, string name, string tag)
        {
            try
            {
                var path = MatchCachePath(name, tag);
                var dir  = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(matches, Formatting.None);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        public List<EloGrafikNokta> LoadMmrCache(string name, string tag)
        {
            try
            {
                var path = MmrCachePath(name, tag);
                if (!File.Exists(path)) return new List<EloGrafikNokta>();
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<EloGrafikNokta>>(json)
                       ?? new List<EloGrafikNokta>();
            }
            catch { return new List<EloGrafikNokta>(); }
        }

        public void SaveMmrCache(List<EloGrafikNokta> data, string name, string tag)
        {
            try
            {
                var path = MmrCachePath(name, tag);
                var dir  = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(data, Formatting.None);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        public async Task<List<AnalizMac>> GetTumMacGecmisiCachedAsync(
            string region, string name, string tag,
            CancellationToken ct = default, int maxPages = 100)
        {
            // 12 ay öncesinin unix timestamp'i
            long kesmeZaman = new DateTimeOffset(
                DateTime.UtcNow.AddMonths(-12)).ToUnixTimeSeconds();

            // 1. Önbellekten yükle, 12 aydan eski maçları at
            var cache = LoadMatchCache(name, tag)
                .Where(m => m.MacZaman >= kesmeZaman)
                .ToList();
            SaveMatchCache(cache, name, tag); // eski verileri temizle
            var cachedIds = new HashSet<string>(
                cache.Where(m => !string.IsNullOrEmpty(m.MatchId)).Select(m => m.MatchId));
            bool ilkYukleme = cache.Count == 0;

            // 2. İlk sayfayı çek, 429'da 5sn bekle + yeniden dene
            var ilkSayfa = await SayfaGetirGuvenliAsync(region, name, tag, 1, ct);
            if (ilkSayfa == null || ilkSayfa.Count == 0)
                return cache;

            // 3. En yeni maç önbellekte var mı?
            var enYeniId = ilkSayfa.FirstOrDefault()?.MatchId ?? "";
            if (!string.IsNullOrEmpty(enYeniId) && cachedIds.Contains(enYeniId))
                return cache; // Önbellek güncel

            // 4. Yeni maçları sayfala
            var yeniMaclar = new List<AnalizMac>();
            var gorulenIds = new HashSet<string>(cachedIds);

            // İlk sayfayı ekle
            foreach (var m in ilkSayfa)
            {
                if (m.MacZaman > 0 && m.MacZaman < kesmeZaman) break;
                if (string.IsNullOrEmpty(m.MatchId) || gorulenIds.Add(m.MatchId))
                    yeniMaclar.Add(m);
            }

            for (int page = 2; page <= maxPages; page++)
            {
                if (ct.IsCancellationRequested) break;

                // 1000ms bekle (60 req/dk'yı aşmamak için)
                try { await Task.Delay(1000, ct); }
                catch (OperationCanceledException) { break; }

                var sayfa = await SayfaGetirGuvenliAsync(region, name, tag, page, ct);
                if (sayfa == null || sayfa.Count == 0) break;

                bool dur = false;
                foreach (var m in sayfa)
                {
                    if (!string.IsNullOrEmpty(m.MatchId) && cachedIds.Contains(m.MatchId))
                    { dur = true; break; }
                    if (m.MacZaman > 0 && m.MacZaman < kesmeZaman)
                    { dur = true; break; }
                    if (string.IsNullOrEmpty(m.MatchId) || gorulenIds.Add(m.MatchId))
                        yeniMaclar.Add(m);
                }
                if (dur) break;
            }

            // 5. Sırala (en yeni önde) + 12 ay filtresi
            yeniMaclar = yeniMaclar
                .Where(m => m.MacZaman <= 0 || m.MacZaman >= kesmeZaman)
                .OrderByDescending(m => m.MacZaman)
                .ToList();

            // 6. Birleştir + kaydet
            var merged = yeniMaclar.Concat(cache)
                .OrderByDescending(m => m.MacZaman)
                .ToList();
            SaveMatchCache(merged, name, tag);

            return merged;
        }

        private async Task<List<AnalizMac>> SayfaGetirGuvenliAsync(
            string region, string name, string tag,
            int page, CancellationToken ct)
        {
            for (int deneme = 0; deneme < 3; deneme++)
            {
                try
                {
                    var (sayfa, _) = await GetMacGecmisiSayfaliAsync(
                        region, name, tag, page, ct);
                    return sayfa;
                }
                catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("Çok fazla"))
                {
                    if (deneme >= 2) return new List<AnalizMac>(); // 3 deneme başarısız
                    try { await Task.Delay(5000, ct); } catch { break; }
                }
            }
            return new List<AnalizMac>();
        }

        // ─── Performans için size=20 ve size=30 ile iki ayrı istek ───────────────

        public async Task<(List<AnalizMac> Son20, List<AnalizMac> Son30)> GetPerformansVerisiAsync(
            string region, string name, string tag,
            CancellationToken ct = default, string filter = null)
        {
            var url20 = $"{BaseUrl}/valorant/v3/matches/{region}" +
                        $"/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}?size=20&type=competitive";
            if (!string.IsNullOrEmpty(filter))
                url20 += $"&filter={Uri.EscapeDataString(filter)}";

            var url30 = $"{BaseUrl}/valorant/v3/matches/{region}" +
                        $"/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}?size=30&type=competitive";
            if (!string.IsNullOrEmpty(filter))
                url30 += $"&filter={Uri.EscapeDataString(filter)}";

            var task20 = GetJsonAsync(url20, ct);
            var task30 = GetJsonAsync(url30, ct);
            await Task.WhenAll(task20, task30);

            var son20 = ParseMacListesi(task20.Result, name, tag);
            var son30 = ParseMacListesi(task30.Result, name, tag);

            return (son20, son30);
        }

        private List<AnalizMac> ParseMacListesi(JObject root, string name, string tag)
        {
            var result = new List<AnalizMac>();
            var data = root["data"] as JArray;
            if (data == null) return result;
            foreach (var mac in data)
            {
                try
                {
                    var parsed = ParseMacVerisi(mac, name, tag);
                    if (parsed != null) result.Add(parsed);
                }
                catch { }
            }
            return result;
        }

        private static JToken MetaOku(JToken meta, string v4Key, string v3Key)
        {
            var val = meta?[v4Key];
            if (val != null) return val;
            return meta?[v3Key];
        }

        private static string JTokenToString(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return "";
            if (token is JValue) return token.ToString();
            if (token is JObject) return "";
            return token.ToString();
        }

        private static string JTokenNestedAd(JToken token, string childKey)
        {
            if (token == null || token.Type == JTokenType.Null) return "";
            if (token is JValue) return token.ToString();
            if (token is JObject) return token[childKey]?.ToString() ?? "";
            return token.ToString();
        }

        private static long ParseZaman(JToken meta)
        {
            // v3: game_start (unix timestamp)
            var gameStart = meta?["game_start"]?.Value<long>();
            if (gameStart.HasValue && gameStart.Value > 1000000000)
                return gameStart.Value;
            // v4: started_at (ISO string)
            var startedAt = meta?["started_at"]?.ToString() ?? "";
            if (long.TryParse(startedAt, out var unixTs) && unixTs > 1000000000)
                return unixTs;
            if (DateTime.TryParse(startedAt, out var dt))
                return ((DateTimeOffset)dt).ToUnixTimeSeconds();
            return 0;
        }

        private static int TeamRoundOku(JToken team, string key)
        {
            // v3: team["rounds_won"] / team["rounds_lost"]
            var v3Val = team["rounds_" + key]?.Value<int>();
            if (v3Val.HasValue) return v3Val.Value;
            // v4: team["rounds"]["won"] / team["rounds"]["lost"]
            return team["rounds"]?[key]?.Value<int>() ?? 0;
        }

        private AnalizMac ParseMacVerisi(JToken mac, string name, string tag)
        {
            var meta    = mac["metadata"];
            var players = mac["players"] as JArray;
            var teams   = mac["teams"] as JArray;

            // Oyuncuyu name+tag ile bul (v3/v4: flat array)
            var oyuncu = players?.FirstOrDefault(p =>
                string.Equals(p["name"]?.ToString(), name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p["tag"]?.ToString(), tag, StringComparison.OrdinalIgnoreCase));

            if (oyuncu == null) return null;

            var stats  = oyuncu["stats"];
            var teamId = oyuncu["team_id"]?.ToString()
                      ?? oyuncu["team"]?.ToString()
                      ?? "Blue";

            bool kazandi = false;
            string macSkoru = "";
            foreach (var t in teams ?? Enumerable.Empty<JToken>())
            {
                if (string.Equals(t["team_id"]?.ToString(), teamId, StringComparison.OrdinalIgnoreCase))
                {
                    kazandi = t["won"]?.Value<bool>() ?? false;
                    int bizimRound = TeamRoundOku(t, "won");
                    foreach (var ot in teams ?? Enumerable.Empty<JToken>())
                    {
                        if (!string.Equals(ot["team_id"]?.ToString(), teamId, StringComparison.OrdinalIgnoreCase))
                        {
                            int onlarinRound = TeamRoundOku(ot, "won");
                            macSkoru = $"{bizimRound}-{onlarinRound}";
                            break;
                        }
                    }
                    break;
                }
            }

            // Map: v3'te string, v4'te object { name: ... }
            var mapToken = meta?["map"];
            var mapAdi   = JTokenNestedAd(mapToken, "name");
            if (string.IsNullOrEmpty(mapAdi)) mapAdi = "Bilinmiyor";

            // Mode/Queue: v3 = mode (string), v4 = queue.name
            var queueToken = MetaOku(meta, "queue", "mode");
            var mod = JTokenNestedAd(queueToken, "name");

            // MatchId: v3 = matchid, v4 = match_id
            var matchId = MetaOku(meta, "match_id", "matchid")?.ToString() ?? "";

            // Zaman: v3 unix timestamp (long), v4 ISO string
            long macZaman = ParseZaman(meta);

            // Rounds: v3 = rounds_won/rounds_lost (int), v4 = rounds.won/rounds.lost (nested)
            int toplamRoundMac = 0;
            if (teams != null)
            {
                toplamRoundMac = teams.Sum(t =>
                    TeamRoundOku(t, "won") + TeamRoundOku(t, "lost"));
            }
            if (toplamRoundMac < 1) toplamRoundMac = 13;

            int kills   = stats?["kills"]?.Value<int>() ?? 0;
            int deaths  = stats?["deaths"]?.Value<int>() ?? 0;
            int assists = stats?["assists"]?.Value<int>() ?? 0;
            double hasar = stats?["damage"]?["dealt"]?.Value<double>() ?? 0;
            int headshots = stats?["headshots"]?.Value<int>() ?? 0;
            int bodyshots = stats?["bodyshots"]?.Value<int>() ?? 0;
            int legshots  = stats?["legshots"]?.Value<int>() ?? 0;
            int oyuncuRoundOynanan = toplamRoundMac;

            return new AnalizMac
            {
                MapAdi        = mapAdi,
                AjanAdi       = oyuncu["agent"]?["name"]?.ToString() ?? "",
                Mod           = mod,
                Kazandi       = kazandi,
                Kills         = kills,
                Deaths        = deaths,
                Assists       = assists,
                Hasar         = hasar,
                RoundOynanan  = oyuncuRoundOynanan,
                HeadshotCount = headshots,
                BodyshotCount = bodyshots,
                LegshotCount  = legshots,
                MacZaman      = macZaman,
                MacSkoru      = macSkoru,
                MatchId       = matchId,
                RrDegisim     = 0,
                RrSonrasi     = 0
            };
        }

        // ─── MMR Geçmişi ──────────────────────────────────────────────────────────

        public async Task<(List<AnalizMac> MaclarRrIle, List<EloGrafikNokta> EloGrafik, EloOzet Ozet)> GetMmrGecmisiAsync(
            string region, string name, string tag,
            CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/valorant/v1/mmr-history/{region}" +
                      $"/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}";

            var root = await GetJsonAsync(url, ct);
            var data = root["data"] as JArray;
            if (data == null) return (new List<AnalizMac>(), new List<EloGrafikNokta>(), new EloOzet());

            var gecici = new List<(AnalizMac Mac, EloGrafikNokta Nokta)>();

            foreach (var entry in data)
            {
                try
                {
                    var rr        = entry["ranking_in_tier"]?.Value<int>() ?? 0;
                    var rrDegisim = entry["mmr_change_to_last_game"]?.Value<int>() ?? 0;
                    var tier      = entry["currenttier"]?.Value<int>() ?? 0;
                    var dateRaw   = entry["date_raw"]?.Value<long>() ?? 0;
                    var tierStr   = entry["currenttierpatched"]?.ToString() ?? "";
                    var matchId   = entry["match_id"]?.ToString() ?? "";

                    if (tier <= 0) continue;

                    int elo = (tier * 100) + rr;
                    if (elo < 300) elo = 300;

                    gecici.Add((new AnalizMac
                    {
                        RrDegisim = rrDegisim,
                        RrSonrasi = rr,
                        Kazandi   = rrDegisim > 0,
                        MacZaman  = dateRaw,
                        MatchId   = matchId
                    }, new EloGrafikNokta
                    {
                        Elo      = elo,
                        Tier     = tier,
                        RankAdi  = tierStr,
                        Kazandi  = rrDegisim > 0,
                        MacZaman = dateRaw,
                        MatchId  = matchId
                    }));
                }
                catch { }
            }

            // Descending by date
            gecici.Sort((a, b) => b.Nokta.MacZaman.CompareTo(a.Nokta.MacZaman));

            // Dedup by match_id
            var seen = new HashSet<string>();
            var deduped = new List<(AnalizMac Mac, EloGrafikNokta Nokta)>();
            foreach (var item in gecici)
            {
                if (string.IsNullOrEmpty(item.Nokta.MatchId) || seen.Add(item.Nokta.MatchId))
                    deduped.Add(item);
            }

            // Sort ascending by date for display
            deduped.Sort((a, b) => a.Nokta.MacZaman.CompareTo(b.Nokta.MacZaman));

            var maclar = deduped.Select(s => s.Mac).ToList();
            var eloGrafik = deduped.Select(s => s.Nokta).ToList();

            for (int i = 0; i < eloGrafik.Count; i++)
                eloGrafik[i].MacIndex = i + 1;

            // ELO summary from all data
            var ozet = new EloOzet();
            if (eloGrafik.Any())
            {
                ozet.EnYuksekElo    = eloGrafik.Max(e => e.Elo);
                ozet.EnDusukElo     = eloGrafik.Min(e => e.Elo);
                ozet.ToplamEloFarki = eloGrafik.Last().Elo - eloGrafik.First().Elo;
            }
            if (maclar.Any())
            {
                ozet.ToplamMac        = maclar.Count;
                ozet.Galibiyet        = maclar.Count(m => m.Kazandi);
                ozet.Maglubiyet       = maclar.Count(m => !m.Kazandi);
                ozet.GalibiyetYuzdesi = ozet.ToplamMac > 0
                    ? ozet.Galibiyet * 100.0 / ozet.ToplamMac : 0;
            }

            return (maclar, eloGrafik, ozet);
        }

        // ─── Tam Analiz ────────────────────────────────────────────────────────────

        public async Task<(List<AnalizMac> Maclar, List<EloGrafikNokta> EloGrafik, EloOzet EloOzet, AnalizOzet Ozet)>
            GetTamAnalizAsync(string region, string name, string tag, CancellationToken ct = default)
        {
            var macTask = GetMacGecmisiAsync(region, name, tag, ct);
            var mmrTask = GetMmrGecmisiAsync(region, name, tag, ct);

            await Task.WhenAll(macTask, mmrTask);

            var maclar = macTask.Result;
            var (mmrMaclar, eloGrafik, eloOzet) = mmrTask.Result;

            foreach (var mac in maclar)
            {
                if (!string.Equals(mac.Mod, "competitive", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (mac.MacZaman <= 0 || mmrMaclar.All(m => m.MacZaman <= 0))
                    continue;
                var eslesen = mmrMaclar
                    .OrderBy(m => Math.Abs(m.MacZaman - mac.MacZaman))
                    .FirstOrDefault();
                if (eslesen != null)
                {
                    mac.RrDegisim = eslesen.RrDegisim;
                    mac.RrSonrasi = eslesen.RrSonrasi;
                }
            }

            var ozet = new AnalizOzet
            {
                ToplamMac     = maclar.Count,
                KazanmaOrani  = maclar.Count > 0 ? maclar.Count(m => m.Kazandi) * 100.0 / maclar.Count : 0,
                OrtalamaKda   = maclar.Count > 0 ? maclar.Average(m => m.Kda) : 0,
                OrtalamaHasar = maclar.Count > 0 ? maclar.Average(m => m.Hasar) : 0
            };

            return (maclar, eloGrafik, eloOzet, ozet);
        }

        // ─── Canlı Maç ───────────────────────────────────────────────────────────

        public async Task<LiveMatchData> GetLiveMatchAsync(
            string region, string name, string tag,
            CancellationToken ct = default)
        {
            try
            {
                return await _riotLiveMatch.GetLiveMatchAsync(region, name, tag, ct);
            }
            catch
            {
                return null;
            }
        }

        public bool IsLockfileAvailable() => _riotLiveMatch.IsLockfileAvailable();

        // ─── 6 PERFORMANS METRİĞİ ───────────────────────────────────────────────
        // son20: son 20 maç (ana değer), son30: önceki 30 maç (karşılaştırma için)

        public List<PerformansMetrik> GetPerformansMetrikleri(List<AnalizMac> son20, List<AnalizMac> onceki30 = null)
        {
            if (son20 == null || !son20.Any())
                return new List<PerformansMetrik>();

            onceki30 = onceki30 ?? new List<AnalizMac>();

            var metrikler = new List<PerformansMetrik>();

            double DegisimHesapla(double yeni, double eski)
                => Math.Round(yeni - eski, 2);

            int sR = son20.Sum(m => m.RoundOynanan);
            int oR = onceki30.Sum(m => m.RoundOynanan);

            // 0. Kazanma Orani
            double kazanc = son20.Count > 0 ? son20.Count(m => m.Kazandi) * 100.0 / son20.Count : 0;
            double kazancOnce = onceki30.Count > 0 ? onceki30.Count(m => m.Kazandi) * 100.0 / onceki30.Count : kazanc;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Kazanma Oran\u0131", Deger = kazanc, Maksimum = 100, Format = "%", Tip = 0,
                Degisim = DegisimHesapla(kazanc, kazancOnce)
            });

            // 1. ADR
            double adr = sR > 0 ? son20.Sum(m => m.Hasar) / (double)sR : 0;
            double adrOnce = oR > 0 ? onceki30.Sum(m => m.Hasar) / (double)oR : adr;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "ADR", Deger = adr, Maksimum = 250, Format = "F0", Tip = 1,
                Degisim = DegisimHesapla(adr, adrOnce)
            });

            // 2. K/D
            int sK = son20.Sum(m => m.Kills);
            int sD = son20.Sum(m => m.Deaths);
            double kd = sD > 0 ? (double)sK / sD : sK;
            int oK = onceki30.Sum(m => m.Kills);
            int oD = onceki30.Sum(m => m.Deaths);
            double kdOnce = oD > 0 ? (double)oK / oD : oK;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "K/D", Deger = kd, Maksimum = 2.5, Format = "F2", Tip = 2,
                Degisim = DegisimHesapla(kd, kdOnce)
            });

            // 3. HS%
            int sHS = son20.Sum(m => m.HeadshotCount);
            int sShots = son20.Sum(m => m.HeadshotCount + m.BodyshotCount + m.LegshotCount);
            double hs = sShots > 0 ? sHS * 100.0 / sShots : 0;
            int oHS = onceki30.Sum(m => m.HeadshotCount);
            int oShots = onceki30.Sum(m => m.HeadshotCount + m.BodyshotCount + m.LegshotCount);
            double hsOnce = oShots > 0 ? oHS * 100.0 / oShots : hs;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Kafa Vuru\u015Fu %", Deger = hs, Maksimum = 100, Format = "%", Tip = 3,
                Degisim = DegisimHesapla(hs, hsOnce)
            });

            // 4. Entry Success (First Blood)
            double girisBasari = son20.Any() ? son20.Count(m => m.Kills > m.Deaths) * 100.0 / son20.Count : 0;
            double girisOnce = onceki30.Any() ? onceki30.Count(m => m.Kills > m.Deaths) * 100.0 / onceki30.Count : girisBasari;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Entry Ba\u015Far\u0131s\u0131", Deger = girisBasari, Maksimum = 100, Format = "%", Tip = 4,
                Degisim = DegisimHesapla(girisBasari, girisOnce)
            });

            // 5. ACS
            double acs = sR > 0 ? (sK * 150.0 + son20.Sum(m => m.Assists) * 50.0 + son20.Sum(m => m.Hasar)) / sR : 0;
            double acsOnce = oR > 0 ? (oK * 150.0 + onceki30.Sum(m => m.Assists) * 50.0 + onceki30.Sum(m => m.Hasar)) / oR : acs;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "ACS", Deger = acs, Maksimum = 350, Format = "F0", Tip = 5,
                Degisim = DegisimHesapla(acs, acsOnce)
            });

            return metrikler;
        }

        // Eski imza ile uyumluluk için overload
        public List<PerformansMetrik> GetPerformansMetrikleri(List<AnalizMac> maclar)
        {
            if (maclar == null || !maclar.Any())
                return new List<PerformansMetrik>();

            var metrikler = new List<PerformansMetrik>();
            var son20 = maclar.Take(20).ToList();
            var son5  = maclar.Take(5).ToList();
            var once5 = maclar.Skip(5).Take(5).ToList();
            if (!son20.Any()) son20 = maclar;

            double DegisimHesapla(double yeni, double eski)
                => eski > 0 ? Math.Round(yeni - eski, 2) : 0;

            // Toplam istatistikler
            int toplamMac  = maclar.Count;
            int galibiyet  = maclar.Count(m => m.Kazandi);
            int toplamKill = maclar.Sum(m => m.Kills);
            int toplamDeath = maclar.Sum(m => m.Deaths);
            int toplamAsist = maclar.Sum(m => m.Assists);
            double toplamHasar = maclar.Sum(m => m.Hasar);
            int toplamRound = maclar.Sum(m => m.RoundOynanan);
            int toplamHS = maclar.Sum(m => m.HeadshotCount);
            int toplamBS = maclar.Sum(m => m.BodyshotCount);
            int toplamLS = maclar.Sum(m => m.LegshotCount);

            // 0. Kazanma Oranı = galibiyet / toplam mac * 100
            double kazanc = toplamMac > 0 ? galibiyet * 100.0 / toplamMac : 0;
            double kazancSon = son5.Any() ? son5.Count(m => m.Kazandi) * 100.0 / son5.Count : kazanc;
            double kazancOnce = once5.Any() ? once5.Count(m => m.Kazandi) * 100.0 / once5.Count : kazanc;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Kazanma Oran\u0131", Deger = kazanc, Maksimum = 100, Format = "%", Tip = 0,
                Degisim = DegisimHesapla(kazancSon, kazancOnce)
            });

            // 1. KDA = (kills + assists) / deaths
            double kda = toplamDeath > 0 ? (toplamKill + toplamAsist) / (double)toplamDeath : 0;
            double kdaSon = son5.Any() ? (son5.Sum(m => m.Kills + m.Assists) / (double)Math.Max(1, son5.Sum(m => m.Deaths))) : kda;
            double kdaOnce = once5.Any() ? (once5.Sum(m => m.Kills + m.Assists) / (double)Math.Max(1, once5.Sum(m => m.Deaths))) : kda;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "KDA", Deger = kda, Maksimum = 5, Format = "F2", Tip = 1,
                Degisim = DegisimHesapla(kdaSon, kdaOnce)
            });

            // 2. ADR = toplam hasar / toplam round
            double adr = toplamRound > 0 ? toplamHasar / toplamRound : 0;
            double adrSon = son5.Any() ? son5.Sum(m => m.Hasar) / (double)Math.Max(1, son5.Sum(m => m.RoundOynanan)) : adr;
            double adrOnce = once5.Any() ? once5.Sum(m => m.Hasar) / (double)Math.Max(1, once5.Sum(m => m.RoundOynanan)) : adr;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "ADR", Deger = adr, Maksimum = 250, Format = "F0", Tip = 2,
                Degisim = DegisimHesapla(adrSon, adrOnce)
            });

            // 3. K/R = toplam kill / toplam round
            double kr = toplamRound > 0 ? toplamKill / (double)toplamRound : 0;
            double krSon = son5.Any() ? son5.Sum(m => m.Kills) / (double)Math.Max(1, son5.Sum(m => m.RoundOynanan)) : kr;
            double krOnce = once5.Any() ? once5.Sum(m => m.Kills) / (double)Math.Max(1, once5.Sum(m => m.RoundOynanan)) : kr;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "K/R", Deger = kr, Maksimum = 1.5, Format = "F2", Tip = 3,
                Degisim = DegisimHesapla(krSon, krOnce)
            });

            // 4. Headshot % = headshots / (headshots + bodyshots + legshots) * 100
            int toplamAtis = toplamHS + toplamBS + toplamLS;
            double hsYuzde = toplamAtis > 0 ? toplamHS * 100.0 / toplamAtis : 0;
            int hsSon = son5.Sum(m => m.HeadshotCount);
            int atisSon = son5.Sum(m => m.HeadshotCount + m.BodyshotCount + m.LegshotCount);
            double hsSonYuzde = atisSon > 0 ? hsSon * 100.0 / atisSon : hsYuzde;
            int hsOnce = once5.Sum(m => m.HeadshotCount);
            int atisOnce = once5.Sum(m => m.HeadshotCount + m.BodyshotCount + m.LegshotCount);
            double hsOnceYuzde = atisOnce > 0 ? hsOnce * 100.0 / atisOnce : hsYuzde;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Headshot %", Deger = hsYuzde, Maksimum = 100, Format = "%", Tip = 4,
                Degisim = DegisimHesapla(hsSonYuzde, hsOnceYuzde)
            });

            // 5. KD Oranı = kills / deaths
            double kd = toplamDeath > 0 ? toplamKill / (double)toplamDeath : 0;
            double kdSon = son5.Any() ? son5.Sum(m => m.Kills) / (double)Math.Max(1, son5.Sum(m => m.Deaths)) : kd;
            double kdOnce = once5.Any() ? once5.Sum(m => m.Kills) / (double)Math.Max(1, once5.Sum(m => m.Deaths)) : kd;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "KD Oran\u0131", Deger = kd, Maksimum = 3, Format = "F2", Tip = 5,
                Degisim = DegisimHesapla(kdSon, kdOnce)
            });

            return metrikler;
        }

        public KazanmaOraniDetay GetKazanmaOraniDetay(List<AnalizMac> maclar)
        {
            var detay = new KazanmaOraniDetay();
            if (!maclar.Any()) return detay;

            int toplam = maclar.Count;
            int kazanc = maclar.Count(m => m.Kazandi);
            int kayip = toplam - kazanc;

            detay.KazanilanMac = kazanc;
            detay.KaybedilenMac = kayip;

            double maxYukseklik = Math.Max(kazanc, kayip);
            detay.KazanilanYukseklik = maxYukseklik > 0 ? kazanc / maxYukseklik * 100 : 0;
            detay.KaybedilenYukseklik = maxYukseklik > 0 ? kayip / maxYukseklik * 100 : 0;

            var son10 = maclar.Take(10).ToList();
            detay.Son10MacOrani = son10.Count > 0 ? Math.Round(son10.Count(m => m.Kazandi) * 100.0 / son10.Count, 1) : 0;

            var son20 = maclar.Take(20).ToList();
            detay.Son20MacOrani = son20.Count > 0 ? Math.Round(son20.Count(m => m.Kazandi) * 100.0 / son20.Count, 1) : 0;

            var son50 = maclar.Take(50).ToList();
            detay.Son50MacOrani = son50.Count > 0 ? Math.Round(son50.Count(m => m.Kazandi) * 100.0 / son50.Count, 1) : 0;

            detay.EnCokKazanilanHarita = maclar.Where(m => m.Kazandi)
                .GroupBy(m => m.MapAdi).OrderByDescending(g => g.Count())
                .Select(g => g.Key).FirstOrDefault() ?? "";

            detay.EnCokKaybedilenHarita = maclar.Where(m => !m.Kazandi)
                .GroupBy(m => m.MapAdi).OrderByDescending(g => g.Count())
                .Select(g => g.Key).FirstOrDefault() ?? "";

            return detay;
        }

        public AdrDetay GetAdrDetay(List<AnalizMac> maclar)
        {
            var detay = new AdrDetay();
            if (!maclar.Any()) return detay;

            var son20 = maclar.Take(20).ToList();
            var noktalar = son20.Select((m, i) => new AdrGrafikNokta
            {
                Index = i + 1,
                Adr = m.RoundOynanan > 0 ? Math.Round(m.Hasar / m.RoundOynanan, 1) : 0
            }).ToList();

            detay.AdrNoktalari = noktalar;
            detay.EnYuksekAdr = noktalar.Any() ? noktalar.Max(n => n.Adr) : 0;
            detay.EnDusukAdr = noktalar.Any() ? noktalar.Min(n => n.Adr) : 0;
            detay.OrtalamaAdr = noktalar.Any() ? Math.Round(noktalar.Average(n => n.Adr), 1) : 0;

            detay.HaritaAdrListe = maclar.GroupBy(m => m.MapAdi)
                .Select(g => new HaritaAdrBilgi
                {
                    HaritaAdi = g.Key,
                    Adr = Math.Round(g.Sum(m => m.Hasar) / Math.Max(1, g.Sum(m => m.RoundOynanan)), 1)
                })
                .OrderByDescending(h => h.Adr)
                .ToList();

            return detay;
        }

        public KrDetay GetKrDetay(List<AnalizMac> maclar)
        {
            var detay = new KrDetay();
            if (!maclar.Any()) return detay;

            detay.ToplamKill = maclar.Sum(m => m.Kills);
            detay.ToplamDeath = maclar.Sum(m => m.Deaths);
            detay.ToplamAsist = maclar.Sum(m => m.Assists);

            var son30 = maclar.Take(30).ToList();
            var onceki30 = maclar.Skip(30).Take(30).ToList();

            if (son30.Count >= 5 && onceki30.Count >= 5)
            {
                double sonKda = son30.Sum(m => m.Kills + m.Assists) / (double)Math.Max(1, son30.Sum(m => m.Deaths));
                double onceKda = onceki30.Sum(m => m.Kills + m.Assists) / (double)Math.Max(1, onceki30.Sum(m => m.Deaths));
                if (sonKda >= onceKda)
                {
                    detay.KarsilastirmaIyi = true;
                    double fark = onceKda > 0 ? (sonKda - onceKda) / onceKda * 100 : 0;
                    detay.KarsilastirmaText = $"Son 30 maç KDA değeriniz önceki 30 maça göre %{Math.Round(fark)} daha iyi";
                }
                else
                {
                    detay.KarsilastirmaIyi = false;
                    double fark = onceKda > 0 ? (onceKda - sonKda) / onceKda * 100 : 0;
                    detay.KarsilastirmaText = $"Son 30 maç KDA değeriniz önceki 30 maça göre %{Math.Round(fark)} daha kötü";
                }
            }
            else
            {
                detay.KarsilastirmaText = "Kar\u015F\u0131la\u015Ft\u0131rma i\u00E7in yeterli ma\u00E7 verisi yok";
            }

            return detay;
        }

        public HeadshotDetay GetHeadshotDetay(List<AnalizMac> maclar)
        {
            var detay = new HeadshotDetay();
            if (!maclar.Any()) return detay;

            detay.ToplamHeadshot = maclar.Sum(m => m.HeadshotCount);
            detay.ToplamBodyshot = maclar.Sum(m => m.BodyshotCount);
            detay.ToplamLegshot  = maclar.Sum(m => m.LegshotCount);
            int toplam = detay.ToplamHeadshot + detay.ToplamBodyshot + detay.ToplamLegshot;
            detay.HeadshotYuzdesi = toplam > 0 ? Math.Round(detay.ToplamHeadshot * 100.0 / toplam, 1) : 0;

            detay.HaritaListe = maclar.GroupBy(m => m.MapAdi)
                .Select(g =>
                {
                    int hs = g.Sum(m => m.HeadshotCount);
                    int total = hs + g.Sum(m => m.BodyshotCount + m.LegshotCount);
                    return new HaritaHeadshotBilgi
                    {
                        HaritaAdi = g.Key,
                        HeadshotOrani = total > 0 ? Math.Round(hs * 100.0 / total, 1) : 0
                    };
                })
                .OrderByDescending(h => h.HeadshotOrani)
                .ToList();

            return detay;
        }

        public KdDetay GetKdDetay(List<AnalizMac> maclar)
        {
            var detay = new KdDetay();
            if (!maclar.Any()) return detay;

            detay.ToplamKill = maclar.Sum(m => m.Kills);
            detay.ToplamDeath = maclar.Sum(m => m.Deaths);
            detay.KdOrani = detay.ToplamDeath > 0 ? Math.Round(detay.ToplamKill / (double)detay.ToplamDeath, 2) : 0;

            var son30 = maclar.Take(30).ToList();
            var onceki30 = maclar.Skip(30).Take(30).ToList();

            if (son30.Count >= 5 && onceki30.Count >= 5)
            {
                double sonKd = son30.Sum(m => m.Kills) / (double)Math.Max(1, son30.Sum(m => m.Deaths));
                double onceKd = onceki30.Sum(m => m.Kills) / (double)Math.Max(1, onceki30.Sum(m => m.Deaths));
                if (sonKd >= onceKd)
                {
                    detay.KarsilastirmaIyi = true;
                    double fark = onceKd > 0 ? (sonKd - onceKd) / onceKd * 100 : 0;
                    detay.KarsilastirmaText = $"Son 30 maç KD oranınız önceki 30 maça göre %{Math.Round(fark)} daha iyi";
                }
                else
                {
                    detay.KarsilastirmaIyi = false;
                    double fark = onceKd > 0 ? (onceKd - sonKd) / onceKd * 100 : 0;
                    detay.KarsilastirmaText = $"Son 30 maç KD oranınız önceki 30 maça göre %{Math.Round(fark)} daha kötü";
                }
            }
            else
            {
                detay.KarsilastirmaText = "Kar\u015F\u0131la\u015Ft\u0131rma i\u00E7in yeterli ma\u00E7 verisi yok";
            }

            return detay;
        }

        public BitisBasarisiDetay GetBitisBasarisiDetay(List<AnalizMac> maclar)
        {
            var detay = new BitisBasarisiDetay();
            if (!maclar.Any()) return detay;

            foreach (var m in maclar)
            {
                int fark = Math.Abs(m.Kills - m.Deaths);
                if (fark <= 1)
                {
                    detay.Clutch1v1++;
                    if (m.Kazandi && m.Kills > m.Deaths) detay.Clutch1v1Basarili++;
                }
                else if (fark <= 2)
                {
                    detay.Clutch1v2++;
                    if (m.Kazandi && m.Kills > m.Deaths) detay.Clutch1v2Basarili++;
                }
                else if (fark <= 3)
                {
                    detay.Clutch1v3++;
                    if (m.Kazandi && m.Kills > m.Deaths) detay.Clutch1v3Basarili++;
                }
                else if (fark <= 4)
                {
                    detay.Clutch1v4++;
                    if (m.Kazandi && m.Kills > m.Deaths) detay.Clutch1v4Basarili++;
                }
                else
                {
                    detay.Clutch1v5++;
                    if (m.Kazandi && m.Kills > m.Deaths) detay.Clutch1v5Basarili++;
                }
            }

            int toplam = detay.Clutch1v1 + detay.Clutch1v2 + detay.Clutch1v3 + detay.Clutch1v4 + detay.Clutch1v5;
            int basarili = detay.Clutch1v1Basarili + detay.Clutch1v2Basarili + detay.Clutch1v3Basarili + detay.Clutch1v4Basarili + detay.Clutch1v5Basarili;
            detay.ToplamBasariOrani = toplam > 0 ? Math.Round(basarili * 100.0 / toplam, 1) : 0;

            return detay;
        }

        public GirisBasarisiDetay GetGirisBasarisiDetay(List<AnalizMac> maclar)
        {
            var detay = new GirisBasarisiDetay();
            if (!maclar.Any()) return detay;

            detay.ToplamMac = maclar.Count;
            detay.IlkKanAlinanMac = maclar.Count(m => m.Kills > m.Deaths);
            detay.BasariOrani = detay.ToplamMac > 0 ? Math.Round(detay.IlkKanAlinanMac * 100.0 / detay.ToplamMac, 1) : 0;

            detay.HaritaListe = maclar.GroupBy(m => m.MapAdi)
                .Select(g =>
                {
                    int toplam = g.Count();
                    int basarili = g.Count(m => m.Kills > m.Deaths);
                    return new HaritaGirisBilgi
                    {
                        HaritaAdi = g.Key,
                        BasariOrani = toplam > 0 ? Math.Round(basarili * 100.0 / toplam, 1) : 0
                    };
                })
                .OrderByDescending(h => h.BasariOrani)
                .ToList();

            return detay;
        }

        public MultiKillDetay GetMultiKillDetay(List<AnalizMac> maclar)
        {
            var detay = new MultiKillDetay();
            if (!maclar.Any()) return detay;

            foreach (var m in maclar)
            {
                double killsPerRound = m.RoundOynanan > 0 ? m.Kills / (double)m.RoundOynanan : 0;

                if (killsPerRound >= 0.8) { detay.Ace++; AddMultiKillItem(detay, m, "Ace", 5); }
                else if (killsPerRound >= 0.6) { detay.FourK++; AddMultiKillItem(detay, m, "4K", 4); }
                else if (killsPerRound >= 0.4) { detay.ThreeK++; AddMultiKillItem(detay, m, "3K", 3); }
                else if (killsPerRound >= 0.25) { detay.TwoK++; }
                else if (killsPerRound >= 0.1) { detay.OneK++; }
            }

            return detay;
        }

        private void AddMultiKillItem(MultiKillDetay detay, AnalizMac m, string tur, int killSayisi)
        {
            detay.MultiKillListe.Add(new MultiKillItem
            {
                HaritaAdi = m.MapAdi ?? "",
                Tur = tur,
                KillSayisi = killSayisi,
                MacText = m.TarihKisa ?? "",
                MacZaman = m.MacZaman
            });
        }

        // ─── Aktivite ────────────────────────────────────────────────────────────

        public (List<SaatlikAktivite> Saatlik, AktiviteGrafikOzet Ozet) GetSaatlikAktivite(List<AnalizMac> maclar)
        {
            var liste = Enumerable.Range(0, 24).Select(s => new SaatlikAktivite { Saat = s }).ToList();
            if (maclar == null || !maclar.Any())
                return (liste, new AktiviteGrafikOzet());

            var gecerli = maclar.Where(m => m.MacZaman > 0).ToList();
            foreach (var mac in gecerli)
            {
                var dt   = DateTimeOffset.FromUnixTimeSeconds(mac.MacZaman).LocalDateTime;
                var item = liste[dt.Hour];
                item.MacSayisi++;
                if (mac.Kazandi) item.Galibiyet++;
            }

            // Günlük ortalama hesapla (son 30 gün)
            var bugun = DateTime.Today;
            var son30Gun = bugun.AddDays(-30);
            var son30Mac = gecerli.Where(m =>
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(m.MacZaman).LocalDateTime.Date;
                return dt >= son30Gun && dt <= bugun;
            }).ToList();

            var onceki30Mac = gecerli.Where(m =>
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(m.MacZaman).LocalDateTime.Date;
                return dt < son30Gun && dt >= son30Gun.AddDays(-30);
            }).ToList();

            double son30GunSayisi = 30;
            double onceki30GunSayisi = 30;

            double ort = son30Mac.Count > 0 ? son30Mac.Count / son30GunSayisi : 0;
            double oncekiOrt = onceki30Mac.Count > 0 ? onceki30Mac.Count / onceki30GunSayisi : 0;

            return (liste, new AktiviteGrafikOzet
            {
                OrtalamaGunlukMac = Math.Round(ort, 1),
                GunlukDegisim = Math.Round(ort - oncekiOrt, 1)
            });
        }

        public (List<HaftalikAktivite> Haftalik, AktiviteGrafikOzet Ozet) GetHaftalikAktivite(List<AnalizMac> maclar)
        {
            var gunAdlari = new[] { "Pzt", "Sal", "Car", "Per", "Cum", "Cmt", "Paz" };
            var liste = Enumerable.Range(0, 7).Select(i => new HaftalikAktivite
            {
                GunIndex = i,
                GunAdi   = gunAdlari[i]
            }).ToList();

            if (maclar == null || !maclar.Any())
                return (liste, new AktiviteGrafikOzet());

            var gecerli = maclar.Where(m => m.MacZaman > 0).ToList();
            foreach (var mac in gecerli)
            {
                var dt  = DateTimeOffset.FromUnixTimeSeconds(mac.MacZaman).LocalDateTime;
                int idx = ((int)dt.DayOfWeek + 6) % 7;
                liste[idx].MacSayisi++;
                if (mac.Kazandi) liste[idx].Galibiyet++;
            }

            // Haftalık ortalama: son 12 hafta / önceki 12 hafta
            var bugun = DateTime.Today;
            var son12Hafta = bugun.AddDays(-84);
            var onceki12Hafta = bugun.AddDays(-168);

            var son12HaftaMac = gecerli.Where(m =>
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(m.MacZaman).LocalDateTime.Date;
                return dt >= son12Hafta && dt <= bugun;
            }).Count();

            var onceki12HaftaMac = gecerli.Where(m =>
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(m.MacZaman).LocalDateTime.Date;
                return dt >= onceki12Hafta && dt < son12Hafta;
            }).Count();

            double haftaSayisi = 12;
            double ort = son12HaftaMac > 0 ? son12HaftaMac / haftaSayisi : 0;
            double oncekiOrt = onceki12HaftaMac > 0 ? onceki12HaftaMac / haftaSayisi : 0;

            return (liste, new AktiviteGrafikOzet
            {
                OrtalamaHaftalikMac = Math.Round(ort, 1),
                HaftalikDegisim = Math.Round(ort - oncekiOrt, 1)
            });
        }

        public (List<TakvimHaftasi> Haftalar, List<TakvimAyBaslik> AyBasliklari) GetAktiviteTakvimi(List<AnalizMac> maclar)
        {
            var bugun = DateTime.Today;
            var yil   = bugun.Year;
            var baslangic = new DateTime(yil, 1, 1);

            var gunMap = new Dictionary<DateTime, (int Mac, int Galibiyet)>();
            if (maclar != null)
            {
                foreach (var mac in maclar.Where(m => m.MacZaman > 0))
                {
                    var gun = DateTimeOffset.FromUnixTimeSeconds(mac.MacZaman).LocalDateTime.Date;
                    if (!gunMap.ContainsKey(gun)) gunMap[gun] = (0, 0);
                    var (m, g) = gunMap[gun];
                    gunMap[gun] = (m + 1, g + (mac.Kazandi ? 1 : 0));
                }
            }

            var haftalar     = new List<TakvimHaftasi>();
            var ayBasliklari = new List<TakvimAyBaslik>();
            int sutunSayac   = 0;
            int oncekiAy     = -1;

            var haftaBaslangic = baslangic;
            while (haftaBaslangic <= bugun)
            {
                var hafta = new TakvimHaftasi();
                for (int g = 0; g < 7; g++)
                {
                    var gun = haftaBaslangic.AddDays(g);
                    gunMap.TryGetValue(gun, out var veri);

                    hafta.Gunler[g] = new AktiviteHucre
                    {
                        Yil       = gun.Year,
                        Ay        = gun.Month,
                        Gun       = gun.Day,
                        MacSayisi = veri.Mac,
                        Galibiyet = veri.Galibiyet,
                        TarihText = gun.ToString("dd MMM")
                    };

                    if (gun.Month != oncekiAy)
                    {
                        ayBasliklari.Add(new TakvimAyBaslik
                        {
                            AyAdi    = AyKisalt(gun.Month),
                            SutunPos = sutunSayac,
                            Genislik = 1
                        });
                        oncekiAy = gun.Month;
                    }
                }
                haftalar.Add(hafta);
                haftaBaslangic = haftaBaslangic.AddDays(7);
                sutunSayac++;
            }

            return (haftalar, ayBasliklari);
        }

        private static string AyKisalt(int ay) => ay switch
        {
            1  => "Oca", 2  => "Sub", 3  => "Mar", 4  => "Nis",
            5  => "May", 6  => "Haz", 7  => "Tem", 8  => "Agu",
            9  => "Eyl", 10 => "Eki", 11 => "Kas", 12 => "Ara",
            _  => ""
        };

        // ─── Harita İstatistikleri ────────────────────────────────────────────────

        public List<HaritaIstatistik> GetHaritaIstatistikleri(List<AnalizMac> maclar)
        {
            var tumHaritalar = new[] {
                "Ascent", "Bind", "Haven", "Split", "Icebox",
                "Breeze", "Fracture", "Pearl", "Lotus", "Sunset", "Abyss"
            };

            var haritaDict = new Dictionary<string, (int Oynanan, int Kazanilan, double ToplamHasar,
                int ToplamKill, int ToplamDeath, int ToplamAsist,
                int ToplamHeadshot, int ToplamBodyshot, int ToplamLegshot, List<bool> SonMaclar)>();

            foreach (var harita in tumHaritalar)
                haritaDict[harita] = (0, 0, 0, 0, 0, 0, 0, 0, 0, new List<bool>());

            int toplamMac = 0;
            if (maclar != null)
            {
                toplamMac = maclar.Count;
                foreach (var mac in maclar)
                {
                    var map = mac.MapAdi;
                    if (string.IsNullOrEmpty(map)) continue;

                    var normalized = tumHaritalar.FirstOrDefault(h =>
                        string.Equals(h, map, StringComparison.OrdinalIgnoreCase));
                    if (normalized == null) continue;

                    var (oynanan, kazanilan, toplamHasar, topKill, topDeath, topAsist, topHS, topBS, topLS, sonMaclar) = haritaDict[normalized];
                    sonMaclar.Add(mac.Kazandi);
                    if (sonMaclar.Count > 3) sonMaclar.RemoveAt(0);

                    haritaDict[normalized] = (
                        oynanan + 1,
                        kazanilan + (mac.Kazandi ? 1 : 0),
                        toplamHasar + mac.Hasar,
                        topKill + mac.Kills,
                        topDeath + mac.Deaths,
                        topAsist + mac.Assists,
                        topHS + mac.HeadshotCount,
                        topBS + mac.BodyshotCount,
                        topLS + mac.LegshotCount,
                        sonMaclar
                    );
                }
            }

            var sonuc = haritaDict
                .Select(kv =>
                {
                    var (oynanan, kazanilan, toplamHasar, topKill, topDeath, topAsist, topHS, topBS, topLS, sonMaclar) = kv.Value;
                    double galibiyetYuzde = oynanan > 0 ? kazanilan * 100.0 / oynanan : 0;
                    double oynanmaOrani = toplamMac > 0 ? oynanan * 100.0 / toplamMac : 0;
                    double adr = oynanan > 0 ? toplamHasar / oynanan : 0;
                    double ortKill = oynanan > 0 ? (double)topKill / oynanan : 0;
                    double ortDeath = oynanan > 0 ? (double)topDeath / oynanan : 0;
                    double ortAsist = oynanan > 0 ? (double)topAsist / oynanan : 0;
                    int toplamAtis = topHS + topBS + topLS;
                    double hsOrani = toplamAtis > 0 ? topHS * 100.0 / toplamAtis : 0;

                    return new HaritaIstatistik
                    {
                        HaritaAdi      = kv.Key,
                        OynanmaSayisi  = oynanan,
                        GalibiyetSayisi = kazanilan,
                        GalibiyetOrani = galibiyetYuzde,
                        ADR            = adr,
                        OrtalamaKill   = ortKill,
                        OrtalamaDeath  = ortDeath,
                        OrtalamaAsist  = ortAsist,
                        HeadshotOrani  = hsOrani,
                        SonMaclar      = sonMaclar
                    };
                })
                .OrderByDescending(h => h.OynanmaSayisi)
                .ToList();

            return sonuc;
        }

        // ─── Sezon ───────────────────────────────────────────────────────────────

        public async Task<(string SezonAdi, DateTime? BitisTarihi)> GetCurrentSeasonAsync(
            CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/valorant/v1/seasons";
            try
            {
                var root = await GetJsonAsync(url, ct);
                var data = root["data"] as JArray;
                if (data == null) return ("", null);

                foreach (var entry in data)
                {
                    var isActive = entry["is_active"]?.Value<bool>() ?? false;
                    if (!isActive) continue;

                    var name = entry["name"]?.ToString() ?? "";
                    var endTimeStr = entry["end_time"]?.ToString() ?? "";
                    DateTime? endTime = null;
                    if (DateTime.TryParse(endTimeStr, out var parsed))
                        endTime = parsed;

                    return (name, endTime);
                }

                if (data.Count > 0)
                {
                    var name = data[0]["name"]?.ToString() ?? "";
                    return (name, null);
                }

                return ("", null);
            }
            catch
            {
                return ("", null);
            }
        }

        // ─── HTTP ───────────────────────────────────────────────────────────────

        private async Task<JObject> GetJsonAsync(string url, CancellationToken ct)
        {
            if (!_http.DefaultRequestHeaders.Contains("Authorization") && !string.IsNullOrEmpty(ApiKeyProvider.HenrikDevKey))
                _http.DefaultRequestHeaders.Add("Authorization", ApiKeyProvider.HenrikDevKey);

            HttpResponseMessage resp;
            try
            {
                resp = await _http.GetAsync(url, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new Exception("İstek zaman aşımına uğradı.");
            }

            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                switch ((int)resp.StatusCode)
                {
                    case 404: throw new Exception("Hesap bulunamadı veya maç geçmişi yok.");
                    case 429: throw new Exception("Çok fazla istek. Lütfen bekleyin.");
                    case 401:
                    case 403: throw new Exception("API anahtarı geçersiz.");
                    default:  throw new Exception($"API hatası ({(int)resp.StatusCode}).");
                }
            }

            return JObject.Parse(body);
        }

        // ─── Rank Yenileme ──────────────────────────────────────────────────────────

        public async Task<(string rankAdi, int currentTier, int rr)> GetRankFromApiAsync(
            string region, string name, string tag, CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/valorant/v2/mmr/{region}/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}";
            var root = await GetJsonAsync(url, ct);

            if (root["status"]?.Value<int>() != 200)
                return ("", 0, 0);

            var current = root["data"]?["current_data"];
            if (current == null) return ("", 0, 0);

            return (
                current["currenttierpatched"]?.ToString() ?? "",
                current["currenttier"]?.Value<int>() ?? 0,
                current["ranking_in_tier"]?.Value<int>() ?? 0
            );
        }

        // ─── Maç Detay ──────────────────────────────────────────────────────────

        public async Task<MacDetay> GetMacDetayAsync(
            string matchId, string benAdi, string benTag,
            CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/valorant/v2/match/{matchId}";
            var root = await GetJsonAsync(url, ct);

            var data = root["data"];
            if (data == null) throw new Exception("Maç detayı bulunamadı.");

            var meta = data["metadata"];
            var players = data["players"];
            var teams = data["teams"];

            var detay = new MacDetay
            {
                MatchId = matchId,
                MapAdi = meta?["map"]?.ToString() ?? "",
                Mod = meta?["mode"]?.ToString() ?? "",
                MacZaman = meta?["game_start"]?.Value<long>() ?? 0,
            };

            // Takim skorlari
            var redTeam = teams?["red"];
            var blueTeam = teams?["blue"];
            bool redKazandi = redTeam?["has_won"]?.Value<bool>() ?? false;
            int redRound = redTeam?["rounds_won"]?.Value<int>() ?? 0;
            int blueRound = blueTeam?["rounds_won"]?.Value<int>() ?? 0;

            // Butun oyunculari parse et (v2: all_players + team field)
            var allPlayers = new List<MacDetayOyuncu>();
            var allPlayersToken = players?["all_players"];
            if (allPlayersToken != null)
            {
                foreach (var p in allPlayersToken)
                {
                    var teamStr = p["team"]?.ToString() ?? "";
                    var takimKirmizi = string.Equals(teamStr, "Red", StringComparison.OrdinalIgnoreCase);

                    var oyuncu = new MacDetayOyuncu
                    {
                        Puuid = p["puuid"]?.ToString() ?? "",
                        Ad = p["name"]?.ToString() ?? "",
                        Tag = p["tag"]?.ToString() ?? "",
                        Tier = p["currenttier"]?.Value<int>() ?? 0,
                        TierAdi = p["currenttier_patched"]?.ToString() ?? "",
                        KartUrl = p["assets"]?["card"]?["small"]?.ToString() ?? "",
                        AjanUrl = p["assets"]?["agent"]?["small"]?.ToString() ?? "",
                        TakimKirmizi = takimKirmizi,
                        Kills = p["stats"]?["kills"]?.Value<int>() ?? 0,
                        Deaths = p["stats"]?["deaths"]?.Value<int>() ?? 0,
                        Assists = p["stats"]?["assists"]?.Value<int>() ?? 0,
                        Headshots = p["stats"]?["headshots"]?.Value<int>() ?? 0,
                        Bodyshots = p["stats"]?["bodyshots"]?.Value<int>() ?? 0,
                        Legshots = p["stats"]?["legshots"]?.Value<int>() ?? 0,
                        Hasar = p["damage_made"]?.Value<double>() ?? 0,
                        HasarAlan = p["damage_received"]?.Value<int>() ?? 0,
                        RoundOynanan = meta?["rounds_played"]?.Value<int>() ?? 0,
                    };
                    oyuncu.AjanAdi = p["character"]?.ToString() ?? "";

                    oyuncu.IsCurrentUser = string.Equals(oyuncu.Ad, benAdi, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(oyuncu.Tag, benTag, StringComparison.OrdinalIgnoreCase);

                    allPlayers.Add(oyuncu);
                }
            }

            // Rounds: multi-kill + KAST hesapla
            var rounds = data["rounds"] as JArray;
            if (rounds != null && allPlayers.Count > 0)
            {
                var roundKillsPerPlayer = new Dictionary<string, Dictionary<int, int>>();
                var kastRoundsPerPlayer = new Dictionary<string, HashSet<int>>();
                foreach (var p in allPlayers)
                {
                    roundKillsPerPlayer[p.Puuid] = new Dictionary<int, int>();
                    kastRoundsPerPlayer[p.Puuid] = new HashSet<int>();
                }
                int totalRounds = rounds.Count;
                foreach (var r in rounds)
                {
                    var ps = r["player_stats"] as JArray;
                    if (ps == null) continue;
                    int roundNum = r["round"]?.Value<int>() ?? -1;
                    foreach (var psEntry in ps)
                    {
                        var puuid = psEntry["player_puuid"]?.ToString() ?? "";
                        if (!roundKillsPerPlayer.ContainsKey(puuid)) continue;
                        int kills = psEntry["kills"]?.Value<int>() ?? 0;
                        if (kills > 0 && roundNum >= 0)
                            roundKillsPerPlayer[puuid][roundNum] = kills;

                        int assists = psEntry["assists"]?.Value<int>() ?? 0;
                        bool survived = psEntry["survived"]?.Value<bool>() ?? false;
                        bool traded = psEntry["traded"]?.Value<bool>() ?? false;
                        if ((kills > 0 || assists > 0 || survived || traded) && roundNum >= 0)
                            kastRoundsPerPlayer[puuid].Add(roundNum);
                    }
                }
                // Track round MVP (most kills in each round)
                var roundMvpPuids = new Dictionary<int, string>(); // roundNum -> puuid
                foreach (var r in rounds)
                {
                    var ps = r["player_stats"] as JArray;
                    if (ps == null) continue;
                    int roundNum = r["round"]?.Value<int>() ?? -1;
                    string bestPuuid = null;
                    int bestKills = -1;
                    foreach (var psEntry in ps)
                    {
                        var puuid = psEntry["player_puuid"]?.ToString() ?? "";
                        int kills = psEntry["kills"]?.Value<int>() ?? 0;
                        if (kills > bestKills)
                        {
                            bestKills = kills;
                            bestPuuid = puuid;
                        }
                    }
                    if (bestPuuid != null && roundNum >= 0)
                        roundMvpPuids[roundNum] = bestPuuid;
                }

                foreach (var oyuncu in allPlayers)
                {
                    // Multi-kill (mutually exclusive)
                    foreach (var rk in roundKillsPerPlayer[oyuncu.Puuid].Values)
                    {
                        if (rk >= 5) oyuncu.Kill5k++;
                        else if (rk == 4) oyuncu.Kill4k++;
                        else if (rk == 3) oyuncu.Kill3k++;
                        else if (rk == 2) oyuncu.Kill2k++;
                    }
                    // KAST
                    if (totalRounds > 0)
                        oyuncu.Kast = Math.Round(kastRoundsPerPlayer[oyuncu.Puuid].Count / (double)totalRounds * 100, 1);
                    // MVP count (round EDO)
                    oyuncu.MvpSayisi = roundMvpPuids.Values.Count(p => p == oyuncu.Puuid);
                }
            }

            // Kullanici hangi takimda?
            var ben = allPlayers.FirstOrDefault(p => p.IsCurrentUser);
            if (ben != null)
            {
                detay.TakimOyuncular = allPlayers.Where(p => p.TakimKirmizi == ben.TakimKirmizi).ToList();
                detay.Rakipler = allPlayers.Where(p => p.TakimKirmizi != ben.TakimKirmizi).ToList();
            }
            else
            {
                detay.TakimOyuncular = allPlayers.Where(p => p.TakimKirmizi).ToList();
                detay.Rakipler = allPlayers.Where(p => !p.TakimKirmizi).ToList();
            }
            detay.TumOyuncular = allPlayers;
            detay.Ben = ben ?? allPlayers.FirstOrDefault();

            // Skor
            if (ben != null)
            {
                detay.SkorBizim = ben.TakimKirmizi ? redRound : blueRound;
                detay.SkorOnlar = ben.TakimKirmizi ? blueRound : redRound;
                detay.Kazandi = ben.TakimKirmizi ? redKazandi : !redKazandi;
            }
            else
            {
                detay.SkorBizim = redRound;
                detay.SkorOnlar = blueRound;
                detay.Kazandi = redKazandi;
            }

            // Round split (first half = first 12 rounds)
            if (rounds != null)
            {
                int totalRounds = rounds.Count;
                int ilkYariLimit = Math.Min(12, totalRounds);
                for (int i = 0; i < totalRounds; i++)
                {
                    var r = rounds[i];
                    string kazananTakim = r["winning_team"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(kazananTakim)) continue;
                    bool redWon = string.Equals(kazananTakim, "Red", StringComparison.OrdinalIgnoreCase);
                    bool bizKazandi = (ben?.TakimKirmizi == true) ? redWon : !redWon;
                    if (i < ilkYariLimit)
                    {
                        if (bizKazandi) detay.IlkYariBizim++;
                        else detay.IlkYariOnlar++;
                    }
                    else
                    {
                        if (bizKazandi) detay.IkinciYariBizim++;
                        else detay.IkinciYariOnlar++;
                    }
                }
            }

            // MVP: en yuksek KD
            if (allPlayers.Count > 0)
            {
                var enIyi = allPlayers.OrderByDescending(p => p.KdOran).FirstOrDefault();
                if (enIyi != null) enIyi.IsMvp = true;
            }

            // Swing: player's K/D compared to match average K/D (percentage)
            double matchAvgKd = allPlayers.Count > 0 ? allPlayers.Average(p => p.KdOran) : 1.0;
            foreach (var p in allPlayers)
            {
                if (matchAvgKd > 0)
                    p.Swing = Math.Round((p.KdOran - matchAvgKd) * 10, 2);
                else
                    p.Swing = 0;
            }

            return detay;
        }

        public void Dispose()
        {
            _http?.Dispose();
            _riotLiveMatch?.Dispose();
        }
    }
}
