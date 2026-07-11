using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValorantAutoClicker.Helpers;

namespace ValorantAutoClicker.Services
{
    public class FirebaseService : IDisposable
    {
        private static string RtdbUrl => Helpers.StringObfuscator.Decode(
            "y9fX09CZjIzIz9rZxsTEjsfGxcLWz9eO0dfHwY3FytHGwcLQxsrMjcDMzg==", 0xA3);
        private static string ApiKey { get; set; } = "";
        private static string AuthUrl => string.IsNullOrEmpty(ApiKey) ? "" :
            Helpers.StringObfuscator.Decode("3MDAxMeOm5vd0NHawN3AzcDb29jf3cCa09vb09jR1cTdx5rX29mbwoWb1dfX28HawMeOx93T2uHEi9/RzYk=", 0xB4) + ApiKey;

        public static bool IsConfigured => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(RtdbUrl);

        public async Task<bool> BootstrapApiKeyAsync()
        {
            if (string.IsNullOrEmpty(RtdbUrl)) return false;
            try
            {
                var url = $"{RtdbUrl}{Helpers.StringObfuscator.Decode("m9fb2tLd05vS3cbR1tXH0fXE3f/RzZrex9va", 0xB4)}";
                var resp = await _http.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(json) && json != "null")
                    {
                        ApiKey = SafeJson.Deserialize<string>(json) ?? "";
                        if (!string.IsNullOrEmpty(ApiKey))
                            return true;
                    }
                }
            }
            catch { }

            // Fallback: RTDB'den okunamazsa (Permission denied vb.) gömülü key'i kullan
            ApiKey = Helpers.StringObfuscator.Decode("9f3O1efN8P3izs2Amfzs7MfRwdD62M7lwMDkg8PY7tjgzcbu99Dx", 0xB4);
            return !string.IsNullOrEmpty(ApiKey);
        }

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
            if (!IsConfigured) return false;

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

        // ─── Google OAuth ────────────────────────────────────────────────────────

        public async Task<GoogleOAuthCredentials> GetGoogleOAuthCredentialsAsync()
        {
            try
            {
                var url = $"{RtdbUrl}/config/googleOAuth.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null") return null;
                return SafeJson.Deserialize<GoogleOAuthCredentials>(json);
            }
            catch { return null; }
        }

        /// <summary>
        /// Firebase signInWithIdp — Google ID token ile Firebase'e giriş yapar.
        /// </summary>
        public async Task<(string idToken, string localId, bool isNewUser)> GoogleSignInAsync(string googleIdToken)
        {
            try
            {
                var postBody = $"id_token={Uri.EscapeDataString(googleIdToken)}&providerId=google.com";
                var body = JsonConvert.SerializeObject(new
                {
                    requestUri = "http://localhost",
                    returnSecureToken = true,
                    postBody = postBody
                });

                var resp = await _http.PostAsync(
                    $"{Helpers.StringObfuscator.Decode("3MDAxMeOm5vd0NHawN3AzcDb29jf3cCa09vb09jR1cTdx5rX29mbwoWb1dfX28HawMeOx93T2uHEi9/RzYk=", 0xB4)}{ApiKey}",
                    new StringContent(body, Encoding.UTF8, "application/json"));

                if (!resp.IsSuccessStatusCode) return (null, null, false);

                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var idToken = json["idToken"]?.ToString();
                var localId = json["localId"]?.ToString();
                var isNewUser = json["isNewUser"]?.ToObject<bool>() ?? false;

                // Firebase auth state'ini güncelle
                if (!string.IsNullOrEmpty(idToken))
                {
                    _idToken = idToken;
                    _localId = localId;
                }

                return (idToken, localId, isNewUser);
            }
            catch { return (null, null, false); }
        }

        /// <summary>
        /// Kullanıcı profilini Firebase RTDB'ye kaydeder (/users/{uid}/).
        /// </summary>
        public async Task<bool> SaveUserProfileAsync(string uid, object profile)
        {
            try
            {
                var json = JsonConvert.SerializeObject(profile);
                var url = $"{RtdbUrl}/users/{uid}.json{Auth()}";
                var resp = await _http.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>
        /// Kullanıcı profilini Firebase RTDB'den okur.
        /// </summary>
        public async Task<string> GetUserProfileAsync(string uid)
        {
            try
            {
                var url = $"{RtdbUrl}/users/{uid}.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                return json == "null" ? null : json;
            }
            catch { return null; }
        }

        // ─── Config (API keys stored here) ──────────────────────────────────────

        public async Task<FirebaseConfig> GetConfigAsync()
        {
            try
            {
                // Try primary path first
                var url = $"{RtdbUrl}{Helpers.StringObfuscator.Decode("m9fb2tLd05vVxN3/0c3Hmt7H29o=", 0xB4)}{Auth()}";
                var resp = await _http.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    if (json != "null")
                        return SafeJson.Deserialize<FirebaseConfig>(json);
                }
            }
            catch { }

            try
            {
                // Fallback: root-level henrikDevKey (writable via REST API)
                var url = $"{RtdbUrl}{Helpers.StringObfuscator.Decode("m9zR2sbd3/DRwv/RzZrex9va", 0xB4)}{Auth()}";
                var resp = await _http.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    if (json != "null")
                        return SafeJson.Deserialize<FirebaseConfig>(json);
                }
            }
            catch { }

            return null;
        }

        // ─── Lobi ────────────────────────────────────────────────────────────────

        public async Task<string> CreateLobbyAsync(string gameMode, int maxPlayers,
            string hostName, string hostTag, int hostElo, int hostTier, string hostRank, string region)
        {
            var lobbyId = Guid.NewGuid().ToString("N")[..12];
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var lobi = new Dictionary<string, object>
            {
                { "id", lobbyId },
                { "hostName", hostName },
                { "hostTag", hostTag },
                { "hostElo", hostElo },
                { "hostTier", hostTier },
                { "hostRank", hostRank },
                { "gameMode", gameMode },
                { "region", region },
                { "status", "waiting" },
                { "maxPlayers", maxPlayers },
                { "groupCode", "" },
                { "createdAt", now },
                { "expiresAt", now + 3600 },
                { "players", new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "name", hostName },
                            { "tag", hostTag },
                            { "elo", hostElo },
                            { "tier", hostTier },
                            { "rank", hostRank }
                        }
                    }
                }
            };
            var url = $"{RtdbUrl}/rooms/l_{lobbyId}.json{Auth()}";
            var body = JsonConvert.SerializeObject(lobi);
            var resp = await _http.PutAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new Exception($"RTDB yazma hatası: {resp.StatusCode} — {err}");
            }
            return lobbyId;
        }

        public async Task<string> LobiOlusturAsync(FirestoreLobi lobi)
        {
            var lobbyId = Guid.NewGuid().ToString("N")[..12];
            var url = $"{RtdbUrl}/rooms/l_{lobbyId}.json{Auth()}";
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
                var url = $"{RtdbUrl}/rooms/l_{lobbyId}.json{Auth()}";
                await _http.DeleteAsync(url);
            }
            catch { }
        }

        public async Task<List<FirestoreLobi>> GetWaitingLobilerAsync()
        {
            try
            {
                var url = $"{RtdbUrl}/rooms.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return new List<FirestoreLobi>();
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null") return new List<FirestoreLobi>();
                var dict = SafeJson.Deserialize<Dictionary<string, FirestoreLobi>>(json);
                if (dict == null) return new List<FirestoreLobi>();
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return dict
                    .Where(kv => kv.Key.StartsWith("l_") && kv.Value.Status == "waiting" && kv.Value.ExpiresAt > now)
                    .Select(kv => { kv.Value.Id = kv.Key[2..]; return kv.Value; })
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(50)
                    .ToList();
            }
            catch { return new List<FirestoreLobi>(); }
        }

        // ─── Yeni: Lobi Getir (ID ile) ──────────────────────────────────────────────

        public async Task<FirestoreLobi> LobiGetirAsync(string lobbyId)
        {
            try
            {
                var url = $"{RtdbUrl}/rooms/l_{lobbyId}.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null") return null;
                var lobi = SafeJson.Deserialize<FirestoreLobi>(json);
                if (lobi != null) lobi.Id = lobbyId;
                return lobi;
            }
            catch { return null; }
        }

        // ─── Yeni: Lobi Güncelle (Patch) ─────────────────────────────────────────────

        public async Task LobiGuncelleAsync(string lobbyId, object data)
        {
            var url = $"{RtdbUrl}/rooms/l_{lobbyId}.json{Auth()}";
            var body = JsonConvert.SerializeObject(data);
            var resp = await _http.PatchAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new Exception($"RTDB güncelleme hatası: {resp.StatusCode} — {err}");
            }
        }

        // ─── Yeni: Sona ermiş lobileri temizle ──────────────────────────────────────

        public async Task CleanExpiredLobbiesAsync()
        {
            try
            {
                var url = $"{RtdbUrl}/rooms.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return;
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null") return;
                var dict = SafeJson.Deserialize<Dictionary<string, FirestoreLobi>>(json);
                if (dict == null) return;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (var kv in dict)
                {
                    if (kv.Key.StartsWith("l_") && kv.Value.ExpiresAt <= now)
                    {
                        var delUrl = $"{RtdbUrl}/rooms/{kv.Key}.json{Auth()}";
                        await _http.DeleteAsync(delUrl);
                    }
                }
            }
            catch { }
        }

        // ─── Yeni: Tek lobi dinleyici (polling) ──────────────────────────────────────

        private CancellationTokenSource _lobbyListenerCts;

        public event Action<FirestoreLobi> LobbyGuncellendi;

        public void StartLobbyListener(string lobbyId)
        {
            StopLobbyListener();
            _lobbyListenerCts = new CancellationTokenSource();
            _ = LobbyListenLoopAsync(lobbyId, _lobbyListenerCts.Token);
        }

        public void StopLobbyListener()
        {
            _lobbyListenerCts?.Cancel();
            _lobbyListenerCts = null;
        }

        private async Task LobbyListenLoopAsync(string lobbyId, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2000, ct);
                    if (ct.IsCancellationRequested) break;
                    var lobi = await LobiGetirAsync(lobbyId);
                    if (lobi != null)
                        LobbyGuncellendi?.Invoke(lobi);
                    else
                        LobbyGuncellendi?.Invoke(null); // lobi silinmiş
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(3000).ConfigureAwait(false); }
            }
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

        public async Task QueueEkleAsync(string localId, string name, string tag, int elo, int tier, string cardUrl, string region)
        {
            var url = $"{RtdbUrl}/matchmaking/{localId}.json{Auth()}";
            var data = new QueueOyuncu
            {
                LocalId = localId,
                PlayerName = name,
                PlayerTag = tag,
                Elo = elo,
                Tier = tier,
                CardUrl = cardUrl,
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
                var dict = SafeJson.Deserialize<Dictionary<string, QueueOyuncu>>(json);
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
            string p1Name, string p1Tag, int p1Elo, int p1Tier, string p1CardUrl,
            string p2Name, string p2Tag, int p2Elo, int p2Tier, string p2CardUrl)
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
                Player1CardUrl = p1CardUrl,
                Player2LocalId = player2LocalId,
                Player2Name = p2Name,
                Player2Tag = p2Tag,
                Player2Elo = p2Elo,
                Player2Tier = p2Tier,
                Player2CardUrl = p2CardUrl,
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
                var oda = SafeJson.Deserialize<Oda>(json);
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
                var dict = SafeJson.Deserialize<Dictionary<string, Oda>>(json);
                if (dict == null) return null;
                return dict
                    .Where(kv => kv.Value.Status == "matched" || kv.Value.Status == "group_code_set")
                    .Select(kv => { kv.Value.Id = kv.Key; return kv.Value; })
                    .FirstOrDefault(o => o.Player1LocalId == localId || o.Player2LocalId == localId);
            }
            catch { return null; }
        }

        // ─── Duyuru / Bildirim Mesajları ───────────────────────────────────────

        public async Task<AppBildirim> GetBildirimAsync()
        {
            try
            {
                var url = $"{RtdbUrl}/bildirimler.json{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null") return null;
                var data = SafeJson.Deserialize<AppBildirim>(json);
                return data;
            }
            catch { return null; }
        }

        // ─── Keepalive / Update Check ─────────────────────────────────────────────

        public async Task<bool> PingAsync()
        {
            try
            {
                var idToken = _idToken ?? "";
                var url = $"{RtdbUrl}{Helpers.StringObfuscator.Decode("m8HE0NXA0ceb2NXA0cfAmt7H29o=", 0xB4)}?auth={idToken}";
                var resp = await _http.GetAsync(url);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<AppGuncelleme> GuncellemeKontrolFirestoreAsync()
        {
            try
            {
                var url = $"{RtdbUrl}{Helpers.StringObfuscator.Decode("m8HE0NXA0ceb2NXA0cfAmt7H29o=", 0xB4)}{Auth()}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                if (json == "null") return null;
                return SafeJson.Deserialize<AppGuncelleme>(json);
            }
            catch { return null; }
        }

        /// <summary>Firebase auth state'ini güncelle (Google giriş sonrası çağrılır).</summary>
        public void SetAuthState(string idToken, string localId)
        {
            _idToken = idToken;
            _localId = localId;
        }

        public async Task<bool> DownloadUpdateZipAsync(string downloadUrl, string destPath, IProgress<int> progress)
        {
            try
            {
                if (string.IsNullOrEmpty(downloadUrl)) return false;
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                using var dlHttp = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                using var resp = await dlHttp.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (!resp.IsSuccessStatusCode) return false;

                // Detect JSON (Base64) vs binary by checking first byte
                var bodyStream = await resp.Content.ReadAsStreamAsync();
                var header = new byte[4];
                int headerLen = await bodyStream.ReadAsync(header, 0, 4);

                if (headerLen > 0 && header[0] == '{')
                {
                    // JSON response — Firebase RTDB / GitHub JSON asset with Base64
                    byte[] fullBody;
                    using (var memStream = new System.IO.MemoryStream())
                    {
                        memStream.Write(header, 0, headerLen);
                        await bodyStream.CopyToAsync(memStream);
                        fullBody = memStream.ToArray();
                    }
                    var json = System.Text.Encoding.UTF8.GetString(fullBody);
                    if (json == "null" || json.Length < 100) return false;
                    var obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    var base64 = obj?.Values.FirstOrDefault();
                    if (string.IsNullOrEmpty(base64)) return false;
                    var bytes = Convert.FromBase64String(base64);
                    await System.IO.File.WriteAllBytesAsync(destPath, bytes);
                    if (progress != null) progress.Report(100);
                }
                else
                {
                    // Binary stream — ZIP or EXE
                    var totalBytes = resp.Content.Headers.ContentLength ?? -1L;
                    using var fileStream = new System.IO.FileStream(destPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);

                    // Write the header bytes we already read
                    await fileStream.WriteAsync(header, 0, headerLen);
                    long totalRead = headerLen;

                    var buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await bodyStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                        if (totalBytes > 0 && progress != null)
                        {
                            progress.Report((int)((totalRead * 100) / totalBytes));
                        }
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

    // ─── Firebase Static Config (loaded from appsettings.json) ────────────────────
    public class FirebaseStaticConfig
    {
        public string ApiKey  { get; set; } = Helpers.StringObfuscator.Decode("9f3O1efN8P3izs2Amfzs7MfRwdD62M7lwMDkg8PY7tjgzcbu99Dx", 0xB4);
        public string RtdbUrl { get; set; } = Helpers.StringObfuscator.Decode("y9fX09CZjIzIz9rZxsTEjsfGxcLWz9eO0dfHwY3FytHGwcLQxsrMjcDMzg==", 0xA3);
    }

    // ─── Güncelleme Modeli (Firebase RTDB /updates/latest.json) ────────────────────
    public class AppGuncelleme
    {
        public string Version { get; set; } = "";
        public string Title { get; set; } = "";
        public string Notes { get; set; } = "";
        public long Date { get; set; }
        public string DosyaUrl { get; set; } = "";
        public string DirectUrl { get; set; } = "";
    }

    // ─── Bildirim Modeli (UI için) ─────────────────────────────────────────────
    public class AppBildirim : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Baslik { get; set; } = "";
        public string Mesaj { get; set; } = "";
        public string Tarih { get; set; } = "";
        public bool Okundu { get; set; }
        public bool Aktif { get; set; }
        public BildirimTipi Tip { get; set; } = BildirimTipi.Bilgi;
        public AppGuncelleme Guncelleme { get; set; }
    }

    public enum BildirimTipi
    {
        Guncelleme,
        Uyari,
        Bilgi,
        Hata
    }

    // ─── Lobi Modeli (Firestore-style) ────────────────────────────────────────────
    public class LobbyPlayer
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";
        [JsonProperty("tag")]
        public string Tag { get; set; } = "";
        [JsonProperty("elo")]
        public int Elo { get; set; }
        [JsonProperty("tier")]
        public int Tier { get; set; }
        [JsonProperty("rank")]
        public string Rank { get; set; } = "";
        [JsonProperty("cardUrl")]
        public string CardUrl { get; set; } = "";
    }

    public class FirestoreLobi
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        [JsonProperty("hostName")]
        public string HostName { get; set; } = "";
        [JsonProperty("hostTag")]
        public string HostTag { get; set; } = "";
        [JsonProperty("hostElo")]
        public int HostElo { get; set; }
        [JsonProperty("hostTier")]
        public int HostTier { get; set; }
        [JsonProperty("hostRank")]
        public string HostRank { get; set; } = "";
        [JsonProperty("gameMode")]
        public string GameMode { get; set; } = "";
        [JsonProperty("groupCode")]
        public string GroupCode { get; set; } = "";
        [JsonProperty("region")]
        public string Region { get; set; } = "eu";
        [JsonProperty("status")]
        public string Status { get; set; } = "waiting";
        [JsonProperty("maxPlayers")]
        public int MaxPlayers { get; set; } = 5;
        [JsonProperty("createdAt")]
        public long CreatedAt { get; set; }
        [JsonProperty("expiresAt")]
        public long ExpiresAt { get; set; }
        [JsonProperty("players")]
        public List<LobbyPlayer> Players { get; set; } = new();
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
        public string CardUrl { get; set; } = "";
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
        public string Player1CardUrl { get; set; } = "";
        public string Player2LocalId { get; set; } = "";
        public string Player2Name { get; set; } = "";
        public string Player2Tag { get; set; } = "";
        public int Player2Elo { get; set; }
        public int Player2Tier { get; set; }
        public string Player2CardUrl { get; set; } = "";
        public string GroupCode { get; set; } = "";
        public string Status { get; set; } = "matched";
        public long CreatedAt { get; set; }
    }
}
