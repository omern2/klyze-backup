using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    /// <summary>
    /// Riot Games RSO (Riot Sign-On) OAuth2 akışı.
    /// 
    /// Akış:
    ///   1. Yerel HTTP listener başlatılır (http://localhost:PORT/callback)
    ///   2. Tarayıcıda Riot login sayfası açılır
    ///   3. Kullanıcı giriş yapar → Riot, callback URL'e yönlendirir
    ///   4. URL'deki access_token yakalanır
    ///   5. Token ile Riot Account API'den isim/tag/bölge çekilir
    ///   6. Henrik Dev API'den MMR/rütbe çekilir
    /// </summary>
    public class RiotAuthService : IDisposable
    {
        // ─── Riot RSO Ayarları ───────────────────────────────────────────────────
        private const string ClientId    = "riot-client";
        private static string RedirectUri => Helpers.StringObfuscator.Decode(
            "oLy8uPLn56Snq6mkoKe7vPL7+Pj456uppKSqqauj", 0xC8);
        private const int    ListenPort  = 3000;

        private static string AuthUrl =>
            Helpers.StringObfuscator.Decode(
                "oLy8uLvy5+epvbyg5rqhp7yvqaWtu+arp6Xnqb28oKe6obKt", 0xC8) +
            "?client_id=riot-client" +
            "&redirect_uri=http%3A%2F%2Flocalhost%3A3000%2Fcallback" +
            "&response_type=token" +
            "&scope=openid%20offline_access" +
            "&nonce=klyze_nonce";

        private static string AccountApiUrl => Helpers.StringObfuscator.Decode(
            "oLy8uLvy5+etvbqnuK3mqbih5rqhp7yvqaWtu+arp6XnuqGnvOepq6unvaa8577556mrq6e9pry756Wt", 0xC8);

        private readonly HenrikApiService _henrikApi;
        private readonly HttpClient       _http;

        public RiotAuthService(HenrikApiService henrikApi)
        {
            _henrikApi = henrikApi;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        // ─── Ana Giriş Akışı ─────────────────────────────────────────────────────

        /// <summary>
        /// Riot OAuth akışını başlatır.
        /// Tarayıcıyı açar, callback'i bekler, profil döndürür.
        /// </summary>
        public async Task<(bool basarili, string hata, UserProfile profil)> GirisYapAsync(
            CancellationToken ct = default)
        {
            string accessToken = null;

            try
            {
                // 1. Callback listener başlat
                accessToken = await DinleVeTokenAl(ct);
            }
            catch (OperationCanceledException)
            {
                return (false, "Giriş iptal edildi.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Callback hatası: {ex.Message}", null);
            }

            if (string.IsNullOrEmpty(accessToken))
                return (false, "Token alınamadı. Lütfen tekrar deneyin.", null);

            try
            {
                // 2. Token ile hesap bilgisi çek
                var (name, tag, puuid) = await GetAccountFromToken(accessToken, ct);

                if (string.IsNullOrEmpty(name))
                    return (false, "Hesap bilgisi alınamadı.", null);

                // 3. Henrik Dev API ile tam profil oluştur
                var profil = await _henrikApi.GetFullProfileAsync(name, tag, ct);
                return (true, null, profil);
            }
            catch (HenrikApiException ex)
            {
                return (false, ex.Message, null);
            }
            catch (Exception ex)
            {
                return (false, $"Profil yüklenemedi: {ex.Message}", null);
            }
        }

        // ─── Tarayıcı Aç ─────────────────────────────────────────────────────────

        /// <summary>
        /// Varsayılan tarayıcıda Riot login sayfasını açar.
        /// </summary>
        public void TarayiciAc()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = AuthUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Tarayıcı açılamadı: {ex.Message}");
            }
        }

        // ─── HTTP Listener ───────────────────────────────────────────────────────

        /// <summary>
        /// localhost:3000/callback adresini dinler, URL fragment'tan token'ı çıkarır.
        /// Riot implicit flow'da token URL fragment (#) içinde gelir.
        /// Fragment sunucuya gelmediği için JS ile query param'a çeviriyoruz.
        /// </summary>
        private async Task<string> DinleVeTokenAl(CancellationToken ct)
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{ListenPort}/");
            listener.Start();

            // Tarayıcıyı aç
            TarayiciAc();

            // Timeout: 3 dakika
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            using var linked     = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            HttpListenerContext context = null;

            try
            {
                // Async olarak bağlantı bekle
                var getContextTask = listener.GetContextAsync();
                var cancelTask     = Task.Delay(Timeout.Infinite, linked.Token);

                var completed = await Task.WhenAny(getContextTask, cancelTask);

                if (completed == cancelTask)
                {
                    listener.Stop();
                    throw new OperationCanceledException("Giriş zaman aşımına uğradı.");
                }

                context = await getContextTask;
            }
            catch (OperationCanceledException)
            {
                listener.Stop();
                throw;
            }

            var req  = context.Request;
            var resp = context.Response;

            // İlk istek: /callback — fragment'ı query'e çeviren HTML sayfası sun
            if (req.Url.AbsolutePath == "/callback" && string.IsNullOrEmpty(req.Url.Query))
            {
                // Fragment (#access_token=...) sunucuya gelmiyor.
                // JS ile fragment'ı okuyup /token?access_token=... adresine yönlendiriyoruz.
                var html = @"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>Klyze - Giriş</title>
<style>
  body { background:#0D0D0D; color:white; font-family:sans-serif;
         display:flex; align-items:center; justify-content:center; height:100vh; margin:0; }
  .box { text-align:center; }
  .logo { font-size:32px; font-weight:bold; margin-bottom:12px; }
  .msg  { color:#888; font-size:14px; }
</style>
</head>
<body>
<div class='box'>
  <div class='logo'>Klyze</div>
  <div class='msg'>Giriş yapılıyor, lütfen bekleyin...</div>
</div>
<script>
  var hash = window.location.hash.substring(1);
  var params = new URLSearchParams(hash);
  var token = params.get('access_token');
  if (token) {
    fetch('/token?access_token=' + encodeURIComponent(token))
      .then(function() {
        document.querySelector('.msg').textContent = 'Giriş başarılı! Bu sekmeyi kapatabilirsiniz.';
      });
  } else {
    document.querySelector('.msg').textContent = 'Token alınamadı. Lütfen uygulamaya dönün.';
  }
</script>
</body>
</html>";
                var htmlBytes = Encoding.UTF8.GetBytes(html);
                resp.ContentType     = "text/html; charset=utf-8";
                resp.ContentLength64 = htmlBytes.Length;
                await resp.OutputStream.WriteAsync(htmlBytes, 0, htmlBytes.Length);
                resp.Close();

                // İkinci istek: /token?access_token=...
                var getTokenTask   = listener.GetContextAsync();
                var cancelTask2    = Task.Delay(Timeout.Infinite, linked.Token);
                var completed2     = await Task.WhenAny(getTokenTask, cancelTask2);

                if (completed2 == cancelTask2)
                {
                    listener.Stop();
                    throw new OperationCanceledException("Token bekleme zaman aşımı.");
                }

                var tokenCtx = await getTokenTask;
                var tokenReq = tokenCtx.Request;
                var tokenResp = tokenCtx.Response;

                var query       = tokenReq.Url.Query.TrimStart('?');
                var queryParams = System.Web.HttpUtility.ParseQueryString(query);
                var token       = queryParams["access_token"];

                // Boş 200 yanıt
                tokenResp.StatusCode = 200;
                tokenResp.Close();

                listener.Stop();
                return token;
            }
            else
            {
                // Doğrudan /token?access_token=... geldi (bazı tarayıcılar)
                var query       = req.Url.Query.TrimStart('?');
                var queryParams = System.Web.HttpUtility.ParseQueryString(query);
                var token       = queryParams["access_token"];

                resp.StatusCode = 200;
                resp.Close();

                listener.Stop();
                return token;
            }
        }

        // ─── Riot Account API ────────────────────────────────────────────────────

        /// <summary>
        /// Access token ile Riot Account API'den isim ve tag çeker.
        /// </summary>
        private async Task<(string name, string tag, string puuid)> GetAccountFromToken(
            string accessToken, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, AccountApiUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Riot Account API hatası ({(int)resp.StatusCode}).");

            var json  = JObject.Parse(body);
            var name  = json["gameName"]?.ToString() ?? "";
            var tag   = json["tagLine"]?.ToString() ?? "";
            var puuid = json["puuid"]?.ToString() ?? "";

            return (name, tag, puuid);
        }

        public void Dispose() => _http?.Dispose();
    }
}
