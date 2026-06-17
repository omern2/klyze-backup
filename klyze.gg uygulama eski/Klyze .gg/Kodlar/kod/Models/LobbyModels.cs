using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ValorantAutoClicker.Models
{
    // ─── Rütbe Sistemi ────────────────────────────────────────────────────────────
    public static class Rutbeler
    {
        public static readonly string[] Liste = 
        {
            "Demir", "Bronz", "Gümüş", "Altın", "Platin", "Elmas", "Ölümsüz", "Radyant"
        };

        public static int Index(string rutbe)
        {
            var idx = Array.IndexOf(Liste, rutbe);
            return idx < 0 ? 0 : idx;
        }

        // Tolerans: kaç rütbe üst/alt eşleşebilir
        public const int Tolerans = 1;

        public static bool Uyumlu(string arayanRutbe, string lobiRutbe)
        {
            int a = Index(arayanRutbe);
            int b = Index(lobiRutbe);
            return Math.Abs(a - b) <= Tolerans;
        }
    }

    // ─── Oyuncu ───────────────────────────────────────────────────────────────────
    public class OyuncuData
    {
        [JsonProperty("ad")]
        public string Ad { get; set; } = "Oyuncu";

        [JsonProperty("rutbe")]
        public string Rutbe { get; set; } = "Altın";

        [JsonProperty("rutbePuani")]
        public int RutbePuani { get; set; } = 0;

        [JsonProperty("ping")]
        public int Ping { get; set; } = 0;
    }

    // ─── Lobi ─────────────────────────────────────────────────────────────────────
    public class LobiData
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("grupKodu")]
        public string GrupKodu { get; set; } = "";

        [JsonProperty("olusturan")]
        public string Olusturan { get; set; } = "";

        [JsonProperty("rutbe")]
        public string Rutbe { get; set; } = "Altın";

        [JsonProperty("rutbePuani")]
        public int RutbePuani { get; set; } = 0;

        [JsonProperty("maxOyuncu")]
        public int MaxOyuncuJson { get; set; } = 5;

        [JsonProperty("oyuncular")]
        public List<OyuncuData> Oyuncular { get; set; } = new();

        [JsonProperty("durum")]
        public string Durum { get; set; } = "bekliyor";

        [JsonProperty("olusturmaZamani")]
        public long OlusturmaZamani { get; set; } = 0;

        [JsonIgnore]
        public int MaxOyuncu => 5;

        [JsonIgnore]
        public bool Dolu => Oyuncular.Count >= MaxOyuncu;
    }

    // ─── JSON Kök ─────────────────────────────────────────────────────────────────
    public class LobilerJson
    {
        [JsonProperty("lobiler")]
        public List<LobiData> Lobiler { get; set; } = new();
    }

    // ─── UI Modeli (ViewModel için) ───────────────────────────────────────────────
    public class OyuncuSlot
    {
        public bool Dolu { get; set; }
        public OyuncuData Oyuncu { get; set; }
    }
}
