using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    /// <summary>
    /// Henrik Dev API üzerinden maç geçmişi ve MMR geçmişi çeker.
    /// Tüm API çağrıları bu servisten yapılır — ileride değiştirmek için tek nokta.
    /// </summary>
    public class AnalizService : IDisposable
    {
        private const string ApiKey  = "HDEV-06d4da7c-c8ae-446d-a653-9277e0ea7cb1";
        private const string BaseUrl = "https://api.henrikdev.xyz";

        private readonly HttpClient _http;

        public AnalizService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            _http.DefaultRequestHeaders.Add("Authorization", ApiKey);
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ─── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Son maçları çeker ve AnalizMac listesine dönüştürür.
        /// GET /valorant/v3/matches/{region}/{name}/{tag}
        /// Detaylı veriler (headshot, flash, round, vb.) dahil.
        /// </summary>
        public async Task<List<AnalizMac>> GetMacGecmisiAsync(
            string region, string name, string tag,
            CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/valorant/v3/matches/{region}" +
                      $"/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}";

            var root = await GetJsonAsync(url, ct);
            var data = root["data"] as JArray;
            if (data == null) return new List<AnalizMac>();

            var result = new List<AnalizMac>();
            foreach (var mac in data)
            {
                try
                {
                    var meta    = mac["metadata"];
                    var players = mac["players"]?["all_players"] as JArray;
                    var teams   = mac["teams"];
                    var rounds  = mac["rounds"] as JArray;

                    // Oyuncuyu bul (case-insensitive)
                    var oyuncu = players?.FirstOrDefault(p =>
                        string.Equals(p["name"]?.ToString(), name, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p["tag"]?.ToString(), tag, StringComparison.OrdinalIgnoreCase));

                    if (oyuncu == null) continue;

                    var stats  = oyuncu["stats"];
                    var team   = oyuncu["team"]?.ToString() ?? "Blue";

                    // Kazanma durumu: teams.{team}.has_won
                    bool kazandi = false;
                    foreach (var t in teams?.Children<JProperty>() ?? Enumerable.Empty<JProperty>())
                    {
                        if (string.Equals(t.Name, team, StringComparison.OrdinalIgnoreCase))
                        {
                            kazandi = t.Value["has_won"]?.Value<bool>() ?? false;
                            break;
                        }
                    }

                    var mapAdi    = meta?["map"]?.ToString() ?? "Bilinmiyor";
                    var mod       = meta?["mode"]?.ToString() ?? meta?["queue"]?.ToString() ?? "";
                    // Henrik Dev v3: timestamp "game_start" alanında unix epoch olarak geliyor
                    long macZaman = meta?["game_start"]?.Value<long>() ?? 0;
                    int roundOynanan = 0;
                    if (rounds != null)
                    {
                        foreach (var rnd in rounds)
                        {
                            var rndTeam = rnd["team"]?.ToString();
                            if (string.Equals(rndTeam, team, StringComparison.OrdinalIgnoreCase))
                                roundOynanan++;
                        }
                    }

                    // Hasar detayları (body, head, leg)
                    var damage = oyuncu["damage"] ?? oyuncu["stats"]?["damage"];
                    int headshot = 0, bodyshot = 0, legshot = 0;
                    if (damage != null)
                    {
                        headshot = damage["bodyshots"]?.Value<int>() ?? 0; // API'de headshot bodyshots içinde olabilir
                        bodyshot = damage["bodyshots"]?.Value<int>() ?? 0;
                        legshot = damage["legshots"]?.Value<int>() ?? 0;
                        // Headshot genellikle bodyshots'tan ayrılmıyor, ama toplamdan çıkarabiliriz
                        var totalShots = damage["modes"]?["standard"]?["shots"]?.Value<int>() ?? 0;
                        if (totalShots > bodyshot + legshot)
                            headshot = totalShots - bodyshot - legshot;
                    }

                    // Flash/utility verileri
                    var loadout = oyuncu["ability_casts"]; // Flash, smoke, vb.
                    int flashed = 0, enemiesFlashed = 0;
                    if (loadout != null)
                    {
                        flashed = loadout["ability1_casts"]?.Value<int>() ?? 0
                                + loadout["ability2_casts"]?.Value<int>() ?? 0
                                + loadout["grenade_casts"]?.Value<int>() ?? 0;
                    }

                    // Maç verisinden estimated enemies flashed (basit hesaplama)
                    // Gerçek API'de bu veri "player_stats" içinde olabilir

                    // First death / clutch (rounds verisinden)
                    int firstDeath = 0, clutchWon = 0, clutchTotal = 0;
                    if (rounds != null && team.ToLower() == "blue")
                    {
                        foreach (var rnd in rounds)
                        {
                            var winner = rnd["winning_team"]?.ToString();
                            var oyuncuOid = oyuncu["puuid"]?.ToString();
                            var kills = rnd["kills"] as JArray;
                            if (kills != null)
                            {
                                foreach (var k in kills)
                                {
                                    if (k["finisher_puuid"]?.ToString() == oyuncuOid)
                                        clutchTotal++;
                                }
                            }
                        }
                    }

                    result.Add(new AnalizMac
                    {
                        MapAdi         = mapAdi,
                        AjanAdi        = oyuncu["character"]?.ToString() ?? "",
                        Mod            = mod,
                        Kazandi        = kazandi,
                        Kills          = stats?["kills"]?.Value<int>() ?? 0,
                        Deaths         = stats?["deaths"]?.Value<int>() ?? 0,
                        Assists        = stats?["assists"]?.Value<int>() ?? 0,
                        Hasar          = oyuncu["damage_made"]?.Value<double>() ?? 0,
                        RoundOynanan   = roundOynanan > 0 ? roundOynanan : 13, // Varsayılan
                        HeadshotCount  = headshot,
                        BodyshotCount  = bodyshot,
                        LegshotCount   = legshot,
                        FlashedCount   = flashed,
                        EnemiesFlashed = enemiesFlashed,
                        FirstDeathCount = firstDeath,
                        ClutchesWon    = clutchWon,
                        ClutchesTotal  = clutchTotal,
                        ACS            = (int)(oyuncu["stats"]?["score"]?.Value<int>() ?? 0) / (roundOynanan > 0 ? roundOynanan : 1),
                        MacZaman       = macZaman,
                        RrDegisim      = 0,
                        RrSonrasi      = 0
                    });
                }
                catch { /* tek maç parse hatası tüm listeyi bozmasın */ }
            }

            return result.Take(20).ToList();
        }

        /// <summary>
        /// MMR geçmişini çeker ve EloGrafikNokta listesi döndürür.
        /// GET /valorant/v1/mmr-history/{region}/{name}/{tag}
        /// ELO = tier_baz + (rank_no * 100) + rr
        /// Demir 1 = 0, Demir 2 = 100, Demir 3 = 200, Bronz 1 = 300 ...
        /// </summary>
        public async Task<(List<AnalizMac> MaclarRrIle, List<EloGrafikNokta> EloGrafik, EloOzet Ozet)> GetMmrGecmisiAsync(
            string region, string name, string tag,
            CancellationToken ct = default)
        {
            var url = $"{BaseUrl}/valorant/v1/mmr-history/{region}" +
                      $"/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}";

            var root = await GetJsonAsync(url, ct);
            var data = root["data"] as JArray;
            if (data == null) return (new List<AnalizMac>(), new List<EloGrafikNokta>(), new EloOzet());

            var maclar    = new List<AnalizMac>();
            var eloGrafik = new List<EloGrafikNokta>();

            foreach (var entry in data.Take(20))
            {
                try
                {
                    var rr        = entry["ranking_in_tier"]?.Value<int>() ?? 0;
                    var rrDegisim = entry["mmr_change_to_last_game"]?.Value<int>() ?? 0;
                    var tier      = entry["currenttierpatched"]?.ToString() ?? "";
                    // API'nin kendi elo alanı varsa kullan, yoksa hesapla
                    var apiElo    = entry["elo"]?.Value<int>() ?? 0;
                    int elo       = apiElo > 0 ? apiElo : HesaplaElo(tier, rr);

                    maclar.Add(new AnalizMac
                    {
                        RrDegisim = rrDegisim,
                        RrSonrasi = rr,
                        Kazandi   = rrDegisim >= 0
                    });

                    eloGrafik.Add(new EloGrafikNokta
                    {
                        MacIndex = eloGrafik.Count,
                        Elo      = elo,
                        RankAdi  = tier
                    });
                }
                catch { }
            }

            // Kronolojik sıra (eski → yeni)
            eloGrafik.Reverse();
            maclar.Reverse();

            // MacIndex'i yeniden ata
            for (int i = 0; i < eloGrafik.Count; i++)
                eloGrafik[i].MacIndex = i + 1;

            // Özet
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

        /// <summary>
        /// Tier adı + RR'dan toplam ELO hesaplar.
        /// Her tier 300 puan, her rank içi 100 puan.
        /// Demir 1 = 0 ... Radyant = 2700+
        /// </summary>
        public static int HesaplaElo(string tier, int rr)
        {
            if (string.IsNullOrEmpty(tier)) return rr;
            var t = tier.ToLowerInvariant();

            // Tier baz değerleri (her tier 300, her rank 100)
            int baz = 0;
            if      (t.Contains("iron 1"))        baz = 0;
            else if (t.Contains("iron 2"))        baz = 100;
            else if (t.Contains("iron 3"))        baz = 200;
            else if (t.Contains("bronze 1"))      baz = 300;
            else if (t.Contains("bronze 2"))      baz = 400;
            else if (t.Contains("bronze 3"))      baz = 500;
            else if (t.Contains("silver 1"))      baz = 600;
            else if (t.Contains("silver 2"))      baz = 700;
            else if (t.Contains("silver 3"))      baz = 800;
            else if (t.Contains("gold 1"))        baz = 900;
            else if (t.Contains("gold 2"))        baz = 1000;
            else if (t.Contains("gold 3"))        baz = 1100;
            else if (t.Contains("platinum 1"))    baz = 1200;
            else if (t.Contains("platinum 2"))    baz = 1300;
            else if (t.Contains("platinum 3"))    baz = 1400;
            else if (t.Contains("diamond 1"))     baz = 1500;
            else if (t.Contains("diamond 2"))     baz = 1600;
            else if (t.Contains("diamond 3"))     baz = 1700;
            else if (t.Contains("ascendant 1"))   baz = 1800;
            else if (t.Contains("ascendant 2"))   baz = 1900;
            else if (t.Contains("ascendant 3"))   baz = 2000;
            else if (t.Contains("immortal 1"))    baz = 2100;
            else if (t.Contains("immortal 2"))    baz = 2200;
            else if (t.Contains("immortal 3"))    baz = 2300;
            else if (t.Contains("radiant"))       baz = 2400;
            // Türkçe karşılıklar
            else if (t.Contains("demir 1"))       baz = 0;
            else if (t.Contains("demir 2"))       baz = 100;
            else if (t.Contains("demir 3"))       baz = 200;
            else if (t.Contains("bronz 1"))       baz = 300;
            else if (t.Contains("bronz 2"))       baz = 400;
            else if (t.Contains("bronz 3"))       baz = 500;
            else if (t.Contains("iron"))          baz = 0;
            else if (t.Contains("bronze"))        baz = 300;
            else if (t.Contains("silver"))        baz = 600;
            else if (t.Contains("gold"))          baz = 900;
            else if (t.Contains("platinum"))      baz = 1200;
            else if (t.Contains("diamond"))       baz = 1500;
            else if (t.Contains("ascendant"))     baz = 1800;
            else if (t.Contains("immortal"))      baz = 2100;
            else if (t.Contains("radiant"))       baz = 2400;

            return baz + rr;
        }

        /// <summary>
        /// Maç geçmişi + MMR geçmişini birleştirip tam AnalizMac listesi döndürür.
        /// </summary>
        public async Task<(List<AnalizMac> Maclar, List<EloGrafikNokta> EloGrafik, EloOzet EloOzet, AnalizOzet Ozet)>
            GetTamAnalizAsync(string region, string name, string tag, CancellationToken ct = default)
        {
            // Paralel çek
            var macTask = GetMacGecmisiAsync(region, name, tag, ct);
            var mmrTask = GetMmrGecmisiAsync(region, name, tag, ct);

            await Task.WhenAll(macTask, mmrTask);

            var maclar = macTask.Result;
            var (mmrMaclar, eloGrafik, eloOzet) = mmrTask.Result;

            // RR bilgilerini maç listesine ekle (index eşleştir)
            for (int i = 0; i < maclar.Count && i < mmrMaclar.Count; i++)
            {
                maclar[i].RrDegisim = mmrMaclar[i].RrDegisim;
                maclar[i].RrSonrasi = mmrMaclar[i].RrSonrasi;
            }

            // Özet hesapla
            var ozet = new AnalizOzet
            {
                ToplamMac     = maclar.Count,
                KazanmaOrani  = maclar.Count > 0 ? maclar.Count(m => m.Kazandi) * 100.0 / maclar.Count : 0,
                OrtalamaKda   = maclar.Count > 0 ? maclar.Average(m => m.Kda) : 0,
                OrtalamaHasar = maclar.Count > 0 ? maclar.Average(m => m.Hasar) : 0
            };

            return (maclar, eloGrafik, eloOzet, ozet);
        }

        // ─── BÖLÜM 2: Performans Metrikleri ────────────────────────────────────────

        /// <summary>
        /// Maç geçmişinden 9 performans metriği hesaplar.
        /// </summary>
        public List<PerformansMetrik> GetPerformansMetrikleri(List<AnalizMac> maclar)
        {
            if (maclar == null || !maclar.Any())
                return new List<PerformansMetrik>();

            var metrikler = new List<PerformansMetrik>();

            // Son 5 maç vs önceki 5 maç karşılaştırması için
            var son5   = maclar.Take(5).ToList();
            var once5  = maclar.Skip(5).Take(5).ToList();

            double DegisimHesapla(double yeni, double eski)
                => eski > 0 ? Math.Round(yeni - eski, 2) : 0;

            // 1. Kazanma Oranı
            double kazanc     = maclar.Count(m => m.Kazandi) * 100.0 / maclar.Count;
            double kazancSon  = son5.Any()  ? son5.Count(m => m.Kazandi)  * 100.0 / son5.Count  : kazanc;
            double kazancOnce = once5.Any() ? once5.Count(m => m.Kazandi) * 100.0 / once5.Count : kazanc;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Kazanma Oranı", Deger = kazanc, Maksimum = 100, Format = "%",
                Degisim = DegisimHesapla(kazancSon, kazancOnce)
            });

            // 2. ADR
            double adr     = maclar.Average(m => m.Hasar);
            double adrSon  = son5.Any()  ? son5.Average(m => m.Hasar)  : adr;
            double adrOnce = once5.Any() ? once5.Average(m => m.Hasar) : adr;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "ADR", Deger = adr, Maksimum = 250, Format = "F0",
                Degisim = DegisimHesapla(adrSon, adrOnce)
            });

            // 3. K/R (Kill per Round)
            double kr     = maclar.Sum(m => m.Kills) / (double)Math.Max(1, maclar.Sum(m => m.RoundOynanan));
            double krSon  = son5.Any()  ? son5.Sum(m => m.Kills)  / (double)Math.Max(1, son5.Sum(m => m.RoundOynanan))  : kr;
            double krOnce = once5.Any() ? once5.Sum(m => m.Kills) / (double)Math.Max(1, once5.Sum(m => m.RoundOynanan)) : kr;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "K/R", Deger = kr, Maksimum = 1.5, Format = "F2",
                Degisim = DegisimHesapla(krSon, krOnce)
            });

            // 4. Bitiriş Başarısı (KDA)
            double kda     = maclar.Average(m => m.Kda);
            double kdaSon  = son5.Any()  ? son5.Average(m => m.Kda)  : kda;
            double kdaOnce = once5.Any() ? once5.Average(m => m.Kda) : kda;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Bitiriş Başarısı", Deger = kda, Maksimum = 4.0, Format = "F2",
                Degisim = DegisimHesapla(kdaSon, kdaOnce)
            });

            // 5. Giriş Başarılı (Kill/Maç)
            double entry     = maclar.Average(m => m.Kills);
            double entrySon  = son5.Any()  ? son5.Average(m => m.Kills)  : entry;
            double entryOnce = once5.Any() ? once5.Average(m => m.Kills) : entry;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Giriş Başarılı", Deger = entry, Maksimum = 25.0, Format = "F1",
                Degisim = DegisimHesapla(entrySon, entryOnce)
            });

            // 6. Headshot %
            int toplamVurus = maclar.Sum(m => m.HeadshotCount + m.BodyshotCount + m.LegshotCount);
            double hs       = toplamVurus > 0 ? maclar.Sum(m => m.HeadshotCount) * 100.0 / toplamVurus : 0;
            int tvSon       = son5.Sum(m => m.HeadshotCount + m.BodyshotCount + m.LegshotCount);
            double hsSon    = tvSon > 0 ? son5.Sum(m => m.HeadshotCount) * 100.0 / tvSon : hs;
            int tvOnce      = once5.Sum(m => m.HeadshotCount + m.BodyshotCount + m.LegshotCount);
            double hsOnce   = tvOnce > 0 ? once5.Sum(m => m.HeadshotCount) * 100.0 / tvOnce : hs;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Headshot %", Deger = hs, Maksimum = 100, Format = "%",
                Degisim = DegisimHesapla(hsSon, hsOnce)
            });

            // 7. Flaş Başarı (flaş/maç)
            double flash     = maclar.Average(m => m.FlashedCount);
            double flashSon  = son5.Any()  ? son5.Average(m => m.FlashedCount)  : flash;
            double flashOnce = once5.Any() ? once5.Average(m => m.FlashedCount) : flash;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Flaş Başarı", Deger = flash, Maksimum = 15.0, Format = "F1",
                Degisim = DegisimHesapla(flashSon, flashOnce)
            });

            // 8. Round Başına Flaş
            double rndFlash     = maclar.Sum(m => m.FlashedCount) / (double)Math.Max(1, maclar.Sum(m => m.RoundOynanan));
            double rndFlashSon  = son5.Any()  ? son5.Sum(m => m.FlashedCount)  / (double)Math.Max(1, son5.Sum(m => m.RoundOynanan))  : rndFlash;
            double rndFlashOnce = once5.Any() ? once5.Sum(m => m.FlashedCount) / (double)Math.Max(1, once5.Sum(m => m.RoundOynanan)) : rndFlash;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Round Başına Flaş", Deger = rndFlash, Maksimum = 1.0, Format = "F2",
                Degisim = DegisimHesapla(rndFlashSon, rndFlashOnce)
            });

            // 9. Yardımcı Hasar/Tur (Asist/Round)
            double asst     = maclar.Sum(m => m.Assists) / (double)Math.Max(1, maclar.Sum(m => m.RoundOynanan));
            double asstSon  = son5.Any()  ? son5.Sum(m => m.Assists)  / (double)Math.Max(1, son5.Sum(m => m.RoundOynanan))  : asst;
            double asstOnce = once5.Any() ? once5.Sum(m => m.Assists) / (double)Math.Max(1, once5.Sum(m => m.RoundOynanan)) : asst;
            metrikler.Add(new PerformansMetrik
            {
                Adi = "Yardımcı Hasar/Tur", Deger = asst, Maksimum = 0.8, Format = "F2",
                Degisim = DegisimHesapla(asstSon, asstOnce)
            });

            return metrikler;
        }

        // ─── BÖLÜM 4: ELO İlerleme (GetMmrGecmisiAsync içinde hesaplanır) ──────────

        // ─── BÖLÜM 6: Aktivite ──────────────────────────────────────────────────

        /// <summary>
        /// Maç geçmişinden saatlik aktivite (0-23) hesaplar.
        /// </summary>
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

            double ort = gecerli.Count > 0 ? gecerli.Count / 24.0 : 0;
            return (liste, new AktiviteGrafikOzet { OrtalamaGunlukMac = Math.Round(ort, 1) });
        }

        /// <summary>
        /// Maç geçmişinden haftalık aktivite (Pzt=0 … Paz=6) hesaplar.
        /// </summary>
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
                // DayOfWeek: Sunday=0, Monday=1 … Saturday=6 → Pzt=0 için dönüştür
                int idx = ((int)dt.DayOfWeek + 6) % 7;
                liste[idx].MacSayisi++;
                if (mac.Kazandi) liste[idx].Galibiyet++;
            }

            double ort = gecerli.Count > 0 ? gecerli.Count / 7.0 : 0;
            return (liste, new AktiviteGrafikOzet { OrtalamaHaftalikMac = Math.Round(ort, 1) });
        }

        /// <summary>
        /// Maç geçmişinden GitHub tarzı aktivite takvimi oluşturur.
        /// Son ~6 ay kapsanır, her gün için AktiviteHucre döner.
        /// </summary>
        public (List<TakvimHaftasi> Haftalar, List<TakvimAyBaslik> AyBasliklari) GetAktiviteTakvimi(List<AnalizMac> maclar)
        {
            // Tarih aralığı: bugünden geriye 26 hafta (~6 ay)
            var bugun    = DateTime.Today;
            var baslangic = bugun.AddDays(-((int)bugun.DayOfWeek == 0 ? 6 : (int)bugun.DayOfWeek - 1)); // bu haftanın Pazartesi
            baslangic = baslangic.AddDays(-25 * 7); // 26 hafta geri

            // Maçları güne göre grupla
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

            // Max maç sayısı (yoğunluk için)
            int maxMac = gunMap.Count > 0 ? gunMap.Values.Max(v => v.Mac) : 1;

            var haftalar    = new List<TakvimHaftasi>();
            var ayBasliklari = new List<TakvimAyBaslik>();
            int sutunSayac  = 0;
            int oncekiAy    = -1;

            var haftaBaslangic = baslangic;
            while (haftaBaslangic <= bugun)
            {
                var hafta = new TakvimHaftasi();
                for (int g = 0; g < 7; g++)
                {
                    var gun = haftaBaslangic.AddDays(g);
                    if (gun > bugun) break;

                    gunMap.TryGetValue(gun, out var veri);
                    int yogunluk = 0;
                    if (veri.Mac > 0)
                    {
                        double oran = (double)veri.Mac / maxMac;
                        yogunluk = oran >= 0.66 ? 3 : oran >= 0.33 ? 2 : 1;
                    }

                    hafta.Gunler[g] = new AktiviteHucre
                    {
                        Yil       = gun.Year,
                        Ay        = gun.Month,
                        Gun       = gun.Day,
                        MacSayisi = veri.Mac,
                        Galibiyet = veri.Galibiyet,
                        Yogunluk  = yogunluk,
                        TarihText = gun.ToString("dd MMM")
                    };

                    // Ay başlığı
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

        // ─── BÖLÜM 5: Harita İstatistikleri ────────────────────────────────────────

        /// <summary>
        /// Maç geçmişinden harita bazlı istatistikler.
        /// </summary>
        public List<HaritaIstatistik> GetHaritaIstatistikleri(List<AnalizMac> maclar)
        {
            if (maclar == null || !maclar.Any())
                return new List<HaritaIstatistik>();

            // Harita başına ham sayıları topla
            var haritaDict = new Dictionary<string, (int Oynanan, int Kazanilan, double ToplamHasar, List<bool> SonMaclar)>();

            foreach (var mac in maclar)
            {
                var map = mac.MapAdi;
                if (string.IsNullOrEmpty(map) || map == "Bilinmiyor") continue;

                if (!haritaDict.ContainsKey(map))
                    haritaDict[map] = (0, 0, 0, new List<bool>());

                var (oynanan, kazanilan, toplamHasar, sonMaclar) = haritaDict[map];
                sonMaclar.Add(mac.Kazandi);
                haritaDict[map] = (
                    oynanan + 1,
                    kazanilan + (mac.Kazandi ? 1 : 0),
                    toplamHasar + mac.Hasar,
                    sonMaclar
                );
            }

            var sonuc = haritaDict
                .OrderByDescending(kv => kv.Value.Oynanan)
                .Select(kv =>
                {
                    var (oynanan, kazanilan, toplamHasar, sonMaclar) = kv.Value;
                    double galibiyetYuzde = oynanan > 0 ? kazanilan * 100.0 / oynanan : 0;
                    var sonUcMac = sonMaclar.Count > 3 ? sonMaclar.TakeLast(3).ToList() : sonMaclar;

                    return new HaritaIstatistik
                    {
                        HaritaAdi      = kv.Key,
                        OynanmaSayisi  = oynanan,
                        GalibiyetOrani = galibiyetYuzde,
                        ADR            = oynanan > 0 ? toplamHasar / oynanan : 0,
                        SonMaclar      = sonUcMac
                    };
                })
                .ToList();

            return sonuc;
        }

        // ─── HTTP ───────────────────────────────────────────────────────────────

        private async Task<JObject> GetJsonAsync(string url, CancellationToken ct)
        {
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

        // ─── Yardımcı ───────────────────────────────────────────────────────────

        private static string MapRutbe(string tier)
        {
            if (string.IsNullOrEmpty(tier)) return tier;
            var l = tier.ToLowerInvariant();
            if (l.Contains("radiant"))   return "Radyant";
            if (l.Contains("immortal"))  return "Ölümsüz";
            if (l.Contains("ascendant")) return "Yükselen";
            if (l.Contains("diamond"))   return "Elmas";
            if (l.Contains("platinum"))  return "Platin";
            if (l.Contains("gold"))      return "Altın";
            if (l.Contains("silver"))    return "Gümüş";
            if (l.Contains("bronze"))    return "Bronz";
            if (l.Contains("iron"))      return "Demir";
            return tier;
}

        public void Dispose() => _http?.Dispose();
    }
}
