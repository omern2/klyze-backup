using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    public class RiotLiveMatchService : IDisposable
    {
        private const string LockfilePath = @"Riot Games\Riot Client\Config\lockfile";
        private static string HenrikBase => Helpers.StringObfuscator.Decode(
            "rbGxtbb/6uqktazrraCrt6yuoaCz6728vw==", 0xC5);
        private static readonly string LogPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "riot_debug.log");

        private readonly HttpClient _localHttp;
        private readonly HttpClient _henrikHttp;
        private readonly HttpClient _glzHttp;

        private string _lockfilePath;
        private static readonly object _logLock = new();

        private static string TokenHash(string token)
        {
            if (string.IsNullOrEmpty(token)) return "null";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash)[..8] + "...";
        }

        private static void Log(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            System.Diagnostics.Debug.WriteLine(line);
            try
            {
                lock (_logLock)
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch { }
        }

        public RiotLiveMatchService()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _lockfilePath = Path.Combine(localAppData, LockfilePath);

            // Debug log — removed hardcoded developer paths

            var handler = new HttpClientHandler();
            // Riot local API uses self-signed cert on 127.0.0.1 — only bypass for localhost
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) =>
            {
                if (sender is HttpRequestMessage req &&
                    (req.RequestUri?.Host == "127.0.0.1" || req.RequestUri?.Host == "localhost"))
                    return true;
                return errors == System.Net.Security.SslPolicyErrors.None;
            };
            _localHttp = new HttpClient(handler)
            { Timeout = TimeSpan.FromSeconds(10) };

            _henrikHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            if (!string.IsNullOrEmpty(ApiKeyProvider.HenrikDevKey))
                _henrikHttp.DefaultRequestHeaders.Add("Authorization", ApiKeyProvider.HenrikDevKey);

            _glzHttp = new HttpClient
            { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<LiveMatchData> GetLiveMatchAsync(
            string currentRegion, string currentName, string currentTag,
            CancellationToken ct = default)
        {
            // Step 1: Read lockfile
            Log($"[RiotLiveMatch] Lockfile path: {_lockfilePath}");
            var lockData = ReadLockfile();
            if (lockData == null)
            {
                Log("[RiotLiveMatch] Lockfile OKUNAMADI - null");
                return null;
            }

            var (port, password) = lockData.Value;
            Log($"[RiotLiveMatch] Lockfile -> port={port}, password={password}");

            // Her çağrıda client version'ı taze al (Valorant açılıp kapanabilir)
            _cachedClientVersion = null;

            byte[] credBytes = null;
            try
            {
                credBytes = Encoding.UTF8.GetBytes($"riot:{password}");
                var base64 = Convert.ToBase64String(credBytes);
                _localHttp.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", base64);
            }
            finally
            {
                if (credBytes != null)
                    Array.Clear(credBytes, 0, credBytes.Length);
            }

            var localBase = $"https://127.0.0.1:{port}";

            // Step 2: Get PUUID from local API
            string puuid;
            try
            {
                var authResp = await _localHttp.GetAsync(
                    $"{localBase}/rso-auth/v1/authorization", ct);
                Log($"[RiotLiveMatch] /rso-auth/v1/authorization -> status={authResp.StatusCode}");
                var authBody = await authResp.Content.ReadAsStringAsync();
                Log($"[RiotLiveMatch] auth body: {authBody}");
                if (!authResp.IsSuccessStatusCode)
                {
                    Log("[RiotLiveMatch] auth basarisiz, null donuyorum");
                    return null;
                }
                var authJson = JObject.Parse(authBody);
                puuid = authJson["subject"]?.ToString();
                Log($"[RiotLiveMatch] PUUID: {puuid}");
                if (string.IsNullOrEmpty(puuid))
                {
                    Log("[RiotLiveMatch] PUUID bos, null donuyorum");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] auth HATA: {ex.Message}");
                return null;
            }

            // Step 3: Get entitlements + access token from local API
            string accessToken, entitlementsToken;
            try
            {
                var entResp = await _localHttp.GetAsync(
                    $"{localBase}/entitlements/v1/token", ct);
                Log($"[RiotLiveMatch] /entitlements/v1/token -> status={entResp.StatusCode}");
                var entBody = await entResp.Content.ReadAsStringAsync();
                if (!entResp.IsSuccessStatusCode)
                {
                    Log("[RiotLiveMatch] entitlements token basarisiz");
                    return null;
                }
                var entJson = JObject.Parse(entBody);
                accessToken = entJson["accessToken"]?.ToString();
                entitlementsToken = entJson["token"]?.ToString();
                Log($"[RiotLiveMatch] accessToken={TokenHash(accessToken)}");
                Log($"[RiotLiveMatch] entitlementsToken={TokenHash(entitlementsToken)}");
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(entitlementsToken))
                {
                    Log("[RiotLiveMatch] tokenlar bos, null donuyorum");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] entitlements HATA: {ex.Message}");
                return null;
            }

            var clientPlatform = "ew0KCSJwbGF0Zm9ybVR5cGUiOiAiUEMiLA0KCSJwbGF0Zm9ybU9TIjogIldpbmRvd3MiLA0KCSJwbGF0Zm9ybU9TVmVyc2lvbiI6ICIxMC4wLjE5MDQyLjEuMjU2LjY0Yml0IiwNCgkicGxhdGZvcm1DaGlwc2V0IjogIlVua25vd24iDQp9";

            // Step 5: GLZ sunucusuna core-game / pregame isteği at
            // NOT: Bu endpoint'ler local API'de değil, GLZ (Valorant game server) üzerinde!
            var glzHost = GetGlzHost(currentRegion);
            Log($"[RiotLiveMatch] region={currentRegion}, glzHost={glzHost}");
            if (string.IsNullOrEmpty(glzHost))
            {
                Log("[RiotLiveMatch] glzHost bulunamadi, null donuyorum");
                return null;
            }

            var glzBase = $"https://{glzHost}";

            string matchId;
            string gameMode = "core";
            try
            {
                // core-game: oyuncu aktif maçtaysa
                var coreUrl = $"{glzBase}/core-game/v1/players/{puuid}";
                Log($"[RiotLiveMatch] core-game URL: {coreUrl}");

                var coreReq = new HttpRequestMessage(HttpMethod.Get, coreUrl);
                coreReq.Headers.Add("Authorization", $"Bearer {accessToken}");
                coreReq.Headers.Add("X-Riot-Entitlements-JWT", entitlementsToken);
                coreReq.Headers.Add("X-Riot-ClientPlatform", clientPlatform);
                coreReq.Headers.Add("X-Riot-ClientVersion", await GetClientVersionAsync(localBase, ct));

                var playerResp = await _glzHttp.SendAsync(coreReq, ct);
                Log($"[RiotLiveMatch] core-game -> status={playerResp.StatusCode}");
                if (playerResp.IsSuccessStatusCode)
                {
                    var playerBody = await playerResp.Content.ReadAsStringAsync();
                    Log($"[RiotLiveMatch] core-game body: {playerBody}");
                    var playerJson = JObject.Parse(playerBody);
                    matchId = playerJson["MatchID"]?.ToString();
                    Log($"[RiotLiveMatch] core-game MatchID: {matchId}");
                }
                else
                {
                    // pregame: ajan seçim ekranındaysa
                    var pregameUrl = $"{glzBase}/pregame/v1/players/{puuid}";
                    Log($"[RiotLiveMatch] core-game basarisiz, pregame URL: {pregameUrl}");

                    var pregameReq = new HttpRequestMessage(HttpMethod.Get, pregameUrl);
                    pregameReq.Headers.Add("Authorization", $"Bearer {accessToken}");
                    pregameReq.Headers.Add("X-Riot-Entitlements-JWT", entitlementsToken);
                    pregameReq.Headers.Add("X-Riot-ClientPlatform", clientPlatform);
                    pregameReq.Headers.Add("X-Riot-ClientVersion", await GetClientVersionAsync(localBase, ct));

                    var pregameResp = await _glzHttp.SendAsync(pregameReq, ct);
                    Log($"[RiotLiveMatch] pregame -> status={pregameResp.StatusCode}");
                    if (!pregameResp.IsSuccessStatusCode)
                    {
                        var errBody = await pregameResp.Content.ReadAsStringAsync();
                        Log($"[RiotLiveMatch] pregame hata body: {errBody}");
                        Log("[RiotLiveMatch] pregame de basarisiz, null donuyorum");
                        return null;
                    }
                    var pregameBody = await pregameResp.Content.ReadAsStringAsync();
                    Log($"[RiotLiveMatch] pregame body: {pregameBody}");
                    var pregameJson = JObject.Parse(pregameBody);
                    matchId = pregameJson["MatchID"]?.ToString();
                    Log($"[RiotLiveMatch] pregame MatchID: {matchId}");
                    gameMode = "pregame";
                }
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] core/pregame HATA: {ex.Message}");
                return null;
            }

            if (string.IsNullOrEmpty(matchId))
            {
                Log("[RiotLiveMatch] matchId bos, null donuyorum");
                return null;
            }

            // Step 6: GLZ'den maç detaylarını çek
            JObject matchJson;
            try
            {
                var endpoint = gameMode == "core"
                    ? $"{glzBase}/core-game/v1/matches/{matchId}"
                    : $"{glzBase}/pregame/v1/matches/{matchId}";
                Log($"[RiotLiveMatch] match detay URL: {endpoint}");

                var matchReq = new HttpRequestMessage(HttpMethod.Get, endpoint);
                matchReq.Headers.Add("Authorization", $"Bearer {accessToken}");
                matchReq.Headers.Add("X-Riot-Entitlements-JWT", entitlementsToken);
                matchReq.Headers.Add("X-Riot-ClientPlatform", clientPlatform);
                matchReq.Headers.Add("X-Riot-ClientVersion", await GetClientVersionAsync(localBase, ct));

                var matchResp = await _glzHttp.SendAsync(matchReq, ct);
                Log($"[RiotLiveMatch] match detay -> status={matchResp.StatusCode}");
                if (!matchResp.IsSuccessStatusCode)
                {
                    var errBody = await matchResp.Content.ReadAsStringAsync();
                    Log($"[RiotLiveMatch] match detay hata body: {errBody}");
                    Log("[RiotLiveMatch] match detay basarisiz, null donuyorum");
                    return null;
                }
                var matchBody = await matchResp.Content.ReadAsStringAsync();
                if (matchBody.Length > 1000)
                    Log($"[RiotLiveMatch] match body (ilk 1000): {matchBody[..1000]}");
                else
                    Log($"[RiotLiveMatch] match body: {matchBody}");
                matchJson = JObject.Parse(matchBody);
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] match detay HATA: {ex.Message}");
                return null;
            }

            // Step 7: Parse match data
            Log("[RiotLiveMatch] ParseCoreGameData cagriliyor...");
            var result = await ParseCoreGameDataAsync(matchJson, currentName, currentTag, puuid, gameMode, ct);
            Log($"[RiotLiveMatch] sonuc={(result != null ? "OK" : "NULL")}");
            return result;
        }

        private string _cachedClientVersion = null;
        private async Task<string> GetClientVersionAsync(string localBase, CancellationToken ct)
        {
            if (_cachedClientVersion != null) return _cachedClientVersion;
            try
            {
                var sessResp = await _localHttp.GetAsync($"{localBase}/product-session/v1/external-sessions", ct);
                if (sessResp.IsSuccessStatusCode)
                {
                    var sessBody = await sessResp.Content.ReadAsStringAsync();
                    Log($"[RiotLiveMatch] external-sessions: {(sessBody.Length > 1000 ? sessBody[..1000] : sessBody)}");
                    var sessJson = JObject.Parse(sessBody);

                    foreach (var prop in sessJson.Properties())
                    {
                        var productId = prop.Value["productId"]?.ToString() ?? "";
                        var sessionVersion = prop.Value["version"]?.ToString() ?? "";
                        var hasArgs = (prop.Value["launchConfiguration"]?["arguments"] as JArray)?.Count > 0;
                        Log($"[RiotLiveMatch] Session key={prop.Name[..Math.Min(8,prop.Name.Length)]}, productId={productId}, version={sessionVersion}, hasArgs={hasArgs}");

                        // Sadece riot_client session'ını atla, diğerlerini (productId boş olsa bile) dene
                        if (productId == "riot_client")
                            continue;

                        // launchConfiguration.arguments içinde -config-endpoint URL'den versiyon çıkar
                        var launchArgs = prop.Value["launchConfiguration"]?["arguments"] as JArray;
                        if (launchArgs != null)
                        {
                            foreach (var arg in launchArgs)
                            {
                                var argStr = arg.ToString();
                                // "-config-endpoint=https://.../release-XX.XX-shipping-XX-XXXXXXX/..."
                                var idx = argStr.IndexOf("release-", StringComparison.OrdinalIgnoreCase);
                                if (idx >= 0)
                                {
                                    var version = argStr.Substring(idx);
                                    var end = version.IndexOfAny(new[] { '/', ' ', '"' });
                                    if (end > 0) version = version[..end];
                                    if (!string.IsNullOrEmpty(version))
                                    {
                                        Log($"[RiotLiveMatch] ClientVersion (arg): {version}");
                                        _cachedClientVersion = version;
                                        return version;
                                    }
                                }
                                // Ayrıca "shipping-" ile başlayan versiyon da ara
                                var shipIdx = argStr.IndexOf("shipping-", StringComparison.OrdinalIgnoreCase);
                                if (shipIdx >= 0)
                                {
                                    var version2 = argStr.Substring(shipIdx);
                                    var end2 = version2.IndexOfAny(new[] { '/', ' ', '"' });
                                    if (end2 > 0) version2 = version2[..end2];
                                    if (!string.IsNullOrEmpty(version2))
                                    {
                                        Log($"[RiotLiveMatch] ClientVersion (shipping arg): {version2}");
                                        _cachedClientVersion = version2;
                                        return version2;
                                    }
                                }
                            }
                        }

                        // version alanını kullan (yeni format: hex hash, eski format: release-XX.XX-shipping-XX-XXXXXXX)
                        if (!string.IsNullOrEmpty(sessionVersion) && sessionVersion != "0")
                        {
                            Log($"[RiotLiveMatch] ClientVersion (session): {sessionVersion}");
                            _cachedClientVersion = sessionVersion;
                            return sessionVersion;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] GetClientVersion HATA: {ex.Message}");
            }

            // Fallback: güncel Valorant sürümü
            Log("[RiotLiveMatch] ClientVersion fallback kullaniliyor");
            _cachedClientVersion = "release-13.01-shipping-6-1234567";
            return _cachedClientVersion;
        }

        private (int port, string password)? ReadLockfile()
        {
            try
            {
                if (!File.Exists(_lockfilePath))
                {
                    Log("[RiotLiveMatch] ReadLockfile: File.Exists false");
                    return null;
                }
byte[] fileBytes = null;
try
{
    using var fs = new FileStream(_lockfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    fileBytes = new byte[fs.Length];
    fs.ReadExactly(fileBytes, 0, (int)fs.Length);
    var content = Encoding.UTF8.GetString(fileBytes).Trim();
    var parts = content.Split(':');
    if (parts.Length < 5) return null;
    if (!int.TryParse(parts[2], out int port)) return null;
    return (port, parts[3]);
}
finally
{
    if (fileBytes != null)
        Array.Clear(fileBytes, 0, fileBytes.Length);
}
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] ReadLockfile EXCEPTION: {ex.Message}");
                return null;
            }
        }

        private static string GetGlzHost(string region)
        {
            // Riot'un GLZ (Game Logic Zone) sunucuları.
            // TR1 platformId'si EU bölgesine bağlıdır.
            // Shard: eu, na, latam, br, ap, kr
            return region?.ToLowerInvariant() switch
            {
                "eu" or "tr" or "tr1" or "ru" => "glz-eu-1.eu.a.pvp.net",
                "na" or "us" => "glz-na-1.na.a.pvp.net",
                "br" or "br1" => "glz-br-1.br.a.pvp.net",
                "latam" or "la" or "la1" or "la2" => "glz-latam-1.latam.a.pvp.net",
                "kr" or "kr1" => "glz-kr-1.kr.a.pvp.net",
                "ap" or "jp" or "jp1" or "sg2" or "oc1" => "glz-ap-1.ap.a.pvp.net",
                _ => "glz-eu-1.eu.a.pvp.net" // varsayılan EU
            };
        }

        private async Task<LiveMatchData> ParseCoreGameDataAsync(JObject matchJson, string currentName, string currentTag, string currentPuuid, string gameMode, CancellationToken ct)
        {
            try
            {
                var result = new LiveMatchData();

                var mapId = matchJson["MapID"]?.ToString() ?? "";
                result.Map = MapIdToName(mapId);

                var queueId = matchJson["QueueID"]?.ToString() ?? matchJson["ModeID"]?.ToString() ?? "";
                result.Mode = QueueIdToName(queueId);
                result.GameType = gameMode;

                var gamePodId = matchJson["GamePodID"]?.ToString() ?? "";
                result.ServerName = GamePodToServerAdi(gamePodId);

                Log($"[RiotLiveMatch] Parse: MapID={mapId}, QueueID={queueId}");
                Log($"[RiotLiveMatch] Parse: JSON keys={string.Join(",", matchJson.Properties().Select(p => p.Name))}");
                var connDetails = matchJson["ConnectionDetails"] as JObject;
                if (connDetails != null)
                {
                    Log($"[RiotLiveMatch] ConnectionDetails keys={string.Join(",", connDetails.Properties().Select(x => x.Name))}");
                    foreach (var cdProp in connDetails.Properties())
                    {
                        var val = cdProp.Value?.ToString() ?? "null";
                        if (val.Length > 500) val = val[..500];
                        Log($"[RiotLiveMatch]   CD.{cdProp.Name}={val}");
                    }
                }
                var postGame = matchJson["PostGameDetails"] as JObject;
                if (postGame != null)
                    Log($"[RiotLiveMatch] PostGameDetails keys={string.Join(",", postGame.Properties().Select(x => x.Name))}");

                JArray players = null;
                var teamsToken = matchJson["Teams"] as JArray;
                var playersToken = matchJson["Players"] as JArray;

                Log($"[RiotLiveMatch] Parse: Teams={teamsToken?.Count.ToString() ?? "null"}, Players={playersToken?.Count.ToString() ?? "null"}");

                if (teamsToken != null && teamsToken.Count > 0 && teamsToken[0]["Players"] != null)
                {
                    players = new JArray(teamsToken.SelectMany(t => t["Players"] as JArray ?? new JArray()));
                    Log($"[RiotLiveMatch] Parse: Teams->Players yolu, toplam={players.Count}");
                }
                else if (playersToken != null)
                {
                    players = playersToken;
                    Log($"[RiotLiveMatch] Parse: Players yolu, toplam={players.Count}");
                }
                else
                {
                    Log("[RiotLiveMatch] Parse: Players bulunamadi, null donuyorum");
                    return null;
                }

                bool isDeathmatch = queueId.Contains("Deathmatch", StringComparison.OrdinalIgnoreCase)
                                 || queueId.Contains("deathmatch", StringComparison.OrdinalIgnoreCase);

                var team1 = new LiveMatchTeam { TeamName = "Red" };
                var team2 = new LiveMatchTeam { TeamName = "Blue" };

                foreach (var p in players)
                {
                    var teamId = p["TeamID"]?.ToString() ?? "Blue";
                    var puuid = p["Subject"]?.ToString() ?? "";
                    var charId = p["CharacterID"]?.ToString() ?? "";
                    Log($"[RiotLiveMatch] Parse: oyuncu puuid={puuid[..Math.Min(8,puuid.Length)]}, team={teamId}, char={charId[..Math.Min(8,charId.Length)]}");

                    var stats = p["Stats"];
                    if (stats != null)
                        Log($"[RiotLiveMatch] PlayerStats: K={stats["Kills"]?.ToString() ?? "?"} D={stats["Deaths"]?.ToString() ?? "?"} A={stats["Assists"]?.ToString() ?? "?"}");
                    var kills = 0; var deaths = 0; var assists = 0;
                    if (stats != null)
                    {
                        Log($"[RiotLiveMatch] Stats keys={string.Join(",", (stats as JObject)?.Properties().Select(x => x.Name) ?? Array.Empty<string>())}");
                        kills = stats["Kills"]?.Value<int>() ?? stats["kills"]?.Value<int>() ?? 0;
                        deaths = stats["Deaths"]?.Value<int>() ?? stats["deaths"]?.Value<int>() ?? 0;
                        assists = stats["Assists"]?.Value<int>() ?? stats["assists"]?.Value<int>() ?? 0;
                    }
                    else
                    {
                        Log($"[RiotLiveMatch] Stats=null, player keys={string.Join(",", (p as JObject)?.Properties().Select(x => x.Name) ?? Array.Empty<string>())}");
                    }

                    var player = new LiveMatchPlayer
                    {
                        Agent = AgentUuidToName(charId),
                        Puuid = puuid,
                        IsCurrentUser = puuid.Equals(currentPuuid, StringComparison.OrdinalIgnoreCase),
                        Kills = kills,
                        Deaths = deaths,
                        Assists = assists,
                        HasKda = stats != null
                    };

                    // Deathmatch'te herkes aynı takımda gelir, 5'e böl
                    if (isDeathmatch)
                    {
                        if (team1.Players.Count < 6)
                            team1.Players.Add(player);
                        else
                            team2.Players.Add(player);
                    }
                    else
                    {
                        if (teamId.IndexOf("Red", StringComparison.OrdinalIgnoreCase) >= 0)
                            team1.Players.Add(player);
                        else
                            team2.Players.Add(player);
                    }
                }

                Log($"[RiotLiveMatch] Parse: team1={team1.Players.Count}, team2={team2.Players.Count}");

                // Henrik API'den isim/rank/elo/kart çek — rate limit'i aşmamak için sırayla 250ms ara ile
                var allPlayers = team1.Players.Concat(team2.Players).ToList();
                foreach (var p in allPlayers.Where(p => !string.IsNullOrEmpty(p.Puuid)))
                {
                    try
                    {
                        var info = await FetchPlayerFullInfoAsync(p.Puuid);
                        p.Name = info.name;
                        p.Tag = info.tag;
                        p.Rank = info.rank;
                        p.PlayerCardUrl = info.cardUrl;
                        p.Tier = info.tier;
                        p.Elo = info.elo;
                        await Task.Delay(1200);
                        if (!string.IsNullOrEmpty(info.name) && !string.IsNullOrEmpty(info.region))
                        {
                            var matchResults = await FetchPlayerMatchHistoryAsync(info.region, info.name, info.tag);
                            for (int i = 0; i < matchResults.Count && i < 4; i++)
                            {
                                if (i == 0) p.Match1Win = matchResults[i];
                                else if (i == 1) p.Match2Win = matchResults[i];
                                else if (i == 2) p.Match3Win = matchResults[i];
                                else if (i == 3) p.Match4Win = matchResults[i];
                            }
                            await Task.Delay(1200);
                        }
                    }
                    catch { }
                    // Henrik'te bulunamayan oyuncular için PUUID'yi yedek isim yap
                    if (string.IsNullOrEmpty(p.Name))
                    {
                        p.Name = $"Oyuncu {p.Puuid[..Math.Min(6, p.Puuid.Length)]}";
                        p.Tag = "";
                    }
                }

                var currentPlayer = allPlayers.FirstOrDefault(p =>
                    p.IsCurrentUser ||
                    (p.Name.Equals(currentName, StringComparison.OrdinalIgnoreCase) &&
                     p.Tag.Equals(currentTag, StringComparison.OrdinalIgnoreCase)));
                if (currentPlayer != null)
                    currentPlayer.IsCurrentUser = true;

                result.RedTeam = team1;
                result.BlueTeam = team2;
                Log($"[RiotLiveMatch] Parse: BASARILI map={result.Map}, mode={result.Mode}");
                return result;
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] Parse EXCEPTION: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private void EnsureHenrikAuth()
        {
            if (!_henrikHttp.DefaultRequestHeaders.Contains("Authorization") && !string.IsNullOrEmpty(ApiKeyProvider.HenrikDevKey))
                _henrikHttp.DefaultRequestHeaders.Add("Authorization", ApiKeyProvider.HenrikDevKey);
        }

        private async Task<(string puuid, string name, string tag, string rank, string cardUrl, int tier, int elo, string region)> FetchPlayerFullInfoAsync(string puuid)
        {
            try
            {
                EnsureHenrikAuth();
                var hasAuth = _henrikHttp.DefaultRequestHeaders.Contains("Authorization");
                Log($"[RiotLiveMatch] FetchPlayer: puuid={puuid[..Math.Min(8, puuid.Length)]}, hasAuth={hasAuth}, key={(!string.IsNullOrEmpty(ApiKeyProvider.HenrikDevKey) ? "var" : "YOK")}");

                var url = $"{HenrikBase}/valorant/v2/by-puuid/account/{puuid}";
                Log($"[RiotLiveMatch] FetchPlayer URL: {url}");
                var resp = await _henrikHttp.GetAsync(url);
                Log($"[RiotLiveMatch] FetchPlayer status: {resp.StatusCode}");
                if (!resp.IsSuccessStatusCode)
                {
                    Log($"[RiotLiveMatch] FetchPlayer basarisiz, 2sn bekleyip tekrar deniyorum");
                    await Task.Delay(2000);
                    resp = await _henrikHttp.GetAsync(url);
                    Log($"[RiotLiveMatch] FetchPlayer retry status: {resp.StatusCode}");
                    if (!resp.IsSuccessStatusCode)
                    {
                        Log($"[RiotLiveMatch] FetchPlayer retry de basarisiz, atliyorum");
                        return (puuid, "", "", "", "", 0, 0, "");
                    }
                }

                var body = await resp.Content.ReadAsStringAsync();
                Log($"[RiotLiveMatch] FetchPlayer body (ilk 300): {(body.Length > 300 ? body[..300] : body)}");
                var root = JObject.Parse(body);
                var data = root["data"];
                if (data == null)
                {
                    Log($"[RiotLiveMatch] FetchPlayer: data=null, status={root["status"]}");
                    return (puuid, "", "", "", "", 0, 0, "");
                }

                var name = data["name"]?.ToString() ?? "";
                var tag = data["tag"]?.ToString() ?? "";
                var region = data["region"]?.ToString() ?? "eu";

                // card: UUID string ("612cd02d-...") veya { "small": "...", "large": "..." } objesi olabilir
                var cardUrl = "";
                var cardToken = data["card"];
                if (cardToken != null)
                {
                    if (cardToken.Type == Newtonsoft.Json.Linq.JTokenType.String)
                    {
                        var cardId = cardToken.ToString();
                        if (!string.IsNullOrEmpty(cardId) && cardId.Length > 10)
                            cardUrl = $"https://media.valorant-api.com/playercards/{cardId}/smallart.png";
                    }
                    else if (cardToken["small"] != null)
                    {
                        cardUrl = cardToken["small"]?.ToString() ?? "";
                    }
                }
                Log($"[RiotLiveMatch] FetchPlayer: name={name}, tag={tag}, region={region}, cardUrl={(string.IsNullOrEmpty(cardUrl) ? "bos" : "var")}");

                // Fetch MMR for rank, tier, elo
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(tag))
                {
                    var (rank, tier, elo) = await FetchRankAndTierAsync(region, name, tag);
                    return (puuid, name, tag, rank, cardUrl, tier, elo, region);
                }
                return (puuid, "", "", "", "", 0, 0, "");
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] FetchPlayer EXCEPTION: {ex.Message}");
                return (puuid, "", "", "", "", 0, 0, "");
            }
        }

        private async Task<(string rank, int tier, int elo)> FetchRankAndTierAsync(string region, string name, string tag)
        {
            try
            {
                EnsureHenrikAuth();
                var url = $"{HenrikBase}/valorant/v2/mmr/{region}/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}";
                Log($"[RiotLiveMatch] FetchRank URL: {url}");
                var resp = await _henrikHttp.GetAsync(url);
                Log($"[RiotLiveMatch] FetchRank status: {resp.StatusCode}");
                if (!resp.IsSuccessStatusCode)
                {
                    Log($"[RiotLiveMatch] FetchRank basarisiz, 2sn bekleyip tekrar deniyorum");
                    await Task.Delay(2000);
                    resp = await _henrikHttp.GetAsync(url);
                    Log($"[RiotLiveMatch] FetchRank retry status: {resp.StatusCode}");
                    if (!resp.IsSuccessStatusCode)
                    {
                        var errBody = await resp.Content.ReadAsStringAsync();
                        Log($"[RiotLiveMatch] FetchRank hata body: {(errBody.Length > 200 ? errBody[..200] : errBody)}");
                        return ("", 0, 0);
                    }
                }

                var body = await resp.Content.ReadAsStringAsync();
                var root = JObject.Parse(body);
                var current = root["data"]?["current_data"];
                var rank = current?["currenttierpatched"]?.ToString() ?? "";
                var tier = current?["currenttier"]?.Value<int>() ?? 0;
                var elo = current?["elo"]?.Value<int>() ?? 0;
                Log($"[RiotLiveMatch] FetchRank: rank={rank}, tier={tier}, elo={elo}");
                return (rank, tier, elo);
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] FetchRank EXCEPTION: {ex.Message}");
                return ("", 0, 0);
            }
        }

        private async Task<List<bool>> FetchPlayerMatchHistoryAsync(string region, string name, string tag)
        {
            try
            {
                EnsureHenrikAuth();
                var url = $"{HenrikBase}/valorant/v4/matches/{region}/pc/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}?page=1&size=4";
                Log($"[RiotLiveMatch] MatchHistory URL: {url}");
                var resp = await _henrikHttp.GetAsync(url);
                Log($"[RiotLiveMatch] MatchHistory status: {resp.StatusCode}");
                if (!resp.IsSuccessStatusCode) return new List<bool>();
                var body = await resp.Content.ReadAsStringAsync();
                var root = JObject.Parse(body);
                var data = root["data"] as JArray;
                if (data == null) return new List<bool>();
                var results = new List<bool>();
                foreach (var match in data)
                {
                    string playerTeam = "";
                    var players = match["players"] as JArray;
                    if (players != null)
                    {
                        foreach (var pl in players)
                        {
                            if (pl["name"]?.ToString() == name && pl["tag"]?.ToString() == tag)
                            {
                                playerTeam = pl["team"]?.ToString() ?? "";
                                break;
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(playerTeam))
                    {
                        var teamData = match["teams"]?[playerTeam.ToLower()];
                        if (teamData != null)
                            results.Add(teamData["has_won"]?.Value<bool>() ?? false);
                    }
                }
                Log($"[RiotLiveMatch] MatchHistory: {results.Count} sonuc, string={string.Concat(results.Select(r => r ? "W" : "L"))}");
                return results;
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] MatchHistory EXCEPTION: {ex.Message}");
                return new List<bool>();
            }
        }

        private static string GamePodToServerAdi(string gamePodId)
        {
            if (string.IsNullOrEmpty(gamePodId)) return "";
            var pod = gamePodId.ToLowerInvariant();
            if (pod.Contains("istanbul")) return "Türkiye (İstanbul)";
            if (pod.Contains("frankfurt")) return "Europe (Frankfurt)";
            if (pod.Contains("london")) return "Europe (London)";
            if (pod.Contains("paris")) return "Europe (Paris)";
            if (pod.Contains("madrid")) return "Europe (Madrid)";
            if (pod.Contains("warsaw")) return "Europe (Warsaw)";
            if (pod.Contains("stockholm")) return "Europe (Stockholm)";
            if (pod.Contains("moscow")) return "Russia (Moscow)";
            if (pod.Contains("chicago")) return "North America (Chicago)";
            if (pod.Contains("dallas")) return "North America (Dallas)";
            if (pod.Contains("losangeles") || pod.Contains("la")) return "North America (Los Angeles)";
            if (pod.Contains("miami")) return "Latin America (Miami)";
            if (pod.Contains("saopaulo") || pod.Contains("sao paulo")) return "Brazil (São Paulo)";
            if (pod.Contains("seoul")) return "Korea (Seoul)";
            if (pod.Contains("tokyo")) return "Japan (Tokyo)";
            if (pod.Contains("singapore")) return "Asia Pacific (Singapore)";
            if (pod.Contains("sydney")) return "Oceania (Sydney)";
            return gamePodId;
        }

        private static string MapIdToName(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return "";
            var id = mapId.ToLowerInvariant();
            if (id.Contains("ascent")) return "Ascent";
            if (id.Contains("bind")) return "Bind";
            if (id.Contains("haven")) return "Haven";
            if (id.Contains("split")) return "Split";
            if (id.Contains("icebox")) return "Icebox";
            if (id.Contains("breeze")) return "Breeze";
            if (id.Contains("fracture")) return "Fracture";
            if (id.Contains("pearl")) return "Pearl";
            if (id.Contains("lotus")) return "Lotus";
            if (id.Contains("sunset")) return "Sunset";
            if (id.Contains("abyss") || id.Contains("jam")) return "Abyss";
            if (id.Contains("range")) return "Practice Range";
            return mapId;
        }

        private static string QueueIdToName(string queueId)
        {
            if (string.IsNullOrEmpty(queueId)) return "Competitive";
            var q = queueId.ToLowerInvariant();
            if (q.Contains("competitive")) return "Competitive";
            if (q.Contains("unrated")) return "Unrated";
            if (q.Contains("deathmatch")) return "Deathmatch";
            if (q.Contains("spikerush") || q.Contains("spike_rush")) return "Spike Rush";
            if (q.Contains("swiftplay")) return "Swiftplay";
            if (q.Contains("escalation")) return "Escalation";
            if (q.Contains("replication")) return "Replication";
            if (q.Contains("snowball")) return "Snowball Fight";
            if (q.Contains("premier")) return "Premier";
            return queueId;
        }

        private static readonly Dictionary<string, string> AgentUuids = new(StringComparer.OrdinalIgnoreCase)
        {
            { "dade69b4-4f5a-8528-247b-219e5a1facd6", "Fade" },
            { "95b78ed7-4637-86d9-7e41-71ba8c293152", "Harbor" },
            { "601dbbe7-43ce-be57-2a40-4abd24953621", "KAY/O" },
            { "22697a3d-45bf-8dd7-4fec-84a9e28c69d7", "Chamber" },
            { "bb2a4828-46eb-8cd1-e765-15848195d751", "Neon" },
            { "41fb69c1-4189-7b37-f117-bcaf1e96f1bf", "Astra" },
            { "7f94d92c-4234-0a36-9646-3a87eb8b5c89", "Yoru" },
            { "6f2a04ca-43e0-be17-7f36-b3908627744d", "Skye" },
            { "5f8d3a7f-467b-97f3-062c-13acf203c006", "Breach" },
            { "eb93336a-449b-9c1b-0a54-a891f7921d69", "Phoenix" },
            { "569fdd95-4d10-43ab-ca70-79becc718b46", "Sage" },
            { "a3bfb853-43b2-7238-a4f1-ad90e9e46bcc", "Reyna" },
            { "add6443a-41bd-e414-f6ad-e58d267f4e95", "Jett" },
            { "117ed9e3-49f3-6512-3ccf-0cada7e3823b", "Cypher" },
            { "8e253930-4c05-31dd-1b6c-968525494517", "Omen" },
            { "9f0d8ba9-4140-b941-57d3-a7ad57c6b417", "Brimstone" },
            { "320b2a48-4d9b-a075-30f1-1f93a9b638fa", "Sova" },
            { "1e58de9c-4950-5125-93e9-a0aee9f98746", "Killjoy" },
            { "707eab51-4836-f488-046a-cda6bf494859", "Viper" },
            { "f94c3b30-42be-e959-889c-5aa313dba261", "Raze" },
            { "0e38b510-41a8-5780-5e8f-568b2a4f2d6c", "Iso" },
            { "1dbf2edd-4729-0984-3115-daa5eed44993", "Clove" },
            { "cc8b64c8-4b25-4ff9-6e7f-37b4da43d235", "Deadlock" },
            { "e370fa57-4757-3604-3648-499e1f642d3f", "Gekko" },
            { "efba5359-4016-a1e5-7626-b1ae76895940", "Vyse" },
            { "b444168c-4e35-8076-db47-ef9bf368f384", "Tejo" },
            { "df1cb487-4902-002e-5c17-d28e83e78588", "Waylay" }
        };

        private static string AgentUuidToName(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) return "";
            if (AgentUuids.TryGetValue(uuid, out var name))
            {
                Log($"[RiotLiveMatch] Agent matched: {uuid[..Math.Min(8,uuid.Length)]} -> {name}");
                return name;
            }
            Log($"[RiotLiveMatch] Agent UNMATCHED: {uuid} | ilk8={uuid[..Math.Min(8,uuid.Length)]}");
            if (uuid.Length >= 8) return uuid[..8];
            return uuid;
        }

        public bool IsLockfileAvailable() => ReadLockfile() != null;

        /// <summary>
        /// Hızlı kontrol: sadece oyuncunun aktif maçta olup olmadığını döndürür.
        /// Henrik API çağrısı yapmaz, sadece GLZ'ye bakıp MatchID var mı yok mu kontrol eder.
        /// </summary>
        public async Task<bool> CheckInMatchAsync(string currentRegion, CancellationToken ct = default)
        {
            try
            {
                Log($"[RiotLiveMatch] CheckInMatch basladi region={currentRegion}");
                var lockData = ReadLockfile();
                if (lockData == null) { Log("[RiotLiveMatch] CheckInMatch lockfile yok"); return false; }
                var (port, password) = lockData.Value;
                _cachedClientVersion = null;

                var credBytes = Encoding.UTF8.GetBytes($"riot:{password}");
                var base64 = Convert.ToBase64String(credBytes);
                _localHttp.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", base64);
                Array.Clear(credBytes, 0, credBytes.Length);

                var localBase = $"https://127.0.0.1:{port}";

                var authResp = await _localHttp.GetAsync($"{localBase}/rso-auth/v1/authorization", ct);
                if (!authResp.IsSuccessStatusCode) { Log("[RiotLiveMatch] CheckInMatch auth basarisiz"); return false; }
                var authBody = await authResp.Content.ReadAsStringAsync();
                var authJson = JObject.Parse(authBody);
                var puuid = authJson["subject"]?.ToString();
                if (string.IsNullOrEmpty(puuid)) { Log("[RiotLiveMatch] CheckInMatch puuid yok"); return false; }

                var entResp = await _localHttp.GetAsync($"{localBase}/entitlements/v1/token", ct);
                if (!entResp.IsSuccessStatusCode) { Log("[RiotLiveMatch] CheckInMatch entitlements basarisiz"); return false; }
                var entBody = await entResp.Content.ReadAsStringAsync();
                var entJson = JObject.Parse(entBody);
                var accessToken = entJson["accessToken"]?.ToString();
                var entitlementsToken = entJson["token"]?.ToString();
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(entitlementsToken)) { Log("[RiotLiveMatch] CheckInMatch token yok"); return false; }

                var glzHost = GetGlzHost(currentRegion);
                if (string.IsNullOrEmpty(glzHost)) { Log("[RiotLiveMatch] CheckInMatch glzHost yok"); return false; }
                var glzBase = $"https://{glzHost}";

                var clientPlatform = "ew0KCSJwbGF0Zm9ybVR5cGUiOiAiUEMiLA0KCSJwbGF0Zm9ybU9TIjogIldpbmRvd3MiLA0KCSJwbGF0Zm9ybU9TVmVyc2lvbiI6ICIxMC4wLjE5MDQyLjEuMjU2LjY0Yml0IiwNCgkicGxhdGZvcm1DaGlwc2V0IjogIlVua25vd24iDQp9";
                var clientVer = await GetClientVersionAsync(localBase, ct);

                var coreReq = new HttpRequestMessage(HttpMethod.Get, $"{glzBase}/core-game/v1/players/{puuid}");
                coreReq.Headers.Add("Authorization", $"Bearer {accessToken}");
                coreReq.Headers.Add("X-Riot-Entitlements-JWT", entitlementsToken);
                coreReq.Headers.Add("X-Riot-ClientPlatform", clientPlatform);
                coreReq.Headers.Add("X-Riot-ClientVersion", clientVer);
                var coreResp = await _glzHttp.SendAsync(coreReq, ct);
                Log($"[RiotLiveMatch] CheckInMatch core-game status={coreResp.StatusCode}");
                if (coreResp.IsSuccessStatusCode)
                {
                    var body = await coreResp.Content.ReadAsStringAsync();
                    var json = JObject.Parse(body);
                    var matchId = json["MatchID"]?.ToString();
                    if (!string.IsNullOrEmpty(matchId)) { Log($"[RiotLiveMatch] CheckInMatch core-game mac bulundu {matchId[..Math.Min(8,matchId.Length)]}"); return true; }
                }

                var preReq = new HttpRequestMessage(HttpMethod.Get, $"{glzBase}/pregame/v1/players/{puuid}");
                preReq.Headers.Add("Authorization", $"Bearer {accessToken}");
                preReq.Headers.Add("X-Riot-Entitlements-JWT", entitlementsToken);
                preReq.Headers.Add("X-Riot-ClientPlatform", clientPlatform);
                preReq.Headers.Add("X-Riot-ClientVersion", clientVer);
                var preResp = await _glzHttp.SendAsync(preReq, ct);
                Log($"[RiotLiveMatch] CheckInMatch pregame status={preResp.StatusCode}");
                if (preResp.IsSuccessStatusCode)
                {
                    var body = await preResp.Content.ReadAsStringAsync();
                    var json = JObject.Parse(body);
                    var matchId = json["MatchID"]?.ToString();
                    if (!string.IsNullOrEmpty(matchId)) { Log($"[RiotLiveMatch] CheckInMatch pregame mac bulundu {matchId[..Math.Min(8,matchId.Length)]}"); return true; }
                }

                Log("[RiotLiveMatch] CheckInMatch mac yok");
                return false;
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] CheckInMatch HATA: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _localHttp?.Dispose();
            _henrikHttp?.Dispose();
            _glzHttp?.Dispose();
            try { if (File.Exists(LogPath)) File.Delete(LogPath); } catch { }
        }
    }
}
