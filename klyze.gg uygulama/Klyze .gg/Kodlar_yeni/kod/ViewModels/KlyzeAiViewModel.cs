using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class KlyzeAiViewModel : ObservableObject
    {
        private readonly GoogleAiService _aiService;
        private CancellationTokenSource _streamCts;

        [ObservableProperty]
        private ObservableCollection<AiSohbet> _sohbetler = new();

        [ObservableProperty]
        private AiSohbet _aktifSohbet;

        public ObservableCollection<AiSohbetMesaj> AktifMesajlar => AktifSohbet?.Mesajlar;

        partial void OnAktifSohbetChanged(AiSohbet value)
        {
            OnPropertyChanged(nameof(AktifMesajlar));
        }

        [ObservableProperty]
        private string _girisMetni = "";

        [ObservableProperty]
        private bool _yukleniyor;

        [ObservableProperty]
        private int _kalanToken = 500;

        [ObservableProperty]
        private int _harcananToken;

        [ObservableProperty]
        private bool _apiHazir;

        [ObservableProperty]
        private string _durumEtiketi = "";

        [ObservableProperty]
        private string _aktifSohbetBaslik = "Klyze AI";

        [ObservableProperty]
        private bool _welcomeModu = true;

        public KlyzeAiViewModel()
        {
            _aiService = new GoogleAiService();
            YeniSohbet();
        }

        public void ApiKeyGuncelle(string key)
        {
            _aiService.SetApiKey(key);
            ApiHazir = _aiService.ApiKeyReady;
        }

        public void GroqKeyleriniGuncelle(List<string> keys)
        {
            _aiService.SetApiKeys(keys);
            ApiHazir = _aiService.ApiKeyReady;
        }

        public void TavilyKeyGuncelle(string key)
        {
            _aiService.SetTavilyKey(key);
        }

        public void OpenRouterKeyGuncelle(string key)
        {
            _aiService.SetApiKey(key);
            _aiService.ModelDegistir("openai/gpt-4o");
            ApiHazir = _aiService.ApiKeyReady;
        }

        [RelayCommand]
        private void YeniSohbet()
        {
            var yeni = new AiSohbet
            {
                Baslik = "Yeni Sohbet",
                Olusturulma = DateTime.Now,
                Aktif = true
            };

            if (AktifSohbet != null)
                AktifSohbet.Aktif = false;

            AktifSohbet = yeni;
            Sohbetler.Insert(0, yeni);
            AktifSohbetBaslik = "Yeni Sohbet";
            WelcomeModu = true;

            if (Sohbetler.Count > 20)
                Sohbetler.RemoveAt(Sohbetler.Count - 1);
        }

        [RelayCommand]
        private void SohbetSec(AiSohbet sohbet)
        {
            if (sohbet == null) return;
            if (AktifSohbet != null)
                AktifSohbet.Aktif = false;
            sohbet.Aktif = true;
            AktifSohbet = sohbet;
            AktifSohbetBaslik = sohbet.Baslik;
        }

        [RelayCommand]
        private void SohbetSil(AiSohbet sohbet)
        {
            if (sohbet == null) return;
            var idx = Sohbetler.IndexOf(sohbet);
            Sohbetler.Remove(sohbet);

            if (sohbet == AktifSohbet)
            {
                if (Sohbetler.Count > 0)
                {
                    var yeniAktif = idx > 0 ? Sohbetler[idx - 1] : Sohbetler[0];
                    SohbetSec(yeniAktif);
                }
                else
                {
                    YeniSohbet();
                }
            }
        }

        [RelayCommand]
        private async Task HizliEylemAsync(string eylem)
        {
            var prompt = eylem switch
            {
                "write" => "Valorant oyun stratejisi hakkında detaylı bir analiz yaz. Harita kontrolü, eko yönetimi ve takım kompozisyonu üzerine öneriler ver.",
                "learn" => "Bana Valorant'da nasıl daha iyi nişan alabileceğimi öğret. Aim çalışmaları, crosshair placement ve oyun içi mekanik ipuçları ver.",
                "code" => "Valorant'da ajan seçimi ve takım sinerjisi hakkında stratejik bir rehber hazırla. Her ajanın rolüne göre takım kompozisyonu öner.",
                "life" => "Rekabetçi oyunlarda mental dayanıklılık, tilt yönetimi ve oyun-yaşam dengesi hakkında tavsiyeler ver.",
                "claude" => "Merhaba! Valorant hakkında genel sohbet edelim. Bana güncel meta, ajan seçimleri ve stratejiler hakkında tavsiyeler ver.",
                _ => eylem
            };
            GirisMetni = prompt;
            await GonderAsync();
        }

        [RelayCommand]
        private async Task GonderAsync()
        {
            WelcomeModu = false;
            var metin = GirisMetni?.Trim();
            if (string.IsNullOrEmpty(metin) || Yukleniyor) return;

            if (KalanToken <= 0)
            {
                AktifSohbet.Mesajlar.Add(new AiSohbetMesaj
                {
                    Icerik = "⚠️ Token hakkınız kalmadı. Gelecek ay yenilenecek.",
                    KullaniciMesaji = false,
                    Zaman = DateTime.Now
                });
                return;
            }

            if (!ApiHazir)
            {
                AktifSohbet.Mesajlar.Add(new AiSohbetMesaj
                {
                    Icerik = "⚠️ API anahtarı ayarlanmamış. Lütfen ayarlardan API key'inizi girin.",
                    KullaniciMesaji = false,
                    Zaman = DateTime.Now
                });
                return;
            }

            AktifSohbetBaslik = metin.Length > 28 ? metin[..28] + "..." : metin;
            if (AktifSohbet.Baslik == "Yeni Sohbet")
                AktifSohbet.Baslik = AktifSohbetBaslik;

            var kullaniciMesaj = new AiSohbetMesaj
            {
                Icerik = metin,
                KullaniciMesaji = true,
                Zaman = DateTime.Now
            };
            AktifSohbet.Mesajlar.Add(kullaniciMesaj);
            GirisMetni = "";

            await YanitlaAsync(metin);
        }

        private async Task YanitlaAsync(string metin)
        {
            var yukleniyorMesaj = new AiSohbetMesaj
            {
                Icerik = "",
                KullaniciMesaji = false,
                Yukleniyor = true,
                StreamAktif = false,
                Zaman = DateTime.Now
            };
            AktifSohbet.Mesajlar.Add(yukleniyorMesaj);
            Yukleniyor = true;

            var dotsCts = new CancellationTokenSource();
            var dotsToken = dotsCts.Token;
            _ = DotsAnimasyonuAsync(yukleniyorMesaj, dotsToken);

            try
            {
                var gecmis = AktifSohbet.Mesajlar
                    .Where(m => !m.Yukleniyor)
                    .ToList();

                _streamCts?.Cancel();
                _streamCts = new CancellationTokenSource();
                var token = _streamCts.Token;

                var (yanit, tokenKullanimi) = await Task.Run(
                    () => _aiService.SendMessageAsync(metin, gecmis), token);

                token.ThrowIfCancellationRequested();

                dotsCts.Cancel();
                yukleniyorMesaj.Yukleniyor = false;
                yukleniyorMesaj.StreamAktif = true;

                var sozcukler = yanit.Split(' ');
                for (int i = 0; i < sozcukler.Length; i++)
                {
                    if (token.IsCancellationRequested) break;

                    yukleniyorMesaj.StreamingMetin = string.Join(" ", sozcukler.Take(i + 1));
                    if (i < sozcukler.Length - 1)
                        await Task.Delay(30, token);
                }

                yukleniyorMesaj.Icerik = yanit;
                yukleniyorMesaj.StreamingMetin = "";
                yukleniyorMesaj.StreamAktif = false;

                KalanToken = Math.Max(0, KalanToken - 1);
                HarcananToken++;
            }
            catch (OperationCanceledException)
            {
                dotsCts.Cancel();
                AktifSohbet.Mesajlar.Remove(yukleniyorMesaj);
            }
            catch (Exception ex)
            {
                dotsCts.Cancel();
                yukleniyorMesaj.Yukleniyor = false;
                yukleniyorMesaj.StreamAktif = false;
                yukleniyorMesaj.Icerik = $"Bir hata oluştu: {ex.Message}";
                yukleniyorMesaj.StreamingMetin = "";
            }
            finally
            {
                Yukleniyor = false;
            }
        }

        private async Task DotsAnimasyonuAsync(AiSohbetMesaj mesaj, CancellationToken token)
        {
            try
            {
                while (mesaj.Yukleniyor)
                {
                    mesaj.DurumEtiketi = ".";
                    await Task.Delay(400, token);
                    if (!mesaj.Yukleniyor) break;
                    mesaj.DurumEtiketi = "..";
                    await Task.Delay(400, token);
                    if (!mesaj.Yukleniyor) break;
                    mesaj.DurumEtiketi = "...";
                    await Task.Delay(400, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        [RelayCommand]
        private void SohbetiTemizle()
        {
            if (AktifSohbet == null) return;
            AktifSohbet.Mesajlar.Clear();
        }
    }
}
