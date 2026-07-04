using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Helpers;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class PlayViewModel : ObservableObject
    {
        private readonly LobbyService _lobbyService;
        private readonly UserService _userService;
        private CancellationTokenSource _eslesmeCts;
        private CancellationTokenSource _aramaCts;
        private string _localId;
        private bool _lobbyDinleniyor;

        [ObservableProperty] private string _oyuncuAdi = "";
        [ObservableProperty] private string _oyuncuTag = "";
        [ObservableProperty] private string _rutbeAdi = "";
        [ObservableProperty] private int _currentTier;
        [ObservableProperty] private int _hostElo;
        [ObservableProperty] private string _cardUrl = "";
        [ObservableProperty] private string _bolge = "eu";
        [ObservableProperty] private string _seciliOyunModu = "1v1";
        [ObservableProperty] private int _aranacakOyuncuSayisi = 2;

        public ImageSource RankIkonKaynak => RankIkonHelper.RankIkonFromTier(CurrentTier);
        public string EloText => HostElo > 0 ? HostElo.ToString() : "—";

        partial void OnCurrentTierChanged(int value) => OnPropertyChanged(nameof(RankIkonKaynak));
        partial void OnHostEloChanged(int value) => OnPropertyChanged(nameof(EloText));

        [ObservableProperty] private ImageSource _cardImageSource;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private string _statusMessage = "";

        [ObservableProperty] private string _state = "idle";

        // Lobi bulma
        [ObservableProperty] private string _aktifLobiId = "";
        [ObservableProperty] private string _aktifGrupKodu = "";
        [ObservableProperty] private int _lobiOyuncuSayisi;
        [ObservableProperty] private int _lobiMaxOyuncu;
        [ObservableProperty] private List<LobbyPlayer> _lobiOyuncular = new();
        [ObservableProperty] private bool _lobiSahibi;

        // Modal
        [ObservableProperty] private bool _modalAcik;
        [ObservableProperty] private string _grupKoduInput = "";
        [ObservableProperty] private bool _modalLoading;
        [ObservableProperty] private string _modalHata = "";

        // Kopyalama
        [ObservableProperty] private string _kopyalaButonText = "Kodu Kopyala";
        [ObservableProperty] private bool _kopyalandi;

        // Legacy (geçici uyum)
        [ObservableProperty] private FirestoreLobi _bulunanLobi;
        [ObservableProperty] private Oda _aktifOda;
        [ObservableProperty] private string _odaGrupKoduInput = "";
        [ObservableProperty] private bool _odaGrupKoduGirildi;
        [ObservableProperty] private string _rakipAdi = "";
        [ObservableProperty] private string _rakipTag = "";
        [ObservableProperty] private int _rakipElo;
        [ObservableProperty] private int _rakipTier;
        [ObservableProperty] private string _rakipKartUrl = "";

        public ImageSource BulunanLobiRankIkon =>
            BulunanLobi != null ? RankIkonHelper.RankIkonFromTier(BulunanLobi.HostTier) : null;
        partial void OnBulunanLobiChanged(FirestoreLobi value) => OnPropertyChanged(nameof(BulunanLobiRankIkon));

        public ImageSource RakipRankIkon => RankIkonHelper.RankIkonFromTier(RakipTier);
        public string RakipEloText => RakipElo > 0 ? RakipElo.ToString() : "—";
        public bool BenHost => AktifOda != null && AktifOda.Player1LocalId == _localId;

        partial void OnRakipTierChanged(int value) => OnPropertyChanged(nameof(RakipRankIkon));
        partial void OnRakipEloChanged(int value) => OnPropertyChanged(nameof(RakipEloText));
        partial void OnAktifOdaChanged(Oda value)
        {
            OnPropertyChanged(nameof(BenHost));
            OnPropertyChanged(nameof(RakipRankIkon));
            OnPropertyChanged(nameof(RakipEloText));
        }

        [ObservableProperty] private ImageSource _rakipCardImageSource;

        public IRelayCommand MacBulCommand { get; }
        public IRelayCommand LobiOlusturAcCommand { get; }
        public IRelayCommand LobiOlusturOnayCommand { get; }
        public IRelayCommand ModalKapatCommand { get; }
        public IRelayCommand KoduKopyalaCommand { get; }
        public IRelayCommand SifirlaCommand { get; }
        public IRelayCommand EslesmeyiIptalCommand { get; }
        public IRelayCommand OdaGrupKoduKaydetCommand { get; }
        public IRelayCommand OdaKoduKopyalaCommand { get; }
        public IRelayCommand LobidenCikCommand { get; }
        public IRelayCommand LobbyGrupKoduKaydetCommand { get; }

        public PlayViewModel(UserService userService = null)
        {
            _userService = userService;
            _lobbyService = new LobbyService();
            _lobbyService.LobilerGuncellendi += OnLobilerGuncellendi;
            _lobbyService.LobbyGuncellendi += OnLobbySnapshot;

            MacBulCommand = new AsyncRelayCommand(MacBulAsync);
            LobiOlusturAcCommand = new RelayCommand(() =>
            {
                GrupKoduInput = "";
                ModalHata = "";
                ModalAcik = true;
            });
            LobiOlusturOnayCommand = new AsyncRelayCommand(LobiOlusturAsync);
            ModalKapatCommand = new RelayCommand(() => ModalAcik = false);
            KoduKopyalaCommand = new AsyncRelayCommand(KoduKopyalaAsync);
            SifirlaCommand = new RelayCommand(Sifirla);
            EslesmeyiIptalCommand = new AsyncRelayCommand(EslesmeyiIptalAsync);
            OdaGrupKoduKaydetCommand = new AsyncRelayCommand(OdaGrupKoduKaydetAsync);
            OdaKoduKopyalaCommand = new AsyncRelayCommand(OdaKoduKopyalaAsync);
            LobidenCikCommand = new AsyncRelayCommand(LobidenCikAsync);
            LobbyGrupKoduKaydetCommand = new AsyncRelayCommand(LobbyGrupKoduKaydetAsync);

            YenidenYukle();
            _ = BaslatAsync();
        }

        private async Task BaslatAsync()
        {
            await _lobbyService.BaslatAsync();
            _localId = _lobbyService.GetLocalId();
            _lobbyService.StartListening();

            // Süresi dolan lobileri temizle
            await _lobbyService.CleanExpiredLobbiesAsync();

            // Mevcut aktif oda var mı kontrol et (legacy)
            if (!string.IsNullOrEmpty(_localId))
            {
                var oda = await _lobbyService.OyuncuAktifOdaGetirAsync(_localId);
                if (oda != null)
                    OdaBulundu(oda);
            }
        }

        public void YenidenYukle()
        {
            var profil = _userService?.GetProfile();
            if (profil == null) return;

            OyuncuAdi = profil.OyuncuAdi;
            OyuncuTag = profil.Tag;
            CurrentTier = profil.CurrentTier;
            RutbeAdi = profil.Rutbe;
            HostElo = profil.Elo > 0 ? profil.Elo : HesaplaElo(profil.CurrentTier, profil.RutbePuani);
            CardUrl = profil.CardSmallUrl ?? "";
            Bolge = string.IsNullOrEmpty(profil.Bolge) ? "eu" : profil.Bolge;

            if (!string.IsNullOrEmpty(CardUrl))
                _ = KartGorselYukleAsync(CardUrl);
        }

        private async Task KartGorselYukleAsync(string url)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var bytes = await http.GetByteArrayAsync(url);
                var app = Application.Current;
                if (app?.Dispatcher == null) return;
                await app.Dispatcher.InvokeAsync(() =>
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new System.IO.MemoryStream(bytes);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    CardImageSource = bmp;
                });
            }
            catch { }
        }

        private async Task RakipKartGorselYukleAsync(string url)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var bytes = await http.GetByteArrayAsync(url);
                var app = Application.Current;
                if (app?.Dispatcher == null) return;
                await app.Dispatcher.InvokeAsync(() =>
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new System.IO.MemoryStream(bytes);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    RakipCardImageSource = bmp;
                });
            }
            catch { }
        }

        private static int HesaplaElo(int tier, int rr)
        {
            return Math.Max(0, (tier - 1) * 100 + rr);
        }

        // ─── MAÇ BUL (Yeni: 5 kişilik lobby sistemi) ────────────────────────────

        private async Task MacBulAsync()
        {
            if (State is "lobby_created" or "lobby_full")
            {
                ErrorMessage = "Zaten bir lobidesin.";
                return;
            }

            ErrorMessage = "";
            StatusMessage = "";
            State = "searching";
            IsLoading = true;

            if (string.IsNullOrEmpty(_localId))
            {
                ErrorMessage = "Kimlik alınamadı.";
                State = "idle";
                IsLoading = false;
                return;
            }

            _aramaCts?.Cancel();
            _aramaCts = new CancellationTokenSource();
            var ct = _aramaCts.Token;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    StatusMessage = "Lobi aranıyor...";

                    var lobi = await _lobbyService.FindAndJoinLobbyAsync(
                        HostElo, SeciliOyunModu,
                        OyuncuAdi, OyuncuTag, CurrentTier, RutbeAdi, CardUrl, Bolge, AranacakOyuncuSayisi);

                    if (lobi != null)
                    {
                        StatusMessage = "";
                        LobiBulundu(lobi);
                        return;
                    }

                    await Task.Delay(3000, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                State = "idle";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LobiBulundu(FirestoreLobi lobi)
        {
            AktifLobiId = lobi.Id;
            LobiOyuncular = lobi.Players ?? new List<LobbyPlayer>();
            LobiOyuncuSayisi = LobiOyuncular.Count;
            LobiMaxOyuncu = lobi.MaxPlayers;
            LobiSahibi = lobi.HostName == OyuncuAdi && lobi.HostTag == OyuncuTag;
            AktifGrupKodu = lobi.GroupCode ?? "";

            // Lobby listener'ı başlat
            if (!_lobbyDinleniyor)
            {
                _lobbyService.StartLobbyListener(lobi.Id);
                _lobbyDinleniyor = true;
            }

            State = lobi.Status == "full" && !string.IsNullOrEmpty(lobi.GroupCode)
                ? "lobby_full"
                : "lobby_created";
        }

        // ─── Gerçek Zamanlı Lobby Güncellemeleri ──────────────────────────────

        private void OnLobbySnapshot(FirestoreLobi lobi)
        {
            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                if (lobi == null)
                {
                    if (State == "lobby_created" && !string.IsNullOrEmpty(AktifGrupKodu))
                        State = "lobby_full";
                    return;
                }

                var oncekiKod = AktifGrupKodu;
                LobiOyuncular = lobi.Players ?? new List<LobbyPlayer>();
                LobiOyuncuSayisi = LobiOyuncular.Count;
                AktifGrupKodu = lobi.GroupCode ?? "";

                if (lobi.Status == "full" && !string.IsNullOrEmpty(lobi.GroupCode))
                    State = "lobby_full";
                else if (State == "lobby_full" && lobi.Status != "full")
                    State = "lobby_created";

                // Grup kodu değiştiyse lobby panelini yenile
                if (oncekiKod != AktifGrupKodu)
                    OnPropertyChanged(nameof(AktifGrupKodu));
            });
        }

        // ─── LOBİDEN ÇIK ───────────────────────────────────────────────────────

        private async Task LobidenCikAsync()
        {
            if (string.IsNullOrEmpty(AktifLobiId)) return;

            var eskiLobiId = AktifLobiId;

            await _lobbyService.LeaveLobbyAsync(eskiLobiId, OyuncuAdi, OyuncuTag);

            if (_lobbyDinleniyor)
            {
                _lobbyService.StopLobbyListener();
                _lobbyDinleniyor = false;
            }

            Sifirla();
        }

        // ─── LOBİ GRUP KODUNU KAYDET (sadece lobby panel) ─────────────────────

        private async Task LobbyGrupKoduKaydetAsync()
        {
            if (string.IsNullOrWhiteSpace(GrupKoduInput) || string.IsNullOrEmpty(AktifLobiId))
            {
                ModalHata = "Grup kodunu gir.";
                return;
            }

            ModalHata = "";
            ModalLoading = true;

            try
            {
                await _lobbyService.UpdateGroupCodeAsync(AktifLobiId, GrupKoduInput.Trim().ToUpper());
                AktifGrupKodu = GrupKoduInput.Trim().ToUpper();
            }
            catch (Exception ex)
            {
                ModalHata = ex.Message;
            }
            finally
            {
                ModalLoading = false;
            }
        }

        // ─── GRUP KODU GİR (eski modal, yeni lobby oluştur) ───────────────────

        private async Task LobiOlusturAsync()
        {
            if (State is "lobby_created" or "lobby_full")
            {
                ModalHata = "Zaten bir lobidesin.";
                return;
            }

            if (string.IsNullOrWhiteSpace(GrupKoduInput))
            {
                ModalHata = "Grup kodunu gir.";
                return;
            }

            ModalLoading = true;
            ModalHata = "";

            try
            {
                // Eğer zorla lobby oluştur butonu ise (eski akış)
                var id = await _lobbyService.LobiOlusturAsync(
                    GrupKoduInput.Trim().ToUpper(),
                    OyuncuAdi, OyuncuTag,
                    HostElo, CurrentTier, RutbeAdi, CardUrl, Bolge,
                    SeciliOyunModu, AranacakOyuncuSayisi);

                AktifLobiId = id;
                AktifGrupKodu = GrupKoduInput.Trim().ToUpper();
                ModalAcik = false;

                // Lobby'yi çek ve göster
                var lobi = await _lobbyService.GetLobbyAsync(id);
                if (lobi != null)
                    LobiBulundu(lobi);
                else
                    State = "lobby_created";
            }
            catch (Exception ex)
            {
                ModalHata = ex.Message;
            }
            finally
            {
                ModalLoading = false;
            }
        }

        // ─── Kodu Kopyala ──────────────────────────────────────────────────────

        private async Task KoduKopyalaAsync()
        {
            if (string.IsNullOrEmpty(AktifGrupKodu)) return;
            try
            {
                Clipboard.SetText(AktifGrupKodu);
                KopyalaButonText = "Kopyalandı ✓";
                Kopyalandi = true;
                await Task.Delay(2000);
                KopyalaButonText = "Kodu Kopyala";
                Kopyalandi = false;
            }
            catch { }
        }

        // ─── Legacy: Eşleşme İptal ──────────────────────────────────────────────

        private async Task EslesmeyiIptalAsync()
        {
            _eslesmeCts?.Cancel();
            _aramaCts?.Cancel();

            if (!string.IsNullOrEmpty(_localId))
                await _lobbyService.QueueKaldirAsync(_localId);

            if (AktifOda != null && !string.IsNullOrEmpty(AktifOda.Id))
            {
                await _lobbyService.OdaSilAsync(AktifOda.Id);
                AktifOda = null;
            }

            Sifirla();
        }

        // ─── Legacy: Oda Grup Kodu ─────────────────────────────────────────────

        private async Task OdaGrupKoduKaydetAsync()
        {
            if (AktifOda == null || string.IsNullOrWhiteSpace(OdaGrupKoduInput)) return;
            try
            {
                await _lobbyService.OdaGrupKoduGuncelleAsync(AktifOda.Id, OdaGrupKoduInput.Trim().ToUpper());
                AktifOda.GroupCode = OdaGrupKoduInput.Trim().ToUpper();
                OdaGrupKoduGirildi = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private async Task OdaKoduKopyalaAsync()
        {
            if (AktifOda == null || string.IsNullOrEmpty(AktifOda.GroupCode)) return;
            try
            {
                Clipboard.SetText(AktifOda.GroupCode);
                KopyalaButonText = "Kopyalandı ✓";
                Kopyalandi = true;
                await Task.Delay(2000);
                KopyalaButonText = "Kodu Kopyala";
                Kopyalandi = false;
            }
            catch { }
        }

        // ─── Legacy: Lobi Oluştur (eski) ───────────────────────────────────────

        public async Task<string> LegacyLobiOlusturAsync(string grupKodu,
            string gameMode = "5v5 Normal", int maxPlayers = 5)
        {
            return await _lobbyService.LobiOlusturAsync(
                grupKodu, OyuncuAdi, OyuncuTag, HostElo, CurrentTier, RutbeAdi, CardUrl, Bolge,
                gameMode, maxPlayers);
        }

        // ─── Legacy: Oda Bulundu ───────────────────────────────────────────────

        private void OdaBulundu(Oda oda)
        {
            AktifOda = oda;
            OdaGrupKoduInput = "";
            OdaGrupKoduGirildi = !string.IsNullOrEmpty(oda.GroupCode);

            if (oda.Player1LocalId == _localId)
            {
                RakipAdi = oda.Player2Name;
                RakipTag = oda.Player2Tag;
                RakipElo = oda.Player2Elo;
                RakipTier = oda.Player2Tier;
                RakipKartUrl = oda.Player2CardUrl ?? "";
            }
            else
            {
                RakipAdi = oda.Player1Name;
                RakipTag = oda.Player1Tag;
                RakipElo = oda.Player1Elo;
                RakipTier = oda.Player1Tier;
                RakipKartUrl = oda.Player1CardUrl ?? "";
            }

            if (!string.IsNullOrEmpty(RakipKartUrl))
                _ = RakipKartGorselYukleAsync(RakipKartUrl);

            State = "matched";
            StatusMessage = "Rakip bulundu!";
        }

        // ─── Sıfırla (sadece yerel state, Firebase'e dokunma) ────────────────

        public void Sifirla()
        {
            _eslesmeCts?.Cancel();
            _aramaCts?.Cancel();

            if (_lobbyDinleniyor)
            {
                _lobbyService.StopLobbyListener();
                _lobbyDinleniyor = false;
            }

            State = "idle";
            BulunanLobi = null;
            AktifOda = null;
            AktifLobiId = "";
            AktifGrupKodu = "";
            ErrorMessage = "";
            StatusMessage = "";
            Kopyalandi = false;
            KopyalaButonText = "Kodu Kopyala";
            OdaGrupKoduGirildi = false;
            OdaGrupKoduInput = "";
            LobiOyuncular = new List<LobbyPlayer>();
            LobiOyuncuSayisi = 0;
        }

        private void OnLobilerGuncellendi(List<FirestoreLobi> lobiler)
        {
            if (State == "found" && BulunanLobi != null)
            {
                var guncel = lobiler.FirstOrDefault(l => l.Id == BulunanLobi.Id);
                if (guncel == null)
                {
                    Application.Current?.Dispatcher?.InvokeAsync(() =>
                    {
                        State = "notfound";
                        StatusMessage = "Lobi artık mevcut değil.";
                        BulunanLobi = null;
                    });
                }
            }
        }

        public void Temizle()
        {
            _eslesmeCts?.Cancel();

            if (!string.IsNullOrEmpty(_localId))
                _ = _lobbyService.QueueKaldirAsync(_localId);

            if (AktifOda != null && !string.IsNullOrEmpty(AktifOda.Id))
                _ = _lobbyService.OdaSilAsync(AktifOda.Id);

            _lobbyService.StopListening();

            if (_lobbyDinleniyor)
            {
                _lobbyService.StopLobbyListener();
                _lobbyDinleniyor = false;
            }

            if (!string.IsNullOrEmpty(AktifLobiId))
                _ = _lobbyService.LobiSilAsync(AktifLobiId);
        }
    }
}
