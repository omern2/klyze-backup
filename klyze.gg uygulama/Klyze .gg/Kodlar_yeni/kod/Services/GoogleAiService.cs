using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    public class GoogleAiService : IDisposable
    {
        private static readonly string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
        private static readonly string OpenRouterEndpoint = "https://openrouter.ai/api/v1/chat/completions";
        private static readonly string GeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-001:generateContent";

        private string _aktifModel = "llama-3.1-8b-instant";

        private readonly HttpClient _http;
        private readonly ValorantDataService _valorantData;
        private readonly WebSearchService _webSearch;
        private readonly List<string> _apiKeys = new();
        private int _aktifKeyIndex;
        private string _saglayiciAdi;
        private DateTime _keyKilitZamani;

        public GoogleAiService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            _valorantData = new ValorantDataService();
            _webSearch = new WebSearchService();
            _webSearch.SetApiKey(ApiKeyProvider.TavilyApiKey);
            _aktifKeyIndex = 0;

            if (!string.IsNullOrEmpty(ApiKeyProvider.GroqAiKey))
                _apiKeys.Add(ApiKeyProvider.GroqAiKey);
            if (!string.IsNullOrEmpty(ApiKeyProvider.GroqAiKey2))
                _apiKeys.Add(ApiKeyProvider.GroqAiKey2);
            if (!string.IsNullOrEmpty(ApiKeyProvider.OpenRouterAiKey))
                _apiKeys.Add(ApiKeyProvider.OpenRouterAiKey);
            if (!string.IsNullOrEmpty(ApiKeyProvider.GoogleAiKey))
                _apiKeys.Add(ApiKeyProvider.GoogleAiKey);

            if (_apiKeys.Count > 0)
            {
                var ilk = _apiKeys[0];
                if (ilk.StartsWith("gsk_"))
                    _saglayiciAdi = "Groq";
                else if (ilk.StartsWith("sk-or-"))
                    _saglayiciAdi = "OpenRouter";
                else
                    _saglayiciAdi = "Gemini";
            }
            else
            {
                _saglayiciAdi = "";
            }
        }

        public string AktifModel => _aktifModel;
        public string Saglayici => _saglayiciAdi;
        public string ApiKeyPreview => _apiKeys.Count > 0 ? _apiKeys[_aktifKeyIndex][..8] + "..." : "YOK";
        public int KeySayisi => _apiKeys.Count;
        public int AktifKeyIndex => _aktifKeyIndex;

        public void ModelDegistir(string modelAdi)
        {
            _aktifModel = modelAdi;
        }

        public void SetApiKeys(List<string> keys)
        {
            _apiKeys.Clear();
            foreach (var k in keys)
            {
                if (!string.IsNullOrEmpty(k))
                    _apiKeys.Add(k.Trim());
            }
            _aktifKeyIndex = 0;
            if (_apiKeys.Count > 0)
            {
                if (_apiKeys[0].StartsWith("gsk_"))
                    _saglayiciAdi = "Groq";
                else if (_apiKeys[0].StartsWith("sk-or-"))
                    _saglayiciAdi = "OpenRouter";
                else
                    _saglayiciAdi = "Gemini";
            }
        }

        public void SetApiKey(string key)
        {
            _apiKeys.Clear();
            if (!string.IsNullOrEmpty(key))
                _apiKeys.Add(key.Trim());
            _aktifKeyIndex = 0;

            if (key?.StartsWith("gsk_") == true)
                _saglayiciAdi = "Groq";
            else if (key?.StartsWith("sk-or-") == true)
                _saglayiciAdi = "OpenRouter";
            else if (!string.IsNullOrEmpty(key))
                _saglayiciAdi = "Gemini";
        }

        public void SetTavilyKey(string key)
        {
            _webSearch.SetApiKey(key ?? "");
        }

        public bool ApiKeyReady => _apiKeys.Count > 0;

        private string AktifKey => _aktifKeyIndex < _apiKeys.Count ? _apiKeys[_aktifKeyIndex] : "";

        private void SonrakiKey()
        {
            _aktifKeyIndex = (_aktifKeyIndex + 1) % _apiKeys.Count;
        }

        public async Task<(string yanit, int toplamToken)> SendMessageAsync(string mesaj, List<AiSohbetMesaj> gecmis)
        {
            if (_apiKeys.Count == 0)
                return ("API anahtarı ayarlanmamış.", 0);

            try
            {
                if (!_webSearch.ApiKeyReady && !string.IsNullOrEmpty(ApiKeyProvider.TavilyApiKey))
                    _webSearch.SetApiKey(ApiKeyProvider.TavilyApiKey);

                var genelVeri = await _valorantData.GetDetayliDataAsync();
                var hedefVeri = await _valorantData.GetHedefliVeriAsync(mesaj);
                var webVeri = await _webSearch.SearchGuncelBilgiAsync(mesaj);
                var bugun = DateTime.UtcNow.ToString("dd MMMM yyyy");
                var sabitPromptlar = SystemPromptLoader.TumPromptlariGetir();

                if (_saglayiciAdi == "Gemini")
                    return await GeminiGonderAsync(mesaj, gecmis, genelVeri, hedefVeri, webVeri, bugun, sabitPromptlar);
                else
                    return await OpenAICompatibleGonderAsync(mesaj, gecmis, genelVeri, hedefVeri, webVeri, bugun, sabitPromptlar);
            }
            catch (TaskCanceledException)
            {
                return ("Yanıt zaman aşımına uğradı. Lütfen tekrar dene.", 0);
            }
            catch (Exception ex)
            {
                return ($"Hata: {ex.Message}", 0);
            }
        }

        private async Task<(string, int)> OpenAICompatibleGonderAsync(string mesaj, List<AiSohbetMesaj> gecmis,
            string genelVeri, string hedefVeri, string webVeri, string bugun, List<(string, string)> sabitPromptlar)
        {
            var endpoint = _saglayiciAdi == "Groq" ? GroqEndpoint : OpenRouterEndpoint;
            var modelAdi = _saglayiciAdi == "Groq" ? _aktifModel : "openai/gpt-4o";

            var messages = new List<GroqMesaj>();

            foreach (var (dosyaAdi, icerik) in sabitPromptlar)
                messages.Add(new GroqMesaj { role = "system", content = icerik.Replace("{BUGUN}", bugun) });

            if (!string.IsNullOrEmpty(hedefVeri))
                messages.Add(new GroqMesaj { role = "system", content = $"[HEDEF_SORU_VERISI]\n{hedefVeri}[/HEDEF_SORU_VERISI]" });

            if (!string.IsNullOrEmpty(webVeri))
                messages.Add(new GroqMesaj { role = "system", content = webVeri });

            messages.Add(new GroqMesaj { role = "system", content = $"[API VERITABANI]\n{genelVeri}[/API VERITABANI]" });

            var sonGecmis = gecmis.Count > 4 ? gecmis.Skip(gecmis.Count - 4).ToList() : gecmis;
            foreach (var m in sonGecmis)
                messages.Add(new GroqMesaj { role = m.KullaniciMesaji ? "user" : "assistant", content = m.Icerik });

            messages.Add(new GroqMesaj { role = "user", content = mesaj });

            var request = new GroqIstek
            {
                model = modelAdi,
                messages = messages.ToArray(),
                max_tokens = 300,
                temperature = 0.3
            };

            var jsonBody = JsonConvert.SerializeObject(request);

            var sonRateLimit = DateTime.MinValue;
            var denenenKeyler = new HashSet<int>();
            var toplamDeneme = 0;
            var maxDeneme = Math.Max(_apiKeys.Count * 2, 5);

            while (denenenKeyler.Count < _apiKeys.Count && toplamDeneme < maxDeneme)
            {
                toplamDeneme++;

                if ((DateTime.UtcNow - sonRateLimit).TotalSeconds < 2)
                    await Task.Delay(2000);

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AktifKey);
                if (_saglayiciAdi == "OpenRouter")
                {
                    httpRequest.Headers.Add("HTTP-Referer", "https://klyze.gg");
                    httpRequest.Headers.Add("X-Title", "Klyze AI");
                }
                httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(httpRequest);
                httpRequest.Dispose();
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var yanit = JsonConvert.DeserializeObject<GroqYanit>(json);
                    if (yanit?.choices == null || yanit.choices.Length == 0)
                        return ("Yanıt alınamadı.", 0);
                    var metin = yanit.choices[0]?.message?.content ?? "";
                    var token = yanit.usage?.total_tokens ?? 0;
                    return (metin, token);
                }
                if ((int)resp.StatusCode == 429)
                {
                    sonRateLimit = DateTime.UtcNow;
                    denenenKeyler.Add(_aktifKeyIndex);
                    SonrakiKey();
                    var bekle = resp.Headers.RetryAfter?.Delta?.TotalSeconds is double d && d > 0
                        ? (int)(d * 1000) : 3000;
                    await Task.Delay(bekle);
                    continue;
                }
                var hata = await resp.Content.ReadAsStringAsync();
                var onEk = AktifKey.Length > 6 ? AktifKey[..6] : AktifKey;
                return ($"[{_saglayiciAdi}:{modelAdi}] {hata}", 0);
            }
            return ($"Tüm API anahtarları rate limit aştı. 30 saniye bekleyip tekrar dene. (denenen: {denenenKeyler.Count}/{_apiKeys.Count})", 0);
        }

        private async Task<(string, int)> GeminiGonderAsync(string mesaj, List<AiSohbetMesaj> gecmis,
            string genelVeri, string hedefVeri, string webVeri, string bugun, List<(string, string)> sabitPromptlar)
        {
            var systemPrompt = "";
            foreach (var (dosyaAdi, icerik) in sabitPromptlar)
                systemPrompt += icerik.Replace("{BUGUN}", bugun) + "\n\n";

            if (!string.IsNullOrEmpty(hedefVeri))
                systemPrompt += $"[HEDEF_SORU_VERISI]\n{hedefVeri}[/HEDEF_SORU_VERISI]\n\n";
            if (!string.IsNullOrEmpty(webVeri))
                systemPrompt += webVeri + "\n\n";
            systemPrompt += $"[API VERITABANI]\n{genelVeri}[/API VERITABANI]\n\n";

            var contents = new List<object>();
            contents.Add(new { role = "user", parts = new[] { new { text = systemPrompt + "\n\n" + mesaj } } });

            var geminiRequest = new
            {
                contents = contents.ToArray(),
                generationConfig = new
                {
                    maxOutputTokens = 800,
                    temperature = 0.3
                }
            };

            var jsonBody = JsonConvert.SerializeObject(geminiRequest);
            var url = $"{GeminiEndpoint}?key={AktifKey}";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(httpRequest);

            if (!resp.IsSuccessStatusCode)
            {
                var hata = await resp.Content.ReadAsStringAsync();
                return ($"Gemini hatası ({(int)resp.StatusCode}): {hata}", 0);
            }

            var json = await resp.Content.ReadAsStringAsync();
            var geminiYanit = JObject.Parse(json);
            var candidates = geminiYanit["candidates"] as JArray;
            if (candidates == null || candidates.Count == 0)
                return ("Yanıt alınamadı.", 0);

            var metin = candidates[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "";
            return (metin, 0);
        }

        public void Dispose()
        {
            _http?.Dispose();
            _valorantData?.Dispose();
            _webSearch?.Dispose();
        }
    }
}
