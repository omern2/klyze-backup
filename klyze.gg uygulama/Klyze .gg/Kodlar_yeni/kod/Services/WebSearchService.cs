using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ValorantAutoClicker.Services
{
    public class WebSearchService : IDisposable
    {
        private readonly HttpClient _http;
        private string _apiKey;

        private static readonly ConcurrentDictionary<string, (string sonuc, DateTime zaman)> _cache = new();

        public WebSearchService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _http.DefaultRequestHeaders.Add("User-Agent", "Klyze/1.0 (Valorant Coach)");
        }

        public bool ApiKeyReady => !string.IsNullOrEmpty(_apiKey);

        public void SetApiKey(string key)
        {
            _apiKey = key ?? "";
        }

        public async Task<string> SearchGuncelBilgiAsync(string soru)
        {
            if (string.IsNullOrEmpty(_apiKey))
                return "";

            var normalized = soru.Trim().ToLowerInvariant();

            if (_cache.TryGetValue(normalized, out var cached) && (DateTime.UtcNow - cached.zaman).TotalHours < 1)
                return cached.sonuc;

            try
            {
                var body = new
                {
                    api_key = _apiKey,
                    query = $"{soru} valorant 2026",
                    search_depth = "advanced",
                    max_results = 8,
                    include_answer = true
                };

                var jsonBody = System.Text.Json.JsonSerializer.Serialize(body);
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search");
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(request);
                if (!resp.IsSuccessStatusCode)
                    return "";

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var root = doc.RootElement;

                var metin = "";

                if (root.TryGetProperty("answer", out var answer) && answer.ValueKind == JsonValueKind.String)
                {
                    var ans = answer.GetString() ?? "";
                    if (!string.IsNullOrEmpty(ans))
                        metin += $"ÖZET: {ans.Substring(0, Math.Min(ans.Length, 500))}\n";
                }

                if (root.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                {
                    var limit = Math.Min(results.GetArrayLength(), 5);
                    for (int i = 0; i < limit; i++)
                    {
                        var r = results[i];
                        var title = r.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                        var content = r.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                        var link = r.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";

                        if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(content))
                        {
                            metin += $"• {title}";
                            if (!string.IsNullOrEmpty(content))
                                metin += $": {content.Substring(0, Math.Min(content.Length, 300))}";
                            if (!string.IsNullOrEmpty(link))
                                metin += $" (Kaynak: {link})";
                            metin += "\n";
                        }
                    }
                }

                var sonuc = string.IsNullOrEmpty(metin) ? "" : $"[GÜNCEL_VERİ]\n{metin}\n[/GÜNCEL_VERİ]";

                _cache[normalized] = (sonuc, DateTime.UtcNow);

                return sonuc;
            }
            catch
            {
                if (_cache.TryGetValue(normalized, out var onceki))
                    return onceki.sonuc;
                return "";
            }
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
