using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class AnalizViewModel : ObservableObject
    {
        private readonly AnalizService  _analizService;
        private readonly UserService    _userService;
        private CancellationTokenSource _cts;

        // ─── State ───────────────────────────────────────────────────────────────
        [ObservableProperty] private bool   _isLoading    = false;
        [ObservableProperty] private bool   _hasData      = false;
        [ObservableProperty] private bool   _hasError     = false;
        [ObservableProperty] private string _errorMessage = "";

        // ─── Kullanici ───────────────────────────────────────────────────────────
        [ObservableProperty] private string _oyuncuAdi = "";
        [ObservableProperty] private string _rutbe     = "";

        // ─── Performans Metrikleri ───────────────────────────────────────────────
        public ObservableCollection<PerformansMetrik> PerformansMetrikleri { get; } = new();

        // ─── ELO Ilerleme ────────────────────────────────────────────────────────
        public ObservableCollection<EloGrafikNokta> EloGrafikNoktalari { get; } = new();
        public ObservableCollection<AnalizMac>       MmrMacListesi      { get; } = new();
        [ObservableProperty] private EloOzet _eloOzet = new();

        // ─── Aktivite ────────────────────────────────────────────────────────────
        public ObservableCollection<SaatlikAktivite>  SaatlikAktiviteler  { get; } = new();
        public ObservableCollection<HaftalikAktivite> HaftalikAktiviteler { get; } = new();
        public ObservableCollection<TakvimHaftasi>    TakvimHaftalari     { get; } = new();
        public ObservableCollection<TakvimAyBaslik>   TakvimAyBasliklari  { get; } = new();

        [ObservableProperty] private AktiviteGrafikOzet _saatlikOzet  = new();
        [ObservableProperty] private AktiviteGrafikOzet _haftalikOzet = new();

        // ─── Grafik eventleri ────────────────────────────────────────────────────
        public event Action GrafikCizilecek;
        public event Action EloGrafikCizilecek;
        public event Action AktiviteGrafikCizilecek;

        // ─── Commands ────────────────────────────────────────────────────────────
        public IRelayCommand YenileCommand { get; }

        public AnalizViewModel(UserService userService)
        {
            _userService   = userService;
            _analizService = new AnalizService();
            YenileCommand  = new AsyncRelayCommand(YukleAsync);
        }

        public async Task YukleAsync()
        {
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

            IsLoading = true;
            HasError  = false;
            HasData   = false;

            PerformansMetrikleri.Clear();
            EloGrafikNoktalari.Clear();
            MmrMacListesi.Clear();
            SaatlikAktiviteler.Clear();
            HaftalikAktiviteler.Clear();
            TakvimHaftalari.Clear();
            TakvimAyBasliklari.Clear();

            try
            {
                var bolge = string.IsNullOrEmpty(profil.Bolge) ? "eu" : profil.Bolge;

                var (maclar, eloGrafik, eloOzet, ozet) = await _analizService.GetTamAnalizAsync(
                    bolge, profil.OyuncuAdi, profil.Tag, _cts.Token);

                // Performans
                foreach (var m in _analizService.GetPerformansMetrikleri(maclar))
                    PerformansMetrikleri.Add(m);

                // ELO
                foreach (var n in eloGrafik) EloGrafikNoktalari.Add(n);
                EloOzet = eloOzet;
                foreach (var m in maclar) MmrMacListesi.Add(m);

                // Saatlik aktivite
                var (saatlik, saatOzet) = _analizService.GetSaatlikAktivite(maclar);
                foreach (var s in saatlik) SaatlikAktiviteler.Add(s);
                SaatlikOzet = saatOzet;

                // Haftalik aktivite
                var (haftalik, haftaOzet) = _analizService.GetHaftalikAktivite(maclar);
                foreach (var h in haftalik) HaftalikAktiviteler.Add(h);
                HaftalikOzet = haftaOzet;

                // Takvim
                var (haftalar, ayBasliklari) = _analizService.GetAktiviteTakvimi(maclar);
                foreach (var h in haftalar)     TakvimHaftalari.Add(h);
                foreach (var a in ayBasliklari) TakvimAyBasliklari.Add(a);

                HasData = true;

                await Task.Delay(100, _cts.Token);
                GrafikCizilecek?.Invoke();
                EloGrafikCizilecek?.Invoke();
                AktiviteGrafikCizilecek?.Invoke();
            }
            catch (OperationCanceledException) { }
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
    }
}
