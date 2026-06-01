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
    /// Lobi servisi — Firebase Firestore REST API üzerinden çalışır.
    /// Fallback: local JSON (Firebase erişilemezse).
    /// </summary>
    public class LobbyService : IDisposable
    {
        private readonly FirebaseService _firebase;
        private readonly string _localJsonPath;
        private readonly string _queueJsonPath;
        private readonly string _roomsJsonPath;
        private bool _firebaseHazir;

        public event Action<List<FirestoreLobi>> LobilerGuncellendi;

        public LobbyService()
        {
            _firebase = new FirebaseService();
            _firebase.LobilerGuncellendi += lobiler => LobilerGuncellendi?.Invoke(lobiler);

            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var dataDir = Path.Combine(dir, "data");
            Directory.CreateDirectory(dataDir);
            _localJsonPath = Path.Combine(dataDir, "lobiler.json");
            _queueJsonPath = Path.Combine(dataDir, "kuyruk.json");
            _roomsJsonPath = Path.Combine(dataDir, "rooms.json");
        }

        // ─── Başlat ──────────────────────────────────────────────────────────────

        public async Task BaslatAsync()
        {
            _firebaseHazir = await _firebase.AnonimGirisAsync();
        }

        public string GetLocalId() => _firebase.LocalId;

        // ─── Lobi Oluştur ────────────────────────────────────────────────────────

        public async Task<string> LobiOlusturAsync(string grupKodu, string hostName, string hostTag,
            int hostElo, int hostTier, string region = "eu")
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var lobi = new FirestoreLobi
            {
                HostName  = hostName,
                HostTag   = hostTag,
                HostElo   = hostElo,
                HostTier  = hostTier,
                GroupCode = grupKodu,
                Region    = region,
                Status    = "waiting",
                CreatedAt = now,
                ExpiresAt = now + 3600  // 1 saat
            };

            if (_firebaseHazir)
                return await _firebase.LobiOlusturAsync(lobi);

            return LocalLobiOlustur(lobi);
        }

        // ─── Lobi Sil ────────────────────────────────────────────────────────────

        public async Task LobiSilAsync(string lobbyId)
        {
            if (_firebaseHazir)
                await _firebase.LobiSilAsync(lobbyId);
            else
                LocalLobiSil(lobbyId);
        }

        // ─── Uygun Lobileri Getir (ELO bazlı) ───────────────────────────────────

        public async Task<List<FirestoreLobi>> GetUygunLobilerAsync(int kullaniciElo)
        {
            List<FirestoreLobi> tumLobiler;

            if (_firebaseHazir)
                tumLobiler = await _firebase.GetWaitingLobilerAsync();
            else
                tumLobiler = LocalLobileriOku();

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return tumLobiler
                .Where(l => l.ExpiresAt > now)
                .Where(l => Math.Abs(l.HostElo - kullaniciElo) <= 200)
                .OrderBy(l => Math.Abs(l.HostElo - kullaniciElo))
                .ToList();
        }

        // ─── Matchmaking Kuyruğu ─────────────────────────────────────────────────

        public async Task QueueEkleAsync(string localId, string name, string tag,
            int elo, int tier, string region)
        {
            if (_firebaseHazir)
                await _firebase.QueueEkleAsync(localId, name, tag, elo, tier, region);
            else
                LocalQueueEkle(localId, name, tag, elo, tier, region);
        }

        public async Task QueueKaldirAsync(string localId)
        {
            if (_firebaseHazir)
                await _firebase.QueueKaldirAsync(localId);
            else
                LocalQueueKaldir(localId);
        }

        public async Task<List<QueueOyuncu>> QueueGetirAsync(string excludeLocalId = "")
        {
            if (_firebaseHazir)
                return await _firebase.QueueGetirAsync(excludeLocalId);
            return LocalQueueOku().Where(q => q.LocalId != excludeLocalId).ToList();
        }

        public async Task<QueueOyuncu> EnYakinEslesmeBulAsync(int myElo, string myLocalId)
        {
            var kuyruk = await QueueGetirAsync(myLocalId);
            return kuyruk
                .Where(q => Math.Abs(q.Elo - myElo) <= 200)
                .OrderBy(q => Math.Abs(q.Elo - myElo))
                .FirstOrDefault();
        }

        // ─── Oda (Eşleşme) ───────────────────────────────────────────────────────

        public async Task<string> OdaOlusturAsync(string player1LocalId, string player2LocalId,
            string p1Name, string p1Tag, int p1Elo, int p1Tier,
            string p2Name, string p2Tag, int p2Elo, int p2Tier)
        {
            if (_firebaseHazir)
                return await _firebase.OdaOlusturAsync(
                    player1LocalId, player2LocalId,
                    p1Name, p1Tag, p1Elo, p1Tier,
                    p2Name, p2Tag, p2Elo, p2Tier);

            return LocalOdaOlustur(player1LocalId, player2LocalId,
                p1Name, p1Tag, p1Elo, p1Tier,
                p2Name, p2Tag, p2Elo, p2Tier);
        }

        public async Task OdaGrupKoduGuncelleAsync(string roomId, string groupCode)
        {
            if (_firebaseHazir)
                await _firebase.OdaGrupKoduGuncelleAsync(roomId, groupCode);
            else
                LocalOdaGrupKoduGuncelle(roomId, groupCode);
        }

        public async Task OdaSilAsync(string roomId)
        {
            if (_firebaseHazir)
                await _firebase.OdaSilAsync(roomId);
            else
                LocalOdaSil(roomId);
        }

        public async Task<Oda> OdaGetirAsync(string roomId)
        {
            if (_firebaseHazir)
                return await _firebase.OdaGetirAsync(roomId);
            return LocalOdaGetir(roomId);
        }

        public async Task<Oda> OyuncuAktifOdaGetirAsync(string localId)
        {
            if (_firebaseHazir)
                return await _firebase.OyuncuAktifOdaGetirAsync(localId);
            return LocalOdaGetirByPlayer(localId);
        }

        // ─── Realtime Listener ───────────────────────────────────────────────────

        public void StartListening() => _firebase.StartListening();
        public void StopListening()  => _firebase.StopListening();

        // ─── Local Fallback: Lobi ────────────────────────────────────────────────

        private string LocalLobiOlustur(FirestoreLobi lobi)
        {
            var list = LocalLobileriOku();
            lobi.Id = Guid.NewGuid().ToString("N")[..12];
            list.Add(lobi);
            LocalLobiYaz(list);
            return lobi.Id;
        }

        private void LocalLobiSil(string id)
        {
            var list = LocalLobileriOku();
            list.RemoveAll(l => l.Id == id);
            LocalLobiYaz(list);
        }

        private List<FirestoreLobi> LocalLobileriOku()
        {
            try
            {
                if (!File.Exists(_localJsonPath)) return new List<FirestoreLobi>();
                var json = File.ReadAllText(_localJsonPath);
                return JsonConvert.DeserializeObject<List<FirestoreLobi>>(json) ?? new List<FirestoreLobi>();
            }
            catch { return new List<FirestoreLobi>(); }
        }

        private void LocalLobiYaz(List<FirestoreLobi> list)
        {
            try { File.WriteAllText(_localJsonPath, JsonConvert.SerializeObject(list, Formatting.Indented)); }
            catch { }
        }

        // ─── Local Fallback: Kuyruk ─────────────────────────────────────────────

        private void LocalQueueEkle(string localId, string name, string tag,
            int elo, int tier, string region)
        {
            var list = LocalQueueOku();
            list.RemoveAll(q => q.LocalId == localId);
            list.Add(new QueueOyuncu
            {
                LocalId = localId,
                PlayerName = name,
                PlayerTag = tag,
                Elo = elo,
                Tier = tier,
                Region = region,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Status = "searching"
            });
            LocalQueueYaz(list);
        }

        private void LocalQueueKaldir(string localId)
        {
            var list = LocalQueueOku();
            list.RemoveAll(q => q.LocalId == localId);
            LocalQueueYaz(list);
        }

        private List<QueueOyuncu> LocalQueueOku()
        {
            try
            {
                if (!File.Exists(_queueJsonPath)) return new List<QueueOyuncu>();
                var json = File.ReadAllText(_queueJsonPath);
                return JsonConvert.DeserializeObject<List<QueueOyuncu>>(json) ?? new List<QueueOyuncu>();
            }
            catch { return new List<QueueOyuncu>(); }
        }

        private void LocalQueueYaz(List<QueueOyuncu> list)
        {
            try { File.WriteAllText(_queueJsonPath, JsonConvert.SerializeObject(list, Formatting.Indented)); }
            catch { }
        }

        // ─── Local Fallback: Oda ────────────────────────────────────────────────

        private string LocalOdaOlustur(string player1LocalId, string player2LocalId,
            string p1Name, string p1Tag, int p1Elo, int p1Tier,
            string p2Name, string p2Tag, int p2Elo, int p2Tier)
        {
            var list = LocalOdaOku();
            var oda = new Oda
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Player1LocalId = player1LocalId,
                Player1Name = p1Name,
                Player1Tag = p1Tag,
                Player1Elo = p1Elo,
                Player1Tier = p1Tier,
                Player2LocalId = player2LocalId,
                Player2Name = p2Name,
                Player2Tag = p2Tag,
                Player2Elo = p2Elo,
                Player2Tier = p2Tier,
                GroupCode = "",
                Status = "matched",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            list.Add(oda);
            LocalOdaYaz(list);
            return oda.Id;
        }

        private void LocalOdaGrupKoduGuncelle(string roomId, string groupCode)
        {
            var list = LocalOdaOku();
            var oda = list.FirstOrDefault(o => o.Id == roomId);
            if (oda != null)
            {
                oda.GroupCode = groupCode;
                oda.Status = "group_code_set";
                LocalOdaYaz(list);
            }
        }

        private void LocalOdaSil(string roomId)
        {
            var list = LocalOdaOku();
            list.RemoveAll(o => o.Id == roomId);
            LocalOdaYaz(list);
        }

        private Oda LocalOdaGetir(string roomId)
        {
            return LocalOdaOku().FirstOrDefault(o => o.Id == roomId);
        }

        private Oda LocalOdaGetirByPlayer(string localId)
        {
            return LocalOdaOku().FirstOrDefault(o =>
                (o.Player1LocalId == localId || o.Player2LocalId == localId) &&
                (o.Status == "matched" || o.Status == "group_code_set"));
        }

        private List<Oda> LocalOdaOku()
        {
            try
            {
                if (!File.Exists(_roomsJsonPath)) return new List<Oda>();
                var json = File.ReadAllText(_roomsJsonPath);
                return JsonConvert.DeserializeObject<List<Oda>>(json) ?? new List<Oda>();
            }
            catch { return new List<Oda>(); }
        }

        private void LocalOdaYaz(List<Oda> list)
        {
            try { File.WriteAllText(_roomsJsonPath, JsonConvert.SerializeObject(list, Formatting.Indented)); }
            catch { }
        }

        public void Dispose()
        {
            _firebase?.Dispose();
        }
    }
}
