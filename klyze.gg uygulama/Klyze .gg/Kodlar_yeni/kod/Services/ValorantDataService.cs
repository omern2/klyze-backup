using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ValorantAutoClicker.Services
{
    public class ValorantDataService : IDisposable
    {
        private readonly HttpClient _http;
        private string _detayCache;
        private DateTime _lastFetch = DateTime.MinValue;

        private static readonly HashSet<string> GizliHaritalar = new(StringComparer.OrdinalIgnoreCase)
        {
            "The Range", "Basic Training", "Skirmish A", "Skirmish B", "Skirmish C",
            "Skirmish D", "Skirmish E", "District", "Kasbah", "Drift", "Glitch",
            "Piazza", "Summit", "Corrode", "Juliett", "Port", "Duality", "HURM",
            "HURM_Yoruna", "Canter"
        };

        private static readonly HashSet<string> GizliAjanlar = new(StringComparer.OrdinalIgnoreCase)
        {
            "Veto", "Miks", "Null UI Data!", "Unused", "Radiant", "Radiant_Clone"
        };

        private static readonly HashSet<string> GizliSilahlar = new(StringComparer.OrdinalIgnoreCase)
        {
            "Melee", "Bandit", "Melee_", "Exe"
        };

        public ValorantDataService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        public async Task<string> GetDetayliDataAsync()
        {
            if ((DateTime.UtcNow - _lastFetch).TotalMinutes < 10 && _detayCache != null)
                return _detayCache;

            try
            {
                var henrikTask = HenrikDevVeriCekAsync();
                var weaponTask = WeaponVerisiCekAsync();
                var agentTask = AjanVerisiCekAsync();
                var mapTask = HaritaVerisiCekAsync();

                await Task.WhenAll(henrikTask, weaponTask, agentTask, mapTask);

                var (henrikMaps, henrikAgents) = henrikTask.Result;
                var (weaponData, weaponCompare) = weaponTask.Result;
                var (agentData, agentRoles) = agentTask.Result;
                var mapData = mapTask.Result;

                var maps = henrikMaps.Count > 0 ? henrikMaps : mapData;
                var agents = henrikAgents.Count > 0 ? henrikAgents : agentData;

                var detay = $"[VALORANT-API.COM GUNCEL VERILER]\n\n" +

                    $"─── HARITALAR ───\n{string.Join(", ", maps)}\n\n" +

                    $"─── SILAHLAR ───\n{weaponData}\n\n" +

                    $"─── SILAH KARSILASTIRMA ───\n{weaponCompare}\n\n" +

                    $"─── AJANLAR (ROLLER) ───\n{agentRoles}\n\n" +

                    $"─── EKONOMI CETVELI ───\n" +
                    $"0-300: Eco\n" +
                    $"300-800: Sidearm (Shorty/Sheriff vs)\n" +
                    $"800-1600: Force (Bucky/Marshal/Spectre)\n" +
                    $"1600-2400: Yarim alisveris (Spectre+Zirh / Guardian+Zirh)\n" +
                    $"2400-3900: Hafif full buy (Bulldog/Guardian/Outlaw + Agir Zirh)\n" +
                    $"3900+: Full buy (Vandal/Phantom + Agir Zirh + yetenekler)\n" +
                    $"Loss bonus: 1. eli kaybedince birikir (1900+1900+2400+...)\n" +
                    $"Round kazaninca loss bonus sifirlanir\n" +
                    $"Hafif Zirh=400, Agir Zirh=1000, Regen Zirh=650\n\n" +

                    $"[VERI BITISI]";

                _detayCache = detay;
                _lastFetch = DateTime.UtcNow;
                return detay;
            }
            catch
            {
                if (_detayCache != null)
                    return _detayCache;
                return "[VALORANT-API.COM GUNCEL VERILER]\nVeriler alinamadi.\n[VERI BITISI]";
            }
        }

        public async Task<string> GetHedefliVeriAsync(string soru)
        {
            var soruLower = soru.ToLowerInvariant();
            var hedefVeri = "";

            var pistolAnahtarlar = new[] { "pistol", "pistol round", "1. el", "1. round", "birinci el", "birinci round" };
            var isPistol = pistolAnahtarlar.Any(k => soruLower.Contains(k));

            var bilinenAjanlar = new[] { "jett", "phoenix", "sage", "sova", "cypher", "brimstone", "viper", "omen",
                "killjoy", "raze", "breach", "skye", "yoru", "astra", "kay/o", "kayo", "chamber",
                "neon", "harbor", "gekko", "deadlock", "iso", "clove", "vyse", "tejo", "waylay",
                "reyna", "fade" };
            var bulunanAjan = bilinenAjanlar.FirstOrDefault(a => soruLower.Contains(a));

            var bilinenSilahlar = new[] { "vandal", "phantom", "operator", "op", "sheriff", "ghost", "spectre",
                "marshal", "guardian", "bulldog", "odin", "ares", "judge", "bucky", "stinger", "frenzy",
                "shorty", "classic", "outlaw" };
            var bulunanSilah = bilinenSilahlar.FirstOrDefault(s => soruLower.Contains(s));

            try
            {
                var weaponJson = await _http.GetStringAsync("https://valorant-api.com/v1/weapons");
                var weaponRoot = JObject.Parse(weaponJson);
                if (weaponRoot["status"]?.ToString() != "200" || weaponRoot["data"] is not JArray wArr)
                    return "";

                if (isPistol)
                {
                    var sidearmlar = new[] { "Classic", "Shorty", "Frenzy", "Ghost", "Sheriff" };
                    var pList = new List<string>();
                    foreach (var w in wArr)
                    {
                        var name = w["displayName"]?.ToString() ?? "";
                        if (!sidearmlar.Contains(name)) continue;
                        var cost = w["shopData"]?["cost"]?.ToString() ?? "?";
                        var stats = w["weaponStats"];
                        if (stats == null) { pList.Add($"{name} ({cost}c)"); continue; }
                        var ranges = stats["damageRanges"] as JArray;
                        var dmg = "";
                        if (ranges != null && ranges.Count > 0)
                        {
                            var r = ranges[0];
                            var head = r["headDamage"]?.ToString() ?? "?";
                            var body = r["bodyDamage"]?.ToString() ?? "?";
                            dmg = $" {head}hs/{body}bd";
                        }
                        pList.Add($"{name} ({cost}c){dmg}");
                    }
                    if (pList.Count > 0)
                        hedefVeri += $"[PISTOL SILAHLARI]\n{string.Join("\n", pList)}\nPistol round baslangic kredisi: 800\n\n";
                }

                if (bulunanSilah != null)
                {
                    var silahAdi = bulunanSilah == "op" ? "Operator" : char.ToUpper(bulunanSilah[0]) + bulunanSilah[1..];
                    var w = wArr.FirstOrDefault(x => (x["displayName"]?.ToString() ?? "").Equals(silahAdi, StringComparison.OrdinalIgnoreCase));
                    if (w != null)
                    {
                        var cost = w["shopData"]?["cost"]?.ToString() ?? "?";
                        var cat = w["shopData"]?["category"]?.ToString() ?? w["category"]?.ToString() ?? "";
                        var stats = w["weaponStats"];
                        if (stats != null)
                        {
                            var fr = stats["fireRate"]?.ToString() ?? "?";
                            var mag = stats["magazineSize"]?.ToString() ?? "?";
                            var ranges = stats["damageRanges"] as JArray;
                            var dmgStr = "";
                            if (ranges != null)
                            {
                                foreach (var r in ranges)
                                {
                                    var start = r["rangeStartMeters"]?.ToString() ?? "0";
                                    var end = r["rangeEndMeters"]?.ToString() ?? "50";
                                    var head = r["headDamage"]?.ToString() ?? "?";
                                    var body = r["bodyDamage"]?.ToString() ?? "?";
                                    dmgStr += $" {start}-{end}m: {head}hs/{body}bd |";
                                }
                            }
                            hedefVeri += $"[{silahAdi} STATS]\nFiyat: {cost}c, FR: {fr}, Mermi: {mag}, Kategori: {cat}\nHasar: {dmgStr}\n\n";
                        }
                    }
                }

                if (bulunanAjan != null)
                {
                    var agentJson = await _http.GetStringAsync("https://valorant-api.com/v1/agents?isPlayableCharacter=true");
                    var agentRoot = JObject.Parse(agentJson);
                    if (agentRoot["status"]?.ToString() == "200" && agentRoot["data"] is JArray aArr)
                    {
                        var ajanAdi = char.ToUpper(bulunanAjan[0]) + bulunanAjan[1..];
                        if (ajanAdi == "Kayo") ajanAdi = "KAY/O";
                        var a = aArr.FirstOrDefault(x => (x["displayName"]?.ToString() ?? "").Equals(ajanAdi, StringComparison.OrdinalIgnoreCase));
                        if (a != null)
                        {
                            var role = a["role"]?["displayName"]?.ToString() ?? "?";
                            var abilities = a["abilities"] as JArray;
                            var abilList = new List<string>();
                            if (abilities != null)
                            {
                                foreach (var ab in abilities)
                                {
                                    var abName = ab["displayName"]?.ToString() ?? "?";
                                    var abDesc = ab["description"]?.ToString() ?? "";
                                    var abSlot = ab["slot"]?.ToString() ?? "?";
                                    var cost = "?";
                                    if (ab["cost"] != null) cost = ab["cost"]?.ToString();
                                    else if (abSlot == "Ultimate") cost = "7 ulti puani";
                                    var desc = abDesc.Length > 100 ? abDesc[..100] + "..." : abDesc;
                                    abilList.Add($"{abSlot}: {abName} ({cost}) - {desc}");
                                }
                            }
                            hedefVeri += $"[{ajanAdi} YETENEKLER]\nRol: {role}\n{string.Join("\n", abilList)}\n\n";
                        }
                    }
                }
            }
            catch { }

            return hedefVeri;
        }

        private async Task<(List<string> maps, List<string> agents)> HenrikDevVeriCekAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(ApiKeyProvider.HenrikDevKey))
                {
                    if (!_http.DefaultRequestHeaders.Contains("Authorization"))
                        _http.DefaultRequestHeaders.Add("Authorization", ApiKeyProvider.HenrikDevKey);
                }

                var json = await _http.GetStringAsync("https://api.henrikdev.xyz/valorant/v1/content");
                var root = JObject.Parse(json);
                var data = root["data"];

                var maps = new List<string>();
                if (data["maps"] is JArray mapArr)
                {
                    foreach (var m in mapArr)
                    {
                        var name = m["displayName"]?.ToString() ?? m["name"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(name) && !GizliHaritalar.Contains(name) && name != "Null UI Data!")
                            maps.Add(name);
                    }
                }

                var agents = new List<string>();
                if (data["characters"] is JArray charArr)
                {
                    foreach (var c in charArr)
                    {
                        var name = c["name"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(name) && !GizliAjanlar.Contains(name))
                            agents.Add(name);
                    }
                }

                return (maps, agents);
            }
            catch
            {
                return (new List<string>(), new List<string>());
            }
        }

        private async Task<(string weaponData, string weaponCompare)> WeaponVerisiCekAsync()
        {
            var weaponData = "";
            var weaponCompare = "";

            try
            {
                var json = await _http.GetStringAsync("https://valorant-api.com/v1/weapons");
                var root = JObject.Parse(json);
                if (root["status"]?.ToString() != "200" || root["data"] is not JArray arr)
                    return ("", "");

                var wList = new List<string>();
                JToken vandal = null, phantom = null, operatorGun = null, marshal = null, guardian = null, spectre = null;

                foreach (var w in arr)
                {
                    var name = w["displayName"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(name) || GizliSilahlar.Contains(name)) continue;

                    var cost = w["shopData"]?["cost"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(cost)) continue;

                    var cat = w["shopData"]?["category"]?.ToString() ?? "";
                    var stats = w["weaponStats"];
                    var damage = "";
                    var fireRate = "";

                    if (stats != null)
                    {
                        fireRate = stats["fireRate"]?.ToString() ?? "";
                        var ranges = stats["damageRanges"] as JArray;
                        if (ranges != null && ranges.Count > 0)
                        {
                            var r = ranges[0];
                            var head = r["headDamage"]?.ToString() ?? "";
                            var body = r["bodyDamage"]?.ToString() ?? "";
                            damage = $" {head}hs/{body}bd";
                        }
                    }

                    wList.Add($"{name} ({cost}c {cat}){damage}{(fireRate != "" ? $" FR:{fireRate}" : "")}");

                    switch (name)
                    {
                        case "Vandal": vandal = w; break;
                        case "Phantom": phantom = w; break;
                        case "Operator": operatorGun = w; break;
                        case "Marshal": marshal = w; break;
                        case "Guardian": guardian = w; break;
                        case "Spectre": spectre = w; break;
                    }
                }

                weaponData = string.Join("\n", wList);

                if (vandal != null && phantom != null)
                {
                    var vStats = vandal["weaponStats"];
                    var pStats = phantom["weaponStats"];
                    weaponCompare =
                        $"Vandal (2900c): 160hs 0-50m, FR:9.75, 25 mermi, delis:Orta\n" +
                        $"Phantom (2900c): 156hs 0-20m / 140hs 20-50m, FR:11, 30 mermi, susturuculu, delis:Orta\n" +
                        $"Karsilastirma: Phantom yakin-mesafe daha iyi (FR yuksek, az geri tepme). Vandal her mesafede tek atis headshot (160>150). " +
                        $"Vandal uzak mesafe, Phantom yakin/orta icin.";
                }
            }
            catch { }

            return (weaponData, weaponCompare);
        }

        private async Task<(List<string> agents, string agentRoles)> AjanVerisiCekAsync()
        {
            var agents = new List<string>();
            var agentRoles = "";

            try
            {
                var json = await _http.GetStringAsync("https://valorant-api.com/v1/agents?isPlayableCharacter=true");
                var root = JObject.Parse(json);
                if (root["status"]?.ToString() != "200" || root["data"] is not JArray arr)
                    return (agents, agentRoles);

                var roles = new Dictionary<string, List<string>>();
                foreach (var a in arr)
                {
                    var name = a["displayName"]?.ToString() ?? "";
                    var role = a["role"]?["displayName"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(name) || GizliAjanlar.Contains(name)) continue;

                    agents.Add(name);

                    if (!string.IsNullOrEmpty(role))
                    {
                        if (!roles.ContainsKey(role))
                            roles[role] = new List<string>();
                        roles[role].Add(name);
                    }
                }

                agentRoles = string.Join("\n", roles.Select(r => $"{r.Key}: {string.Join(", ", r.Value)}"));
            }
            catch { }

            return (agents, agentRoles);
        }

        private async Task<List<string>> HaritaVerisiCekAsync()
        {
            var maps = new List<string>();

            try
            {
                var json = await _http.GetStringAsync("https://valorant-api.com/v1/maps");
                var root = JObject.Parse(json);
                if (root["status"]?.ToString() != "200" || root["data"] is not JArray arr)
                    return maps;

                foreach (var m in arr)
                {
                    var name = m["displayName"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(name) && !GizliHaritalar.Contains(name))
                        maps.Add(name);
                }
            }
            catch { }

            return maps;
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
