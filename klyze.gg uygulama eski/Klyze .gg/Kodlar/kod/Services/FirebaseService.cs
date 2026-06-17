using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ValorantAutoClicker.Services
{
    /// <summary>
    /// Firebase Realtime Database REST API ile oda eşleşme servisi.
    /// rooms/{roomCode}/host, guest, createdAt, status alanlarını yönetir.
    /// </summary>
    public class FirebaseService : IDisposable
    {
        // Firebase projenizin Realtime Database URL'si
        // Kendi Firebase projenizi oluşturun: https://console.firebase.google.com
        // Realtime Database'i etkinleştirin ve URL'yi buraya yapıştırın.
        // Örnek: "https://PROJE-ADINIZ-default-rtdb.firebaseio.com"
        private const string FirebaseBaseUrl = "https://klyze-default-rtdb.firebaseio.com";

        private readonly HttpClient _http;
        private CancellationTokenSource _listenerCts;

        public event Action<RoomData> RoomUpdated;

        public FirebaseService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        // ─── Oda Oluştur ─────────────────────────────────────────────────────────

        /// <summary>
        /// Yeni bir oda oluşturur, host olarak işaretler.
        /// </summary>
        public async Task<bool> CreateRoomAsync(string roomCode)
        {
            var data = new RoomData
            {
                Host = true,
                Guest = false,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Status = "waiting"
            };

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{FirebaseBaseUrl}/rooms/{roomCode}.json";

            var response = await _http.PutAsync(url, content);
            return response.IsSuccessStatusCode;
        }

        // ─── Odaya Katıl ─────────────────────────────────────────────────────────

        /// <summary>
        /// Mevcut bir odaya guest olarak katılır.
        /// Oda yoksa false döner.
        /// </summary>
        public async Task<(bool success, string error)> JoinRoomAsync(string roomCode)
        {
            // Önce odanın var olup olmadığını kontrol et
            var getUrl = $"{FirebaseBaseUrl}/rooms/{roomCode}.json";
            var getResp = await _http.GetAsync(getUrl);
            if (!getResp.IsSuccessStatusCode)
                return (false, "Bağlantı hatası.");

            var body = await getResp.Content.ReadAsStringAsync();
            if (body == "null" || string.IsNullOrWhiteSpace(body))
                return (false, "Kod bulunamadı.");

            var room = JsonConvert.DeserializeObject<RoomData>(body);
            if (room == null)
                return (false, "Kod bulunamadı.");

            if (room.Status == "matched")
                return (false, "Bu oda zaten dolu.");

            // Guest alanını true yap, status'u matched yap
            var patchData = new { guest = true, status = "matched" };
            var patchJson = JsonConvert.SerializeObject(patchData);
            var patchContent = new StringContent(patchJson, Encoding.UTF8, "application/json");

            // Firebase REST PATCH
            var patchUrl = $"{FirebaseBaseUrl}/rooms/{roomCode}.json";
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), patchUrl)
            {
                Content = patchContent
            };
            var patchResp = await _http.SendAsync(request);
            if (!patchResp.IsSuccessStatusCode)
                return (false, "Odaya katılırken hata oluştu.");

            return (true, null);
        }

        // ─── Gerçek Zamanlı Dinleme ───────────────────────────────────────────────

        /// <summary>
        /// Firebase Server-Sent Events (SSE) ile odayı gerçek zamanlı dinler.
        /// Değişiklik olduğunda RoomUpdated event'i tetiklenir.
        /// </summary>
        public void StartListening(string roomCode)
        {
            StopListening();
            _listenerCts = new CancellationTokenSource();
            _ = ListenLoopAsync(roomCode, _listenerCts.Token);
        }

        public void StopListening()
        {
            _listenerCts?.Cancel();
            _listenerCts = null;
        }

        private async Task ListenLoopAsync(string roomCode, CancellationToken ct)
        {
            // Firebase SSE endpoint
            var url = $"{FirebaseBaseUrl}/rooms/{roomCode}.json";

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Polling: her 2 saniyede bir oda verisini çek
                    await Task.Delay(2000, ct);
                    if (ct.IsCancellationRequested) break;

                    var resp = await _http.GetAsync(url, ct);
                    if (!resp.IsSuccessStatusCode) continue;

                    var body = await resp.Content.ReadAsStringAsync(ct);
                    if (body == "null" || string.IsNullOrWhiteSpace(body)) continue;

                    var room = JsonConvert.DeserializeObject<RoomData>(body);
                    if (room != null)
                        RoomUpdated?.Invoke(room);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ağ hatası — sessizce devam et
                    await Task.Delay(3000, ct).ConfigureAwait(false);
                }
            }
        }

        // ─── Oda Sil ─────────────────────────────────────────────────────────────

        public async Task DeleteRoomAsync(string roomCode)
        {
            try
            {
                var url = $"{FirebaseBaseUrl}/rooms/{roomCode}.json";
                await _http.DeleteAsync(url);
            }
            catch { }
        }

        // ─── Oda Verisi Al ───────────────────────────────────────────────────────

        public async Task<RoomData> GetRoomAsync(string roomCode)
        {
            var url = $"{FirebaseBaseUrl}/rooms/{roomCode}.json";
            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadAsStringAsync();
            if (body == "null" || string.IsNullOrWhiteSpace(body)) return null;
            return JsonConvert.DeserializeObject<RoomData>(body);
        }

        public void Dispose()
        {
            StopListening();
            _http?.Dispose();
        }
    }

    public class RoomData
    {
        [JsonProperty("host")]
        public bool Host { get; set; }

        [JsonProperty("guest")]
        public bool Guest { get; set; }

        [JsonProperty("createdAt")]
        public long CreatedAt { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }
}
