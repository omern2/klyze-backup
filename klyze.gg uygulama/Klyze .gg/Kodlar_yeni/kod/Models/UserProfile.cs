using System;
using System.Windows.Media;
using Newtonsoft.Json;
using ValorantAutoClicker.Helpers;

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

        [JsonProperty("currentTier")]
        public int CurrentTier { get; set; } = 0;

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

        [JsonProperty("cardSmallUrl")]
        public string CardSmallUrl { get; set; } = "";

        [JsonProperty("sonGuncelleme")]
        public long SonGuncelleme { get; set; } = 0;

        // Hesaplanan özellikler (JSON'a yazılmaz)
        [JsonIgnore]
        public string RiotId => string.IsNullOrEmpty(Tag) ? OyuncuAdi : $"{OyuncuAdi}#{Tag}";

        [JsonIgnore]
        public bool GecerliMi => !string.IsNullOrEmpty(OyuncuAdi) && !string.IsNullOrEmpty(Tag);

        [JsonIgnore]
        public ImageSource RankIkonKaynak => RankIkonHelper.RankIkonFromTier(CurrentTier);
    }
}
