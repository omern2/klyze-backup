using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    public class ValorantApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        private static string EuropeUrl => Helpers.StringObfuscator.Decode(
            "r7Ozt7T96OiisrWot6Lppreu6bWuqLOgpqqitOmkqKo=", 0xC7);
        private static string AmericasUrl => Helpers.StringObfuscator.Decode(
            "r7Ozt7T96OimqqK1rqSmtOmmt67pta6os6CmqqK06aSoqg==", 0xC7);
        private static string SeaUrl => Helpers.StringObfuscator.Decode(
            "r7Ozt7T96Oi0oqbppreu6bWuqLOgpqqitOmkqKo=", 0xC7);
        private static string AccountPath => Helpers.StringObfuscator.Decode(
            "6LWuqLPopqSkqLKps+ix9uimpKSosqmztA==", 0xC7);

        private string _baseUrl;
        private string _accountApiBaseUrl;

        public ValorantApiService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add(
                Helpers.StringObfuscator.Decode("n+qVrqiz6pOorKKp", 0xC7), _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(
                Helpers.StringObfuscator.Decode("mYiIlJGbmYyRl5bXkouXlg==", 0xF8)));
            _baseUrl = EuropeUrl;
            _accountApiBaseUrl = EuropeUrl + AccountPath;
        }

        public void SetRegion(string region)
        {
            switch (region.ToLower())
            {
                case "na":
                case "br":
                case "lan":
                case "las":
                    _baseUrl = AmericasUrl;
                    _accountApiBaseUrl = AmericasUrl + AccountPath;
                    break;
                case "eu":
                case "tr":
                case "ru":
                    _baseUrl = EuropeUrl;
                    _accountApiBaseUrl = EuropeUrl + AccountPath;
                    break;
                case "kr":
                case "jp":
                    _baseUrl = SeaUrl;
                    _accountApiBaseUrl = SeaUrl + AccountPath;
                    break;
                default:
                    _baseUrl = EuropeUrl;
                    _accountApiBaseUrl = EuropeUrl + AccountPath;
                    break;
            }
        }

        // Get account by username/tag
        public async Task<ValorantAccountResponse> GetAccountByNameTag(string gameName, string tagLine)
        {
            try
            {
                var url = $"{_accountApiBaseUrl}/by-riot-id/{Uri.EscapeDataString(gameName)}/{Uri.EscapeDataString(tagLine)}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    // Handle specific error cases
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        throw new Exception("Hesap bulunamadı. Kullanıcı adı ve etiketi kontrol edin.");
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        throw new Exception("Geçersiz API anahtarı. Riot API anahtarınızı kontrol edin.");
                    else if ((int)response.StatusCode == 429)
                        throw new Exception("Çok fazla istek. Lütfen bekleyin.");
                    else
                        throw new Exception($"API hatası: {(int)response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ValorantAccountResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Hesap verisi alınamadı: {ex.Message}");
            }
        }

        // Get match IDs for a player (puuid)
        public async Task<List<string>> GetMatchIds(string puuid, int count = 20, int start = 0)
        {
            try
            {
                var url = $"{_baseUrl}/val/match/v1/matchlists/by-puuid/{puuid}?start={start}&count={count}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 429)
                        throw new Exception("Çok fazla istek. Lütfen bekleyin.");
                    else
                        throw new Exception($"Maç listesi alınamadı: {(int)response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                // The response format is { "history": [ ... ] }
                using var doc = JsonDocument.Parse(json);
                var historyElement = doc.RootElement.GetProperty("history");
                var matchIds = new List<string>();

                foreach (var element in historyElement.EnumerateArray())
                {
                    matchIds.Add(element.GetProperty("matchId").GetString());
                }

                return matchIds;
            }
            catch (Exception ex)
            {
                throw new Exception($"Maç ID'leri alınamadı: {ex.Message}");
            }
        }

        // Get detailed match data
        public async Task<ValorantMatchDetail> GetMatchDetails(string matchId)
        {
            try
            {
                var url = $"{_baseUrl}/val/match/v1/matches/{matchId}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 429)
                        throw new Exception("Çok fazla istek. Lütfen bekleyin.");
                    else
                        throw new Exception($"Maç detayı alınamadı: {(int)response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ValorantMatchDetail>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Maç detayı alınamadı: {ex.Message}");
            }
        }

        // Get player's recent matches with details
        public async Task<List<ValorantMatchDetail>> GetRecentMatches(string puuid, int count = 10)
        {
            var matchIds = await GetMatchIds(puuid, count);
            var matches = new List<ValorantMatchDetail>();

            // Fetch match details concurrently (with some delay to avoid rate limiting)
            foreach (var matchId in matchIds)
            {
                try
                {
                    var match = await GetMatchDetails(matchId);
                    matches.Add(match);

                    // Small delay to help with rate limiting
                    await Task.Delay(100);
                }
                catch (Exception)
                {
                    // Skip failed matches but continue
                    continue;
                }
            }

            return matches;
        }
    }
}