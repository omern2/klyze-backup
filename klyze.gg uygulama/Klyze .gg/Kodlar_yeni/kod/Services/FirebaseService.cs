using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ValorantAutoClicker.Services
{
    public class FirebaseService : IDisposable
    {
        // Firebase Web API key — public by design (identifies the project to Firebase).
        // Security is enforced via Firebase Security Rules + App Check.
        private const string ApiKey    = "AIzaSyDIVzy4-HXXseudNlzQttP7wlZlTyrZCdE";
        private const string RtdbUrl   = "https://klyzegg-default-rtdb.firebaseio.com";
        private const string AuthUrl   = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + ApiKey;

        private readonly HttpClient _http;
        private string _idToken;
        private string _localId;
        public string LocalId => _localId;
        private CancellationTokenSource _listenerCts;

        public event Action<List<FirestoreLobi>> LobilerGuncellendi;

        public FirebaseService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        }

        private string Auth() => string.IsNullOrEmpty(_idToken) ? "" : $"?auth={_idToken}";

        // ─── Anonymous Auth ──────────────────────────────────────────────────────

        public async Task<bool> AnonimGirisAsync()
        {
            var delays = new[] { 1000, 2000, 4000 };
            for (int i = 0; i <= delays.Length; i++)
            {
                try
                {
                    var body = JsonConvert.SerializeObject(new { returnSecureToken = true });
                    var resp = await _http.PostAsync(AuthUrl,
                        new StringContent(body, Encoding.UTF8, "application/json"));
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                        _idToken = json["idToken"]?.ToString();
                        _localId = json["localId"]?.ToString();
                        if (!string.IsNullOrEmpty(_idToken)) return true;
                    }
                }
                catch
                {
                    // ignore transient errors, retry
                }

                if (i < delays.Length)
                    await Task.Delay(delays[i]);
            }

            _idToken = null;
            _localId = null;
            return false;
        }

        // ─── Config (API keys stored here) ──────────────────────────────────────

        public async Task<FirebaseConfig> GetConfigAsync()
        {
            try
            {
                // Try primary path first
                var url = $"{RtdbUrl}/config/apiKeys.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    if (json != "null")
                        return JsonConvert.DeserializeObject<FirebaseConfig>(json);
                }
            }
            catch { }

            return null;
        }

        // ─── Lobi ────────────────────────────────────────────────────────────────

        public async Task<string> LobiOlusturAsync(FirestoreLobi lobi)
        {
            var lobbyId = Guid.NewGuid().ToString("N")[..12];
            var url = $"{RtdbUrl}/lobbies/{lobbyId}.json{Auth()}";
            var body = JsonConvert.SerializeObject(lobi);
            var resp = await _http.PatchAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new Exception($"RTDB yazma hatası: {resp.StatusCode} — {err}");
            }
            return lobbyId;
        }

        public async Task LobiSilAsync(string lobbyId)
        {
            try
            {
                var url = $"{RtdbUrl}/lobbies/{lobbyId}.json{Auth()}";
                await _http.DeleteAsync(url);
            }
            catch { }
        }

        public async Task<List<FirestoreLobi>> GetWaitingLobilerAsync()
        {
            try
            {
                var url = $"{RtdbUrl}/lobbies.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return new List<FirestoreLobi>();
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null") return new List<FirestoreLobi>();
                var dict = JsonConvert.DeserializeObject<Dictionary<string, FirestoreLobi>>(json);
                if (dict == null) return new List<FirestoreLobi>();
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return dict
                    .Where(kv => kv.Value.Status == "waiting" && kv.Value.ExpiresAt > now)
                    .Select(kv => { kv.Value.Id = kv.Key; return kv.Value; })
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(50)
                    .ToList();
            }
            catch { return new List<FirestoreLobi>(); }
        }

        // ─── Realtime Polling Listener ───────────────────────────────────────────

        public void StartListening()
        {
            StopListening();
            _listenerCts = new CancellationTokenSource();
            _ = ListenLoopAsync(_listenerCts.Token);
        }

        public void StopListening()
        {
            _listenerCts?.Cancel();
            _listenerCts = null;
        }

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, ct);
                    if (ct.IsCancellationRequested) break;
                    var lobiler = await GetWaitingLobilerAsync();
                    LobilerGuncellendi?.Invoke(lobiler);
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(3000).ConfigureAwait(false); }
            }
        }

        // ─── Matchmaking Kuyruğu ─────────────────────────────────────────────────

        public async Task QueueEkleAsync(string localId, string name, string tag, int elo, int tier, string region)
        {
            var url = $"{RtdbUrl}/matchmaking/{localId}.json{Auth()}";
            var data = new QueueOyuncu
            {
                LocalId = localId,
                PlayerName = name,
                PlayerTag = tag,
                Elo = elo,
                Tier = tier,
                Region = region,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Status = "searching"
            };
            var body = JsonConvert.SerializeObject(data);
            var resp = await _http.PutAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new Exception($"RTDB queue hatası: {resp.StatusCode} — {err}");
            }
        }

        public async Task QueueKaldirAsync(string localId)
        {
            try
            {
                var url = $"{RtdbUrl}/matchmaking/{localId}.json{Auth()}";
                await _http.DeleteAsync(url);
            }
            catch { }
        }

        public async Task<List<QueueOyuncu>> QueueGetirAsync(string excludeLocalId = "")
        {
            try
            {
                var url = $"{RtdbUrl}/matchmaking.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return new List<QueueOyuncu>();
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null") return new List<QueueOyuncu>();
                var dict = JsonConvert.DeserializeObject<Dictionary<string, QueueOyuncu>>(json);
                if (dict == null) return new List<QueueOyuncu>();
                return dict
                    .Where(kv => kv.Value.Status == "searching" && kv.Key != excludeLocalId)
                    .Select(kv => { kv.Value.LocalId = kv.Key; return kv.Value; })
                    .OrderBy(q => q.CreatedAt)
                    .ToList();
            }
            catch { return new List<QueueOyuncu>(); }
        }

        // ─── Oda (Eşleşme) ────────────────────────────────────────────────────────

        public async Task<string> OdaOlusturAsync(string player1LocalId, string player2LocalId,
            string p1Name, string p1Tag, int p1Elo, int p1Tier,
            string p2Name, string p2Tag, int p2Elo, int p2Tier)
        {
            var roomId = Guid.NewGuid().ToString("N")[..12];
            var url = $"{RtdbUrl}/rooms/{roomId}.json{Auth()}";
            var data = new Oda
            {
                Id = roomId,
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
            var body = JsonConvert.SerializeObject(data);
            var resp = await _http.PutAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new Exception($"RTDB oda hatası: {resp.StatusCode} — {err}");
            }
            return roomId;
        }

        public async Task OdaGrupKoduGuncelleAsync(string roomId, string groupCode)
        {
            var url = $"{RtdbUrl}/rooms/{roomId}.json{Auth()}";
            var data = new { groupCode = groupCode, status = "group_code_set" };
            var body = JsonConvert.SerializeObject(data);
            await _http.PatchAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
        }

        public async Task OdaSilAsync(string roomId)
        {
            try
            {
                var url = $"{RtdbUrl}/rooms/{roomId}.json{Auth()}";
                await _http.DeleteAsync(url);
            }
            catch { }
        }

        public async Task<Oda> OdaGetirAsync(string roomId)
        {
            try
            {
                var url = $"{RtdbUrl}/rooms/{roomId}.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null") return null;
                var oda = JsonConvert.DeserializeObject<Oda>(json);
                if (oda != null) oda.Id = roomId;
                return oda;
            }
            catch { return null; }
        }

        public async Task<Oda> OyuncuAktifOdaGetirAsync(string localId)
        {
            try
            {
                var url = $"{RtdbUrl}/rooms.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null") return null;
                var dict = JsonConvert.DeserializeObject<Dictionary<string, Oda>>(json);
                if (dict == null) return null;
                return dict
                    .Where(kv => kv.Value.Status == "matched" || kv.Value.Status == "group_code_set")
                    .Select(kv => { kv.Value.Id = kv.Key; return kv.Value; })
                    .FirstOrDefault(o => o.Player1LocalId == localId || o.Player2LocalId == localId);
            }
            catch { return null; }
        }

        // ─── Keepalive / Update Check ─────────────────────────────────────────────

        public async Task<bool> PingAsync()
        {
            try
            {
                var idToken = _idToken ?? "";
                var url = $"{RtdbUrl}/updates/latest.json?auth={idToken}";
                var resp = await _http.GetAsync(url);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<AppVersionDoc> GuncellemeKontrolFirestoreAsync()
        {
            try
            {
                var url = $"{RtdbUrl}/updates/latest.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null") return null;
                return JsonConvert.DeserializeObject<AppVersionDoc>(json);
            }
            catch { return null; }
        }

        public async Task<bool> DownloadUpdateZipAsync(string downloadUrl, string destPath, IProgress<int> progress)
        {
            try
            {
                if (string.IsNullOrEmpty(downloadUrl)) return false;
                using var resp = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode) return false;

                var totalBytes = resp.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await resp.Content.ReadAsStreamAsync();
                using var fileStream = new System.IO.FileStream(destPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    if (totalBytes > 0 && progress != null)
                    {
                        progress.Report((int)((totalRead * 100) / totalBytes));
                    }
                }
                return true;
            }
            catch { return false; }
        }

        public void Dispose()
        {
            StopListening();
            _http?.Dispose();
        }
    }

    // ─── App Version Document (Firestore app_version/current) ─────────────────────
    public class AppVersionDoc
    {
        public string version { get; set; } = "";
        public string downloadUrl { get; set; } = "";
        public string releaseNotes { get; set; } = "";
        public long releasedAt { get; set; }
    }

    // ─── Bildirim Modeli (UI için) ─────────────────────────────────────────────
    public class AppBildirim : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Baslik { get; set; } = "";
        public string Mesaj { get; set; } = "";
        public DateTime Tarih { get; set; } = DateTime.Now;
        public bool Okundu { get; set; }
        public BildirimTipi Tip { get; set; } = BildirimTipi.Bilgi;
        public AppVersionDoc Guncelleme { get; set; }
    }

    public enum BildirimTipi
    {
        Guncelleme,
        Uyari,
        Bilgi,
        Hata
    }

    // ─── Lobi Modeli ────────────────────────────────────────────────────────────
    public class FirestoreLobi
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public string HostName { get; set; } = "";
        public string HostTag { get; set; } = "";
        public int HostElo { get; set; }
        public int HostTier { get; set; }
        public string GroupCode { get; set; } = "";
        public string Region { get; set; } = "eu";
        public string Status { get; set; } = "waiting";
        public long CreatedAt { get; set; }
        public long ExpiresAt { get; set; }
    }

    // Eski RoomData — geriye dönük uyumluluk için
    public class RoomData
    {
        public bool Host { get; set; }
        public bool Guest { get; set; }
        public long CreatedAt { get; set; }
        public string Status { get; set; }
    }

    // ─── Matchmaking Kuyruk Oyuncusu ────────────────────────────────────────────
    public class QueueOyuncu
    {
        [JsonIgnore]
        public string LocalId { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public string PlayerTag { get; set; } = "";
        public int Elo { get; set; }
        public int Tier { get; set; }
        public string Region { get; set; } = "eu";
        public long CreatedAt { get; set; }
        public string Status { get; set; } = "searching";
    }

    // ─── Oda (Eşleşme) ──────────────────────────────────────────────────────────
    public class Oda
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public string Player1LocalId { get; set; } = "";
        public string Player1Name { get; set; } = "";
        public string Player1Tag { get; set; } = "";
        public int Player1Elo { get; set; }
        public int Player1Tier { get; set; }
        public string Player2LocalId { get; set; } = "";
        public string Player2Name { get; set; } = "";
        public string Player2Tag { get; set; } = "";
        public int Player2Elo { get; set; }
        public int Player2Tier { get; set; }
        public string GroupCode { get; set; } = "";
        public string Status { get; set; } = "matched";
        public long CreatedAt { get; set; }
    }
}
