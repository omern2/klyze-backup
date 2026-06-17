using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Helpers
{
    public static class RankIkonHelper
    {
        private static readonly Dictionary<string, string> RankDosyaMap = new(StringComparer.OrdinalIgnoreCase)
        {
            {"demir 1", "dermir 1.png"}, {"demir 2", "dermir 2.png"}, {"demir 3", "dermir 3.png"}, {"demir", "dermir 1.png"},
            {"iron 1", "dermir 1.png"}, {"iron 2", "dermir 2.png"}, {"iron 3", "dermir 3.png"}, {"iron", "dermir 1.png"},
            {"bronz 1", "bronz 1.png"}, {"bronz 2", "bronz 2.png"}, {"bronz 3", "bronz 3.png"}, {"bronz", "bronz 1.png"},
            {"bronze 1", "bronz 1.png"}, {"bronze 2", "bronz 2.png"}, {"bronze 3", "bronz 3.png"}, {"bronze", "bronz 1.png"},
            {"gümüş 1", "gümüş 1.png"}, {"gümüş 2", "gümüş 2.png"}, {"gümüş 3", "gümüş 3.png"}, {"gümüş", "gümüş 1.png"},
            {"gumus 1", "gümüş 1.png"}, {"gumus 2", "gümüş 2.png"}, {"gumus 3", "gümüş 3.png"},
            {"silver 1", "gümüş 1.png"}, {"silver 2", "gümüş 2.png"}, {"silver 3", "gümüş 3.png"}, {"silver", "gümüş 1.png"},
            {"altın 1", "altın 1.png"}, {"altın 2", "altın 2.png"}, {"altın 3", "altın 3.png"}, {"altın", "altın 1.png"},
            {"altin 1", "altın 1.png"}, {"altin 2", "altın 2.png"}, {"altin 3", "altın 3.png"},
            {"gold 1", "altın 1.png"}, {"gold 2", "altın 2.png"}, {"gold 3", "altın 3.png"}, {"gold", "altın 1.png"},
            {"platin 1", "plat 1.png"}, {"platin 2", "plat 2.png"}, {"platin 3", "plat 3.png"}, {"platin", "plat 1.png"},
            {"platinum 1", "plat 1.png"}, {"platinum 2", "plat 2.png"}, {"platinum 3", "plat 3.png"}, {"platinum", "plat 1.png"},
            {"elmas 1", "elmas 1.png"}, {"elmas 2", "elmas 2.png"}, {"elmas 3", "elmas 3.png"}, {"elmas", "elmas 1.png"},
            {"diamond 1", "elmas 1.png"}, {"diamond 2", "elmas 2.png"}, {"diamond 3", "elmas 3.png"}, {"diamond", "elmas 1.png"},
            {"yücelik 1", "yücelik 1.png"}, {"yücelik 2", "yücelik 2.png"}, {"yücelik 3", "yücelik 3.png"}, {"yücelik", "yücelik 1.png"},
            {"yucelik 1", "yücelik 1.png"}, {"yucelik 2", "yücelik 2.png"}, {"yucelik 3", "yücelik 3.png"},
            {"ascendant 1", "yücelik 1.png"}, {"ascendant 2", "yücelik 2.png"}, {"ascendant 3", "yücelik 3.png"}, {"ascendant", "yücelik 1.png"},
            {"yükselen 1", "yücelik 1.png"}, {"yükselen 2", "yücelik 2.png"}, {"yükselen 3", "yücelik 3.png"}, {"yükselen", "yücelik 1.png"},
            {"ölümsüz 1", "immo 1.png"}, {"ölümsüz 2", "immo 2.png"}, {"ölümsüz 3", "immo 3.png"}, {"ölümsüz", "immo 1.png"},
            {"olumsuz 1", "immo 1.png"}, {"olumsuz 2", "immo 2.png"}, {"olumsuz 3", "immo 3.png"},
            {"immortal 1", "immo 1.png"}, {"immortal 2", "immo 2.png"}, {"immortal 3", "immo 3.png"}, {"immortal", "immo 1.png"},
            {"radyant", "radi.png"}, {"radiant", "radi.png"},
        };

        private static readonly (int Esik, string Ad)[] RrRankMap =
        {
            (2400, "Radyant"),
            (2300, "Ölümsüz 3"), (2200, "Ölümsüz 2"), (2100, "Ölümsüz 1"),
            (2000, "Yücelik 3"), (1900, "Yücelik 2"), (1800, "Yücelik 1"),
            (1700, "Elmas 3"),   (1600, "Elmas 2"),   (1500, "Elmas 1"),
            (1400, "Platin 3"),  (1300, "Platin 2"),  (1200, "Platin 1"),
            (1100, "Altın 3"),   (1000, "Altın 2"),   (900,  "Altın 1"),
            (800,  "Gümüş 3"),   (700,  "Gümüş 2"),   (600,  "Gümüş 1"),
            (500,  "Bronz 3"),   (400,  "Bronz 2"),   (300,  "Bronz 1"),
            (200,  "Demir 3"),   (100,  "Demir 2"),   (0,    "Demir 1"),
        };

        public static string RrdenRankAdi(int rr)
        {
            foreach (var (esik, ad) in RrRankMap)
                if (rr >= esik) return ad;
            return "Demir 1";
        }

        public static ImageSource RankIkonRr(int rr) => RankIkon(RrdenRankAdi(rr));

        private static string _ranklarKlasor;
        private static readonly Dictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        static RankIkonHelper()
        {
            _ranklarKlasor = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ranklar");
        }

        public static string RankDosyaAdi(string rankAdi)
        {
            if (string.IsNullOrWhiteSpace(rankAdi)) return null;
            var trimmed = rankAdi.Trim();
            if (RankDosyaMap.TryGetValue(trimmed, out var dosya))
                return dosya;
            foreach (var kvp in RankDosyaMap)
            {
                if (trimmed.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }

        public static string RankDosyaYolu(string rankAdi)
        {
            var dosya = RankDosyaAdi(rankAdi);
            if (dosya == null) return null;
            var yol = Path.Combine(_ranklarKlasor, dosya);
            return File.Exists(yol) ? yol : null;
        }

        public static ImageSource RankIkon(string rankAdi)
        {
            if (string.IsNullOrWhiteSpace(rankAdi)) return null;
            if (_iconCache.TryGetValue(rankAdi, out var cached))
                return cached;

            var dosyaYolu = RankDosyaYolu(rankAdi);
            if (dosyaYolu == null) return null;

            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(dosyaYolu);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.DecodePixelWidth = 24;
                img.EndInit();
                img.Freeze();
                _iconCache[rankAdi] = img;
                return img;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Rank ikonunu belirtilen genişlikte yükler (büyük gösterim için).
        /// </summary>
        public static ImageSource RankIkonBuyuk(string rankAdi, int pixelWidth = 96)
        {
            if (string.IsNullOrWhiteSpace(rankAdi)) return null;

            var dosyaYolu = RankDosyaYolu(rankAdi);
            if (dosyaYolu == null) return null;

            var cacheKey = $"{rankAdi}_large_{pixelWidth}";
            if (_iconCache.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(dosyaYolu);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.DecodePixelWidth = pixelWidth;
                img.EndInit();
                img.Freeze();
                _iconCache[cacheKey] = img;
                return img;
            }
            catch
            {
                return null;
            }
        }

        // ─── Sayisal Tier Eslestirmesi (currenttier) ───────────────────────────────

        private static readonly Dictionary<int, string> TierDosyaMap = new()
        {
            {3, "dermir 1.png"}, {4, "dermir 2.png"}, {5, "dermir 3.png"},
            {6, "bronz 1.png"},  {7, "bronz 2.png"},  {8, "bronz 3.png"},
            {9, "gümüş 1.png"},  {10, "gümüş 2.png"}, {11, "gümüş 3.png"},
            {12, "altın 1.png"}, {13, "altın 2.png"}, {14, "altın 3.png"},
            {15, "plat 1.png"},  {16, "plat 2.png"},  {17, "plat 3.png"},
            {18, "elmas 1.png"}, {19, "elmas 2.png"}, {20, "elmas 3.png"},
            {21, "immo 1.png"},  {22, "immo 2.png"},  {23, "immo 3.png"},
            {24, "yücelik 1.png"}, {25, "yücelik 2.png"}, {26, "yücelik 3.png"},
            {27, "radi.png"},
        };

        public static string TierdenDosyaAdi(int tier)
        {
            if (TierDosyaMap.TryGetValue(tier, out var dosya)) return dosya;
            return null;
        }

        public static string TierdenRutbeAdi(int tier)
        {
            return tier switch
            {
                3 or 4 or 5 => "Demir",
                6 or 7 or 8 => "Bronz",
                9 or 10 or 11 => "Gümüş",
                12 or 13 or 14 => "Altın",
                15 or 16 or 17 => "Platin",
                18 or 19 or 20 => "Elmas",
                21 or 22 or 23 => "Ölümsüz",
                24 or 25 or 26 => "Yükselen",
                27 => "Radyant",
                _ => "Altın"
            };
        }

        private static ImageSource Yukle(string dosyaAdi, string cacheKey, int pixelWidth)
        {
            var yol = Path.Combine(_ranklarKlasor, dosyaAdi);
            if (!File.Exists(yol)) return null;

            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(yol);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.DecodePixelWidth = pixelWidth;
                img.EndInit();
                img.Freeze();
                _iconCache[cacheKey] = img;
                return img;
            }
            catch
            {
                return null;
            }
        }

        public static ImageSource RankIkonFromTier(int tier, int pixelWidth = 24)
        {
            var dosyaAdi = TierdenDosyaAdi(tier);
            if (dosyaAdi == null) return null;

            var cacheKey = $"tier_{tier}_{pixelWidth}";
            if (_iconCache.TryGetValue(cacheKey, out var cached))
                return cached;

            return Yukle(dosyaAdi, cacheKey, pixelWidth);
        }

        public static ImageSource RankIkonFromTierBuyuk(int tier, int pixelWidth = 96)
        {
            var dosyaAdi = TierdenDosyaAdi(tier);
            if (dosyaAdi == null) return null;

            var cacheKey = $"tier_{tier}_large_{pixelWidth}";
            if (_iconCache.TryGetValue(cacheKey, out var cached))
                return cached;

            return Yukle(dosyaAdi, cacheKey, pixelWidth);
        }
    }
}
