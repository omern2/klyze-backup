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
        private string _localId;

        [ObservableProperty] private string _oyuncuAdi = "";
        [ObservableProperty] private string _oyuncuTag = "";
        [ObservableProperty] private string _rutbeAdi = "";
        [ObservableProperty] private int _currentTier;
        [ObservableProperty] private int _hostElo;
        [ObservableProperty] private string _cardUrl = "";
        [ObservableProperty] private string _bolge = "eu";

        public ImageSource RankIkonKaynak => RankIkonHelper.RankIkonFromTier(CurrentTier);
        public string EloText => HostElo > 0 ? HostElo.ToString() : "—";

        partial void OnCurrentTierChanged(int value) => OnPropertyChanged(nameof(RankIkonKaynak));
        partial void OnHostEloChanged(int value) => OnPropertyChanged(nameof(EloText));

        [ObservableProperty] private ImageSource _cardImageSource;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private string _statusMessage = "";

        [ObservableProperty] private string _state = "idle";

        // Lobi bulma (mevcut)
        [ObservableProperty] private bool _modalAcik;
        [ObservableProperty] private string _grupKoduInput = "";
        [ObservableProperty] private bool _modalLoading;
        [ObservableProperty] private string _modalHata = "";

        [ObservableProperty] private FirestoreLobi _bulunanLobi;
        [ObservableProperty] private string _kopyalaButonText = "Kodu Kopyala";
        [ObservableProperty] private bool _kopyalandi;

        public ImageSource BulunanLobiRankIkon =>
            BulunanLobi != null ? RankIkonHelper.RankIkonFromTier(BulunanLobi.HostTier) : null;

        partial void OnBulunanLobiChanged(FirestoreLobi value) => OnPropertyChanged(nameof(BulunanLobiRankIkon));

        [ObservableProperty] private string _aktifLobiId = "";
        [ObservableProperty] private string _aktifGrupKodu = "";

        // Eşleşme (matchmaking)
        [ObservableProperty] private Oda _aktifOda;
        [ObservableProperty] private string _odaGrupKoduInput = "";
        [ObservableProperty] private bool _odaGrupKoduGirildi;
        [ObservableProperty] private string _rakipAdi = "";
        [ObservableProperty] private string _rakipTag = "";
        [ObservableProperty] private int _rakipElo;
        [ObservableProperty] private int _rakipTier;
        [ObservableProperty] private string _rakipKartUrl = "";

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

        public PlayViewModel(UserService userService = null)
        {
            _userService = userService;
            _lobbyService = new LobbyService();
            _lobbyService.LobilerGuncellendi += OnLobilerGuncellendi;

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

            YenidenYukle();
            _ = BaslatAsync();
        }

        private async Task BaslatAsync()
        {
            await _lobbyService.BaslatAsync();
            _localId = _lobbyService.GetLocalId();
            _lobbyService.StartListening();

            // Uygulama açıldığında aktif oda var mı kontrol et
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
            HostElo = HesaplaElo(profil.CurrentTier, profil.RutbePuani);
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

        // ─── MAÇ BUL ──────────────────────────────────────────────────────────────

        private async Task MacBulAsync()
        {
            ErrorMessage = "";
            StatusMessage = "";
            State = "searching";
            IsLoading = true;
            BulunanLobi = null;

            try
            {
                await Task.Delay(600);

                // 1. Önce mevcut lobileri kontrol et
                var lobiler = await _lobbyService.GetUygunLobilerAsync(HostElo);

                if (lobiler.Count > 0)
                {
                    BulunanLobi = lobiler.First();
                    KopyalaButonText = "Kodu Kopyala";
                    Kopyalandi = false;
                    State = "found";
                    StatusMessage = $"{lobiler.Count} lobi bulundu";
                    return;
                }

                // 2. Lobi yok → matchmaking kuyruğuna gir
                if (string.IsNullOrEmpty(_localId))
                {
                    ErrorMessage = "Kimlik alınamadı.";
                    State = "idle";
                    return;
                }

                await _lobbyService.QueueEkleAsync(_localId, OyuncuAdi, OyuncuTag,
                    HostElo, CurrentTier, Bolge);

                StatusMessage = "Rakip aranıyor...";
                State = "matching";

                // 3. Eşleşme için polling başlat
                _eslesmeCts?.Cancel();
                _eslesmeCts = new CancellationTokenSource();
                _ = EslesmePollingAsync(_eslesmeCts.Token);
            }
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

        private async Task EslesmePollingAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && State == "matching")
            {
                try
                {
                    await Task.Delay(3000, ct);
                    if (ct.IsCancellationRequested || State != "matching") break;

                    var eslesme = await _lobbyService.EnYakinEslesmeBulAsync(HostElo, _localId);
                    if (eslesme != null)
                    {
                        // Eşleşme bulundu → ikimizi de kuyruktan çıkar → oda oluştur
                        await _lobbyService.QueueKaldirAsync(_localId);
                        await _lobbyService.QueueKaldirAsync(eslesme.LocalId);

                        var roomId = await _lobbyService.OdaOlusturAsync(
                            _localId, eslesme.LocalId,
                            OyuncuAdi, OyuncuTag, HostElo, CurrentTier,
                            eslesme.PlayerName, eslesme.PlayerTag, eslesme.Elo, eslesme.Tier);

                        var oda = await _lobbyService.OdaGetirAsync(roomId);
                        if (oda != null)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() => OdaBulundu(oda));
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        private void OdaBulundu(Oda oda)
        {
            AktifOda = oda;
            OdaGrupKoduInput = "";
            OdaGrupKoduGirildi = !string.IsNullOrEmpty(oda.GroupCode);

            // Rakip bilgilerini ayarla
            if (oda.Player1LocalId == _localId)
            {
                RakipAdi = oda.Player2Name;
                RakipTag = oda.Player2Tag;
                RakipElo = oda.Player2Elo;
                RakipTier = oda.Player2Tier;
            }
            else
            {
                RakipAdi = oda.Player1Name;
                RakipTag = oda.Player1Tag;
                RakipElo = oda.Player1Elo;
                RakipTier = oda.Player1Tier;
            }

            // Rakip rank kartını yükle
            var rakipCardUrl = _userService?.GetProfile()?.CardSmallUrl ?? "";
            if (!string.IsNullOrEmpty(rakipCardUrl))
                _ = RakipKartGorselYukleAsync(rakipCardUrl);

            State = "matched";
            StatusMessage = "Rakip bulundu!";
        }

        // ─── Eşleşmeyi İptal Et ──────────────────────────────────────────────────

        private async Task EslesmeyiIptalAsync()
        {
            _eslesmeCts?.Cancel();

            if (!string.IsNullOrEmpty(_localId))
                await _lobbyService.QueueKaldirAsync(_localId);

            if (AktifOda != null && !string.IsNullOrEmpty(AktifOda.Id))
            {
                await _lobbyService.OdaSilAsync(AktifOda.Id);
                AktifOda = null;
            }

            Sifirla();
        }

        // ─── Oda Grup Kodu ──────────────────────────────────────────────────────

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

        // ─── Lobi Oluştur (mevcut) ──────────────────────────────────────────────

        private async Task LobiOlusturAsync()
        {
            if (string.IsNullOrWhiteSpace(GrupKoduInput))
            {
                ModalHata = "Grup kodunu gir.";
                return;
            }

            ModalLoading = true;
            ModalHata = "";

            try
            {
                var id = await _lobbyService.LobiOlusturAsync(
                    GrupKoduInput.Trim().ToUpper(),
                    OyuncuAdi, OyuncuTag,
                    HostElo, CurrentTier, Bolge);

                AktifLobiId = id;
                AktifGrupKodu = GrupKoduInput.Trim().ToUpper();
                ModalAcik = false;
                State = "lobby_created";
                StatusMessage = "Lobi oluşturuldu, maç bekleniyor...";
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

        private async Task KoduKopyalaAsync()
        {
            if (BulunanLobi == null) return;
            try
            {
                Clipboard.SetText(BulunanLobi.GroupCode);
                KopyalaButonText = "Kopyalandı ✓";
                Kopyalandi = true;
                await Task.Delay(2000);
                KopyalaButonText = "Kodu Kopyala";
                Kopyalandi = false;
            }
            catch { }
        }

        // ─── Sıfırla ──────────────────────────────────────────────────────────────

        public void Sifirla()
        {
            _eslesmeCts?.Cancel();
            State = "idle";
            BulunanLobi = null;
            AktifOda = null;
            ErrorMessage = "";
            StatusMessage = "";
            Kopyalandi = false;
            KopyalaButonText = "Kodu Kopyala";
            OdaGrupKoduGirildi = false;
            OdaGrupKoduInput = "";

            if (!string.IsNullOrEmpty(AktifLobiId))
            {
                _ = _lobbyService.LobiSilAsync(AktifLobiId);
                AktifLobiId = "";
                AktifGrupKodu = "";
            }
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
            if (!string.IsNullOrEmpty(AktifLobiId))
                _ = _lobbyService.LobiSilAsync(AktifLobiId);
        }
    }
}
