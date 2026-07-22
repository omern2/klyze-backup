using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class GruplarViewModel : ObservableObject
    {
        private readonly UserService _userService;
        private readonly ConcurrentDictionary<string, byte> _katildigimLobiIds = new();
        private readonly HashSet<string> _gizliLobiIds = new();
        private readonly string _gizliLobiIdsFilePath;

        public GruplarViewModel(UserService userService)
        {
            _userService = userService;
            _gizliLobiIdsFilePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Klyze", "data", "gizli_lobi_ids.json");
            GizliLobiIdsYukle();
            SadeceAcik = true;
            if (App.Supabase != null)
            {
                App.Supabase.LobilerGuncellendi += OnLobilerGuncellendi;
                App.Supabase.StartListening();
                _ = GruplariYukleAsync();
            }
        }

        private void OnLobilerGuncellendi(List<FirestoreLobi> lobiler)
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var eskiler = Gruplar.ToList();
                DonusturVeGuncelle(lobiler);
                foreach (var g in Gruplar)
                    g.BenKatildimMi = _katildigimLobiIds.ContainsKey(g.Id);

                // Katıldığım ama artık Firebase'de olmayan (full olmuş) lobileri geri ekle
                foreach (var eski in eskiler)
                {
                    if (_katildigimLobiIds.ContainsKey(eski.Id) && !Gruplar.Any(g => g.Id == eski.Id))
                    {
                        eski.BenKatildimMi = true;
                        Gruplar.Insert(0, eski);
                    }
                }

                FiltreUygula();
            });
        }

        [ObservableProperty]
        private ObservableCollection<GrupKarti> _gruplar = new();

        [ObservableProperty]
        private ObservableCollection<GrupKarti> _filtrelenmisGruplar = new();

        [ObservableProperty]
        private bool _yukleniyor = true;

        [ObservableProperty]
        private string _seciliOyun = "VALORANT";

        [ObservableProperty]
        private bool _sadeceAcik;

        [ObservableProperty]
        private int _toplamBekleyen;

        [ObservableProperty]
        private int _katilabilirSayi;

        [ObservableProperty]
        private int _toplamGrupSayisi;

        [ObservableProperty]
        private string _seciliFiltreRank = "Tümü";

        [ObservableProperty]
        private bool _modalAcik;

        [ObservableProperty]
        private bool _uyariGoster;

        [ObservableProperty]
        private string _uyariMesaji = "";

        // ─── Modal Alanları ───

        [ObservableProperty]
        private string _grupAdi = "";

        [ObservableProperty]
        private int _minRankTier = 0;

        [ObservableProperty]
        private int _maxRankTier = 30;

        [ObservableProperty]
        private string _seciliMacTipi = "Premier";

        [ObservableProperty]
        private bool _sesliIletisim = true;

        [ObservableProperty]
        private int _maksOyuncu = 5;

        [ObservableProperty]
        private string _grupDili = "TR";

        [ObservableProperty]
        private string _grupKodu = "";

        [ObservableProperty]
        private bool _olusturmaHatasi;

        [ObservableProperty]
        private string _olusturmaHataMesaji = "";

        [ObservableProperty]
        private bool _olusturuyor;

        public List<string> MacTipleri => new() { "Premier", "Derece", "Normal" };
        public List<string> Diller => new() { "TR", "EN", "DE", "FR", "ES", "RU", "AR", "PL", "IT", "PT", "NL" };
        public List<RankOption> RankSecenekleri => new()
        {
            new("Demir", 0, 3),
            new("Bronz", 4, 6),
            new("Gümüş", 7, 9),
            new("Altın", 10, 12),
            new("Platin", 13, 15),
            new("Elmas", 16, 18),
            new("Yükselen", 19, 21),
            new("Ölümsüz", 22, 24),
            new("Işınsal", 25, 30),
        };

        public string[] OyunSecenekleri => new[] { "VALORANT", "CS2", "Fortnite", "Overwatch 2" };
        public string[] RankFiltreSecenekleri => new[] { "Tümü", "Demir", "Bronz", "Gümüş", "Altın", "Platin", "Elmas", "Yükselen", "Ölümsüz", "Işınsal" };

        [RelayCommand]
        private async Task GruplariYukleAsync()
        {
            Yukleniyor = true;
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    if (App.Supabase != null)
                        break;
                    await Task.Delay(1000);
                }

                await Task.Delay(300);
                var lobiler = App.Supabase != null
                    ? await App.Supabase.GetWaitingLobilerAsync()
                    : new List<FirestoreLobi>();

                var profil = _userService.GetProfile();
                if (profil != null)
                {
                    foreach (var l in lobiler)
                    {
                        if (l.Players.Any(p => p.Name == profil.OyuncuAdi && p.Tag == profil.Tag))
                            _katildigimLobiIds.TryAdd(l.Id, 0);
                    }
                }

                DonusturVeGuncelle(lobiler);
                foreach (var g in Gruplar)
                    g.BenKatildimMi = _katildigimLobiIds.ContainsKey(g.Id);
                FiltreUygula();
            }
            catch
            {
            }
            finally
            {
                Yukleniyor = false;
            }
        }

        private void GizliLobiIdsYukle()
        {
            try
            {
                if (System.IO.File.Exists(_gizliLobiIdsFilePath))
                {
                    var json = System.IO.File.ReadAllText(_gizliLobiIdsFilePath);
                    var ids = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);
                    if (ids != null)
                        foreach (var id in ids)
                            _gizliLobiIds.Add(id);
                }
            }
            catch { }
        }

        private void GizliLobiIdsKaydet()
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_gizliLobiIds.ToList(), Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(_gizliLobiIdsFilePath, json);
            }
            catch { }
        }

        private void DonusturVeGuncelle(List<FirestoreLobi> lobiler)
        {
            var liste = lobiler
                .Where(l => l.Status == "waiting" && !_gizliLobiIds.Contains(l.Id))
                .Select(DonusturGrupKarti)
                .ToList();

            Gruplar = new ObservableCollection<GrupKarti>(liste);
        }

        private static GrupKarti DonusturGrupKarti(FirestoreLobi l)
        {
            var uyeler = l.Players?.Select(p => new GrupUye
            {
                Isim = p.Name,
                Tag = p.Tag,
                Tier = p.Tier,
                Rank = p.Rank,
                CardUrl = p.CardUrl ?? "",
                ProfileIconUrl = p.CardUrl?.Replace("/wideart.png", "/displayicon.png") ?? "",
                Elo = p.Elo,
                Bayrak = "TR",
                Dogrulandi = false
            }).ToList() ?? new();

            return new GrupKarti
            {
                Id = l.Id,
                TakimAdi = l.HostName + "#" + l.HostTag,
                TakimHandle = "@" + l.HostName,
                OrtalamaRank = l.HostTier,
                MinRankTier = l.MinRankTier,
                MaxRankTier = l.MaxRankTier,
                MacTipi = l.GameMode ?? "Premier",
                MacFormati = "5v5",
                Dil = "TR",
                Bolge = "TR",
                Sesli = false,
                Mood = "",
                OyuncuSayisi = uyeler.Count,
                MaksOyuncu = l.MaxPlayers,
                Premium = false,
                Oyun = "VALORANT",
                Olusturan = l.HostName,
                HostPuuid = "",
                GrupKodu = l.GroupCode ?? "",
                OlusturmaZamani = l.CreatedAt,
                SureZamani = l.ExpiresAt,
                Uyeler = uyeler
            };
        }

        partial void OnSadeceAcikChanged(bool value) => FiltreUygula();
        partial void OnSeciliFiltreRankChanged(string value) => FiltreUygula();

        private (int min, int max) RankTierAralik(string rankName) => rankName switch
        {
            "Demir" => (0, 3),
            "Bronz" => (4, 6),
            "Gümüş" => (7, 9),
            "Altın" => (10, 12),
            "Platin" => (13, 15),
            "Elmas" => (16, 18),
            "Yükselen" => (19, 21),
            "Ölümsüz" => (22, 24),
            "Işınsal" => (25, 30),
            _ => (0, 30)
        };

        private void FiltreUygula()
        {
            var filtered = Gruplar.AsEnumerable();
            if (!string.IsNullOrEmpty(SeciliFiltreRank) && SeciliFiltreRank != "Tümü")
            {
                var (minT, maxT) = RankTierAralik(SeciliFiltreRank);
                filtered = filtered.Where(g => g.Uyeler.Any(u => u.Tier >= minT && u.Tier <= maxT));
            }
            if (SadeceAcik)
                filtered = filtered.Where(g => g.OyuncuSayisi < g.MaksOyuncu || g.BenKatildimMi);
            filtered = filtered.Where(g => !_gizliLobiIds.Contains(g.Id));
            FiltrelenmisGruplar = new ObservableCollection<GrupKarti>(filtered);
            ToplamGrupSayisi = FiltrelenmisGruplar.Count;
            ToplamBekleyen = FiltrelenmisGruplar.Sum(g => g.MaksOyuncu - g.OyuncuSayisi);
            KatilabilirSayi = FiltrelenmisGruplar.Count(g => g.OyuncuSayisi < g.MaksOyuncu);
        }

        [RelayCommand]
        private void Filtrele() { }

        [RelayCommand]
        private async Task GrubaKatil(GrupKarti grup)
        {
            if (grup == null) return;
            if (_katildigimLobiIds.Count > 0)
            {
                UyariMesaji = "Zaten bir gruptasınız. Çıkış yapmak için önce ÇIK butonuna basın.";
                UyariGoster = true;
                return;
            }
            try
            {
                var lobi = await App.Supabase.LobiGetirAsync(grup.Id);
                if (lobi == null) return;
                if (lobi.Players.Count >= lobi.MaxPlayers) return;

                var profil = _userService.GetProfile();
                if (profil == null) return;

                if (lobi.Players.Any(p => p.Name == profil.OyuncuAdi && p.Tag == profil.Tag))
                    return;

                int oyuncuTier = profil.CurrentTier;
                if (oyuncuTier < lobi.MinRankTier || oyuncuTier > lobi.MaxRankTier)
                {
                    var minLabel = RankOption.LabelFromTier(lobi.MinRankTier);
                    var maxLabel = RankOption.LabelFromTier(lobi.MaxRankTier);
                    UyariMesaji = $"Bu grup {minLabel} - {maxLabel} arası oyuncular için. Senin rankın bu aralıkta değil.";
                    UyariGoster = true;
                    return;
                }

                var mevcutOyuncular = lobi.Players.Select(p => new Dictionary<string, object>
                {
                    { "name", p.Name }, { "tag", p.Tag }, { "elo", p.Elo },
                    { "tier", p.Tier }, { "rank", p.Rank }, { "cardUrl", p.CardUrl ?? "" }
                }).Cast<object>().ToList();

                mevcutOyuncular.Add(new Dictionary<string, object>
                {
                    { "name", profil.OyuncuAdi }, { "tag", profil.Tag },
                    { "elo", profil.Elo }, { "tier", profil.CurrentTier },
                    { "rank", "" }, { "cardUrl", profil.CardSmallUrl ?? "" }
                });

                bool full = mevcutOyuncular.Count >= lobi.MaxPlayers;

                await App.Supabase.LobiGuncelleAsync(grup.Id, new
                {
                    players = mevcutOyuncular,
                    status = full ? "full" : "waiting"
                });

                _katildigimLobiIds.TryAdd(grup.Id, 0);
                _gizliLobiIds.Remove(grup.Id);
                grup.OyuncuSayisi = mevcutOyuncular.Count;
                grup.BenKatildimMi = true;
                FiltreUygula();
            }
            catch { }
        }

        [RelayCommand]
        private async Task GruptanCik(GrupKarti grup)
        {
            if (grup == null) return;
            try
            {
                var profil = _userService.GetProfile();
                if (profil == null) return;

                var lobi = await App.Supabase.LobiGetirAsync(grup.Id);
                if (lobi == null)
                {
                    _gizliLobiIds.Add(grup.Id);
                    GizliLobiIdsKaydet();
                    _katildigimLobiIds.TryRemove(grup.Id, out _);
                    grup.BenKatildimMi = false;
                    FiltreUygula();
                    return;
                }

                var kalan = lobi.Players
                    .Where(p => !(p.Name == profil.OyuncuAdi && p.Tag == profil.Tag))
                    .ToList();

                if (kalan.Count == 0)
                {
                    await App.Supabase.LobiSilAsync(grup.Id);
                }
                else
                {
                    bool hostAyriliyor = lobi.HostName == profil.OyuncuAdi && lobi.HostTag == profil.Tag;
                    var guncelleme = new Dictionary<string, object>
                    {
                        { "players", kalan.Select(p => new Dictionary<string, object>
                        {
                            { "name", p.Name }, { "tag", p.Tag }, { "elo", p.Elo },
                            { "tier", p.Tier }, { "rank", p.Rank }, { "cardUrl", p.CardUrl ?? "" }
                        }).Cast<object>().ToList() }
                    };

                    if (hostAyriliyor)
                    {
                        guncelleme["hostName"] = kalan.First().Name;
                        guncelleme["hostTag"] = kalan.First().Tag;
                        guncelleme["hostElo"] = kalan.First().Elo;
                        guncelleme["hostTier"] = kalan.First().Tier;
                        guncelleme["hostRank"] = "";
                    }

                    await App.Supabase.LobiGuncelleAsync(grup.Id, guncelleme);
                }

                _gizliLobiIds.Add(grup.Id);
                GizliLobiIdsKaydet();
                _katildigimLobiIds.TryRemove(grup.Id, out _);
                grup.BenKatildimMi = false;
                grup.OyuncuSayisi = 0;
                FiltreUygula();
            }
            catch (Exception ex)
            {
                UyariMesaji = $"Çıkış hatası: {ex.Message}";
                UyariGoster = true;
                LoggingService.Error("GruptanCik", $"Hata: {ex.Message}", ex);
            }
        }

        [RelayCommand]
        private void UyariKapat() => UyariGoster = false;

        public async Task TumGruplardanCikAsync()
        {
            var ids = _katildigimLobiIds.Keys.ToList();
            if (ids.Count == 0) return;
            var profil = _userService.GetProfile();
            if (profil == null) return;

            foreach (var id in ids)
            {
                try
                {
                    var lobi = await App.Supabase.LobiGetirAsync(id);
                    if (lobi == null) continue;

                    var kalan = lobi.Players.Where(p => !(p.Name == profil.OyuncuAdi && p.Tag == profil.Tag)).ToList();

                    if (kalan.Count == 0)
                    {
                        await App.Supabase.LobiSilAsync(id);
                    }
                    else
                    {
                        bool hostAyriliyor = lobi.HostName == profil.OyuncuAdi && lobi.HostTag == profil.Tag;
                        var guncelleme = new Dictionary<string, object>
                        {
                            { "players", kalan.Select(p => new Dictionary<string, object>
                            {
                                { "name", p.Name }, { "tag", p.Tag }, { "elo", p.Elo },
                                { "tier", p.Tier }, { "rank", p.Rank }, { "cardUrl", p.CardUrl ?? "" }
                            }).Cast<object>().ToList() }
                        };
                        if (hostAyriliyor)
                        {
                            guncelleme["hostName"] = kalan.First().Name;
                            guncelleme["hostTag"] = kalan.First().Tag;
                            guncelleme["hostElo"] = kalan.First().Elo;
                            guncelleme["hostTier"] = kalan.First().Tier;
                        }
                        await App.Supabase.LobiGuncelleAsync(id, guncelleme);
                    }
                }
                catch { }
            }
            _katildigimLobiIds.Clear();
        }

        public void Temizle()
        {
            _katildigimLobiIds.Clear();
            Gruplar.Clear();
            FiltrelenmisGruplar.Clear();
            if (App.Supabase != null)
            {
                App.Supabase.LobilerGuncellendi -= OnLobilerGuncellendi;
                App.Supabase.StopListening();
            }
        }

        [RelayCommand]
        private void ModalAc()
        {
            GrupAdi = "";
            MinRankTier = 0;
            MaxRankTier = 30;
            SeciliMacTipi = "Premier";
            SesliIletisim = true;
            MaksOyuncu = 5;
            GrupDili = "TR";
            GrupKodu = "";
            OlusturmaHatasi = false;
            OlusturmaHataMesaji = "";
            ModalAcik = true;
        }

        [RelayCommand]
        private void ModalKapat()
        {
            ModalAcik = false;
        }

        [RelayCommand]
        private async Task GrupOlusturAsync()
        {
            if (_katildigimLobiIds.Count > 0)
            {
                OlusturmaHatasi = true;
                OlusturmaHataMesaji = "Zaten bir gruptasınız. Önce mevcut gruptan çıkış yapın.";
                return;
            }

            if (string.IsNullOrWhiteSpace(GrupAdi))
            {
                OlusturmaHatasi = true;
                OlusturmaHataMesaji = "Grup adı gerekli";
                return;
            }
            if (string.IsNullOrWhiteSpace(GrupKodu) || GrupKodu.Length < 4)
            {
                OlusturmaHatasi = true;
                OlusturmaHataMesaji = "Grup kodu gerekli (en az 4 karakter)";
                return;
            }

            if (Olusturuyor) return;
            Olusturuyor = true;
            OlusturmaHatasi = false;
            try
            {
                var profil = _userService.GetProfile();
                string hostName = profil?.OyuncuAdi ?? "Oyuncu";
                string hostTag = profil?.Tag ?? "0000";
                int hostTier = profil?.CurrentTier ?? 0;
                int hostElo = profil?.Elo ?? 0;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var code = string.IsNullOrWhiteSpace(GrupKodu) ? KodOlustur() : GrupKodu.ToUpperInvariant();

                if (App.Supabase != null)
                {
                    var lobi = new FirestoreLobi
                    {
                        HostName = hostName,
                        HostTag = hostTag,
                        HostElo = hostElo,
                        HostTier = hostTier,
                        HostRank = RankTierToName(hostTier),
                        GameMode = SeciliMacTipi,
                        Region = "eu",
                        Status = "waiting",
                        Durum = "waiting",
                        OyunModu = SeciliMacTipi,
                        MevcutOyuncu = 1,
                        OlusturanUid = App.Supabase.LocalId ?? "",
                        GroupCode = code,
                        MaxPlayers = MaksOyuncu,
                        MinRankTier = MinRankTier,
                        MaxRankTier = MaxRankTier,
                        CreatedAt = now,
                        ExpiresAt = now + 7200,
                        Players = new List<LobbyPlayer>
                        {
                            new() { Name = hostName, Tag = hostTag, Elo = hostElo, Tier = hostTier, Rank = RankTierToName(hostTier), CardUrl = profil?.CardSmallUrl ?? "" }
                        }
                    };
                    var lobbyId = await App.Supabase.LobiOlusturAsync(lobi);
                    if (string.IsNullOrEmpty(lobbyId))
                    {
                        OlusturmaHatasi = true;
                        OlusturmaHataMesaji = "Grup oluşturulamadı. Lütfen tekrar deneyin.";
                        return;
                    }
                    lobi.Id = lobbyId;

                    _katildigimLobiIds.TryAdd(lobbyId, 0);

                    var yeniGrup = DonusturGrupKarti(lobi);
                    yeniGrup.BenKatildimMi = true;
                    var mevcut = Gruplar.ToList();
                    mevcut.Insert(0, yeniGrup);
                    Gruplar = new ObservableCollection<GrupKarti>(mevcut);
                    FiltreUygula();
                }

                ModalAcik = false;
            }
            catch (Exception ex)
            {
                OlusturmaHatasi = true;
                OlusturmaHataMesaji = $"Hata: {ex.Message}";
            }
            finally
            {
                Olusturuyor = false;
            }
        }

        private static string RankTierToName(int tier)
        {
            if (tier <= 3) return "Demir";
            if (tier <= 6) return "Bronz";
            if (tier <= 9) return "Gümüş";
            if (tier <= 12) return "Altın";
            if (tier <= 15) return "Platin";
            if (tier <= 18) return "Elmas";
            if (tier <= 21) return "Yükselen";
            if (tier <= 24) return "Ölümsüz";
            return "Işınsal";
        }

        private static string KodOlustur()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rand = new Random();
            return new string(Enumerable.Range(0, 6).Select(_ => chars[rand.Next(chars.Length)]).ToArray());
        }

        [RelayCommand]
        private void KoduKopyala(string kod)
        {
            try
            {
                System.Windows.Clipboard.SetText(kod);
            }
            catch { }
        }

        [RelayCommand]
        private void KomutUygula(string komut)
        {
            switch (komut)
            {
                case "derece":
                    SeciliMacTipi = "Derece";
                    MaksOyuncu = 5;
                    SesliIletisim = true;
                    break;
                case "premier":
                    SeciliMacTipi = "Premier";
                    MaksOyuncu = 5;
                    SesliIletisim = true;
                    break;
                case "normal":
                    SeciliMacTipi = "Normal";
                    MaksOyuncu = 5;
                    SesliIletisim = false;
                    break;
                case "duo":
                    SeciliMacTipi = "Derece";
                    MaksOyuncu = 2;
                    SesliIletisim = true;
                    break;
            }
        }
    }
}
