using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ValorantAutoClicker.Helpers;
using ValorantAutoClicker.ViewModels;

namespace ValorantAutoClicker.Views
{
    public partial class PlayPage : UserControl
    {
        private PlayViewModel VM => DataContext as PlayViewModel;

        public PlayPage()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PlayViewModel oldVm)
                oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is PlayViewModel newVm)
                newVm.PropertyChanged += OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlayViewModel.State))
                Dispatcher.Invoke(DurumGuncelle);
            if (e.PropertyName == nameof(PlayViewModel.ModalAcik))
                Dispatcher.Invoke(ModalAnimasyon);
            if (e.PropertyName == nameof(PlayViewModel.OyuncuAdi))
                Dispatcher.Invoke(AvatarHarfGuncelle);
            if (e.PropertyName == nameof(PlayViewModel.RakipAdi))
                Dispatcher.Invoke(OdaAvatarHarfleriGuncelle);
            if (e.PropertyName == nameof(PlayViewModel.LobiOyuncular))
                Dispatcher.Invoke(LobbySlotGuncelle);
            if (e.PropertyName == nameof(PlayViewModel.AktifGrupKodu))
                Dispatcher.Invoke(LobbyAltBolumGuncelle);
            if (e.PropertyName == nameof(PlayViewModel.LobiSahibi))
                Dispatcher.Invoke(LobbyAltBolumGuncelle);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            AvatarHarfGuncelle();
            // Animasyonu atla — sayfa collapsed yüklenir, user girise animasyonu göremez
            OyuncuKutu.Opacity = 1;
            KartScale.ScaleX = 1;
            KartScale.ScaleY = 1;
            MacTuruBox.Opacity = 1;
        }

        private void GirisAnimasyonu()
        {
            OyuncuKutu.Opacity = 0;
            KartScale.ScaleX = 0.8;
            KartScale.ScaleY = 0.8;

            var kartFade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(350)))
            { EasingFunction = CubicBezierEase.Spring };
            var kartScaleX = new DoubleAnimation(0.8, 1, new Duration(TimeSpan.FromMilliseconds(400)))
            { EasingFunction = CubicBezierEase.Spring };
            var kartScaleY = new DoubleAnimation(0.8, 1, new Duration(TimeSpan.FromMilliseconds(400)))
            { EasingFunction = CubicBezierEase.Spring };

            OyuncuKutu.BeginAnimation(OpacityProperty, kartFade);
            KartScale.BeginAnimation(ScaleTransform.ScaleXProperty, kartScaleX);
            KartScale.BeginAnimation(ScaleTransform.ScaleYProperty, kartScaleY);

            // Maç türü kutusu animasyonu
            MacTuruBox.Opacity = 0;
            var turFade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(350)))
            { BeginTime = TimeSpan.FromMilliseconds(200), EasingFunction = CubicBezierEase.Spring };
            MacTuruBox.BeginAnimation(OpacityProperty, turFade);
        }

        private void MacTuruSec(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border clicked)
            {
                var defaultBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A3A1A"));
                var selectedBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                var dimBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222222"));

                MacTuruStandart.BorderBrush = dimBorder;
                MacTuruSuper.BorderBrush = defaultBorder;
                MacTuruPremium.BorderBrush = defaultBorder;

                clicked.BorderBrush = selectedBorder;
            }
        }

        private void DurumGuncelle()
        {
            if (VM == null) return;
            var state = VM.State;

            GizlePaneli(AramaPaneli);
            GizlePaneli(LobiKarti);
            GizlePaneli(LobiYokPaneli);
            GizlePaneli(LobiOlusturulduPaneli);
            GizlePaneli(EslesmePaneli);
            GizlePaneli(OdaPaneli);
            GizlePaneli(LobbyPaneli);

            switch (state)
            {
                case "searching":
                    GosterPaneli(AramaPaneli);
                    break;

                case "matching":
                    GosterPaneli(EslesmePaneli);
                    break;

                case "matched":
                    OdaAvatarHarfleriGuncelle();
                    OdaPaneli.Visibility = Visibility.Visible;
                    OdaScale.ScaleX = 0.92;
                    OdaScale.ScaleY = 0.92;
                    OdaPaneli.Opacity = 0;

                    OdaPaneli.BeginAnimation(OpacityProperty,
                        new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(350)))
                        { EasingFunction = CubicBezierEase.Spring });
                    OdaScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                        new DoubleAnimation(0.92, 1, new Duration(TimeSpan.FromMilliseconds(350)))
                        { EasingFunction = CubicBezierEase.Spring });
                    OdaScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                        new DoubleAnimation(0.92, 1, new Duration(TimeSpan.FromMilliseconds(350)))
                        { EasingFunction = CubicBezierEase.Spring });
                    break;

                case "found":
                    LobiKarti.Visibility = Visibility.Visible;
                    LobiKartiScale.ScaleX = 0.92;
                    LobiKartiScale.ScaleY = 0.92;
                    LobiKarti.Opacity = 0;

                    LobiKarti.BeginAnimation(OpacityProperty,
                        new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(350)))
                        { EasingFunction = CubicBezierEase.Spring });
                    LobiKartiScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                        new DoubleAnimation(0.92, 1, new Duration(TimeSpan.FromMilliseconds(350)))
                        { EasingFunction = CubicBezierEase.Spring });
                    LobiKartiScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                        new DoubleAnimation(0.92, 1, new Duration(TimeSpan.FromMilliseconds(350)))
                        { EasingFunction = CubicBezierEase.Spring });
                    break;

                case "notfound":
                    GosterPaneli(LobiYokPaneli);
                    break;

                case "lobby_created":
                case "lobby_full":
                    LobbySlotGuncelle();
                    LobbyAltBolumGuncelle();
                    GosterPaneli(LobbyPaneli);
                    break;
            }
        }

        private static void GosterPaneli(Border panel)
        {
            panel.Visibility = Visibility.Visible;
            panel.Opacity = 0;
            panel.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(280)))
                { EasingFunction = CubicBezierEase.Spring });
        }

        private static void GizlePaneli(Border panel)
        {
            panel.BeginAnimation(OpacityProperty, null);
            panel.Opacity = 0;
            panel.Visibility = Visibility.Collapsed;
        }

        private void ModalAnimasyon()
        {
            if (VM == null) return;

            if (VM.ModalAcik)
            {
                ModalOverlay.Visibility = Visibility.Visible;
                ModalKutu.Opacity = 0;
                ModalScale.ScaleX = 0.95;
                ModalScale.ScaleY = 0.95;

                ModalKutu.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(250)))
                    { EasingFunction = CubicBezierEase.Spring });
                ModalScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(0.95, 1, new Duration(TimeSpan.FromMilliseconds(250)))
                    { EasingFunction = CubicBezierEase.Spring });
                ModalScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(0.95, 1, new Duration(TimeSpan.FromMilliseconds(250)))
                    { EasingFunction = CubicBezierEase.Spring });

                Dispatcher.BeginInvoke(new Action(() => GrupKoduBox?.Focus()),
                    System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(180)))
                { EasingFunction = CubicBezierEase.Spring };
                fadeOut.Completed += (_, _) => ModalOverlay.Visibility = Visibility.Collapsed;
                ModalKutu.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        private void AvatarHarfGuncelle()
        {
            if (VM == null || AvatarHarf == null) return;
            var ad = VM.OyuncuAdi;
            AvatarHarf.Text = string.IsNullOrEmpty(ad) ? "?" : ad[0].ToString().ToUpper();
        }

        private void GrupKoduBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                VM?.LobiOlusturOnayCommand?.Execute(null);
            if (e.Key == Key.Escape)
                VM?.ModalKapatCommand?.Execute(null);
        }

        private void OdaGrupKoduBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                VM?.OdaGrupKoduKaydetCommand?.Execute(null);
        }

        private void LobbyGrupKoduBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                VM?.LobbyGrupKoduKaydetCommand?.Execute(null);
        }

        private void GrupKoduKopyala_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
            {
                try { Clipboard.SetText(tb.Text); } catch { }
            }
        }

        private void ModToggle_Click(object sender, MouseButtonEventArgs e)
        {
            if (ModDropdown.Visibility == Visibility.Collapsed)
            {
                ModDropdown.Visibility = Visibility.Visible;
                ModDropdown.Opacity = 0;
                ModDropdown.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)))
                    { EasingFunction = CubicBezierEase.Spring });
                ToggleIkon.Text = "▼";
            }
            else
            {
                var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(150)));
                fade.Completed += (_, _) => ModDropdown.Visibility = Visibility.Collapsed;
                ModDropdown.BeginAnimation(OpacityProperty, fade);
                ToggleIkon.Text = "▶";
            }
        }

        private void ModSec(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b)
            {
                var modAdi = "";
                var kisiSayisi = 1;

                if (b == Mod1v1) { modAdi = "1v1"; kisiSayisi = 2; }
                else if (b == Mod2v2) { modAdi = "2v2"; kisiSayisi = 4; }
                else if (b == Mod3v3) { modAdi = "3v3"; kisiSayisi = 6; }
                else if (b == Mod5v5Normal) { modAdi = "5v5 Normal"; kisiSayisi = 5; }
                else if (b == ModDereceli) { modAdi = "5v5 Dereceli"; kisiSayisi = 5; }

                if (VM != null)
                {
                    VM.SeciliOyunModu = modAdi;
                    VM.AranacakOyuncuSayisi = kisiSayisi;
                }

                // dropdown'u kapat
                ModDropdown.Visibility = Visibility.Collapsed;
                ModDropdown.Opacity = 0;
                ToggleIkon.Text = "▶";

                // seçilen modu border ile işaretle
                var borders = new[] { Mod1v1, Mod2v2, Mod3v3, Mod5v5Normal, ModDereceli };
                foreach (var bord in borders)
                    bord.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222222"));
                b.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
            }
        }

        private void OdaAvatarHarfleriGuncelle()
        {
            if (VM == null) return;
            if (OdaBenAvatarHarf != null)
                OdaBenAvatarHarf.Text = string.IsNullOrEmpty(VM.OyuncuAdi) ? "?" : VM.OyuncuAdi[0].ToString().ToUpper();
            if (OdaRakipAvatarHarf != null)
                OdaRakipAvatarHarf.Text = string.IsNullOrEmpty(VM.RakipAdi) ? "?" : VM.RakipAdi[0].ToString().ToUpper();
        }

        private static string GetFlagEmoji(string region)
        {
            return region?.ToLower() switch
            {
                "tr" or "eu_tr" => "🇹🇷",
                "eu" or "eu_west" => "🇪🇺",
                "na" or "na1" => "🇺🇸",
                "kr" => "🇰🇷",
                "br" or "br1" or "br2" => "🇧🇷",
                "ap" or "apac" => "🇸🇬",
                "latam" => "🌎",
                "me" or "me1" => "🇦🇪",
                _ => "🏳️"
            };
        }

        private void LobbySlotGuncelle()
        {
            if (VM == null) return;
            var oyuncular = VM.LobiOyuncular;

            // Side slot → player: slot1=p1, slot2=p2, slot4=p3, slot5=p4
            int[] sidePlayerMap = [1, 2, 3, 4];

            var sideSlots = new[] { LobbySlot1, LobbySlot2, LobbySlot4, LobbySlot5 };
            var sideEmpty = new[] { Slot1Empty, Slot2Empty, Slot4Empty, Slot5Empty };
            var sideFilled = new[] { Slot1Filled, Slot2Filled, Slot4Filled, Slot5Filled };
            var sideAvatars = new[] { Slot1Avatar, Slot2Avatar, Slot4Avatar, Slot5Avatar };
            var sideNames = new[] { Slot1Ad, Slot2Ad, Slot4Ad, Slot5Ad };
            var sideTags = new[] { Slot1Tag, Slot2Tag, Slot4Tag, Slot5Tag };
            var sideRankTexts = new[] { Slot1RankText, Slot2RankText, Slot4RankText, Slot5RankText };
            var sideRankIcons = new[] { Slot1RankIcon, Slot2RankIcon, Slot4RankIcon, Slot5RankIcon };
            var sideImages = new[] { Slot1Image, Slot2Image, Slot4Image, Slot5Image };

            for (int i = 0; i < sideSlots.Length; i++)
            {
                int playerIdx = sidePlayerMap[i];
                bool hasPlayer = playerIdx < oyuncular.Count;

                if (hasPlayer)
                {
                    var oyuncu = oyuncular[playerIdx];
                    sideEmpty[i].Visibility = Visibility.Collapsed;
                    sideFilled[i].Visibility = Visibility.Visible;
                    sideSlots[i].BorderBrush = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));

                    sideNames[i].Text = oyuncu.Name;
                    sideAvatars[i].Text = string.IsNullOrEmpty(oyuncu.Name) ? "+" : oyuncu.Name[0].ToString().ToUpper();
                    sideTags[i].Text = "#" + oyuncu.Tag;
                    var eloText = oyuncu.Elo > 0 ? oyuncu.Elo + " ELO" : "";
                    var rankDisplay = string.IsNullOrEmpty(oyuncu.Rank) ? "Rütbesiz" : oyuncu.Rank;
                    sideRankTexts[i].Text = string.IsNullOrEmpty(eloText) ? rankDisplay : eloText + " · " + rankDisplay;
                    sideRankIcons[i].Text = string.IsNullOrEmpty(oyuncu.Rank) ? "?" : "★";

                    if (!string.IsNullOrEmpty(oyuncu.CardUrl))
                        SlotGorselYukleAsync(playerIdx, i, oyuncu.CardUrl, false);
                    else
                    {
                        sideImages[i].Source = null;
                        sideAvatars[i].Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    sideEmpty[i].Visibility = Visibility.Visible;
                    sideFilled[i].Visibility = Visibility.Collapsed;
                    sideSlots[i].BorderBrush = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
                }
            }

            // Center slot (index 2 → player[0] host)
            bool hasHost = oyuncular.Count > 0;
            if (hasHost)
            {
                var host = oyuncular[0];
                Slot3Empty.Visibility = Visibility.Collapsed;
                Slot3Filled.Visibility = Visibility.Visible;
                LobbySlot3.BorderBrush = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));

                Slot3Ad.Text = host.Name;
                Slot3Tag.Text = "#" + host.Tag;
                Slot3Elo.Text = host.Elo > 0 ? host.Elo + " ELO" : "";
                Slot3Avatar.Text = string.IsNullOrEmpty(host.Name) ? "+" : host.Name[0].ToString().ToUpper();
                Slot3RankText.Text = string.IsNullOrEmpty(host.Rank) ? "Rütbesiz" : host.Rank;
                Slot3RankIcon.Text = string.IsNullOrEmpty(host.Rank) ? "?" : "★";

                if (!string.IsNullOrEmpty(host.CardUrl))
                    SlotGorselYukleAsync(0, -1, host.CardUrl, true);
                else
                {
                    Slot3Image.Source = null;
                    Slot3Infinity.Visibility = Visibility.Visible;
                    Slot3Avatar.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Slot3Empty.Visibility = Visibility.Visible;
                Slot3Filled.Visibility = Visibility.Collapsed;
            }
        }

        private async void SlotGorselYukleAsync(int playerIdx, int sideSlotIdx, string cardUrl, bool isCenter)
        {
            if (string.IsNullOrEmpty(cardUrl)) return;
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var bytes = await http.GetByteArrayAsync(cardUrl);
                var app = Application.Current;
                if (app?.Dispatcher == null) return;
                await app.Dispatcher.InvokeAsync(() =>
                {
                    if (VM == null) return;
                    if (playerIdx >= VM.LobiOyuncular.Count) return;
                    if (VM.LobiOyuncular[playerIdx].CardUrl != cardUrl) return;

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new System.IO.MemoryStream(bytes);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();

                    if (isCenter)
                    {
                        Slot3Image.Source = bmp;
                        Slot3Image.Visibility = Visibility.Visible;
                        Slot3Infinity.Visibility = Visibility.Collapsed;
                        Slot3Avatar.Visibility = Visibility.Collapsed;
                    }
                    else if (sideSlotIdx >= 0 && sideSlotIdx < 4)
                    {
                        var sideImages = new[] { Slot1Image, Slot2Image, Slot4Image, Slot5Image };
                        var sideTexts = new[] { Slot1Avatar, Slot2Avatar, Slot4Avatar, Slot5Avatar };
                        sideImages[sideSlotIdx].Source = bmp;
                        sideTexts[sideSlotIdx].Visibility = Visibility.Collapsed;
                    }
                });
            }
            catch { }
        }

        private void LobbyAltBolumGuncelle()
        {
            if (VM == null) return;
            var grupKoduVar = !string.IsNullOrEmpty(VM.AktifGrupKodu);

            // Doluluk her zaman görünür
            LobbyDoluluk.Visibility = Visibility.Visible;

            if (grupKoduVar)
            {
                LobbyGrupKoduGir.Visibility = Visibility.Collapsed;
                LobbyGrupKodu.Visibility = Visibility.Visible;
                LobbyKodBekleniyor.Visibility = Visibility.Collapsed;
            }
            else if (VM.LobiSahibi)
            {
                LobbyGrupKoduGir.Visibility = Visibility.Visible;
                LobbyGrupKodu.Visibility = Visibility.Collapsed;
                LobbyKodBekleniyor.Visibility = Visibility.Collapsed;
            }
            else
            {
                LobbyGrupKoduGir.Visibility = Visibility.Collapsed;
                LobbyGrupKodu.Visibility = Visibility.Collapsed;
                LobbyKodBekleniyor.Visibility = Visibility.Visible;
            }
        }
    }
}
