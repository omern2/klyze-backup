using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class PlayViewModel : ObservableObject
    {
        private readonly LobbyService _lobbyService;
        private readonly UserService _userService;

        // ─── Genel State ─────────────────────────────────────────────────────────

        // "main" | "searching" | "results" | "lobby"
        [ObservableProperty] private string _screenState = "main";
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private string _statusMessage = "";

        // ─── Oyuncu Bilgisi (UserService'den otomatik) ───────────────────────────

        [ObservableProperty] private string _oyuncuAdi = "Oyuncu";
        [ObservableProperty] private string _oyuncuTag = "";
        [ObservableProperty] private string _seciliRutbe = "Altın";
        [ObservableProperty] private int _rutbePuani = 0;
        [ObservableProperty] private string _enCokOynadigiAjan = "";
        [ObservableProperty] private double _kazanmaOrani = 0;
        [ObservableProperty] private bool _girisYapildi = false;

        public string[] TumRutbeler => Rutbeler.Liste;

        // ─── Lobi Listesi ────────────────────────────────────────────────────────

        public ObservableCollection<LobiCardVM> LobiListesi { get; } = new();

        // ─── Aktif Lobi ──────────────────────────────────────────────────────────

        [ObservableProperty] private LobiCardVM _aktifLobi = null;

        // ─── Modal State ─────────────────────────────────────────────────────────

        [ObservableProperty] private bool _lobiModalAcik = false;
        [ObservableProperty] private string _grupKoduInput = "";
        [ObservableProperty] private bool _rutbeSecimAcik = false;

        // ─── Commands ────────────────────────────────────────────────────────────

        public IRelayCommand MacBulCommand { get; }
        public IRelayCommand LobiOlusturCommand { get; }
        public IRelayCommand LobiOlusturOnayCommand { get; }
        public IRelayCommand ModalKapatCommand { get; }
        public IRelayCommand RutbeSecimKapatCommand { get; }
        public IRelayCommand<string> RutbeSecCommand { get; }
        public IRelayCommand<LobiCardVM> LobiKatilCommand { get; }
        public IRelayCommand LobidenAyrilCommand { get; }

        public PlayViewModel(UserService userService = null)
        {
            _lobbyService = new LobbyService();
            _userService = userService;

            MacBulCommand = new RelayCommand(MacBulAc);
            LobiOlusturCommand = new RelayCommand(() => { GrupKoduInput = ""; LobiModalAcik = true; });
            LobiOlusturOnayCommand = new AsyncRelayCommand(LobiOlusturAsync);
            ModalKapatCommand = new RelayCommand(() => LobiModalAcik = false);
            RutbeSecimKapatCommand = new RelayCommand(() => RutbeSecimAcik = false);
            RutbeSecCommand = new RelayCommand<string>(RutbeSec);
            LobiKatilCommand = new AsyncRelayCommand<LobiCardVM>(LobiKatilAsync);
            LobidenAyrilCommand = new AsyncRelayCommand(LobidenAyrilAsync);

            // Kullanıcı bilgilerini yükle
            YenidenYukle();
            _ = LobileriYukleAsync();
        }

        /// <summary>Giriş yapıldıktan sonra kullanıcı bilgilerini UserService'den çeker.</summary>
        public void YenidenYukle()
        {
            var profil = _userService?.GetProfile();
            if (profil != null)
            {
                OyuncuAdi = profil.RiotId;
                OyuncuTag = profil.Tag;
                SeciliRutbe = profil.Rutbe;
                RutbePuani = profil.RutbePuani;
                EnCokOynadigiAjan = profil.EnCokOynadigiAjan;
                KazanmaOrani = profil.KazanmaOrani;
                GirisYapildi = true;
            }
            else
            {
                GirisYapildi = false;
            }
        }

        /// <summary>Çıkış yapılınca state'i sıfırlar.</summary>
        public void Sifirla()
        {
            OyuncuAdi = "Oyuncu";
            OyuncuTag = "";
            SeciliRutbe = "Altın";
            RutbePuani = 0;
            EnCokOynadigiAjan = "";
            KazanmaOrani = 0;
            GirisYapildi = false;
            AktifLobi = null;
            LobiListesi.Clear();
            ScreenState = "main";
            ErrorMessage = "";
            StatusMessage = "";
        }

        // ─── Maç Bul ─────────────────────────────────────────────────────────────

        private void MacBulAc()
        {
            ErrorMessage = "";
            // Giriş yapıldıysa rütbe otomatik — direkt ara
            // Giriş yapılmadıysa rütbe seçim paneli aç
            if (GirisYapildi)
                _ = MacAraAsync();
            else
                RutbeSecimAcik = true;
        }

        private void RutbeSec(string rutbe)
        {
            SeciliRutbe = rutbe;
            RutbeSecimAcik = false;
            _ = MacAraAsync();
        }

        private async Task MacAraAsync()
        {
            IsLoading = true;
            ScreenState = "searching";
            ErrorMessage = "";
            LobiListesi.Clear();

            try
            {
                // Arama animasyonu için kısa bekleme
                await Task.Delay(800);

                var lobiler = await _lobbyService.GetUygunLobilerAsync(SeciliRutbe);

                if (lobiler.Count == 0)
                {
                    StatusMessage = $"Şu an uygun lobi bulunamadı. Lobi oluşturabilirsin!";
                    ScreenState = "results";
                    return;
                }

                foreach (var l in lobiler)
                    LobiListesi.Add(new LobiCardVM(l));

                StatusMessage = $"{lobiler.Count} lobi bulundu";
                ScreenState = "results";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Hata: {ex.Message}";
                ScreenState = "main";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ─── Lobi Oluştur ────────────────────────────────────────────────────────

        private async Task LobiOlusturAsync()
        {
            if (string.IsNullOrWhiteSpace(GrupKoduInput))
            {
                ErrorMessage = "Lütfen bir Valorant grup kodu girin.";
                return;
            }

            LobiModalAcik = false;
            IsLoading = true;
            ErrorMessage = "";

            try
            {
                var lobi = await _lobbyService.CreateLobiAsync(
                    GrupKoduInput.Trim(),
                    OyuncuAdi,
                    SeciliRutbe,
                    RutbePuani);

                AktifLobi = new LobiCardVM(lobi);
                ScreenState = "lobby";
                StatusMessage = "Lobi oluşturuldu!";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Hata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ─── Lobiye Katıl ────────────────────────────────────────────────────────

        private async Task LobiKatilAsync(LobiCardVM lobiCard)
        {
            if (lobiCard == null) return;
            IsLoading = true;
            ErrorMessage = "";

            try
            {
                var oyuncu = new OyuncuData
                {
                    Ad = OyuncuAdi,
                    Rutbe = SeciliRutbe,
                    RutbePuani = RutbePuani,
                    Ping = new Random().Next(12, 120)
                };

                var (basarili, hata) = await _lobbyService.JoinLobiAsync(lobiCard.Id, oyuncu);

                if (!basarili)
                {
                    ErrorMessage = hata ?? "Lobiye katılınamadı.";
                    return;
                }

                // Güncel lobi verisini yükle
                var lobiler = await _lobbyService.GetLobilerAsync();
                var guncellenmis = lobiler.FirstOrDefault(l => l.Id == lobiCard.Id);
                if (guncellenmis != null)
                    AktifLobi = new LobiCardVM(guncellenmis);

                ScreenState = "lobby";
                StatusMessage = "Lobiye katıldın!";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Hata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ─── Lobiden Ayrıl ───────────────────────────────────────────────────────

        private async Task LobidenAyrilAsync()
        {
            if (AktifLobi != null && AktifLobi.Olusturan == OyuncuAdi)
                await _lobbyService.DeleteLobiAsync(AktifLobi.Id);

            AktifLobi = null;
            LobiListesi.Clear();
            ScreenState = "main";
            StatusMessage = "";
            ErrorMessage = "";
        }

        // ─── Lobileri Yükle ──────────────────────────────────────────────────────

        private async Task LobileriYukleAsync()
        {
            try
            {
                await _lobbyService.GetLobilerAsync(); // temizlik için
            }
            catch { }
        }

        // ─── Geri ────────────────────────────────────────────────────────────────

        [RelayCommand]
        private void GeriDon()
        {
            ScreenState = "main";
            LobiListesi.Clear();
            ErrorMessage = "";
            StatusMessage = "";
        }
    }

    // ─── Lobi Kart ViewModel ─────────────────────────────────────────────────────

    public class LobiCardVM : ObservableObject
    {
        private readonly LobiData _data;

        public string Id => _data.Id;
        public string GrupKodu => _data.GrupKodu;
        public string Olusturan => _data.Olusturan;
        public string Rutbe => _data.Rutbe;
        public string Durum => _data.Durum;
        public int OyuncuSayisi => _data.Oyuncular.Count;
        public int MaxOyuncu => _data.MaxOyuncu;
        public string OyuncuSayisiText => $"{OyuncuSayisi}/{MaxOyuncu}";
        public bool Dolu => _data.Dolu;

        // 5 slot — dolu olanlar oyuncu, boş olanlar "+" gösterir
        public OyuncuSlot[] Slotlar { get; }

        public LobiCardVM(LobiData data)
        {
            _data = data;
            Slotlar = new OyuncuSlot[5];
            for (int i = 0; i < 5; i++)
            {
                if (i < data.Oyuncular.Count)
                    Slotlar[i] = new OyuncuSlot { Dolu = true, Oyuncu = data.Oyuncular[i] };
                else
                    Slotlar[i] = new OyuncuSlot { Dolu = false, Oyuncu = null };
            }
        }
    }
}
