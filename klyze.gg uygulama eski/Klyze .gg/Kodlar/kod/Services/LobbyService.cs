using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    /// <summary>
    /// Lobi veri katmanı — şu an local JSON, ileride Firebase'e taşınabilir.
    /// Sadece bu dosyayı değiştirerek backend'i değiştirebilirsin.
    /// </summary>
    public class LobbyService
    {
        private readonly string _jsonPath;

        public LobbyService()
        {
            // data/ klasörü exe yanında
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var dataDir = Path.Combine(dir, "data");
            Directory.CreateDirectory(dataDir);
            _jsonPath = Path.Combine(dataDir, "lobiler.json");
            EnsureFile();
        }

        // ─── Public API ──────────────────────────────────────────────────────────

        public Task<List<LobiData>> GetLobilerAsync()
        {
            var root = Oku();
            // Süresi dolmuş lobileri temizle (30 dakika)
            var sinir = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1800;
            root.Lobiler.RemoveAll(l => l.OlusturmaZamani < sinir);
            Yaz(root);
            return Task.FromResult(root.Lobiler);
        }

        public Task<LobiData> CreateLobiAsync(string grupKodu, string olusturan, string rutbe, int rutbePuani = 0)
        {
            var root = Oku();
            var lobi = new LobiData
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                GrupKodu = grupKodu,
                Olusturan = olusturan,
                Rutbe = rutbe,
                RutbePuani = rutbePuani,
                Oyuncular = new List<OyuncuData>
                {
                    new OyuncuData { Ad = olusturan, Rutbe = rutbe, RutbePuani = rutbePuani, Ping = RastgelePing() }
                },
                Durum = "bekliyor",
                OlusturmaZamani = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            root.Lobiler.Add(lobi);
            Yaz(root);
            return Task.FromResult(lobi);
        }

        public Task<(bool basarili, string hata)> JoinLobiAsync(string lobiId, OyuncuData oyuncu)
        {
            var root = Oku();
            var lobi = root.Lobiler.FirstOrDefault(l => l.Id == lobiId);
            if (lobi == null)
                return Task.FromResult((false, "Lobi bulunamadı."));
            if (lobi.Dolu)
                return Task.FromResult((false, "Lobi dolu."));

            lobi.Oyuncular.Add(oyuncu);
            if (lobi.Dolu) lobi.Durum = "dolu";
            Yaz(root);
            return Task.FromResult((true, (string)null));
        }

        public Task DeleteLobiAsync(string lobiId)
        {
            var root = Oku();
            root.Lobiler.RemoveAll(l => l.Id == lobiId);
            Yaz(root);
            return Task.CompletedTask;
        }

        public Task<List<LobiData>> GetUygunLobilerAsync(string rutbe)
        {
            var root = Oku();
            var sinir = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1800;
            var uygun = root.Lobiler
                .Where(l => l.OlusturmaZamani >= sinir
                         && !l.Dolu
                         && Rutbeler.Uyumlu(rutbe, l.Rutbe))
                .ToList();
            return Task.FromResult(uygun);
        }

        // ─── Yardımcı ────────────────────────────────────────────────────────────

        private void EnsureFile()
        {
            if (!File.Exists(_jsonPath))
                Yaz(new LobilerJson());
        }

        private LobilerJson Oku()
        {
            try
            {
                var json = File.ReadAllText(_jsonPath);
                return JsonConvert.DeserializeObject<LobilerJson>(json) ?? new LobilerJson();
            }
            catch { return new LobilerJson(); }
        }

        private void Yaz(LobilerJson root)
        {
            try
            {
                var json = JsonConvert.SerializeObject(root, Formatting.Indented);
                File.WriteAllText(_jsonPath, json);
            }
            catch { }
        }

        private static int RastgelePing()
        {
            return new Random().Next(12, 120);
        }
    }
}
