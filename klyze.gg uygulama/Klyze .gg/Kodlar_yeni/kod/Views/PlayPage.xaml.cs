using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            AvatarHarfGuncelle();
            GirisAnimasyonu();
        }

        private void GirisAnimasyonu()
        {
            OyuncuKarti.Opacity = 0;
            KartScale.ScaleX = 0.8;
            KartScale.ScaleY = 0.8;

            var kartFade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(350)))
            { EasingFunction = CubicBezierEase.Spring };
            var kartScaleX = new DoubleAnimation(0.8, 1, new Duration(TimeSpan.FromMilliseconds(400)))
            { EasingFunction = CubicBezierEase.Spring };
            var kartScaleY = new DoubleAnimation(0.8, 1, new Duration(TimeSpan.FromMilliseconds(400)))
            { EasingFunction = CubicBezierEase.Spring };

            OyuncuKarti.BeginAnimation(OpacityProperty, kartFade);
            KartScale.BeginAnimation(ScaleTransform.ScaleXProperty, kartScaleX);
            KartScale.BeginAnimation(ScaleTransform.ScaleYProperty, kartScaleY);

            MacBulGrid.Opacity = 0;
            MacBulTranslate.Y = 24;

            var btnFade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)))
            { BeginTime = TimeSpan.FromMilliseconds(150), EasingFunction = CubicBezierEase.Spring };
            var btnSlide = new DoubleAnimation(24, 0, new Duration(TimeSpan.FromMilliseconds(300)))
            { BeginTime = TimeSpan.FromMilliseconds(150), EasingFunction = CubicBezierEase.Spring };

            MacBulGrid.BeginAnimation(OpacityProperty, btnFade);
            MacBulTranslate.BeginAnimation(TranslateTransform.YProperty, btnSlide);

            SecenekGrid.Opacity = 0;
            SecenekTranslate.X = -20;

            var secFade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(250)))
            { BeginTime = TimeSpan.FromMilliseconds(300), EasingFunction = CubicBezierEase.Spring };
            var secSlide = new DoubleAnimation(-20, 0, new Duration(TimeSpan.FromMilliseconds(250)))
            { BeginTime = TimeSpan.FromMilliseconds(300), EasingFunction = CubicBezierEase.Spring };

            SecenekGrid.BeginAnimation(OpacityProperty, secFade);
            SecenekTranslate.BeginAnimation(TranslateTransform.XProperty, secSlide);
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
                    GosterPaneli(LobiOlusturulduPaneli);
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

        private void OdaAvatarHarfleriGuncelle()
        {
            if (VM == null) return;
            if (OdaBenAvatarHarf != null)
                OdaBenAvatarHarf.Text = string.IsNullOrEmpty(VM.OyuncuAdi) ? "?" : VM.OyuncuAdi[0].ToString().ToUpper();
            if (OdaRakipAvatarHarf != null)
                OdaRakipAvatarHarf.Text = string.IsNullOrEmpty(VM.RakipAdi) ? "?" : VM.RakipAdi[0].ToString().ToUpper();
        }
    }
}
