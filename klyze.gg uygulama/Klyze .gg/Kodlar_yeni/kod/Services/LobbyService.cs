using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    public class LobbyService : IDisposable
    {
        private readonly FirebaseService _firebase;

        public event Action<List<FirestoreLobi>> LobilerGuncellendi;
        public event Action<FirestoreLobi> LobbyGuncellendi;

        public LobbyService()
        {
            _firebase = new FirebaseService();
            _firebase.LobilerGuncellendi += lobiler => LobilerGuncellendi?.Invoke(lobiler);
        }

        public async Task BaslatAsync()
        {
            await _firebase.BootstrapApiKeyAsync();
            await _firebase.AnonimGirisAsync();
        }

        public string GetLocalId() => _firebase.LocalId;

        // ─── Create Lobby ───────────────────────────────────────────────────────

        public async Task<string> CreateLobbyAsync(string gameMode, int maxPlayers,
            string hostName, string hostTag, int hostElo, int hostTier, string hostRank, string region)
        {
            return await _firebase.CreateLobbyAsync(gameMode, maxPlayers,
                hostName, hostTag, hostElo, hostTier, hostRank, region);
        }

        // ─── Find & Join Lobby (Matchmaking) ────────────────────────────────────

        public async Task<FirestoreLobi> FindAndJoinLobbyAsync(int myElo, string gameMode,
            string myName, string myTag, int myTier, string myRank, string myCardUrl, string myRegion, int maxPlayers = 5)
        {
            var lobiler = await _firebase.GetWaitingLobilerAsync();

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Uygun lobileri filtrele: aynı mod, süresi geçmemiş, dolu değil, 300 ELO fark
            var uygun = lobiler
                .Where(l => l.Status == "waiting")
                .Where(l => l.ExpiresAt > now)
                .Where(l => l.Players.Count < l.MaxPlayers)
                .Where(l => l.GameMode == gameMode)
                .Where(l => l.MaxPlayers == maxPlayers)
                .Where(l => Math.Abs(l.HostElo - myElo) <= 300)
                .OrderBy(l => Math.Abs(l.HostElo - myElo))
                .ToList();

            FirestoreLobi best = uygun.FirstOrDefault();

            if (best == null)
            {
                // Uygun lobi yok → bulunamadı
                return null;
            }

            // Lobiye katıl
            await JoinLobbyAsync(best.Id, myName, myTag, myElo, myTier, myRank, myCardUrl, myRegion);

            if (!best.Players.Any(p => p.Name == myName && p.Tag == myTag))
                best.Players.Add(new LobbyPlayer { Name = myName, Tag = myTag, Elo = myElo, Tier = myTier, Rank = myRank, CardUrl = myCardUrl });
            if (best.Players.Count >= best.MaxPlayers)
                best.Status = "full";
            return best;
        }

        // ─── Join Lobby ─────────────────────────────────────────────────────────

        public async Task JoinLobbyAsync(string lobbyId,
            string name, string tag, int elo, int tier, string rank, string cardUrl, string region)
        {
            var lobi = await _firebase.LobiGetirAsync(lobbyId);
            if (lobi == null) throw new Exception("Lobi bulunamadı.");
            if (lobi.Players.Count >= lobi.MaxPlayers) throw new Exception("Lobi dolu.");
            if (lobi.Players.Any(p => p.Name == name && p.Tag == tag))
                return;

            var yeniOyuncu = new Dictionary<string, object>
            {
                { "name", name },
                { "tag", tag },
                { "elo", elo },
                { "tier", tier },
                { "rank", rank },
                { "cardUrl", cardUrl ?? "" }
            };

            var mevcutOyuncular = lobi.Players.Select(p => new Dictionary<string, object>
            {
                { "name", p.Name },
                { "tag", p.Tag },
                { "elo", p.Elo },
                { "tier", p.Tier },
                { "rank", p.Rank },
                { "cardUrl", p.CardUrl ?? "" }
            }).Cast<object>().ToList();

            mevcutOyuncular.Add(yeniOyuncu);

            var yeniDurum = mevcutOyuncular.Count >= lobi.MaxPlayers ? "full" : "waiting";

            await _firebase.LobiGuncelleAsync(lobbyId, new
            {
                players = mevcutOyuncular,
                status = yeniDurum
            });
        }

        // ─── Leave Lobby ────────────────────────────────────────────────────────

        public async Task LeaveLobbyAsync(string lobbyId, string userName, string userTag)
        {
            var lobi = await _firebase.LobiGetirAsync(lobbyId);
            if (lobi == null) return;

            var kalanOyuncular = lobi.Players
                .Where(p => !(p.Name == userName && p.Tag == userTag))
                .ToList();

            // Son oyuncu ayrılıyorsa lobiyi tamamen sil
            if (kalanOyuncular.Count == 0)
            {
                await _firebase.LobiSilAsync(lobbyId);
                return;
            }

            // Host ayrılıyorsa host'u kalan ilk oyuncuya devret
            bool hostAyriliyor = lobi.HostName == userName && lobi.HostTag == userTag;
            var yeniHost = kalanOyuncular.First();
            var guncelleme = new Dictionary<string, object>
            {
                { "players", kalanOyuncular.Select(p => new Dictionary<string, object>
                    {
                        { "name", p.Name },
                        { "tag", p.Tag },
                        { "elo", p.Elo },
                        { "tier", p.Tier },
                        { "rank", p.Rank },
                        { "cardUrl", p.CardUrl ?? "" }
                    }).Cast<object>().ToList() },
                { "status", "waiting" }
            };

            if (hostAyriliyor)
            {
                guncelleme["hostName"] = yeniHost.Name;
                guncelleme["hostTag"] = yeniHost.Tag;
                guncelleme["hostElo"] = yeniHost.Elo;
                guncelleme["hostTier"] = yeniHost.Tier;
                guncelleme["hostRank"] = yeniHost.Rank;
                guncelleme["hostCardUrl"] = yeniHost.CardUrl ?? "";
            }

            await _firebase.LobiGuncelleAsync(lobbyId, guncelleme);
        }

        // ─── Update Group Code ──────────────────────────────────────────────────

        public async Task UpdateGroupCodeAsync(string lobbyId, string groupCode)
        {
            await _firebase.LobiGuncelleAsync(lobbyId, new
            {
                groupCode = groupCode.ToUpper().Trim(),
                status = "full"
            });
        }

        // ─── Get Lobby ──────────────────────────────────────────────────────────

        public async Task<FirestoreLobi> GetLobbyAsync(string lobbyId)
        {
            return await _firebase.LobiGetirAsync(lobbyId);
        }

        // ─── Bulk Get Waiting Lobbies ──────────────────────────────────────────

        public async Task<List<FirestoreLobi>> GetUygunLobilerAsync(int kullaniciElo)
        {
            var tumLobiler = await _firebase.GetWaitingLobilerAsync();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return tumLobiler
                .Where(l => l.ExpiresAt > now)
                .Where(l => Math.Abs(l.HostElo - kullaniciElo) <= 200)
                .OrderBy(l => Math.Abs(l.HostElo - kullaniciElo))
                .ToList();
        }

        // ─── Clean Expired ────────────────────────────────────────────────────

        public async Task CleanExpiredLobbiesAsync()
        {
            await _firebase.CleanExpiredLobbiesAsync();
        }

        // ─── Lobby Listener ────────────────────────────────────────────────────

        public void StartListening() => _firebase.StartListening();
        public void StopListening() => _firebase.StopListening();

        public void StartLobbyListener(string lobbyId)
        {
            _firebase.LobbyGuncellendi += OnLobbySnapshot;
            _firebase.StartLobbyListener(lobbyId);
        }

        public void StopLobbyListener()
        {
            _firebase.LobbyGuncellendi -= OnLobbySnapshot;
            _firebase.StopLobbyListener();
        }

        private void OnLobbySnapshot(FirestoreLobi lobi)
        {
            LobbyGuncellendi?.Invoke(lobi);
        }

        // ─── Legacy: Lobi Oluştur (eski) ───────────────────────────────────────

        public async Task<string> LobiOlusturAsync(string grupKodu, string hostName, string hostTag,
            int hostElo, int hostTier, string hostRank = "", string hostCardUrl = "", string region = "eu",
            string gameMode = "5v5 Normal", int maxPlayers = 5)
        {
            if (string.IsNullOrWhiteSpace(grupKodu))
                grupKodu = UretGrupKodu();

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var lobi = new FirestoreLobi
            {
                HostName  = hostName,
                HostTag   = hostTag,
                HostElo   = hostElo,
                HostTier  = hostTier,
                GroupCode = grupKodu,
                GameMode  = gameMode,
                MaxPlayers = maxPlayers,
                Region    = region,
                Status    = "waiting",
                CreatedAt = now,
                ExpiresAt = now + 3600,
                Players   = new List<LobbyPlayer>
                {
                    new() { Name = hostName, Tag = hostTag, Elo = hostElo, Tier = hostTier, Rank = hostRank, CardUrl = hostCardUrl }
                }
            };

            return await _firebase.LobiOlusturAsync(lobi);
        }

        public static string UretGrupKodu()
        {
            const string harfler = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rnd = new Random();
            return new string(Enumerable.Range(0, 6).Select(_ => harfler[rnd.Next(harfler.Length)]).ToArray());
        }

        public async Task LobiSilAsync(string lobbyId)
        {
            await _firebase.LobiSilAsync(lobbyId);
        }

        public async Task<Oda> OyuncuAktifOdaGetirAsync(string localId)
        {
            return await _firebase.OyuncuAktifOdaGetirAsync(localId);
        }

        public async Task QueueEkleAsync(string localId, string name, string tag,
            int elo, int tier, string cardUrl, string region)
        {
            await _firebase.QueueEkleAsync(localId, name, tag, elo, tier, cardUrl, region);
        }

        public async Task QueueKaldirAsync(string localId)
        {
            await _firebase.QueueKaldirAsync(localId);
        }

        // ─── Legacy: Oda (Room) proxy'leri ─────────────────────────────────────

        public async Task<string> OdaOlusturAsync(string player1LocalId, string player2LocalId,
            string p1Name, string p1Tag, int p1Elo, int p1Tier, string p1CardUrl,
            string p2Name, string p2Tag, int p2Elo, int p2Tier, string p2CardUrl)
        {
            return await _firebase.OdaOlusturAsync(
                player1LocalId, player2LocalId,
                p1Name, p1Tag, p1Elo, p1Tier, p1CardUrl,
                p2Name, p2Tag, p2Elo, p2Tier, p2CardUrl);
        }

        public async Task OdaGrupKoduGuncelleAsync(string roomId, string groupCode)
        {
            await _firebase.OdaGrupKoduGuncelleAsync(roomId, groupCode);
        }

        public async Task OdaSilAsync(string roomId)
        {
            await _firebase.OdaSilAsync(roomId);
        }

        public async Task<Oda> OdaGetirAsync(string roomId)
        {
            return await _firebase.OdaGetirAsync(roomId);
        }

        public void Dispose()
        {
            _firebase?.Dispose();
        }
    }
}
