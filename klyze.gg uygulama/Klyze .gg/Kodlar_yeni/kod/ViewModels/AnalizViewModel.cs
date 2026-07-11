using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Helpers;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class AnalizViewModel : ObservableObject
    {
        private readonly AnalizService  _analizService;
        private readonly UserService    _userService;
        private CancellationTokenSource _cts;

        private List<AnalizMac> _allMaclar = new();
        private List<MacGecmisiItem> _allMacGecmisiItems = new();
        private Dictionary<string, (int Elo, int Tier)> _mmrDict = new();
        private Dictionary<string, int> _eloDiffDict = new();
        private int _visibleItemCount;
        private const int BatchSize = 20;

        // ─── State ───────────────────────────────────────────────────────────────
        [ObservableProperty] private bool   _isLoading    = false;
        [ObservableProperty] private bool   _hasData      = false;
        [ObservableProperty] private bool   _hasError     = false;
        [ObservableProperty] private string _errorMessage = "";

        [ObservableProperty] private bool   _isLoadingMore = false;
        [ObservableProperty] private bool   _hasMoreMatches = false;

        // ─── Sekme ────────────────────────────────────────────────────────────────
        [ObservableProperty] private int _selectedTab = 0;

        public bool TabIstatistikler => SelectedTab == 0;
        public bool TabMacGecmisi => SelectedTab == 1;
        public bool TabCanliMac => SelectedTab == 2;

        // ─── Kullanici ───────────────────────────────────────────────────────────
        [ObservableProperty] private string _oyuncuAdi = "";
        [ObservableProperty] private string _rutbe     = "";

        // ─── Rank Karti ────────────────────────────────────────────────────────
        [ObservableProperty] private string _sezonAdi = "";
        [ObservableProperty] private string _sezonKisaAdi = "";
        [ObservableProperty] private ImageSource _rankIkonKaynak;
        [ObservableProperty] private ImageSource _profilRankIkonBuyuk;
        [ObservableProperty] private string _rutbeTier = "";
        [ObservableProperty] private string _profilRankAdi = "";
        [ObservableProperty] private string _profilTierDetay = "";
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RrKalan))]
        private int _rrDeger;
        [ObservableProperty] private double _rrProgress;
        [ObservableProperty] private string _rrKalanText = "";
        [ObservableProperty] private string _sezonBitisText = "";
        public double RrProgressMax => 100.0;
        public int RrKalan => 100 - RrDeger;

        [ObservableProperty] private int _mevcutElo;
        [ObservableProperty] private SolidColorBrush _eloRenk = new(Colors.Gray);
        [ObservableProperty] private SolidColorBrush _rankCizgiRenk = new(Colors.Gray);

        // ─── 6 Performans Metrikleri ─────────────────────────────────────────────
        public ObservableCollection<PerformansMetrik> PerformansMetrikleri { get; } = new();

        // ─── ELO ─────────────────────────────────────────────────────────────────
        public ObservableCollection<EloGrafikNokta> EloGrafikNoktalari { get; } = new();
        public ObservableCollection<MacGecmisiItem> MacGecmisiListesi   { get; } = new();
        [ObservableProperty] private EloOzet _eloOzet = new();
        public ObservableCollection<EloGrafikNokta> MmrMacListesi { get; } = new();

        // ─── Aktivite ────────────────────────────────────────────────────────────
        public ObservableCollection<SaatlikAktivite>  SaatlikAktiviteler  { get; } = new();
        public ObservableCollection<HaftalikAktivite> HaftalikAktiviteler { get; } = new();
        public ObservableCollection<TakvimHaftasi>    TakvimHaftalari     { get; } = new();
        public ObservableCollection<TakvimAyBaslik>   TakvimAyBasliklari  { get; } = new();

        [ObservableProperty] private AktiviteGrafikOzet _saatlikOzet  = new();
        [ObservableProperty] private AktiviteGrafikOzet _haftalikOzet = new();

        // ─── Canli Mac ───────────────────────────────────────────────────────────
        [ObservableProperty] private LiveMatchData _canliMacData;
        [ObservableProperty] private bool _canliMacVisible;
        [ObservableProperty] private bool _canliMacWaiting;
        [ObservableProperty] private bool _canliMacError;
        [ObservableProperty] private string _canliMacTypeText = "";
        [ObservableProperty] private string _canliMacMapText = "";
        [ObservableProperty] private string _canliMacTimeText = "";

        public ObservableCollection<LiveMatchPlayer> CanliMacOyuncularim { get; } = new();
        public ObservableCollection<LiveMatchPlayer> CanliMacRakipler { get; } = new();

        [ObservableProperty] private string _canliMacServerText = "";
        [ObservableProperty] private double _winChanceBiz = 50;
        [ObservableProperty] private double _winChanceRakip = 50;
        [ObservableProperty] private SolidColorBrush _winChanceBizRenk = new(Colors.Gray);
        [ObservableProperty] private SolidColorBrush _winChanceRakipRenk = new(Colors.Gray);

        private CancellationTokenSource _canliMacCts;
        private int _canliMacTickCount;
        private bool _canliMacStarted;
        private bool _isFullRefreshing;
        private const int CanliMacFullRefreshTicks = 6;
        private bool _isLoadingCanliMac;
        private bool _isResettingFilters;

        // ─── Detay Paneli ───────────────────────────────────────────────────────────
        [ObservableProperty] private DetayPanelTipi _detayPanelTipi = DetayPanelTipi.None;
        [ObservableProperty] private bool _detayPanelVisible;
        [ObservableProperty] private KazanmaOraniDetay _kazanmaOraniDetay = new();
        [ObservableProperty] private KrDetay _krDetay = new();
        [ObservableProperty] private AdrDetay _adrDetay = new();
        [ObservableProperty] private HeadshotDetay _headshotDetay = new();
        [ObservableProperty] private GirisBasarisiDetay _girisBasarisiDetay = new();
        [ObservableProperty] private MultiKillDetay _multiKillDetay = new();

        // ─── Maç Detay Paneli ────────────────────────────────────────────────────
        [ObservableProperty] private MacDetay _seciliMacDetay;
        [ObservableProperty] private bool _macDetayVisible;
        [ObservableProperty] private bool _macDetayYukleniyor;
        [ObservableProperty] private string _macDetayHata = "";

        // ─── Harita Analiz ───────────────────────────────────────────────────────
        public ObservableCollection<HaritaIstatistik> HaritaAnalizleri { get; } = new();
        public ObservableCollection<HaritaIstatistik> HaritaKartlari   { get; } = new();

        // ─── Filtreler ────────────────────────────────────────────────────────────
        [ObservableProperty] private int _filtreOyunModuIdx;
        [ObservableProperty] private int _filtreAralikIdx;
        [ObservableProperty] private int _filtreHaritaIdx;
        [ObservableProperty] private int _filtreSiralamaIdx;

        private string AktifOyunModuFilter =>
            FiltreOyunModuIdx >= 0 && FiltreOyunModuIdx < FiltreOyunModuListe.Count
                ? FiltreOyunModuListe[FiltreOyunModuIdx].Deger : "";

        public ObservableCollection<FiltreSecenek> FiltreOyunModuListe { get; } = new()
        {
            new() { Deger = "",    Etiket = "Tüm Modlar" },
            new() { Deger = "competitive", Etiket = "Dereceli" },
            new() { Deger = "unrated",    Etiket = "Derecesiz" },
            new() { Deger = "spikerush",  Etiket = "Spike Rush" },
            new() { Deger = "deathmatch", Etiket = "Deathmatch" },
            new() { Deger = "escalation", Etiket = "Escalation" },
            new() { Deger = "swiftplay",  Etiket = "Swift Play" },
            new() { Deger = "onefa",      Etiket = "1v1" },
        };

        public ObservableCollection<FiltreSecenek> FiltreAralikListe { get; } = new()
        {
            new() { Deger = "all",   Etiket = "Tüm Zamanlar" },
            new() { Deger = "week",  Etiket = "Bu Hafta" },
            new() { Deger = "month", Etiket = "Bu Ay" },
            new() { Deger = "30d",   Etiket = "Son 30 Gün" },
            new() { Deger = "90d",   Etiket = "Son 90 Gün" },
            new() { Deger = "season", Etiket = "Bu Sezon" },
        };

        public ObservableCollection<FiltreSecenek> FiltreHaritaListe { get; } = new()
        {
            new() { Deger = "",         Etiket = "Tüm Haritalar" },
            new() { Deger = "Ascent",   Etiket = "Ascent" },
            new() { Deger = "Bind",     Etiket = "Bind" },
            new() { Deger = "Haven",    Etiket = "Haven" },
            new() { Deger = "Split",    Etiket = "Split" },
            new() { Deger = "Icebox",   Etiket = "Icebox" },
            new() { Deger = "Breeze",   Etiket = "Breeze" },
            new() { Deger = "Fracture", Etiket = "Fracture" },
            new() { Deger = "Pearl",    Etiket = "Pearl" },
            new() { Deger = "Lotus",    Etiket = "Lotus" },
            new() { Deger = "Sunset",   Etiket = "Sunset" },
            new() { Deger = "Abyss",    Etiket = "Abyss" },
        };

        public ObservableCollection<FiltreSecenek> FiltreSiralamaListe { get; } = new()
        {
            new() { Deger = "newest", Etiket = "En Yeni" },
            new() { Deger = "oldest", Etiket = "En Eski" },
            new() { Deger = "kda",    Etiket = "En Yüksek KDA" },
            new() { Deger = "damage", Etiket = "En Yüksek Hasar" },
        };

        public bool FiltrelerVarsayilan =>
            FiltreOyunModuIdx == 0 && FiltreAralikIdx == 0 &&
            FiltreHaritaIdx == 0 && FiltreSiralamaIdx == 0;

        // ─── Grafik eventleri ────────────────────────────────────────────────────
        public event Action GrafikCizilecek;
        public event Action EloGrafikCizilecek;
        public event Action EloGrafikTemizlenecek;
        public event Action AktiviteGrafikCizilecek;
        public event Action HaritaGrafikCizilecek;

        // ─── Commands ────────────────────────────────────────────────────────────
        public IRelayCommand YenileCommand { get; }
        public IRelayCommand TabCommand { get; }
        public IRelayCommand DahaFazlaYukleCommand { get; }
        public IRelayCommand FiltreDegistiCommand { get; }
        public IRelayCommand FiltreleriSifirlaCommand { get; }
        public IRelayCommand<object> PerformansSecCommand { get; }
        public IRelayCommand DetayPanelKapatCommand { get; }
        public IRelayCommand<object> MacDetayGosterCommand { get; }
        public IRelayCommand MacDetayKapatCommand { get; }

        public AnalizViewModel(UserService userService)
        {
            _userService   = userService;
            _analizService = new AnalizService();
            YenileCommand  = new AsyncRelayCommand(YukleAsync);
            TabCommand     = new RelayCommand<object>(SekmeDegistir);
            DahaFazlaYukleCommand = new AsyncRelayCommand(DahaFazlaYukleAsync);
            FiltreDegistiCommand = new RelayCommand(FiltreDegisti);
            FiltreleriSifirlaCommand = new RelayCommand(FiltreleriSifirla);
            PerformansSecCommand = new RelayCommand<object>(PerformansSec);
            DetayPanelKapatCommand = new RelayCommand(DetayPanelKapat);
            MacDetayGosterCommand = new AsyncRelayCommand<object>(MacDetayGosterAsync);
            MacDetayKapatCommand = new RelayCommand(MacDetayKapat);
        }

        private void PerformansSec(object parameter)
        {
            int index = 0;
            if (parameter is int i)
                index = i;
            else if (parameter is string s)
                int.TryParse(s, out index);

            if (index < 0 || index >= 6) return;

            if (DetayPanelTipi == (DetayPanelTipi)index && DetayPanelVisible)
            {
                DetayPanelVisible = false;
                DetayPanelTipi = DetayPanelTipi.None;
                for (int j = 0; j < 6; j++)
                    PerformansMetrikleri[j].Selected = false;
                return;
            }

            for (int j = 0; j < 6; j++)
                PerformansMetrikleri[j].Selected = j == index;

            DetayPanelTipi = (DetayPanelTipi)index;
            DetayPanelVisible = false;

            var filtrelenmis = _allMaclar?.ToList() ?? new List<AnalizMac>();

            switch (DetayPanelTipi)
            {
                case DetayPanelTipi.KazanmaOrani:
                    KazanmaOraniDetay = _analizService.GetKazanmaOraniDetay(filtrelenmis);
                    break;
                case DetayPanelTipi.ADR:
                    AdrDetay = _analizService.GetAdrDetay(filtrelenmis);
                    break;
                case DetayPanelTipi.KD:
                    KrDetay = _analizService.GetKrDetay(filtrelenmis);
                    break;
                case DetayPanelTipi.Headshot:
                    // basit headshot bilgisi
                    var hsDetay = new HeadshotDetay();
                    if (filtrelenmis.Any())
                    {
                        hsDetay.ToplamHeadshot = filtrelenmis.Sum(m => m.HeadshotCount);
                        hsDetay.ToplamBodyshot = filtrelenmis.Sum(m => m.BodyshotCount);
                        hsDetay.ToplamLegshot = filtrelenmis.Sum(m => m.LegshotCount);
                        int total = hsDetay.ToplamHeadshot + hsDetay.ToplamBodyshot + hsDetay.ToplamLegshot;
                        hsDetay.HeadshotYuzdesi = total > 0 ? hsDetay.ToplamHeadshot * 100.0 / total : 0;
                    }
                    HeadshotDetay = hsDetay;
                    break;
                case DetayPanelTipi.Entry:
                    GirisBasarisiDetay = _analizService.GetGirisBasarisiDetay(filtrelenmis);
                    break;
                case DetayPanelTipi.ACS:
                    // basit ACS detayi
                    var acsDetay = new MultiKillDetay();
                    if (filtrelenmis.Any())
                    {
                        int r = filtrelenmis.Sum(m => m.RoundOynanan);
                        int k = filtrelenmis.Sum(m => m.Kills);
                        int a = filtrelenmis.Sum(m => m.Assists);
                        double d = filtrelenmis.Sum(m => m.Hasar);
                        acsDetay.Ace = r > 0 ? (int)Math.Round((k * 150.0 + a * 50.0 + d) / r) : 0;
                    }
                    MultiKillDetay = acsDetay;
                    break;
            }

            DetayPanelVisible = true;
        }

        private void DetayPanelKapat()
        {
            DetayPanelVisible = false;
            DetayPanelTipi = DetayPanelTipi.None;
            foreach (var m in PerformansMetrikleri)
                m.Selected = false;
        }

        // ─── Maç Detay ───────────────────────────────────────────────────────────

        private async Task MacDetayGosterAsync(object parameter)
        {
            if (parameter is not string matchId) return;

            MacDetayYukleniyor = true;
            MacDetayVisible = true;
            MacDetayHata = "";
            SeciliMacDetay = null;

            try
            {
                var auth = _userService.GetProfile();
                var benAdi = auth?.OyuncuAdi ?? "";
                var benTag = auth?.Tag ?? "";

                var detay = await _analizService.GetMacDetayAsync(matchId, benAdi, benTag);
                SeciliMacDetay = detay;
            }
            catch (Exception ex)
            {
                MacDetayHata = $"Maç detayı yüklenemedi: {ex.Message}";
            }
            finally
            {
                MacDetayYukleniyor = false;
            }
        }

        private void MacDetayKapat()
        {
            MacDetayVisible = false;
            SeciliMacDetay = null;
        }

        partial void OnFiltreOyunModuIdxChanged(int value)
        {
            if (!_isResettingFilters)
            {
                OnPropertyChanged(nameof(FiltrelerVarsayilan));
                _ = OyunModuFilterDegistiAsync();
            }
        }
        partial void OnFiltreAralikIdxChanged(int value) { if (!_isResettingFilters) FiltreDegisti(); }
        partial void OnFiltreHaritaIdxChanged(int value) { if (!_isResettingFilters) FiltreDegisti(); }
        partial void OnFiltreSiralamaIdxChanged(int value) { if (!_isResettingFilters) FiltreDegisti(); }

        private void FiltreDegisti()
        {
            OnPropertyChanged(nameof(FiltrelerVarsayilan));
            _ = FiltreliYukleAsync();
        }

        private void FiltreleriSifirla()
        {
            var oncekiModIdx = FiltreOyunModuIdx;
            _isResettingFilters = true;
            FiltreOyunModuIdx = 0;
            FiltreAralikIdx = 0;
            FiltreHaritaIdx = 0;
            FiltreSiralamaIdx = 0;
            _isResettingFilters = false;
            OnPropertyChanged(nameof(FiltrelerVarsayilan));
            if (oncekiModIdx != 0)
                _ = OyunModuFilterDegistiAsync();
            else
                _ = FiltreliYukleAsync();
        }

        private async Task OyunModuFilterDegistiAsync()
        {
            if (!HasData || _allMaclar.Count == 0) return;

            var profil = _userService?.GetProfile();
            if (profil == null || !profil.GecerliMi) return;

            IsLoading = true;
            try
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;

                var bolge = string.IsNullOrEmpty(profil.Bolge) ? "eu" : profil.Bolge;
                var filter = AktifOyunModuFilter;

                var yeniMaclar = await _analizService.GetTumMacGecmisiAsync(
                    bolge, profil.OyuncuAdi, profil.Tag, ct);

                _allMaclar = yeniMaclar;
                HasMoreMatches = false;

                // Rebuild MMR dict if needed
                if (_mmrDict.Count == 0)
                {
                    var (_, eloGrafikRebuild, _) = await _analizService.GetMmrGecmisiAsync(
                        bolge, profil.OyuncuAdi, profil.Tag, ct);
                    var eloLookupRebuild = eloGrafikRebuild
                        .Where(e => !string.IsNullOrEmpty(e.MatchId) && e.MacZaman > 0)
                        .OrderBy(e => e.MacZaman)
                        .ToList();
                    _mmrDict = new Dictionary<string, (int Elo, int Tier)>();
                    _eloDiffDict = new Dictionary<string, int>();
                    for (int i = 0; i < eloLookupRebuild.Count; i++)
                    {
                        var e = eloLookupRebuild[i];
                        _mmrDict[e.MatchId] = (e.Elo, e.Tier);
                        _eloDiffDict[e.MatchId] = i > 0 ? e.Elo - eloLookupRebuild[i - 1].Elo : 0;
                    }
                }

                // Rebuild items
                _allMacGecmisiItems = _allMaclar
                    .OrderByDescending(m => m.MacZaman)
                    .Select(m => MapToMacGecmisiItem(m))
                    .ToList();

                var filtrelenmis = FiltreleVeSirala(_allMaclar);

                var app = System.Windows.Application.Current;
                if (app?.Dispatcher != null)
                {
                    await app.Dispatcher.InvokeAsync(() =>
                    {
                        MacGecmisiListesi.Clear();
                        var ilkBatch = _allMacGecmisiItems.Take(BatchSize).ToList();
                        foreach (var item in ilkBatch)
                            MacGecmisiListesi.Add(item);
                        _visibleItemCount = ilkBatch.Count;
                        HasMoreMatches = _visibleItemCount < _allMacGecmisiItems.Count;
                    });
                    await AnalizVerileriniGuncelle(filtrelenmis, ct);
                }

                await Task.Delay(50);
                GrafikCizilecek?.Invoke();
                AktiviteGrafikCizilecek?.Invoke();
                HaritaGrafikCizilecek?.Invoke();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<AnalizMac> FiltreleVeSirala(List<AnalizMac> maclar)
        {
            if (maclar == null || !maclar.Any()) return maclar;

            var sonuc = maclar.AsEnumerable();

            var modDeger = FiltreOyunModuIdx >= 0 && FiltreOyunModuIdx < FiltreOyunModuListe.Count
                ? FiltreOyunModuListe[FiltreOyunModuIdx].Deger : "";
            if (!string.IsNullOrEmpty(modDeger))
                sonuc = sonuc.Where(m => string.Equals(m.Mod, modDeger, StringComparison.OrdinalIgnoreCase));

            var haritaDeger = FiltreHaritaIdx >= 0 && FiltreHaritaIdx < FiltreHaritaListe.Count
                ? FiltreHaritaListe[FiltreHaritaIdx].Deger : "";
            if (!string.IsNullOrEmpty(haritaDeger))
                sonuc = sonuc.Where(m => string.Equals(m.MapAdi, haritaDeger, StringComparison.OrdinalIgnoreCase));

            var aralikDeger = FiltreAralikIdx >= 0 && FiltreAralikIdx < FiltreAralikListe.Count
                ? FiltreAralikListe[FiltreAralikIdx].Deger : "all";
            if (aralikDeger != "all")
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long limit = aralikDeger switch
                {
                    "week" => (long)TimeSpan.FromDays(7).TotalSeconds,
                    "month" => (long)TimeSpan.FromDays(30).TotalSeconds,
                    "30d" => (long)TimeSpan.FromDays(30).TotalSeconds,
                    "90d" => (long)TimeSpan.FromDays(90).TotalSeconds,
                    "season" => (long)TimeSpan.FromDays(180).TotalSeconds,
                    _ => long.MaxValue
                };
                sonuc = sonuc.Where(m => m.MacZaman >= now - limit);
            }

            var sirala = FiltreSiralamaIdx >= 0 && FiltreSiralamaIdx < FiltreSiralamaListe.Count
                ? FiltreSiralamaListe[FiltreSiralamaIdx].Deger : "newest";
            sonuc = sirala switch
            {
                "oldest" => sonuc.OrderBy(m => m.MacZaman),
                "kda" => sonuc.OrderByDescending(m => m.Kda),
                "damage" => sonuc.OrderByDescending(m => m.Hasar),
                _ => sonuc.OrderByDescending(m => m.MacZaman)
            };

            return sonuc.ToList();
        }

        private async Task FiltreliYukleAsync()
        {
            if (!HasData || _allMaclar.Count == 0) return;
            IsLoading = true;
            try
            {
                // Rebuild items with current filter
                _allMacGecmisiItems = _allMaclar
                    .OrderByDescending(m => m.MacZaman)
                    .Select(m => MapToMacGecmisiItem(m))
                    .ToList();

                var filtrelenmis = FiltreleVeSirala(_allMaclar);
                var app = System.Windows.Application.Current;
                if (app?.Dispatcher != null)
                {
                    await app.Dispatcher.InvokeAsync(() =>
                    {
                        MacGecmisiListesi.Clear();
                        var ilkBatch = _allMacGecmisiItems.Take(BatchSize).ToList();
                        foreach (var item in ilkBatch)
                            MacGecmisiListesi.Add(item);
                        _visibleItemCount = ilkBatch.Count;
                        HasMoreMatches = _visibleItemCount < _allMacGecmisiItems.Count;
                    });
                }
                await AnalizVerileriniGuncelle(filtrelenmis, CancellationToken.None);
                await Task.Delay(50);
                GrafikCizilecek?.Invoke();
                AktiviteGrafikCizilecek?.Invoke();
                HaritaGrafikCizilecek?.Invoke();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void CanliMacIzlemeyiBaslat()
        {
            if (_canliMacStarted) return;
            _canliMacStarted = true;
            _canliMacCts = new CancellationTokenSource();
            _canliMacTickCount = 0;
            _ = CanliMacPollingLoopAsync(_canliMacCts.Token);
        }

        public void CanliMacIzlemeyiDurdur()
        {
            _canliMacStarted = false;
            _canliMacCts?.Cancel();
            _canliMacCts?.Dispose();
            _canliMacCts = null;
        }

        private async Task CanliMacPollingLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    _canliMacTickCount++;
                    try
                    {
                        await YukleCanliMacAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        CanliLog($"[CANLIMAC] Poll hata: {ex.Message}");
                    }
                    await Task.Delay(5000, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }

        public async Task YukleCanliMacAsync()
        {
            if (_isLoadingCanliMac)
            {
                CanliLog("[CANLIMAC] Zaten yukleniyor, atlandi");
                return;
            }
            _isLoadingCanliMac = true;
            CanliLog($"[CANLIMAC] Basliyor (tick={_canliMacTickCount})...");
            try
            {
                var profil = _userService?.GetProfile();
                if (profil == null || !profil.GecerliMi)
                {
                    _isLoadingCanliMac = false;
                    return;
                }

                var bolge = string.IsNullOrEmpty(profil.Bolge) ? "eu" : profil.Bolge;

                // Hizli kontrol: su anda mac icinde miyim?
                var inMatch = await _analizService.CheckInMatchAsync(bolge);

                var app = System.Windows.Application.Current;
                if (app?.Dispatcher == null) return;

                if (!inMatch)
                {
                    await app.Dispatcher.InvokeAsync(() =>
                    {
                        if (CanliMacVisible)
                        {
                            CanliMacVisible = false;
                            CanliMacData = null;
                            CanliMacOyuncularim.Clear();
                            CanliMacRakipler.Clear();
                        }
                        CanliMacWaiting = _analizService.IsLockfileAvailable();
                        CanliMacError = !_analizService.IsLockfileAvailable();
                    });
                    return;
                }

                // Mac icinde: tam yenileme zamani mi?
                bool needsFullRefresh = !CanliMacVisible && !_isFullRefreshing;
                if (!needsFullRefresh && CanliMacVisible)
                    needsFullRefresh = _canliMacTickCount > 1 && _canliMacTickCount % CanliMacFullRefreshTicks == 0;

                if (!needsFullRefresh)
                {
                    // Veri zaten var, sadece süreyi güncelle
                    await app.Dispatcher.InvokeAsync(() =>
                    {
                        if (CanliMacVisible)
                            CanliMacTimeText = "Sürüyor";
                    });
                    return;
                }

                // Tam yenileme baslamadiysa baslat (fire-and-forget, timer'i bloklama)
                if (!_isFullRefreshing)
                {
                    _isFullRefreshing = true;
                    if (!CanliMacVisible)
                    {
                        await app.Dispatcher.InvokeAsync(() =>
                        {
                            CanliMacWaiting = true;
                            CanliMacError = false;
                        });
                    }
                    _ = FullRefreshAsync(bolge, profil.OyuncuAdi, profil.Tag);
                }
            }
            catch
            {
                var app = System.Windows.Application.Current;
                if (app?.Dispatcher != null)
                {
                    await app.Dispatcher.InvokeAsync(() =>
                    {
                        if (CanliMacVisible) return;
                        CanliMacVisible = false;
                        CanliMacData = null;
                        CanliMacOyuncularim.Clear();
                        CanliMacRakipler.Clear();
                        CanliMacWaiting = false;
                        CanliMacError = true;
                    });
                }
            }
            finally
            {
                _isLoadingCanliMac = false;
            }
        }

        private async Task FullRefreshAsync(string bolge, string name, string tag)
        {
            CanliLog("[CANLIMAC] FullRefresh basladi...");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var data = await _analizService.GetLiveMatchAsync(bolge, name, tag, cts.Token);

                var app = System.Windows.Application.Current;
                if (app?.Dispatcher == null) return;

                await app.Dispatcher.InvokeAsync(() =>
                {
                    if (data == null)
                    {
                        if (CanliMacVisible) return;
                        CanliMacVisible = false;
                        CanliMacData = null;
                        CanliMacOyuncularim.Clear();
                        CanliMacRakipler.Clear();
                        CanliMacWaiting = _analizService.IsLockfileAvailable();
                        CanliMacError = !_analizService.IsLockfileAvailable();
                        return;
                    }

                    CanliMacError = false;
                    CanliMacWaiting = false;

                    CanliMacData = data;
                    CanliMacTypeText = data.Mode switch
                    {
                        "Competitive" => "Derecelendirmeli",
                        "Unrated" => "Derecesiz",
                        "Spike Rush" => "Spike Rush",
                        "Deathmatch" => "Ölüm Maçı",
                        "Swiftplay" => "Hızlı Oyun",
                        "Escalation" => "Yükseliş",
                        "Replication" => "Kopya",
                        "Snowball Fight" => "Kartopu Savaşı",
                        _ => data.Mode
                    };
                    CanliMacMapText = data.Map;
                    CanliMacTimeText = data.EstimatedTime > 0
                        ? $"~{data.EstimatedTime / 60} dk"
                        : "Sürüyor";

                    CanliMacOyuncularim.Clear();
                    CanliMacRakipler.Clear();

                    var benimTakim = data.RedTeam.Players.Any(p => p.IsCurrentUser)
                        ? data.RedTeam
                        : data.BlueTeam;
                    var rakipTakim = benimTakim == data.RedTeam
                        ? data.BlueTeam
                        : data.RedTeam;

                    foreach (var p in benimTakim.Players)
                        CanliMacOyuncularim.Add(p);
                    foreach (var p in rakipTakim.Players)
                        CanliMacRakipler.Add(p);

                    CanliMacServerText = !string.IsNullOrEmpty(data.ServerName)
                        ? data.ServerName
                        : BolgeToServerAdi(bolge);

                    var bizElo = CanliMacOyuncularim.Where(p => p.Elo > 0).Select(p => p.Elo).DefaultIfEmpty(0).Average();
                    var rakipElo = CanliMacRakipler.Where(p => p.Elo > 0).Select(p => p.Elo).DefaultIfEmpty(0).Average();
                    if (bizElo + rakipElo > 0)
                    {
                        WinChanceBiz = Math.Round(bizElo / (bizElo + rakipElo) * 100, 1);
                        WinChanceRakip = Math.Round(100.0 - WinChanceBiz, 1);
                    }
                    else
                    {
                        WinChanceBiz = 50;
                        WinChanceRakip = 50;
                    }
                    WinChanceBizRenk = new SolidColorBrush(
                        WinChanceBiz >= 50 ? Color.FromRgb(0, 210, 106) : Color.FromRgb(255, 70, 85));
                    WinChanceRakipRenk = new SolidColorBrush(
                        WinChanceRakip >= 50 ? Color.FromRgb(0, 210, 106) : Color.FromRgb(255, 70, 85));

                    CanliMacVisible = true;
                    _canliMacTickCount = 0;
                });
            }
            catch (Exception ex)
            {
                CanliLog($"[CANLIMAC] FullRefresh HATA: {ex.Message}");
            }
            finally
            {
                _isFullRefreshing = false;
                CanliLog("[CANLIMAC] FullRefresh bitti");
            }
        }

        private static string BolgeToServerAdi(string bolge)
        {
            return bolge?.ToLowerInvariant() switch
            {
                "tr" or "tr1" => "Türkiye (İstanbul)",
                "eu" => "Europe (Frankfurt)",
                "na" or "us" => "North America (Chicago)",
                "br" or "br1" => "Brazil (São Paulo)",
                "latam" or "la" or "la1" or "la2" => "Latin America (Miami)",
                "kr" or "kr1" => "Korea (Seoul)",
                "ap" or "jp" or "jp1" => "Asia Pacific (Tokyo)",
                "sg2" => "Asia Pacific (Singapore)",
                "oc1" => "Oceania (Sydney)",
                "ru" => "Russia (Moscow)",
                _ => $"{bolge?.ToUpperInvariant() ?? "EU"}"
            };
        }

        private void SekmeDegistir(object parameter)
        {
            int index = 0;
            if (parameter is int i)
                index = i;
            else if (parameter is string s)
                int.TryParse(s, out index);

            if (SelectedTab == index) return;
            SelectedTab = index;
            OnPropertyChanged(nameof(TabIstatistikler));
            OnPropertyChanged(nameof(TabMacGecmisi));
            OnPropertyChanged(nameof(TabCanliMac));
        }

        private async Task AnalizVerileriniGuncelle(List<AnalizMac> maclar, CancellationToken ct)
        {
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher == null) return;

            await app.Dispatcher.InvokeAsync(() =>
            {
                PerformansMetrikleri.Clear();
                SaatlikAktiviteler.Clear();
                HaftalikAktiviteler.Clear();
                TakvimHaftalari.Clear();
                TakvimAyBasliklari.Clear();
                HaritaAnalizleri.Clear();
                HaritaKartlari.Clear();

                // Performans: son 20 maç ana değer, önceki 30 maç karşılaştırma
                var son20 = maclar.Take(20).ToList();
                var onceki30 = maclar.Skip(20).Take(30).ToList();
                foreach (var m in _analizService.GetPerformansMetrikleri(son20, onceki30))
                    PerformansMetrikleri.Add(m);

                var (saatlik, saatOzet) = _analizService.GetSaatlikAktivite(maclar);
                foreach (var s in saatlik) SaatlikAktiviteler.Add(s);
                SaatlikOzet = saatOzet;

                var (haftalik, haftaOzet) = _analizService.GetHaftalikAktivite(maclar);
                foreach (var h in haftalik) HaftalikAktiviteler.Add(h);
                HaftalikOzet = haftaOzet;

                var (haftalar, ayBasliklari) = _analizService.GetAktiviteTakvimi(maclar);
                foreach (var h in haftalar) TakvimHaftalari.Add(h);
                foreach (var a in ayBasliklari) TakvimAyBasliklari.Add(a);

                var haritaVerileri = _analizService.GetHaritaIstatistikleri(maclar);
                foreach (var h in haritaVerileri) HaritaAnalizleri.Add(h);
                foreach (var h in haritaVerileri.Where(x => x.OynanmaSayisi > 0).Take(3))
                    HaritaKartlari.Add(h);

                // Detay verilerini önceden yükle (tooltip için)
                var allMatches = maclar.ToList();
                KazanmaOraniDetay = _analizService.GetKazanmaOraniDetay(allMatches);
                AdrDetay = _analizService.GetAdrDetay(allMatches);
                KrDetay = _analizService.GetKrDetay(allMatches);
                HeadshotDetay = _analizService.GetHeadshotDetay(allMatches);
                GirisBasarisiDetay = _analizService.GetGirisBasarisiDetay(allMatches);
                MultiKillDetay = _analizService.GetMultiKillDetay(allMatches);

                HasData = true;
            });
        }

        public async Task YukleAsync()
        {
            if (IsLoading) return;

            var profil = _userService?.GetProfile();
            if (profil == null || !profil.GecerliMi)
            {
                HasError     = true;
                ErrorMessage = "Once giris yapmaniz gerekiyor.";
                return;
            }

            OyuncuAdi = profil.RiotId;
            Rutbe     = profil.Rutbe;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            IsLoading = true;
            HasError  = false;
            HasData   = false;
            HasMoreMatches = false;
            IsLoadingMore = false;

            _allMaclar = new List<AnalizMac>();

            EloGrafikNoktalari.Clear();
            EloGrafikTemizlenecek?.Invoke();

            if (!string.IsNullOrEmpty(profil.OyuncuAdi))
            {
                try
                {
                    var bolge = string.IsNullOrEmpty(profil.Bolge) ? "eu" : profil.Bolge;
                    var (yeniRank, yeniTier, yeniRr) = await _analizService.GetRankFromApiAsync(
                        bolge, profil.OyuncuAdi, profil.Tag, ct);
                    if (yeniTier > 0)
                    {
                        profil.Rutbe = yeniRank;
                        profil.CurrentTier = yeniTier;
                        profil.RutbePuani = yeniRr;
                        _userService.SaveProfile(profil);
                        Rutbe = yeniRank;
                    }
                }
                catch { }
            }

            try
            {
                var bolge = string.IsNullOrEmpty(profil.Bolge) ? "eu" : profil.Bolge;

                if (profil.CurrentTier > 0)
                {
                    RutbeTier = profil.Rutbe;
                    RrDeger = profil.RutbePuani;
                    RrProgress = profil.RutbePuani;
                    if (profil.RutbePuani > 0)
                    {
                        int kalan = 100 - profil.RutbePuani;
                        RrKalanText = $"Bir sonraki ranka {kalan} RR kaldi";
                    }
                    else
                    {
                        RrKalanText = "";
                    }

                    RankIkonKaynak = RankIkonHelper.RankIkonFromTierBuyuk(profil.CurrentTier, 96);
                    ProfilRankIkonBuyuk = RankIkonHelper.RankIkonFromTierBuyuk(profil.CurrentTier, 80);
                    ProfilRankAdi = RankIkonHelper.TierdenRutbeAdi(profil.CurrentTier);
                    ProfilTierDetay = profil.Rutbe;

                    int elo = (profil.CurrentTier * 100) + profil.RutbePuani;
                    if (elo < 300) elo = 300;
                    MevcutElo = elo;

                    if (elo < 800)
                        EloRenk = new SolidColorBrush(Color.FromRgb(0xFF, 0x46, 0x55)); // kirmizi
                    else if (elo < 1300)
                        EloRenk = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)); // turuncu
                    else if (elo < 2000)
                        EloRenk = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)); // sari
                    else
                        EloRenk = new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0x6A)); // yesil

                    RankCizgiRenk = (profil.CurrentTier) switch
                    {
                        <= 5  => new SolidColorBrush(Color.FromRgb(0x8B, 0x8B, 0x8B)), // Demir
                        <= 8  => new SolidColorBrush(Color.FromRgb(0xCD, 0x7F, 0x32)), // Bronz
                        <= 11 => new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)), // Gumus
                        <= 14 => new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)), // Altin
                        <= 17 => new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xE5)), // Platin
                        <= 20 => new SolidColorBrush(Color.FromRgb(0x00, 0x87, 0xFF)), // Elmas
                        <= 23 => new SolidColorBrush(Color.FromRgb(0xE8, 0x40, 0x40)), // Olumsuz
                        <= 26 => new SolidColorBrush(Color.FromRgb(0x90, 0x00, 0xFF)), // Yucelik
                        _     => new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))  // Radyant
                    };
                }

                try
                {
                    var (sezonAdi, bitisTarihi) = await _analizService.GetCurrentSeasonAsync(ct);
                    SezonAdi = sezonAdi;
                    if (!string.IsNullOrEmpty(sezonAdi))
                    {
                        var parts = sezonAdi.Split(new[] { " // " }, StringSplitOptions.None);
                        if (parts.Length >= 2)
                        {
                            var ep = parts[0].Replace("Episode", "").Trim();
                            var act = parts[1].Replace("Act", "").Trim();
                            SezonKisaAdi = $"{ep}. sezon - Perde {act}";
                        }
                        else
                        {
                            SezonKisaAdi = sezonAdi;
                        }
                    }
                    else
                    {
                        SezonKisaAdi = "";
                    }

                    if (bitisTarihi.HasValue)
                    {
                        var kalanGun = (int)(bitisTarihi.Value - DateTime.UtcNow).TotalDays;
                        if (kalanGun > 0)
                            SezonBitisText = $"Sezon bitisine {kalanGun} gun kaldi ({bitisTarihi.Value:dd MMM yyyy})";
                        else if (kalanGun == 0)
                            SezonBitisText = $"Sezon bugun bitiyor! ({bitisTarihi.Value:dd MMM yyyy})";
                        else
                            SezonBitisText = $"Sezon bitti ({bitisTarihi.Value:dd MMM yyyy})";
                    }
                    else
                        SezonBitisText = "";
                }
                catch
                {
                    SezonAdi = "";
                    SezonKisaAdi = "";
                    SezonBitisText = "";
                }

                var tumMaclar = await _analizService.GetTumMacGecmisiAsync(
                    bolge, profil.OyuncuAdi, profil.Tag, ct);

                System.Diagnostics.Debug.WriteLine($"[MAC] Tum maclar: {tumMaclar.Count}");
                _allMaclar = tumMaclar;
                HasMoreMatches = false;

                // MMR verisi — API'den çek, düşerse önbellekten oku
                List<EloGrafikNokta> eloGrafik;
                try
                {
                    (_, eloGrafik, _) = await _analizService.GetMmrGecmisiAsync(
                        bolge, profil.OyuncuAdi, profil.Tag, ct);
                    _analizService.SaveMmrCache(eloGrafik, profil.OyuncuAdi, profil.Tag);
                }
                catch
                {
                    eloGrafik = _analizService.LoadMmrCache(profil.OyuncuAdi, profil.Tag);
                }

                if (eloGrafik != null && eloGrafik.Count > 0)
                {
                    var eloLookup = eloGrafik
                        .Where(e => !string.IsNullOrEmpty(e.MatchId) && e.MacZaman > 0)
                        .OrderBy(e => e.MacZaman)
                        .ToList();

                    _mmrDict = new Dictionary<string, (int Elo, int Tier)>();
                    _eloDiffDict = new Dictionary<string, int>();
                    for (int i = 0; i < eloLookup.Count; i++)
                    {
                        var e = eloLookup[i];
                        _mmrDict[e.MatchId] = (e.Elo, e.Tier);
                        _eloDiffDict[e.MatchId] = i > 0 ? e.Elo - eloLookup[i - 1].Elo : 0;
                    }

                    EloGrafikNoktalari.Clear();
                    MmrMacListesi.Clear();
                    var eloGrafik20 = eloGrafik.TakeLast(20).ToList();
                    for (int i = 0; i < eloGrafik20.Count; i++)
                    {
                        eloGrafik20[i].MacIndex = i + 1;
                        EloGrafikNoktalari.Add(eloGrafik20[i]);
                        MmrMacListesi.Add(eloGrafik20[i]);
                    }

                    if (eloGrafik20.Any())
                    {
                        EloOzet = new EloOzet
                        {
                            EnYuksekElo = eloGrafik20.Max(e => e.Elo),
                            EnDusukElo = eloGrafik20.Min(e => e.Elo),
                            ToplamEloFarki = eloGrafik20.Last().Elo - eloGrafik20.First().Elo,
                            ToplamMac = eloGrafik20.Count,
                            Galibiyet = eloGrafik20.Count(m => m.Kazandi),
                            Maglubiyet = eloGrafik20.Count(m => !m.Kazandi),
                            GalibiyetYuzdesi = eloGrafik20.Count > 0
                                ? eloGrafik20.Count(m => m.Kazandi) * 100.0 / eloGrafik20.Count : 0
                        };
                    }
                    else
                    {
                        EloOzet = new EloOzet();
                    }
                }
                else
                {
                    _mmrDict = new();
                    _eloDiffDict = new();
                    EloOzet = new();
                }

                // Build MacGecmisiItem list
                _allMacGecmisiItems = _allMaclar
                    .OrderByDescending(m => m.MacZaman)
                    .Select(m => MapToMacGecmisiItem(m))
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[MAC] MacGecmisi items: {_allMacGecmisiItems.Count}, MMR dict: {_mmrDict.Count}");

                var filtrelenmis = FiltreleVeSirala(_allMaclar);
                await AnalizVerileriniGuncelle(filtrelenmis, ct);

                // Load first batch
                MacGecmisiListesi.Clear();
                var ilkBatch = _allMacGecmisiItems.Take(BatchSize).ToList();
                foreach (var item in ilkBatch)
                    MacGecmisiListesi.Add(item);
                _visibleItemCount = ilkBatch.Count;
                HasMoreMatches = _visibleItemCount < _allMacGecmisiItems.Count;

                System.Diagnostics.Debug.WriteLine($"[MAC] MacGecmisiListesi load: {MacGecmisiListesi.Count} / {_allMacGecmisiItems.Count}");

                CanliMacIzlemeyiBaslat();

                await Task.Delay(100, ct);
                GrafikCizilecek?.Invoke();
                EloGrafikCizilecek?.Invoke();
                AktiviteGrafikCizilecek?.Invoke();
                HaritaGrafikCizilecek?.Invoke();
            }
            catch (OperationCanceledException)
            {
                HasData = _allMacGecmisiItems.Count > 0 || EloGrafikNoktalari.Count > 0;
                if (!HasData)
                {
                    HasError = true;
                    ErrorMessage = "Veriler yuklenirken zaman asimi. Lutfen tekrar deneyin.";
                }
            }
            catch (Exception ex)
            {
                HasError     = true;
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task DahaFazlaYukleAsync()
        {
            if (IsLoadingMore) return;
            if (_visibleItemCount >= _allMacGecmisiItems.Count)
            {
                HasMoreMatches = false;
                return;
            }

            IsLoadingMore = true;

            try
            {
                await Task.Delay(200);
                var app = System.Windows.Application.Current;
                if (app?.Dispatcher != null)
                {
                    await app.Dispatcher.InvokeAsync(() => DahaFazlaGoster());
                }
            }
            finally
            {
                IsLoadingMore = false;
                HasMoreMatches = _visibleItemCount < _allMacGecmisiItems.Count;
            }
        }

        private void DahaFazlaGoster()
        {
            int eklenecek = Math.Min(BatchSize, _allMacGecmisiItems.Count - _visibleItemCount);
            for (int i = 0; i < eklenecek; i++)
                MacGecmisiListesi.Add(_allMacGecmisiItems[_visibleItemCount + i]);
            _visibleItemCount += eklenecek;
            HasMoreMatches = _visibleItemCount < _allMacGecmisiItems.Count;
        }

        private MacGecmisiItem MapToMacGecmisiItem(AnalizMac mac)
        {
            int elo = 0, tier = 0, rrDegisim = 0;
            bool mmrVar = false;
            if (!string.IsNullOrEmpty(mac.MatchId) && _mmrDict.TryGetValue(mac.MatchId, out var mmr))
            {
                elo = mmr.Elo;
                tier = mmr.Tier;
                mmrVar = true;
            }
            if (!string.IsNullOrEmpty(mac.MatchId) && _eloDiffDict.TryGetValue(mac.MatchId, out var eloDiff))
            {
                rrDegisim = eloDiff;
            }

            return new MacGecmisiItem
            {
                MatchId      = mac.MatchId,
                HaritaAdi    = mac.MapAdi,
                AjanAdi      = mac.AjanAdi,
                Kazandi      = mac.Kazandi,
                MacZaman     = mac.MacZaman,
                Kills        = mac.Kills,
                Deaths       = mac.Deaths,
                Assists      = mac.Assists,
                Hasar        = mac.Hasar,
                RoundOynanan = mac.RoundOynanan,
                Mod          = mac.Mod,
                MacSkoru     = mac.MacSkoru,
                RrDegisim    = rrDegisim,
                RrSonrasi    = 0,
                Tier         = tier,
                MmrVar       = mmrVar,
                EloDeger     = elo
            };
        }

        private static readonly object _canliLogLock = new();
        private static void CanliLog(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            System.Diagnostics.Debug.WriteLine(line);
            try
            {
                lock (_canliLogLock)
                {
                    File.AppendAllText(
                        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "riot_debug.log"),
                        line + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}

