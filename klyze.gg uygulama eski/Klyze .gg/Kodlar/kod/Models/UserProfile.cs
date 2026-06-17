using System;
using Newtonsoft.Json;

namespace ValorantAutoClicker.Models
{
    /// <summary>
    /// Giriş yapan kullanıcının profili — data/user.json'a kaydedilir.
    /// </summary>
    public class UserProfile
    {
        [JsonProperty("oyuncuAdi")]
        public string OyuncuAdi { get; set; } = "";

        [JsonProperty("tag")]
        public string Tag { get; set; } = "";

        [JsonProperty("bolge")]
        public string Bolge { get; set; } = "eu";

        [JsonProperty("rutbe")]
        public string Rutbe { get; set; } = "Altın";

        [JsonProperty("rutbePuani")]
        public int RutbePuani { get; set; } = 0;

        [JsonProperty("kazanmaOrani")]
        public double KazanmaOrani { get; set; } = 0;

        [JsonProperty("enCokOynadigiAjan")]
        public string EnCokOynadigiAjan { get; set; } = "";

        [JsonProperty("kdOrani")]
        public double KdOrani { get; set; } = 0;

        [JsonProperty("acs")]
        public double Acs { get; set; } = 0;

        [JsonProperty("hesapSeviyesi")]
        public int HesapSeviyesi { get; set; } = 0;

        [JsonProperty("sonGuncelleme")]
        public long SonGuncelleme { get; set; } = 0;

        // Hesaplanan özellikler (JSON'a yazılmaz)
        [JsonIgnore]
        public string RiotId => string.IsNullOrEmpty(Tag) ? OyuncuAdi : $"{OyuncuAdi}#{Tag}";

        [JsonIgnore]
        public bool GecerliMi => !string.IsNullOrEmpty(OyuncuAdi) && !string.IsNullOrEmpty(Tag);

        /// <summary>
        /// Tracker.gg profilinden UserProfile oluşturur.
        /// </summary>
        public static UserProfile FromTrackerProfile(TrackerPlayerProfile profile)
        {
            // Rütbe adını Türkçe karşılığına çevir
            var rutbe = MapRutbe(profile.CurrentRankName);

            return new UserProfile
            {
                OyuncuAdi = profile.GameName,
                Tag = profile.TagLine,
                Rutbe = rutbe,
                RutbePuani = profile.CurrentRankRating,
                KazanmaOrani = profile.WinRate,
                EnCokOynadigiAjan = profile.TopAgents?.Count > 0
                    ? profile.TopAgents[0].AgentName
                    : "",
                KdOrani = profile.KdRatio,
                Acs = profile.Acs,
                HesapSeviyesi = profile.AccountLevel,
                SonGuncelleme = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        private static string MapRutbe(string trackerRank)
        {
            if (string.IsNullOrEmpty(trackerRank)) return "Altın";

            var lower = trackerRank.ToLowerInvariant();

            if (lower.Contains("radiant") || lower.Contains("radyant")) return "Radyant";
            if (lower.Contains("immortal") || lower.Contains("ölümsüz") || lower.Contains("olmsuz")) return "Ölümsüz";
            if (lower.Contains("ascendant") || lower.Contains("yükselen")) return "Elmas"; // Ascendant → Elmas yakın
            if (lower.Contains("diamond") || lower.Contains("elmas")) return "Elmas";
            if (lower.Contains("platinum") || lower.Contains("platin")) return "Platin";
            if (lower.Contains("gold") || lower.Contains("altın") || lower.Contains("altin")) return "Altın";
            if (lower.Contains("silver") || lower.Contains("gümüş") || lower.Contains("gumus")) return "Gümüş";
            if (lower.Contains("bronze") || lower.Contains("bronz")) return "Bronz";
            if (lower.Contains("iron") || lower.Contains("demir")) return "Demir";

            return "Altın"; // varsayılan
        }
    }
}
