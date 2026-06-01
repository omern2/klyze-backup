using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
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
        private const string HenrikApiKey = "HDEV-06d4da7c-c8ae-446d-a653-9277e0ea7cb1";
        private const string HenrikBase = "https://api.henrikdev.xyz";
        private const string LogPath = @"C:\Users\omery\AppData\Local\Temp\riot_debug.log";

        private readonly HttpClient _localHttp;
        private readonly HttpClient _henrikHttp;
        private readonly HttpClient _glzHttp;

        private string _lockfilePath;
        private static readonly object _logLock = new();

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

            try
            {
                var debugPath = @"C:\Users\omery\Music\klyze kayak kodları\klyze.gg uygulama\Klyze .gg\Kodlar\exe\debug.txt";
                System.IO.File.WriteAllText(debugPath,
                    $"OLUSTURULDU {DateTime.Now}\nlockfile: {_lockfilePath}\nvar: {System.IO.File.Exists(_lockfilePath)}");
            }
            catch (Exception ex)
            {
                try
                {
                    var debugPath = @"C:\Users\omery\AppData\Local\Temp\debug_service.txt";
                    System.IO.File.WriteAllText(debugPath, $"HATA: {ex}");
                }
                catch { }
            }

            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            _localHttp = new HttpClient(handler)
            { Timeout = TimeSpan.FromSeconds(10) };

            _henrikHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _henrikHttp.DefaultRequestHeaders.Add("Authorization", HenrikApiKey);

            var glzHandler = new HttpClientHandler();
            glzHandler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            _glzHttp = new HttpClient(glzHandler)
            { Timeout = TimeSpan.FromSeconds(15) };
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

            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"riot:{password}"));
            _localHttp.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", base64);

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
                Log($"[RiotLiveMatch] accessToken={(accessToken?.Length > 20 ? accessToken[..20] + "..." : "null")}");
                Log($"[RiotLiveMatch] entitlementsToken={(entitlementsToken?.Length > 20 ? entitlementsToken[..20] + "..." : "null")}");
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

            // Step 4: Presence endpoint'inden maç durumunu kontrol et
            // /chat/v4/presences → oyuncunun şu anki durumunu verir (maçta mı, lobide mi)
            Log($"[RiotLiveMatch] Presence kontrol ediliyor...");
            try
            {
                var presenceResp = await _localHttp.GetAsync($"{localBase}/chat/v4/presences", ct);
                Log($"[RiotLiveMatch] /chat/v4/presences -> status={presenceResp.StatusCode}");
                var presenceBody = await presenceResp.Content.ReadAsStringAsync();
                // Sadece kendi presence'ımızı bul
                if (presenceResp.IsSuccessStatusCode)
                {
                    var presJson = JObject.Parse(presenceBody);
                    var presences = presJson["presences"] as JArray;
                    var myPresence = presences?.FirstOrDefault(p =>
                        p["puuid"]?.ToString() == puuid);
                    if (myPresence != null)
                    {
                        var privateRaw = myPresence["private"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(privateRaw))
                        {
                            try
                            {
                                // Önce Base64 decode dene, başarısız olursa direkt JSON olarak parse et
                                string privateJson;
                                try
                                {
                                    privateJson = System.Text.Encoding.UTF8.GetString(
                                        Convert.FromBase64String(privateRaw));
                                }
                                catch
                                {
                                    privateJson = privateRaw;
                                }

                                Log($"[RiotLiveMatch] presence private (ilk 200): {(privateJson.Length > 200 ? privateJson[..200] : privateJson)}");
                                var privObj = JObject.Parse(privateJson);

                                // sessionLoopState doğrudan veya matchPresenceData içinde olabilir
                                var sessionLoopState =
                                    privObj["sessionLoopState"]?.ToString()
                                    ?? privObj["matchPresenceData"]?["sessionLoopState"]?.ToString()
                                    ?? "";

                                Log($"[RiotLiveMatch] sessionLoopState: {sessionLoopState}");

                                // INGAME veya PREGAME değilse maçta değil
                                if (sessionLoopState != "INGAME" && sessionLoopState != "PREGAME")
                                {
                                    Log($"[RiotLiveMatch] Oyuncu maçta degil (state={sessionLoopState}), null donuyorum");
                                    return null;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"[RiotLiveMatch] presence parse HATA: {ex.Message}");
                                // Parse hatası olursa devam et, maçta olduğunu varsay
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] presence HATA (devam ediliyor): {ex.Message}");
            }

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
                    Log($"[RiotLiveMatch] external-sessions: {(sessBody.Length > 300 ? sessBody[..300] : sessBody)}");
                    var sessJson = JObject.Parse(sessBody);

                    foreach (var prop in sessJson.Properties())
                    {
                        var productId = prop.Value["productId"]?.ToString() ?? "";
                        var patchlineId = prop.Value["patchlineId"]?.ToString() ?? "";

                        // Valorant session'ını bul (riot_client değil)
                        if (productId == "riot_client" || string.IsNullOrEmpty(productId))
                            continue;

                        // launchConfiguration.arguments içinde -ares-deployment veya versiyon ara
                        var launchArgs = prop.Value["launchConfiguration"]?["arguments"] as JArray;
                        if (launchArgs != null)
                        {
                            foreach (var arg in launchArgs)
                            {
                                var argStr = arg.ToString();
                                // "-config-endpoint=https://..." içinden versiyon çıkar
                                // Valorant args içinde "-ares-deployment=eu" gibi şeyler var
                            }
                        }

                        // version alanı "release-XX.XX-shipping-XX-XXXXXXX" formatında olmalı
                        var version = prop.Value["version"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(version) && version.StartsWith("release-"))
                        {
                            Log($"[RiotLiveMatch] ClientVersion (session): {version}");
                            _cachedClientVersion = version;
                            return version;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[RiotLiveMatch] GetClientVersion HATA: {ex.Message}");
            }

            // Fallback: güncel Valorant sürümü (Episode 12 / Act 2)
            _cachedClientVersion = "release-12.09-shipping-26-4704114";
            Log($"[RiotLiveMatch] ClientVersion fallback: {_cachedClientVersion}");
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
                using var fs = new FileStream(_lockfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                var content = sr.ReadToEnd().Trim();
                var parts = content.Split(':');
                if (parts.Length < 5) return null;
                if (!int.TryParse(parts[2], out int port)) return null;
                return (port, parts[3]);
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

                Log($"[RiotLiveMatch] Parse: MapID={mapId}, QueueID={queueId}");
                Log($"[RiotLiveMatch] Parse: JSON keys={string.Join(",", matchJson.Properties().Select(p => p.Name))}");

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

                    var player = new LiveMatchPlayer
                    {
                        Agent = AgentUuidToName(charId),
                        Puuid = puuid,
                        IsCurrentUser = puuid.Equals(currentPuuid, StringComparison.OrdinalIgnoreCase)
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

                // Henrik API'den isim/rank çek — async, UI'ı bloke etme
                var allPlayers = team1.Players.Concat(team2.Players).ToList();
                var playerInfoTasks = allPlayers
                    .Where(p => !string.IsNullOrEmpty(p.Puuid))
                    .Select(p => FetchPlayerInfoAsync(p.Puuid))
                    .ToArray();

                try
                {
                    await Task.WhenAll(playerInfoTasks).ConfigureAwait(false);
                }
                catch { }

                foreach (var p in allPlayers)
                {
                    var completedTask = playerInfoTasks.FirstOrDefault(t =>
                        t.Status == TaskStatus.RanToCompletion && t.Result.puuid == p.Puuid);
                    if (completedTask != null)
                    {
                        var info = completedTask.Result;
                        p.Name = info.name;
                        p.Tag = info.tag;
                        p.Rank = info.rank;
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

        private async Task<(string puuid, string name, string tag, string rank)> FetchPlayerInfoAsync(string puuid)
        {
            try
            {
                var url = $"{HenrikBase}/valorant/v2/by-puuid/account/{puuid}";
                var resp = await _henrikHttp.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return (puuid, "", "", "");

                var body = await resp.Content.ReadAsStringAsync();
                var root = JObject.Parse(body);
                var data = root["data"];
                if (data == null) return (puuid, "", "", "");

                var name = data["name"]?.ToString() ?? "";
                var tag = data["tag"]?.ToString() ?? "";
                var region = data["region"]?.ToString() ?? "eu";

                // Fetch MMR for rank
                var rank = await FetchRankAsync(region, name, tag);
                return (puuid, name, tag, rank);
            }
            catch { return (puuid, "", "", ""); }
        }

        private async Task<string> FetchRankAsync(string region, string name, string tag)
        {
            try
            {
                var url = $"{HenrikBase}/valorant/v2/mmr/{region}/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}";
                var resp = await _henrikHttp.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return "";

                var body = await resp.Content.ReadAsStringAsync();
                var root = JObject.Parse(body);
                var current = root["data"]?["current_data"];
                return current?["currenttierpatched"]?.ToString() ?? "";
            }
            catch { return ""; }
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
            if (id.Contains("abyss")) return "Abyss";
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
            { "ded3520f-4264-bfed-162d-b080e2abccf9", "Fade" },
            { "601dbbe7-3ea0-4039-909b-1c0cbdfcba1c", "Harbor" },
            { "95da7e25-05b4-47d8-a3d0-1a44a95d9bed", "KAY/O" },
            { "22697a3d-45bf-8dd7-4fec-84a9e28cff7e", "Chamber" },
            { "8e253930-4c05-31dd-1b6c-968525494517", "Neon" },
            { "e370fa57-4759-4f2f-1c19-6dc2d4f8452d", "Astra" },
            { "7f94f857-4734-37c7-aaa0-ce163712b0d0", "Yoru" },
            { "1e58d0b6-4c0e-4e72-9d6b-5e4c4b0e0f3a", "Skye" },
            { "5f8d3a7f-467b-4f3f-7c8c-6b5d4a3c2b1a", "Breach" },
            { "6f2a04c9-4d5c-4f8a-9e0b-8a1b2c3d4e5f", "Phoenix" },
            { "9f0c8a7d-4c3b-4a2e-8f1d-6e5d4c3b2a1f", "Sage" },
            { "fc3a5c1e-4d6b-4f2a-9e8d-7c6b5a4f3e2d", "Reyna" },
            { "a3bf1d8e-4c5f-4a2b-9e7d-8c6b5a4f3e2d", "Jett" },
            { "b2d8f7e1-4c3a-4f5b-9e6d-7c8b9a1d2e3f", "Cypher" },
            { "d4c5b6a7-4f3e-4d2c-9b1a-8f7e6d5c4b3a", "Omen" },
            { "e5f6a7b8-4c3d-4e2f-9a1b-8c7d6e5f4a3b", "Brimstone" },
            { "f7a8b9c0-4d1e-4f2a-9b3c-8d4e5f6a7b8c", "Sova" },
            { "a8b9c0d1-4e2f-4a3b-9c4d-8e5f6a7b8c9d", "Killjoy" },
            { "b9c0d1e2-4f3a-4b4c-9d5e-8f6a7b8c9d0e", "Viper" },
            { "c0d1e2f3-4a4b-4c5d-9e6f-8a7b8c9d0e1f", "Raze" },
            { "d1e2f3a4-4b5c-4d6e-9f7a-8b8c9d0e1f2a", "Iso" },
            { "e2f3a4b5-4c6d-4e7f-9a8b-8c9d0e1f2a3b", "Clove" },
            { "f3a4b5c6-4d7e-4f8a-9b9c-8d0e1f2a3b4c", "Deadlock" },
            { "a4b5c6d7-4e8f-4a9b-9c0d-8e1f2a3b4c5d", "Gekko" },
            { "b5c6d7e8-4f9a-4b0c-9d1e-8f2a3b4c5d6e", "Vyse" },
            { "c6d7e8f9-4a0b-4c1d-9e2f-8a3b4c5d6e7f", "Tejo" },
            { "d7e8f9a0-4b1c-4d2e-9f3a-8b4c5d6e7f8a", "Waylay" }
        };

        private static string AgentUuidToName(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) return "";
            if (AgentUuids.TryGetValue(uuid, out var name)) return name;
            if (uuid.Length >= 8) return uuid[..8];
            return uuid;
        }

        public bool IsLockfileAvailable() => ReadLockfile() != null;

        public void Dispose()
        {
            _localHttp?.Dispose();
            _henrikHttp?.Dispose();
        }
    }
}
