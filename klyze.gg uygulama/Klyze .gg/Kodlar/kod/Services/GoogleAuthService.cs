using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ValorantAutoClicker.Services
{
    public class GoogleAuthService
    {
        private readonly HttpClient _http;

        private string _clientId = "";
        private string _clientSecret = "";
        private string _firebaseApiKey = "";

        private const string GoogleTokenUrl = "https://oauth2.googleapis.com/token";
        private const string GoogleUserInfoUrl = "https://www.googleapis.com/oauth2/v2/userinfo";
        private const string Scopes = "openid email profile";

        public GoogleAuthService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task LoadCredentialsFromFirebaseAsync(FirebaseService firebase)
        {
            try
            {
                var creds = await firebase.GetGoogleOAuthCredentialsAsync();
                if (creds != null)
                {
                    _clientId = creds.ClientId ?? "";
                    _clientSecret = creds.ClientSecret ?? "";
                    _firebaseApiKey = creds.FirebaseApiKey ?? "";
                }
            }
            catch { }
        }

        public async Task<GoogleSignInResult> SignInAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                return new GoogleSignInResult { Success = false, Error = "Google OAuth credentials yapılandırılmamış." };

            var port = GetAvailablePort();
            var redirectUri = $"http://localhost:{port}";

            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                $"client_id={Uri.EscapeDataString(_clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&response_type=code" +
                $"&scope={Uri.EscapeDataString(Scopes)}" +
                $"&access_type=offline" +
                $"&prompt=consent";

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                return new GoogleSignInResult { Success = false, Error = $"Local HTTP listener başlatılamadı: {ex.Message}" };
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                listener.Stop();
                return new GoogleSignInResult { Success = false, Error = $"Tarayıcı açılamadı: {ex.Message}" };
            }

            string authCode = null;
            string error = null;

            try
            {
                var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                var contextTask = await Task.WhenAny(
                    listener.GetContextAsync(),
                    Task.Delay(TimeSpan.FromMinutes(2), linkedCts.Token)
                );

                if (contextTask is Task<HttpListenerContext> ctxTask && ctxTask.IsCompletedSuccessfully)
                {
                    var ctx = ctxTask.Result;
                    var requestUrl = ctx.Request.Url.ToString();

                    var uri = new Uri(requestUrl);
                    var queryParams = ParseQueryString(uri.Query);

                    queryParams.TryGetValue("code", out authCode);
                    queryParams.TryGetValue("error", out error);

                    var responseBytes = Encoding.UTF8.GetBytes(@"
                        <html><body style='background:#0D0D0D;color:white;font-family:Segoe UI;text-align:center;padding-top:100px'>
                        <h2>Giriş başarılı!</h2>
                        <p>Bu sayfayı kapatabilirsiniz.</p>
                        </body></html>");
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.ContentLength64 = responseBytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    ctx.Response.Close();
                }
                else
                {
                    error = "timeout";
                }
            }
            catch
            {
                error = "connection_failed";
            }
            finally
            {
                listener.Stop();
            }

            if (!string.IsNullOrEmpty(error))
            {
                if (error == "timeout")
                    return new GoogleSignInResult { Success = false, Error = "Giriş zaman aşımına uğradı. Lütfen tekrar deneyin." };
                if (error == "access_denied")
                    return new GoogleSignInResult { Success = false, Error = "Giriş reddedildi." };
                return new GoogleSignInResult { Success = false, Error = $"Google hatası: {error}" };
            }

            if (string.IsNullOrEmpty(authCode))
                return new GoogleSignInResult { Success = false, Error = "Auth code alınamadı." };

            var tokenResult = await ExchangeCodeForTokensAsync(authCode, redirectUri);
            if (!tokenResult.Success)
                return tokenResult;

            var firebaseResult = await SignInWithFirebaseAsync(tokenResult.IdToken);
            if (!firebaseResult.Success)
                return firebaseResult;

            var userInfo = await GetUserInfoAsync(tokenResult.AccessToken);
            if (userInfo != null)
            {
                firebaseResult.Email = userInfo.Email;
                firebaseResult.DisplayName = userInfo.DisplayName;
                firebaseResult.Picture = userInfo.Picture;
            }

            // Firebase auth state'ini güncelle
            if (!string.IsNullOrEmpty(firebaseResult.FirebaseIdToken) && App.Firebase != null)
            {
                App.Firebase.SetAuthState(firebaseResult.FirebaseIdToken, firebaseResult.FirebaseUid);
            }

            return firebaseResult;
        }

        private async Task<GoogleSignInResult> ExchangeCodeForTokensAsync(string authCode, string redirectUri)
        {
            try
            {
                var body = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("code", authCode),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri),
                    new KeyValuePair<string, string>("grant_type", "authorization_code")
                });

                var resp = await _http.PostAsync(GoogleTokenUrl, body);
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    return new GoogleSignInResult { Success = false, Error = $"Token alınamadı: {json}" };

                var data = JObject.Parse(json);
                return new GoogleSignInResult
                {
                    Success = true,
                    IdToken = data["id_token"]?.ToString(),
                    AccessToken = data["access_token"]?.ToString(),
                    RefreshToken = data["refresh_token"]?.ToString()
                };
            }
            catch (Exception ex)
            {
                return new GoogleSignInResult { Success = false, Error = $"Token değiştirme hatası: {ex.Message}" };
            }
        }

        private async Task<GoogleSignInResult> SignInWithFirebaseAsync(string googleIdToken)
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
                    $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={_firebaseApiKey}",
                    new StringContent(body, Encoding.UTF8, "application/json"));

                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    return new GoogleSignInResult { Success = false, Error = $"Firebase giriş hatası: {json}" };

                var data = JObject.Parse(json);
                return new GoogleSignInResult
                {
                    Success = true,
                    FirebaseIdToken = data["idToken"]?.ToString(),
                    FirebaseUid = data["localId"]?.ToString(),
                    FirebaseRefreshToken = data["refreshToken"]?.ToString(),
                    IdToken = googleIdToken,
                    IsNewUser = data["isNewUser"]?.ToObject<bool>() ?? false
                };
            }
            catch (Exception ex)
            {
                return new GoogleSignInResult { Success = false, Error = $"Firebase bağlantı hatası: {ex.Message}" };
            }
        }

        private async Task<GoogleUserInfo> GetUserInfoAsync(string accessToken)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, GoogleUserInfoUrl);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync();
                var data = JObject.Parse(json);

                return new GoogleUserInfo
                {
                    Email = data["email"]?.ToString(),
                    DisplayName = data["name"]?.ToString(),
                    Picture = data["picture"]?.ToString()
                };
            }
            catch { return null; }
        }

        private static int GetAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(query)) return result;

            query = query.TrimStart('?');
            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split('=');
                if (parts.Length == 2)
                    result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
            return result;
        }
    }

    public class GoogleSignInResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }

        public string IdToken { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }

        public string FirebaseIdToken { get; set; }
        public string FirebaseUid { get; set; }
        public string FirebaseRefreshToken { get; set; }

        public string Email { get; set; }
        public string DisplayName { get; set; }
        public string Picture { get; set; }

        public bool IsNewUser { get; set; }
    }

    public class GoogleUserInfo
    {
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public string Picture { get; set; }
    }
}
