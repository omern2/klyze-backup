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
        private string _baseUrl = "https://europe.api.riotgames.com";
        private string _accountApiBaseUrl = "https://europe.api.riotgames.com/riot/account/v1/accounts";

        public ValorantApiService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-Riot-Token", _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // Update region-specific URLs
        public void SetRegion(string region)
        {
            // Update base URL based on region
            // Riot uses different regional shards: americas, europe, sea, etc.
            switch (region.ToLower())
            {
                case "na":
                case "br":
                case "lan":
                case "las":
                    _baseUrl = "https://americas.api.riotgames.com";
                    _accountApiBaseUrl = "https://americas.api.riotgames.com/riot/account/v1/accounts";
                    break;
                case "eu":
                case "tr":
                case "ru":
                    _baseUrl = "https://europe.api.riotgames.com";
                    _accountApiBaseUrl = "https://europe.api.riotgames.com/riot/account/v1/accounts";
                    break;
                case "kr":
                case "jp":
                    _baseUrl = "https://sea.api.riotgames.com";
                    _accountApiBaseUrl = "https://sea.api.riotgames.com/riot/account/v1/accounts";
                    break;
                default:
                    _baseUrl = "https://europe.api.riotgames.com";
                    _accountApiBaseUrl = "https://europe.api.riotgames.com/riot/account/v1/accounts";
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
                        throw new Exception("Account not found. Please check your username and tag.");
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        throw new Exception("Invalid API key. Please check your Riot API key.");
                    else if ((int)response.StatusCode == 429)
                        throw new Exception("Rate limit exceeded. Please try again later.");
                    else
                        throw new Exception($"API error: {(int)response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ValorantAccountResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch account data: {ex.Message}");
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
                        throw new Exception("Rate limit exceeded. Please try again later.");
                    else
                        throw new Exception($"Failed to fetch match list: {(int)response.StatusCode}");
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
                throw new Exception($"Failed to fetch match IDs: {ex.Message}");
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
                        throw new Exception("Rate limit exceeded. Please try again later.");
                    else
                        throw new Exception($"Failed to fetch match details: {(int)response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ValorantMatchDetail>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch match details: {ex.Message}");
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