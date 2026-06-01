using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ValorantAutoClicker.Helpers;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.ViewModels;

namespace ValorantAutoClicker.Views
{
    public partial class AnalizPage : UserControl
    {
        private AnalizViewModel VM => DataContext as AnalizViewModel;

        private bool _animasyonBasladi;
        private System.Timers.Timer _detayHideTimer;
        private const double HucreBoy = 13;
        private const double HucreAra = 2.5;

        public AnalizPage()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (VM == null)
            {
                var win = Window.GetWindow(this);
                if (win?.DataContext is MainViewModel mainVM)
                    DataContext = mainVM.AnalizVM;
            }
            try
            {
                if (VM != null && !VM.HasData && !VM.IsLoading && !VM.HasError)
                    await VM.YukleAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AnalizPage OnLoaded: " + ex.Message);
            }
        }

        private async void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (e.OldValue is AnalizViewModel oldVm)
                {
                    oldVm.GrafikCizilecek         -= OnGrafikCizilecek;
                    oldVm.EloGrafikCizilecek      -= OnEloGrafikCizilecek;
                    oldVm.EloGrafikTemizlenecek   -= OnEloGrafikTemizlenecek;
                    oldVm.AktiviteGrafikCizilecek -= OnAktiviteGrafikCizilecek;
                    oldVm.HaritaGrafikCizilecek  -= OnHaritaGrafikCizilecek;
                    oldVm.PropertyChanged -= OnVmPropertyChanged;
                    oldVm.CanliMacIzlemeyiDurdur();
                }
                if (e.NewValue is AnalizViewModel newVm)
                {
                    _macGecmisiIlkYukleme = true;
                    newVm.GrafikCizilecek         += OnGrafikCizilecek;
                    newVm.EloGrafikCizilecek      += OnEloGrafikCizilecek;
                    newVm.EloGrafikTemizlenecek   += OnEloGrafikTemizlenecek;
                    newVm.AktiviteGrafikCizilecek += OnAktiviteGrafikCizilecek;
                    newVm.HaritaGrafikCizilecek  += OnHaritaGrafikCizilecek;
                    newVm.PropertyChanged += OnVmPropertyChanged;
                    newVm.MacGecmisiListesi.CollectionChanged += (_, _) =>
                    {
                        if (VM != null && VM.MacGecmisiListesi.Count <= 20)
                            _macGecmisiIlkYukleme = true;
                        _macGecmisiAnimated.Clear();
                    };
                }
                if (VM != null && !VM.HasData && !VM.IsLoading && !VM.HasError)
                    await VM.YukleAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AnalizPage OnDataContextChanged: " + ex.Message);
            }
        }

        private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AnalizViewModel.SelectedTab))
                Dispatcher.Invoke(SekmeGecisAnimasyonu);
            if (e.PropertyName == nameof(AnalizViewModel.CanliMacVisible))
                Dispatcher.Invoke(CanliMacAnimasyonuBaslat);
            if (e.PropertyName == nameof(AnalizViewModel.AdrDetay))
                Dispatcher.Invoke(DrawAdrGrafik);
            if (e.PropertyName == nameof(AnalizViewModel.HasData))
                Dispatcher.Invoke(() => PerformansGirisAnimasyonu(), System.Windows.Threading.DispatcherPriority.Background);
            if (e.PropertyName == nameof(AnalizViewModel.DetayPanelVisible))
                Dispatcher.Invoke(DetayPanelAnimasyonu);
        }

        private void SekmeGecisAnimasyonu()
        {
            if (VM == null) return;

            if (SekmeGrid.ActualWidth > 0)
            {
                double tabGenislik = SekmeGrid.ActualWidth / 3.0;
                double hedefX = VM.SelectedTab * tabGenislik;

                var kaydirma = new DoubleAnimation(hedefX, new Duration(TimeSpan.FromMilliseconds(200)))
                {
                    EasingFunction = CubicBezierEase.Spring
                };
                UnderlineTranslate.BeginAnimation(TranslateTransform.XProperty, kaydirma);
            }

            // Görünürlük bağlantıları güncellendikten sonra fade animasyonunu çalıştır
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string[] panelAdlari = { "IcerikIstatistikler", "IcerikMacGecmisi", "IcerikCanliMac" };
                for (int i = 0; i < panelAdlari.Length; i++)
                {
                    if (FindName(panelAdlari[i]) is UIElement panel)
                    {
                        if (i == VM.SelectedTab)
                        {
                            panel.Opacity = 0;
                            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(150)))
                            {
                                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                            };
                            panel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                        }
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void SekmeCubugu_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (SekmeGrid.ActualWidth <= 0) return;
            double tabGenislik = SekmeGrid.ActualWidth / 3.0;
            TabUnderline.Width = tabGenislik * 0.5;
            UnderlineTranslate.X = (VM?.SelectedTab ?? 0) * tabGenislik;
        }

        // ════════════════════════════════════════════════════════════════════════
        // PERFORMANS KUTULARI — GİRİŞ ANİMASYONU
        // ════════════════════════════════════════════════════════════════════════

        private void PerformansGirisAnimasyonu()
        {
            if (!VM?.HasData ?? true) return;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            for (int idx = 0; idx < Math.Min(6, VM.PerformansMetrikleri.Count); idx++)
            {
                var kart = FindName($"Metrik{idx}") as Border;
                if (kart == null) continue;

                kart.Opacity = 0;
                var translate = new TranslateTransform(-24, 0);
                kart.RenderTransform = translate;
                kart.RenderTransformOrigin = new Point(0.5, 0.5);

                var slideAnim = new DoubleAnimation(-24, 0, new Duration(TimeSpan.FromMilliseconds(350)))
                {
                    BeginTime = TimeSpan.FromMilliseconds(idx * 80),
                    EasingFunction = ease
                };
                translate.BeginAnimation(TranslateTransform.XProperty, slideAnim);

                var fadeAnim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)))
                {
                    BeginTime = TimeSpan.FromMilliseconds(idx * 80),
                    EasingFunction = ease
                };
                kart.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

                var bar = FindName($"Metrik{idx}Bar") as Border;
                if (bar != null)
                {
                    var metrik = VM.PerformansMetrikleri[idx];
                    double maxWidth = bar.Parent is Grid pg && pg.ActualWidth > 0 ? pg.ActualWidth : 120;
                    double ratio = metrik.Maksimum > 0 ? Math.Min(1.0, metrik.Deger / metrik.Maksimum) : 0;
                    bar.Width = 0;
                    var widthAnim = new DoubleAnimation(0, ratio * maxWidth, new Duration(TimeSpan.FromMilliseconds(600)))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(idx * 80 + 250),
                        EasingFunction = ease
                    };
                    bar.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // DETAY PANELİ — SAĞDAN KAYARAK AÇILMA + KART SCALE
        // ════════════════════════════════════════════════════════════════════════

        private void DetayPanelAnimasyonu()
        {
            if (VM == null) return;
            var detayPanel = FindName("DetayPanel") as Border;
            var slide = FindName("DetayPanelSlide") as TranslateTransform;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var spring = CubicBezierEase.Spring;

            // Cancel any pending hide timer — prevents race where close timer
            // collapses the panel right after it was opened for a different metric
            if (_detayHideTimer != null)
            {
                _detayHideTimer.Stop();
                _detayHideTimer.Dispose();
                _detayHideTimer = null;
            }

            if (VM.DetayPanelVisible)
            {
                int selectedIndex = (int)VM.DetayPanelTipi;

                // Show correct inner content panel
                string[] panelAdlari = { "DetayKazanmaOrani", "DetayAdr", "DetayKr",
                                         "DetayBitisBasarisi", "DetayGirisBasarisi", "DetayMultiKill" };
                for (int i = 0; i < panelAdlari.Length; i++)
                {
                    if (FindName(panelAdlari[i]) is UIElement p)
                        p.Visibility = i == selectedIndex ? Visibility.Visible : Visibility.Collapsed;
                }

                if (detayPanel != null)
                {
                    detayPanel.Visibility = Visibility.Visible;
                    detayPanel.Opacity = 0;
                    detayPanel.BeginAnimation(UIElement.OpacityProperty,
                        new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200))) { EasingFunction = ease });
                }
                if (slide != null)
                {
                    slide.BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation(300, 0, new Duration(TimeSpan.FromMilliseconds(350))) { EasingFunction = spring });
                }

                for (int i = 0; i < 6; i++)
                {
                    var kart = FindName($"Metrik{i}") as Border;
                    if (kart == null) continue;
                    if (i == selectedIndex)
                    {
                        var transform = new ScaleTransform(1, 1);
                        kart.RenderTransform = transform;
                        kart.RenderTransformOrigin = new Point(0.5, 0.5);
                        transform.BeginAnimation(ScaleTransform.ScaleXProperty,
                            new DoubleAnimation(1, 1.02, new Duration(TimeSpan.FromMilliseconds(200))) { EasingFunction = ease });
                        transform.BeginAnimation(ScaleTransform.ScaleYProperty,
                            new DoubleAnimation(1, 1.02, new Duration(TimeSpan.FromMilliseconds(200))) { EasingFunction = ease });

                        kart.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
                        kart.BorderBrush = Brushes.White;
                    }
                    else
                    {
                        kart.BeginAnimation(UIElement.OpacityProperty,
                            new DoubleAnimation(1, 0.6, new Duration(TimeSpan.FromMilliseconds(200))));
                    kart.Background = Brushes.Transparent;
                    kart.BorderBrush = Brushes.Transparent;
                        kart.BorderBrush = Brushes.Transparent;
                    }
                }
            }
            else
            {
                if (slide != null)
                {
                    slide.BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation(0, 300, new Duration(TimeSpan.FromMilliseconds(250))) { EasingFunction = ease });
                }
                if (detayPanel != null)
                {
                    detayPanel.BeginAnimation(UIElement.OpacityProperty,
                        new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(200))));
                }

                // Hide after animation completes
                _detayHideTimer = new System.Timers.Timer(280);
                _detayHideTimer.AutoReset = false;
                _detayHideTimer.Elapsed += (_, _) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (detayPanel != null) detayPanel.Visibility = Visibility.Collapsed;
                    });
                };
                _detayHideTimer.Start();

                for (int i = 0; i < 6; i++)
                {
                    var kart = FindName($"Metrik{i}") as Border;
                    if (kart == null) continue;
                    kart.RenderTransform = new ScaleTransform(1, 1);
                    kart.BeginAnimation(UIElement.OpacityProperty,
                        new DoubleAnimation(1, new Duration(TimeSpan.FromMilliseconds(200))));
                    kart.Background = Brushes.Transparent;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // GRAFİK EVENTLERİ
        // ════════════════════════════════════════════════════════════════════════

        private void OnGrafikCizilecek() { }

        private void OnEloGrafikTemizlenecek()
        {
            Dispatcher.Invoke(() =>
            {
                _eloAnimasyonBasladi = false;
                _eloAnimasyonIptal = true;
                _eloVeriHazir = false;
                if (_eloAnimasyonTimer != null)
                {
                    _eloAnimasyonTimer.Stop();
                    _eloAnimasyonTimer.Dispose();
                    _eloAnimasyonTimer = null;
                }
                foreach (var t in _eloCountUpTimers)
                    t.Stop();
                _eloCountUpTimers.Clear();
                if (EloCanvas != null) EloCanvas.Children.Clear();
                HoverTemizle();
                // Null-safe TextBlock güncellemeleri
                void SetText(string name, string val) { if (FindName(name) is System.Windows.Controls.TextBlock tb) tb.Text = val; }
                SetText("EloFarkDegeri", "--");
                SetText("EloMaxDegeri", "");
                SetText("EloMinDegeri", "");
                SetText("EloWinRateDegeri", "");
                SetText("EloTotalDegeri", "");
                SetText("EloWinsText", "");
                SetText("EloLossesText", "");
            });
        }

        private void OnEloGrafikCizilecek()
        {
            Dispatcher.Invoke(() =>
            {
                _eloAnimasyonBasladi = false;
                _eloAnimasyonIptal = false;
                _eloVeriHazir = true;
                DrawEloGrafik();
                HoverTemizle();
            });
        }

        private void OnAktiviteGrafikCizilecek()
        {
            Dispatcher.Invoke(() =>
            {
                DrawSaatlikGrafik();
                DrawHaftalikGrafik();
                DrawTakvim();
                DrawTakvimAyBasliklari();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnHaritaGrafikCizilecek()
            => Dispatcher.Invoke(() => DrawHaritaRadarGrafik());

        private void MacGecmisiScroll_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ScrollViewer sv && VM != null)
            {
                if (sv.VerticalOffset >= sv.ScrollableHeight - 60 && !VM.IsLoadingMore && VM.HasMoreMatches)
                {
                    var cmd = VM.DahaFazlaYukleCommand;
                    if (cmd?.CanExecute(null) == true)
                        cmd.Execute(null);
                }
            }
        }

        private readonly HashSet<int> _macGecmisiAnimated = new();
        private bool _macGecmisiIlkYukleme = true;

        private void MacGecmisiRow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (sender is not System.Windows.FrameworkElement fe) return;

                var itemsControl = FindParent<ItemsControl>(fe);
                if (itemsControl == null) return;

                int index = itemsControl.ItemContainerGenerator.IndexFromContainer(
                    FindParent<System.Windows.Controls.ContentPresenter>(fe));
                if (index < 0) index = 0;

                int delay = index * 30;
                bool isNew = _macGecmisiAnimated.Add(index);
                double fromY = isNew ? (_macGecmisiIlkYukleme ? -15 : 15) : 0;

                var sb = new System.Windows.Media.Animation.Storyboard();

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = isNew ? 0 : 1,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(opacityAnim, fe);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(opacityAnim,
                    new System.Windows.PropertyPath(System.Windows.UIElement.OpacityProperty));
                sb.Children.Add(opacityAnim);

                var translate = fe.RenderTransform as System.Windows.Media.TranslateTransform;
                if (translate == null)
                {
                    translate = new System.Windows.Media.TranslateTransform();
                    fe.RenderTransform = translate;
                }

                var translateAnim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = fromY,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(translateAnim, translate);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(translateAnim,
                    new System.Windows.PropertyPath(System.Windows.Media.TranslateTransform.YProperty));
                sb.Children.Add(translateAnim);

                sb.BeginTime = TimeSpan.FromMilliseconds(delay);
                sb.Begin();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Stagger] " + ex.Message);
                if (sender is System.Windows.FrameworkElement fe2)
                {
                    fe2.Opacity = 1;
                }
            }
        }

        private void MacSatir_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border b)
            {
                b.BeginAnimation(System.Windows.Controls.Border.BackgroundProperty, null);
                if (b.Background is not SolidColorBrush scb || scb.IsFrozen)
                {
                    b.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14));
                    scb = (SolidColorBrush)b.Background;
                }
                var anim = new System.Windows.Media.Animation.ColorAnimation
                {
                    To = Color.FromRgb(0x1E, 0x1E, 0x1E),
                    Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                scb.BeginAnimation(SolidColorBrush.ColorProperty, anim);
            }
        }

        private void MacSatir_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border b)
            {
                b.BeginAnimation(System.Windows.Controls.Border.BackgroundProperty, null);
                if (b.Background is SolidColorBrush scb && !scb.IsFrozen)
                {
                    var anim = new System.Windows.Media.Animation.ColorAnimation
                    {
                        To = Color.FromRgb(0x14, 0x14, 0x14),
                        Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    scb.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                }
                else
                {
                    b.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14));
                }
            }
        }

        private static T FindParent<T>(System.Windows.DependencyObject child) where T : System.Windows.DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null && parent is not T)
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            return parent as T;
        }

        // ════════════════════════════════════════════════════════════════════════
        // ELO GRAFİK — TAM YENİDEN TASARIM
        // ════════════════════════════════════════════════════════════════════════

        private List<Point> _eloCanvasPoints = new();
        private List<EloGrafikNokta> _eloNoktalar = new();
        private double _eloYMin, _eloYMax, _eloYRange, _eloStep;
        private double _eloPadLeft, _eloChartW;
        private int _eloNoktaCount;
        private bool _eloAnimasyonBasladi;
        private bool _eloAnimasyonIptal;
        private bool _eloVeriHazir;
        private System.Timers.Timer _eloAnimasyonTimer;
        private readonly List<System.Windows.Threading.DispatcherTimer> _eloCountUpTimers = new();

        private void EloCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_eloVeriHazir) DrawEloGrafik();
        }

        private void EloXEksen_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // EloXEksen artık XAML'da var — no-op, grafik EloCanvas içinde çiziliyor
        }

        private static double NiceStep(double range)
        {
            double rawStep = range / 4.5;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
            double norm = rawStep / mag;
            double nice = norm <= 1.5 ? 1 : norm <= 3.5 ? 2 : norm <= 7.5 ? 5 : 10;
            return nice * mag;
        }

        private UIElement _savedTooltip;
        private void DrawEloGrafik()
        {
            if (EloCanvas == null || VM == null) return;
            // Tooltip'i kurtar, Children.Clear() onu da siler
            _savedTooltip = FindName("EloTooltipBorder") as UIElement;
            EloCanvas.Children.Clear();

            var noktalar = VM.EloGrafikNoktalari?.ToList();
            if (noktalar == null || noktalar.Count < 2)
            {
                _eloCanvasPoints.Clear();
                _eloNoktalar.Clear();
                return;
            }

            double w = EloCanvas.ActualWidth;
            double h = EloCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double padTop = 36, padBottom = 28, padLeft = 36, padRight = 4;
            double chartW = w - padLeft - padRight;
            double chartH = h - padTop - padBottom;
            if (chartW <= 0 || chartH <= 0) return;
            _eloPadLeft = padLeft;
            _eloChartW = chartW;
            _eloNoktaCount = noktalar.Count;

            var eloVals = noktalar.Select(n => n.Elo).ToList();
            int rawMin = eloVals.Min();
            int rawMax = eloVals.Max();

            _eloYMin = rawMin - 80;
            _eloYMax = rawMax + 80;
            _eloYRange = _eloYMax - _eloYMin;
            if (_eloYRange < 40) { _eloYMin -= 20; _eloYMax += 20; _eloYRange = _eloYMax - _eloYMin; }

            _eloStep = NiceStep(_eloYRange);
            double firstStep = Math.Ceiling(_eloYMin / _eloStep) * _eloStep;
            int stepCount = (int)Math.Round((_eloYMax - firstStep) / _eloStep);
            if (stepCount < 2) stepCount = 2;

            // Grid cizgileri ve Y ekseni
            for (int i = 0; i <= stepCount; i++)
            {
                double yVal = firstStep + i * _eloStep;
                double y = padTop + chartH - ((yVal - _eloYMin) / _eloYRange * chartH);

                EloCanvas.Children.Add(new Line
                {
                    X1 = padLeft, Y1 = y, X2 = w - padRight, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(13, 255, 255, 255)),
                    StrokeThickness = 1
                });

                var lbl = new TextBlock
                {
                    Text = yVal.ToString("F0"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255))
                };
                Canvas.SetLeft(lbl, 4);
                Canvas.SetTop(lbl, y - 7);
                EloCanvas.Children.Add(lbl);
            }

            _eloCanvasPoints = noktalar.Select((n, i) => new Point(
                padLeft + (i / (double)(noktalar.Count - 1)) * chartW,
                padTop + chartH - ((n.Elo - _eloYMin) / _eloYRange * chartH)
            )).ToList();
            _eloNoktalar = noktalar.ToList();

            if (!_eloAnimasyonBasladi && _eloCanvasPoints.Count > 0)
            {
                _eloAnimasyonBasladi = true;
                EloAnimasyonuBaslat(_eloCanvasPoints, noktalar, w, h, padTop, padLeft, padRight, chartW, chartH);
            }
            else
            {
                CizEloGrafikTam(_eloCanvasPoints, noktalar, w, h, padTop, padLeft, padRight, chartW, chartH);
            }

            EloOzetGuncelle();

            // Tooltip'i geri ekle (Children.Clear silmisti)
            if (_savedTooltip != null && !EloCanvas.Children.Contains(_savedTooltip))
                EloCanvas.Children.Add(_savedTooltip);
            _savedTooltip = null;
        }

        private void CizEloGrafikTam(List<Point> pts, List<EloGrafikNokta> noktalar,
            double w, double h, double padTop, double padLeft, double padRight,
            double chartW, double chartH)
        {
            if (pts.Count < 2) return;

            var dolguPts = new PointCollection { new Point(pts[0].X, padTop + chartH) };
            foreach (var p in pts) dolguPts.Add(p);
            dolguPts.Add(new Point(pts[pts.Count - 1].X, padTop + chartH));

            EloCanvas.Children.Add(new Polygon
            {
                Points = dolguPts,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(102, 0xFF, 0x45, 0x00), 0.0),
                        new GradientStop(Color.FromArgb(0, 0xFF, 0x00, 0x00), 1.0)
                    }
                }
            });

            for (int i = 0; i < pts.Count - 1; i++)
            {
                EloCanvas.Children.Add(new Line
                {
                    X1 = pts[i].X, Y1 = pts[i].Y, X2 = pts[i + 1].X, Y2 = pts[i + 1].Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x00)),
                    StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Miter
                });
            }

            for (int i = 0; i < noktalar.Count; i++)
            {
                if ((i + 1) % 4 == 1 || i == 0 || i == noktalar.Count - 1)
                {
                    var lbl = new TextBlock
                    {
                        Text = (i + 1).ToString(),
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255))
                    };
                    Canvas.SetLeft(lbl, pts[i].X - 6);
                    Canvas.SetTop(lbl, padTop + chartH + 6);
                    EloCanvas.Children.Add(lbl);
                }
            }
        }

        private void EloAnimasyonuBaslat(List<Point> pts, List<EloGrafikNokta> noktalar,
            double w, double h, double padTop, double padLeft, double padRight,
            double chartW, double chartH)
        {
            if (pts.Count < 2) return;

            if (_eloAnimasyonTimer != null)
            {
                _eloAnimasyonTimer.Stop();
                _eloAnimasyonTimer.Dispose();
                _eloAnimasyonTimer = null;
            }

            var dur = 800;
            var basla = DateTime.Now;

            _eloAnimasyonTimer = new System.Timers.Timer(16);
            _eloAnimasyonTimer.Elapsed += (_, _) =>
            {
                var elapsed = (DateTime.Now - basla).TotalMilliseconds;
                var t = Math.Min(elapsed / dur, 1.0);
                var eased = t < 1 ? (1 - Math.Pow(1 - t, 3)) : 1;
                int count = Math.Max(1, (int)(pts.Count * eased));

                Dispatcher.Invoke(() =>
                {
                    if (_eloAnimasyonIptal) return;
                    var animTooltip = FindName("EloTooltipBorder") as UIElement;
                    if (animTooltip != null && EloCanvas.Children.Contains(animTooltip))
                        EloCanvas.Children.Remove(animTooltip);
                    EloCanvas.Children.Clear();

                    // Grid cizgileri ve Y ekseni
                    double firstStep = Math.Ceiling(_eloYMin / _eloStep) * _eloStep;
                    int stepCount = (int)Math.Round((_eloYMax - firstStep) / _eloStep);
                    if (stepCount < 2) stepCount = 2;
                    for (int i = 0; i <= stepCount; i++)
                    {
                        double yVal = firstStep + i * _eloStep;
                        double y = padTop + chartH - ((yVal - _eloYMin) / _eloYRange * chartH);
                        EloCanvas.Children.Add(new Line
                        {
                            X1 = padLeft, Y1 = y, X2 = w - padRight, Y2 = y,
                            Stroke = new SolidColorBrush(Color.FromArgb(13, 255, 255, 255)),
                            StrokeThickness = 1
                        });
                        var lbl = new TextBlock
                        {
                            Text = yVal.ToString("F0"),
                            FontSize = 9,
                            Foreground = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255))
                        };
                        Canvas.SetLeft(lbl, 4);
                        Canvas.SetTop(lbl, y - 7);
                        EloCanvas.Children.Add(lbl);
                    }

                    var gecerliPts = pts.Take(count).ToList();

                    if (gecerliPts.Count >= 2)
                    {
                        var dolguPts = new PointCollection { new Point(gecerliPts[0].X, padTop + chartH) };
                        foreach (var p in gecerliPts) dolguPts.Add(p);
                        dolguPts.Add(new Point(gecerliPts[gecerliPts.Count - 1].X, padTop + chartH));

                        EloCanvas.Children.Add(new Polygon
                        {
                            Points = dolguPts,
                            Fill = new LinearGradientBrush
                            {
                                StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                                GradientStops = new GradientStopCollection
                                {
                                    new GradientStop(Color.FromArgb(102, 0xFF, 0x45, 0x00), 0.0),
                                    new GradientStop(Color.FromArgb(0, 0xFF, 0x00, 0x00), 1.0)
                                }
                            }
                        });

                        for (int i = 0; i < gecerliPts.Count - 1; i++)
                        {
                            EloCanvas.Children.Add(new Line
                            {
                                X1 = gecerliPts[i].X, Y1 = gecerliPts[i].Y,
                                X2 = gecerliPts[i + 1].X, Y2 = gecerliPts[i + 1].Y,
                                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x00)),
                                StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Miter
                            });
                        }

                        for (int i = 0; i < count && i < noktalar.Count; i++)
                        {
                            if ((i + 1) % 4 == 1 || i == 0 || i == noktalar.Count - 1)
                            {
                                var lbl = new TextBlock
                                {
                                    Text = (i + 1).ToString(),
                                    FontSize = 9,
                                    Foreground = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255))
                                };
                                Canvas.SetLeft(lbl, pts[i].X - 6);
                                Canvas.SetTop(lbl, padTop + chartH + 6);
                                EloCanvas.Children.Add(lbl);
                            }
                        }
                    }

                    // Tooltip'i geri ekle
                    if (animTooltip != null && !EloCanvas.Children.Contains(animTooltip))
                        EloCanvas.Children.Add(animTooltip);
                });

                if (t >= 1)
                {
                    _eloAnimasyonTimer.Stop();
                    _eloAnimasyonTimer.Dispose();
                    _eloAnimasyonTimer = null;
                }
            };
            _eloAnimasyonTimer.Start();
        }

        // ─── Mouse Hover ────────────────────────────────────────────────────

        private void HoverTemizle()
        {
            if (EloCanvas == null) return;
            for (int i = EloCanvas.Children.Count - 1; i >= 0; i--)
            {
                if (EloCanvas.Children[i] is FrameworkElement fe && fe.Tag as string == "eloHover")
                    EloCanvas.Children.RemoveAt(i);
            }
            if (FindName("EloTooltipBorder") is Border tb)
                tb.Visibility = Visibility.Collapsed;
        }

        private void EloGrafik_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var tooltip  = FindName("EloTooltipBorder") as Border;
            var tipValue = FindName("EloTooltipValue")  as System.Windows.Controls.TextBlock;
            if (_eloNoktalar.Count < 2) return;

            var pos = e.GetPosition(EloCanvas);
            if (pos.X < 0 || pos.X > EloCanvas.ActualWidth) return;

            double relativeX = pos.X - _eloPadLeft;
            double ratio = _eloChartW > 0 ? relativeX / _eloChartW : 0;
            int index = (int)Math.Round(ratio * (_eloNoktalar.Count - 1));
            index = Math.Max(0, Math.Min(_eloNoktalar.Count - 1, index));

            double x = _eloPadLeft + (index / (double)(_eloNoktalar.Count - 1)) * _eloChartW;
            if (x < _eloPadLeft) x = _eloPadLeft;

            double y;
            if (index < _eloCanvasPoints.Count)
                y = _eloCanvasPoints[index].Y;
            else
            {
                var nokta = _eloNoktalar[index];
                double ch = EloCanvas.ActualHeight - 36 - 28;
                y = 36 + ch - ((nokta.Elo - _eloYMin) / _eloYRange * ch);
            }

            var data = _eloNoktalar[index];
            HoverTemizle();

            // Dikey çizgi — rgba(255,255,255,0.6)
            var line = new System.Windows.Shapes.Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = EloCanvas.ActualHeight,
                Stroke = new SolidColorBrush(Color.FromArgb(153, 255, 255, 255)),
                StrokeThickness = 1,
                IsHitTestVisible = false,
                Tag = "eloHover"
            };
            EloCanvas.Children.Add(line);

            // Glow — rgba(255,255,255,0.2), radius 12px
            var glow = new System.Windows.Shapes.Ellipse
            {
                Width = 24, Height = 24,
                Fill = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)),
                IsHitTestVisible = false,
                Tag = "eloHover"
            };
            Canvas.SetLeft(glow, x - 12);
            Canvas.SetTop(glow, y - 12);
            EloCanvas.Children.Add(glow);

            // Dot — #ffffff, radius 6px
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 12, Height = 12,
                Fill = new SolidColorBrush(Colors.White),
                IsHitTestVisible = false,
                Tag = "eloHover"
            };
            Canvas.SetLeft(dot, x - 6);
            Canvas.SetTop(dot, y - 6);
            EloCanvas.Children.Add(dot);

            // Tooltip
            if (tooltip != null && tipValue != null)
            {
                tipValue.Text = data.Elo.ToString();
                tooltip.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                double tw = tooltip.DesiredSize.Width;
                Canvas.SetLeft(tooltip, x - tw / 2);
                Canvas.SetTop(tooltip, y + 14);
                tooltip.Visibility = Visibility.Visible;
            }
        }

        private void EloGrafik_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            HoverTemizle();
        }

        // ─── Ozet Panel Guncelleme ──────────────────────────────────────────
        private void EloOzetGuncelle()
        {
            if (VM?.EloOzet == null) return;
            var ozet = VM.EloOzet;

            var eloFarkDegeri = FindName("EloFarkDegeri") as System.Windows.Controls.TextBlock;
            if (eloFarkDegeri != null)
            {
                int fark = ozet.ToplamEloFarki;
                if (fark >= 0)
                {
                    eloFarkDegeri.Text = $"+{fark}";
                    eloFarkDegeri.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                }
                else
                {
                    eloFarkDegeri.Text = fark.ToString();
                    eloFarkDegeri.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                }
            }

            foreach (var t in _eloCountUpTimers) t.Stop();
            _eloCountUpTimers.Clear();

            void CountUp(string name, double hedef, bool yuzde)
            {
                if (FindName(name) is System.Windows.Controls.TextBlock tb)
                    AnimasyonluCountUpBaslat(tb, 0, hedef, 600, yuzde);
            }
            CountUp("EloMaxDegeri",     ozet.EnYuksekElo,       false);
            CountUp("EloMinDegeri",     ozet.EnDusukElo,        false);
            CountUp("EloWinRateDegeri", ozet.GalibiyetYuzdesi,  true);
            CountUp("EloTotalDegeri",   ozet.ToplamMac,         false);
            CountUp("EloWinsText",      ozet.Galibiyet,         false);
            CountUp("EloLossesText",    ozet.Maglubiyet,        false);

            // Win / Loss bar
            double toplam = ozet.ToplamMac > 0 ? ozet.ToplamMac : 1;
            double winOran = ozet.Galibiyet / toplam;
            if (FindName("EloWinBarCol")  is ColumnDefinition winCol)
                winCol.Width  = new GridLength(winOran,     GridUnitType.Star);
            if (FindName("EloLossBarCol") is ColumnDefinition lossCol)
                lossCol.Width = new GridLength(1 - winOran, GridUnitType.Star);
        }

        private void AnimasyonluCountUpBaslat(System.Windows.Controls.TextBlock tb, double baslangic, double hedef, double ms, bool yuzde)
        {
            var basla = DateTime.Now;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) =>
            {
                var elapsed = (DateTime.Now - basla).TotalMilliseconds;
                double t = Math.Min(elapsed / ms, 1.0);
                double eased = 1 - Math.Pow(1 - t, 3);
                double val = baslangic + (hedef - baslangic) * eased;

                if (yuzde)
                    tb.Text = val.ToString("F1") + "%";
                else if (hedef == (int)hedef)
                    tb.Text = ((int)Math.Round(val)).ToString();
                else
                    tb.Text = val.ToString("F0");

                if (t >= 1)
                {
                    timer.Stop();
                    _eloCountUpTimers.Remove(timer);
                }
            };
            _eloCountUpTimers.Add(timer);
            timer.Start();
        }

        // ════════════════════════════════════════════════════════════════════════
        // AKTIVITE — SAATLİK GRAFİK
        // ════════════════════════════════════════════════════════════════════════

        private List<SaatlikAktivite> _saatlikVeri;
        private int _saatlikMax;
        private double _saatlikW, _saatlikH, _saatlikCubukW, _saatlikChartH;

        private void SaatlikCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawSaatlikGrafik();

        private void DrawSaatlikGrafik()
        {
            if (SaatlikCanvas == null || VM == null) return;
            SaatlikCanvas.Children.Clear();
            _animasyonBasladi = false;

            _saatlikVeri = VM.SaatlikAktiviteler?.ToList();
            if (_saatlikVeri == null || !_saatlikVeri.Any()) return;

            _saatlikW = SaatlikCanvas.ActualWidth;
            _saatlikH = SaatlikCanvas.ActualHeight;
            if (_saatlikW <= 0 || _saatlikH <= 0) return;

            double padTop = 8, padBottom = 12, padRight = 28;
            _saatlikChartH = _saatlikH - padTop - padBottom;
            if (_saatlikChartH <= 0) return;

            _saatlikMax = Math.Max(1, _saatlikVeri.Max(v => v.MacSayisi));
            _saatlikCubukW = (_saatlikW - padRight) / 24.0;

            // Y ekseni cizgileri sagda
            int yCizgiAdet = 4;
            for (int i = 0; i <= yCizgiAdet; i++)
            {
                double y = padTop + _saatlikChartH - (i / (double)yCizgiAdet) * _saatlikChartH;
                SaatlikCanvas.Children.Add(new Line
                {
                    X1 = 0, Y1 = y, X2 = _saatlikW - padRight, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
                    StrokeThickness = 1
                });
                var lbl = new TextBlock
                {
                    Text = Math.Round(_saatlikMax * i / (double)yCizgiAdet).ToString(),
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255))
                };
                Canvas.SetLeft(lbl, _saatlikW - padRight + 4);
                Canvas.SetTop(lbl, y - 6);
                SaatlikCanvas.Children.Add(lbl);
            }

            // Cubuklar (beyaz) + galibiyet cizgisi (yesil)
            for (int i = 0; i < 24; i++)
            {
                double cubukH = (_saatlikVeri[i].MacSayisi / (double)_saatlikMax) * _saatlikChartH;
                if (cubukH < 0.5) cubukH = 0.5;

                var rect = new Rectangle
                {
                    Width = Math.Max(1, _saatlikCubukW - 2),
                    Height = cubukH,
                    Fill = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                    RadiusX = 1.5, RadiusY = 1.5,
                    Tag = i
                };
                Canvas.SetLeft(rect, i * _saatlikCubukW + 1);
                Canvas.SetTop(rect, padTop + _saatlikChartH - cubukH);
                SaatlikCanvas.Children.Add(rect);
            }

            // Yesil galibiyet cizgisi
            var galPts = new PointCollection();
            for (int i = 0; i < 24; i++)
            {
                double x = i * _saatlikCubukW + _saatlikCubukW / 2;
                double galH = (_saatlikVeri[i].Galibiyet / (double)_saatlikMax) * _saatlikChartH;
                double y = padTop + _saatlikChartH - galH;
                galPts.Add(new Point(x, y));
            }

            // Dolgu alani (cizginin alti yari saydam yesil)
            var dolguPts = new PointCollection { new Point(galPts[0].X, padTop + _saatlikChartH) };
            foreach (var p in galPts) dolguPts.Add(p);
            dolguPts.Add(new Point(galPts[23].X, padTop + _saatlikChartH));

            SaatlikCanvas.Children.Add(new Polygon
            {
                Points = dolguPts,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(60, 0x00, 0xD2, 0x6A), 0.0),
                        new GradientStop(Color.FromArgb(10, 0x00, 0xD2, 0x6A), 1.0)
                    }
                }
            });

            for (int i = 0; i < 23; i++)
            {
                SaatlikCanvas.Children.Add(new Line
                {
                    X1 = galPts[i].X, Y1 = galPts[i].Y,
                    X2 = galPts[i + 1].X, Y2 = galPts[i + 1].Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0x6A)),
                    StrokeThickness = 2
                });
            }

            // Animasyon cubuklari
            if (_animasyonBasladi) return;
            _animasyonBasladi = true;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            for (int i = 0; i < SaatlikCanvas.Children.Count; i++)
            {
                if (SaatlikCanvas.Children[i] is Rectangle r && r.Tag is int)
                {
                    double targetH = r.Height;
                    r.Height = 0;
                    var anim = new DoubleAnimation(0, targetH, new Duration(TimeSpan.FromMilliseconds(400)))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(i * 30),
                        EasingFunction = ease
                    };
                    r.BeginAnimation(FrameworkElement.HeightProperty, anim);
                }
            }
        }

        // ─── Saatlik Hover ────────────────────────────────────────────────────
        private Border _saatlikTooltip;
        private TextBlock _saatlikTooltipSaat, _saatlikTooltipOynanan, _saatlikTooltipKazanc, _saatlikTooltipYuzde;
        private Line _saatlikHoverLine;

        private Border GetOrCreateSaatlikTooltip()
        {
            if (_saatlikTooltip == null)
            {
                _saatlikTooltip = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(230, 26, 26, 26)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                    BorderThickness = new Thickness(1),
                    IsHitTestVisible = false,
                    Opacity = 0
                };
                var sp = new StackPanel();
                _saatlikTooltipSaat = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                _saatlikTooltipOynanan = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)), Margin = new Thickness(0, 2, 0, 0) };
                _saatlikTooltipKazanc = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)) };
                _saatlikTooltipYuzde = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0x6A)), Margin = new Thickness(0, 2, 0, 0) };
                sp.Children.Add(_saatlikTooltipSaat);
                sp.Children.Add(_saatlikTooltipOynanan);
                sp.Children.Add(_saatlikTooltipKazanc);
                sp.Children.Add(_saatlikTooltipYuzde);
                _saatlikTooltip.Child = sp;
            }
            return _saatlikTooltip;
        }

        private void SaatlikCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_saatlikVeri == null || _saatlikW <= 0) return;
            var pos = e.GetPosition(SaatlikCanvas);
            int idx = (int)(pos.X / _saatlikCubukW);
            if (idx < 0 || idx >= 24) { SaatlikHoverTemizle(); return; }

            var veri = _saatlikVeri[idx];
            double x = idx * _saatlikCubukW + _saatlikCubukW / 2;

            // Hover cizgisi — sadece pozisyonu guncelle, silip ekleme
            if (_saatlikHoverLine == null)
            {
                _saatlikHoverLine = new Line
                {
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    IsHitTestVisible = false
                };
                Canvas.SetZIndex(_saatlikHoverLine, 10);
                SaatlikCanvas.Children.Add(_saatlikHoverLine);
            }
            _saatlikHoverLine.X1 = x; _saatlikHoverLine.Y1 = 0;
            _saatlikHoverLine.X2 = x; _saatlikHoverLine.Y2 = _saatlikH;

            var tip = GetOrCreateSaatlikTooltip();
            _saatlikTooltipSaat.Text = $"{idx}:00 - {idx + 1}:00";
            _saatlikTooltipOynanan.Text = $"Oynanan: {veri.MacSayisi} mac";
            _saatlikTooltipKazanc.Text = $"Kazanilan: {veri.Galibiyet} mac";
            _saatlikTooltipYuzde.Text = $"Kazanma: %{veri.KazanmaYuzdesi:F1}";
            tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double tw = tip.DesiredSize.Width;
            double tx = x - tw / 2;
            if (tx < 0) tx = 4;
            if (tx + tw > _saatlikW) tx = _saatlikW - tw - 4;
            double ty = _saatlikH - tip.DesiredSize.Height - 8;

            if (!SaatlikCanvas.Children.Contains(tip))
            {
                Canvas.SetZIndex(tip, 20);
                SaatlikCanvas.Children.Add(tip);
            }
            Canvas.SetLeft(tip, tx);
            Canvas.SetTop(tip, ty);
            tip.BeginAnimation(UIElement.OpacityProperty, null);
            tip.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }

        private void SaatlikCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            SaatlikHoverTemizle();
        }

        private void SaatlikHoverTemizle()
        {
            if (_saatlikTooltip != null && SaatlikCanvas.Children.Contains(_saatlikTooltip))
                SaatlikCanvas.Children.Remove(_saatlikTooltip);
            if (_saatlikHoverLine != null && SaatlikCanvas.Children.Contains(_saatlikHoverLine))
                SaatlikCanvas.Children.Remove(_saatlikHoverLine);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AKTIVITE — HAFTALIK GRAFİK
        // ════════════════════════════════════════════════════════════════════════

        private List<HaftalikAktivite> _haftalikVeri;
        private int _haftalikMax;
        private double _haftalikW, _haftalikH, _haftalikCubukW, _haftalikChartH;
        private Border _haftalikTooltip;
        private TextBlock _haftalikTooltipGun, _haftalikTooltipOynanan, _haftalikTooltipKazanc, _haftalikTooltipYuzde;

        private void HaftalikCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawHaftalikGrafik();

        private void DrawHaftalikGrafik()
        {
            if (HaftalikCanvas == null || VM == null) return;
            HaftalikCanvas.Children.Clear();

            _haftalikVeri = VM.HaftalikAktiviteler?.ToList();
            if (_haftalikVeri == null || !_haftalikVeri.Any()) return;

            _haftalikW = HaftalikCanvas.ActualWidth;
            _haftalikH = HaftalikCanvas.ActualHeight;
            if (_haftalikW <= 0 || _haftalikH <= 0) return;

            double padTop = 8, padBottom = 12, padRight = 28;
            _haftalikChartH = _haftalikH - padTop - padBottom;
            if (_haftalikChartH <= 0) return;

            _haftalikMax = Math.Max(1, _haftalikVeri.Max(v => v.MacSayisi));
            _haftalikCubukW = (_haftalikW - padRight) / 7.0;

            // Y ekseni
            int yCizgiAdet = 4;
            for (int i = 0; i <= yCizgiAdet; i++)
            {
                double y = padTop + _haftalikChartH - (i / (double)yCizgiAdet) * _haftalikChartH;
                HaftalikCanvas.Children.Add(new Line
                {
                    X1 = 0, Y1 = y, X2 = _haftalikW - padRight, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
                    StrokeThickness = 1
                });
                var lbl = new TextBlock
                {
                    Text = Math.Round(_haftalikMax * i / (double)yCizgiAdet).ToString(),
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255))
                };
                Canvas.SetLeft(lbl, _haftalikW - padRight + 4);
                Canvas.SetTop(lbl, y - 6);
                HaftalikCanvas.Children.Add(lbl);
            }

            double icCubukW = (_haftalikCubukW - 4) / 2;
            int animIdx = 0;

            for (int i = 0; i < 7; i++)
            {
                // Beyaz cubuk (oynanan)
                double cubukH = (_haftalikVeri[i].MacSayisi / (double)_haftalikMax) * _haftalikChartH;
                if (cubukH < 0.5) cubukH = 0.5;
                var rectBeyaz = new Rectangle
                {
                    Width = icCubukW,
                    Height = cubukH,
                    Fill = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    RadiusX = 2, RadiusY = 2,
                    Tag = animIdx++
                };
                Canvas.SetLeft(rectBeyaz, i * _haftalikCubukW + 1);
                Canvas.SetTop(rectBeyaz, padTop + _haftalikChartH - cubukH);
                HaftalikCanvas.Children.Add(rectBeyaz);

                // Yesil cubuk (galibiyet)
                double galH = (_haftalikVeri[i].Galibiyet / (double)_haftalikMax) * _haftalikChartH;
                if (galH < 0.5) galH = 0.5;
                var rectYesil = new Rectangle
                {
                    Width = icCubukW,
                    Height = galH,
                    Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0x6A)),
                    RadiusX = 2, RadiusY = 2,
                    Tag = animIdx++
                };
                Canvas.SetLeft(rectYesil, i * _haftalikCubukW + 1 + icCubukW + 2);
                Canvas.SetTop(rectYesil, padTop + _haftalikChartH - galH);
                HaftalikCanvas.Children.Add(rectYesil);
            }

            // Animasyon: cubuklar asagidan yukari 30ms stagger
            var easeH = new CubicEase { EasingMode = EasingMode.EaseOut };
            for (int i = 0; i < HaftalikCanvas.Children.Count; i++)
            {
                if (HaftalikCanvas.Children[i] is Rectangle r && r.Tag is int tag)
                {
                    double targetH = r.Height;
                    r.Height = 0;
                    var anim = new DoubleAnimation(0, targetH, new Duration(TimeSpan.FromMilliseconds(400)))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(tag * 30),
                        EasingFunction = easeH
                    };
                    r.BeginAnimation(FrameworkElement.HeightProperty, anim);
                }
            }
        }

        private Border GetOrCreateHaftalikTooltip()
        {
            if (_haftalikTooltip == null)
            {
                _haftalikTooltip = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(230, 26, 26, 26)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                    BorderThickness = new Thickness(1),
                    IsHitTestVisible = false,
                    Opacity = 0
                };
                var sp = new StackPanel();
                _haftalikTooltipGun = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                _haftalikTooltipOynanan = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)), Margin = new Thickness(0, 2, 0, 0) };
                _haftalikTooltipKazanc = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)) };
                _haftalikTooltipYuzde = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0x6A)), Margin = new Thickness(0, 2, 0, 0) };
                sp.Children.Add(_haftalikTooltipGun);
                sp.Children.Add(_haftalikTooltipOynanan);
                sp.Children.Add(_haftalikTooltipKazanc);
                sp.Children.Add(_haftalikTooltipYuzde);
                _haftalikTooltip.Child = sp;
            }
            return _haftalikTooltip;
        }

        private Line _haftalikHoverLine;

        private void HaftalikCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_haftalikVeri == null || _haftalikW <= 0) return;
            var pos = e.GetPosition(HaftalikCanvas);
            int idx = (int)(pos.X / _haftalikCubukW);
            if (idx < 0 || idx >= 7) { HaftalikHoverTemizle(); return; }

            var veri = _haftalikVeri[idx];
            var gunAdlari = new[] { "Pazartesi", "Sali", "Carsamba", "Persembe", "Cuma", "Cumartesi", "Pazar" };
            double x = idx * _haftalikCubukW + _haftalikCubukW / 2;

            if (_haftalikHoverLine == null)
            {
                _haftalikHoverLine = new Line
                {
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    IsHitTestVisible = false
                };
                Canvas.SetZIndex(_haftalikHoverLine, 10);
                HaftalikCanvas.Children.Add(_haftalikHoverLine);
            }
            _haftalikHoverLine.X1 = x; _haftalikHoverLine.Y1 = 0;
            _haftalikHoverLine.X2 = x; _haftalikHoverLine.Y2 = _haftalikH;

            var tip = GetOrCreateHaftalikTooltip();
            _haftalikTooltipGun.Text = gunAdlari[idx];
            _haftalikTooltipOynanan.Text = $"Oynanan: {veri.MacSayisi} mac";
            _haftalikTooltipKazanc.Text = $"Kazanilan: {veri.Galibiyet} mac";
            _haftalikTooltipYuzde.Text = $"Kazanma: %{veri.KazanmaYuzdesi:F1}";
            tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double tw = tip.DesiredSize.Width;
            double tx = x - tw / 2;
            if (tx < 0) tx = 4;
            if (tx + tw > _haftalikW) tx = _haftalikW - tw - 4;
            double ty = _haftalikH - tip.DesiredSize.Height - 8;

            if (!HaftalikCanvas.Children.Contains(tip))
            {
                Canvas.SetZIndex(tip, 20);
                HaftalikCanvas.Children.Add(tip);
            }
            Canvas.SetLeft(tip, tx);
            Canvas.SetTop(tip, ty);
            tip.BeginAnimation(UIElement.OpacityProperty, null);
            tip.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }

        private void HaftalikCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            HaftalikHoverTemizle();
        }

        private void HaftalikHoverTemizle()
        {
            if (_haftalikTooltip != null && HaftalikCanvas.Children.Contains(_haftalikTooltip))
                HaftalikCanvas.Children.Remove(_haftalikTooltip);
            if (_haftalikHoverLine != null && HaftalikCanvas.Children.Contains(_haftalikHoverLine))
                HaftalikCanvas.Children.Remove(_haftalikHoverLine);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AKTIVITE TAKVIMI
        // ════════════════════════════════════════════════════════════════════════

        private void TakvimCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawTakvim();
        private void TakvimAyCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawTakvimAyBasliklari();

        private void DrawTakvim()
        {
            if (TakvimCanvas == null || VM == null) return;
            TakvimCanvas.Children.Clear();

            var haftalar = VM.TakvimHaftalari?.ToList();
            if (haftalar == null || !haftalar.Any()) return;

            const double cellSize = 12;
            const double gap = 3;
            const double step = cellSize + gap;
            const double monthGap = 8;

            _takvimHucreler = new List<(Rectangle Rect, AktiviteHucre Veri, int HaftaIdx, int GunIdx)>();

            // Build day dictionary from flat week data
            var gunMap = new Dictionary<DateTime, AktiviteHucre>();
            foreach (var hafta in haftalar)
            {
                foreach (var hucre in hafta.Gunler)
                {
                    if (hucre == null) continue;
                    gunMap[new DateTime(hucre.Yil, hucre.Ay, hucre.Gun)] = hucre;
                }
            }

            var bugun = DateTime.Today;
            var yil = bugun.Year;
            double xOffset = 0;
            int globalWeek = 0;

            for (int ay = 1; ay <= bugun.Month; ay++)
            {
                var ayIlk = new DateTime(yil, ay, 1);
                var aySon = new DateTime(yil, ay, DateTime.DaysInMonth(yil, ay));

                int diff1 = (int)ayIlk.DayOfWeek == 0 ? 6 : (int)ayIlk.DayOfWeek - 1;
                var ilkPzt = ayIlk.AddDays(-diff1);

                int diff2 = (int)aySon.DayOfWeek == 0 ? 0 : 7 - (int)aySon.DayOfWeek;
                var sonPaz = aySon.AddDays(diff2);

                int haftaSayisi = (int)((sonPaz - ilkPzt).TotalDays / 7) + 1;

                for (int h = 0; h < haftaSayisi; h++)
                {
                    double x = xOffset + h * step;
                    for (int g = 0; g < 7; g++)
                    {
                        var gun = ilkPzt.AddDays(h * 7 + g);
                        if (gun.Year != yil) continue;
                        if (gun > bugun) continue;

                        if (!gunMap.TryGetValue(gun, out var hucre))
                        {
                            hucre = new AktiviteHucre
                            {
                                Yil = gun.Year, Ay = gun.Month, Gun = gun.Day,
                                MacSayisi = 0, Galibiyet = 0,
                                TarihText = gun.ToString("dd MMM")
                            };
                        }

                        Color renk = hucre.MacSayisi switch
                        {
                            0 => Color.FromRgb(0x2a, 0x2a, 0x2a),
                            1 or 2 => Color.FromRgb(0x4a, 0x1a, 0x6b),
                            3 or 4 => Color.FromRgb(0x8b, 0x2f, 0xc9),
                            _ => Color.FromRgb(0xd9, 0x46, 0xef)
                        };

                        var rect = new Rectangle
                        {
                            Width = cellSize,
                            Height = cellSize,
                            Fill = new SolidColorBrush(renk),
                            RadiusX = 2.5,
                            RadiusY = 2.5,
                            Opacity = 0
                        };
                        Canvas.SetLeft(rect, x);
                        Canvas.SetTop(rect, g * step);
                        TakvimCanvas.Children.Add(rect);

                        _takvimHucreler.Add((rect, hucre, globalWeek, g));
                    }
                    globalWeek++;
                }

                xOffset += haftaSayisi * step + monthGap;
            }

            double totalW = xOffset - monthGap + step;
            if (totalW > TakvimCanvas.ActualWidth)
                TakvimCanvas.Width = totalW;

            // Animasyon: ay bazında kademeli
            for (int i = 0; i < _takvimHucreler.Count; i++)
            {
                var (rect, _, w, _) = _takvimHucreler[i];
                var anim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)))
                {
                    BeginTime = TimeSpan.FromMilliseconds(w * 15),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                rect.BeginAnimation(UIElement.OpacityProperty, anim);
            }
        }

        private List<(Rectangle Rect, AktiviteHucre Veri, int HaftaIdx, int GunIdx)> _takvimHucreler;
        private Border _takvimTooltip;
        private TextBlock _takvimTooltipTarih, _takvimTooltipOynanan, _takvimTooltipKazanc, _takvimTooltipYuzde;

        private void DrawTakvimAyBasliklari()
        {
            if (TakvimAyCanvas == null || VM == null) return;
            TakvimAyCanvas.Children.Clear();

            var bugun = DateTime.Today;
            var yil = bugun.Year;
            const double cellSize = 12;
            const double gap = 3;
            const double step = cellSize + gap;
            const double monthGap = 8;

            string[] ayKisalt = { "", "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };

            double xOffset = 0;

            for (int ay = 1; ay <= bugun.Month; ay++)
            {
                var ayIlk = new DateTime(yil, ay, 1);
                var aySon = new DateTime(yil, ay, DateTime.DaysInMonth(yil, ay));

                int diff1 = (int)ayIlk.DayOfWeek == 0 ? 6 : (int)ayIlk.DayOfWeek - 1;
                var ilkPzt = ayIlk.AddDays(-diff1);

                int diff2 = (int)aySon.DayOfWeek == 0 ? 0 : 7 - (int)aySon.DayOfWeek;
                var sonPaz = aySon.AddDays(diff2);

                int haftaSayisi = (int)((sonPaz - ilkPzt).TotalDays / 7) + 1;

                var lbl = new TextBlock
                {
                    Text = ayKisalt[ay],
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66))
                };
                Canvas.SetLeft(lbl, xOffset);
                Canvas.SetTop(lbl, 0);
                TakvimAyCanvas.Children.Add(lbl);

                xOffset += haftaSayisi * step + monthGap;
            }

            double totalW = xOffset - monthGap + step;
            if (totalW > TakvimAyCanvas.ActualWidth)
                TakvimAyCanvas.Width = totalW;
        }

        private Border GetOrCreateTakvimTooltip()
        {
            if (_takvimTooltip == null)
            {
                _takvimTooltip = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(230, 26, 26, 26)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                    BorderThickness = new Thickness(1),
                    IsHitTestVisible = false,
                    Opacity = 0
                };
                var sp = new StackPanel();
                _takvimTooltipTarih = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                _takvimTooltipOynanan = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)), Margin = new Thickness(0, 2, 0, 0) };
                _takvimTooltipKazanc = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)) };
                _takvimTooltipYuzde = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0x6A)), Margin = new Thickness(0, 2, 0, 0) };
                sp.Children.Add(_takvimTooltipTarih);
                sp.Children.Add(_takvimTooltipOynanan);
                sp.Children.Add(_takvimTooltipKazanc);
                sp.Children.Add(_takvimTooltipYuzde);
                _takvimTooltip.Child = sp;
            }
            return _takvimTooltip;
        }

        private void TakvimCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_takvimHucreler == null) return;
            var pos = e.GetPosition(TakvimCanvas);

            foreach (var (rect, veri, haftaIdx, gunIdx) in _takvimHucreler)
            {
                double rx = Canvas.GetLeft(rect);
                double ry = Canvas.GetTop(rect);
                if (pos.X >= rx && pos.X <= rx + HucreBoy && pos.Y >= ry && pos.Y <= ry + HucreBoy)
                {
                    TakvimTooltipKapat();
                    var tip = GetOrCreateTakvimTooltip();
                    var tarih = new DateTime(veri.Yil, veri.Ay, veri.Gun);
                    _takvimTooltipTarih.Text = tarih.ToString("dd MMM yyyy");
                    _takvimTooltipOynanan.Text = $"Oynanan: {veri.MacSayisi} mac";
                    _takvimTooltipKazanc.Text = $"Kazanilan: {veri.Galibiyet} mac";
                    _takvimTooltipYuzde.Text = $"Kazanma: %{veri.KazanmaYuzdesi:F1}";
                    tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                    double tw = tip.DesiredSize.Width;
                    double tx = rx + HucreBoy + 6;
                    if (tx + tw > TakvimCanvas.ActualWidth) tx = rx - tw - 6;
                    double ty = ry - tip.DesiredSize.Height / 2 + HucreBoy / 2;
                    if (ty < 0) ty = 0;
                    if (ty + tip.DesiredSize.Height > TakvimCanvas.ActualHeight)
                        ty = TakvimCanvas.ActualHeight - tip.DesiredSize.Height;

                    if (!TakvimCanvas.Children.Contains(tip))
                    {
                        Canvas.SetZIndex(tip, 30);
                        TakvimCanvas.Children.Add(tip);
                    }
                    Canvas.SetLeft(tip, tx);
                    Canvas.SetTop(tip, ty);
                    tip.BeginAnimation(UIElement.OpacityProperty, null);
                    tip.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
                    return;
                }
            }
            TakvimTooltipKapat();
        }

        private void TakvimCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TakvimTooltipKapat();
        }

        private void TakvimTooltipKapat()
        {
            if (_takvimTooltip != null && TakvimCanvas.Children.Contains(_takvimTooltip))
            {
                _takvimTooltip.BeginAnimation(UIElement.OpacityProperty, null);
                _takvimTooltip.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(100)));
                TakvimCanvas.Children.Remove(_takvimTooltip);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // HARITA RADAR
        // ════════════════════════════════════════════════════════════════════════

        private void HaritaRadarCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
            => DrawHaritaRadarGrafik();

        private void DrawHaritaRadarGrafik()
        {
            var HaritaRadarCanvas = FindName("HaritaRadarCanvas") as Canvas;
            if (HaritaRadarCanvas == null || VM == null) return;
            HaritaRadarCanvas.Children.Clear();

            var veriler = VM.HaritaAnalizleri?.Where(v => v.OynanmaSayisi > 0).Take(5).ToList();
            if (veriler == null || veriler.Count < 3) return;

            double w = HaritaRadarCanvas.ActualWidth;
            double h = HaritaRadarCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double cx = w / 2;
            double cy = h / 2 - 6;
            double radius = Math.Min(w, h) / 2 - 42;
            if (radius < 30) return;

            int n = veriler.Count;
            var angles = Enumerable.Range(0, n)
                .Select(i => -Math.PI / 2 + i * (2 * Math.PI / n))
                .ToList();

            var gridPcts = new[] { 20, 40, 60, 80, 100 };
            var gridBrush = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));

            foreach (var pct in gridPcts)
            {
                double r = radius * pct / 100.0;
                var pts = new PointCollection();
                foreach (var a in angles)
                    pts.Add(new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a)));

                HaritaRadarCanvas.Children.Add(new Polygon
                {
                    Points = pts,
                    Stroke = gridBrush,
                    StrokeThickness = 1
                });
            }

            var axisBrush = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255));
            foreach (var a in angles)
            {
                double x = cx + radius * Math.Cos(a);
                double y = cy + radius * Math.Sin(a);
                HaritaRadarCanvas.Children.Add(new Line
                {
                    X1 = cx, Y1 = cy, X2 = x, Y2 = y,
                    Stroke = axisBrush,
                    StrokeThickness = 1
                });
            }

            var pctLabelBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            foreach (var pct in gridPcts)
            {
                double r = radius * pct / 100.0;
                double lx = cx + r * Math.Cos(angles[0]) + 4;
                double ly = cy + r * Math.Sin(angles[0]) - 5;
                HaritaRadarCanvas.Children.Add(new TextBlock
                {
                    Text = $"{pct}%",
                    FontSize = 8,
                    Foreground = pctLabelBrush
                });
                var tb = HaritaRadarCanvas.Children[HaritaRadarCanvas.Children.Count - 1] as TextBlock;
                if (tb != null) { Canvas.SetLeft(tb, lx); Canvas.SetTop(tb, ly); }
            }

            var winPoints = new List<Point>();
            var playPoints = new List<Point>();
            double maxOynanmaOran = veriler.Max(v => v.OynanmaOrani);
            if (maxOynanmaOran <= 0) maxOynanmaOran = 1;

            for (int i = 0; i < n; i++)
            {
                var v = veriler[i];
                double winR = v.GalibiyetOrani / 100.0;
                double playR = v.OynanmaOrani / maxOynanmaOran;
                double a = angles[i];

                winPoints.Add(new Point(cx + radius * winR * Math.Cos(a), cy + radius * winR * Math.Sin(a)));
                playPoints.Add(new Point(cx + radius * playR * Math.Cos(a), cy + radius * playR * Math.Sin(a)));
            }

            if (winPoints.Count >= 3)
            {
                var winPoly = new Polygon
                {
                    Points = new PointCollection(winPoints),
                    Fill = new SolidColorBrush(Color.FromArgb(38, 0x00, 0xFF, 0x00)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x00)),
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Miter
                };
                HaritaRadarCanvas.Children.Add(winPoly);
            }

            if (playPoints.Count >= 3)
            {
                var playPoly = new Polygon
                {
                    Points = new PointCollection(playPoints),
                    Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Miter
                };
                HaritaRadarCanvas.Children.Add(playPoly);

                var dotBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                foreach (var pt in playPoints)
                {
                    HaritaRadarCanvas.Children.Add(new Ellipse
                    {
                        Width = 4, Height = 4,
                        Fill = dotBrush
                    });
                    var dot = HaritaRadarCanvas.Children[HaritaRadarCanvas.Children.Count - 1] as Ellipse;
                    if (dot != null) { Canvas.SetLeft(dot, pt.X - 2); Canvas.SetTop(dot, pt.Y - 2); }
                }
            }

            for (int i = 0; i < n; i++)
            {
                double labelR = radius + 20;
                double lx = cx + labelR * Math.Cos(angles[i]);
                double ly = cy + labelR * Math.Sin(angles[i]);

                var lbl = new TextBlock
                {
                    Text = veriler[i].HaritaAdi,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77))
                };
                lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double tw = lbl.DesiredSize.Width;
                double th = lbl.DesiredSize.Height;

                double a = angles[i];
                if (a > -Math.PI / 4 && a <= Math.PI / 4)
                    { Canvas.SetLeft(lbl, lx + 4); Canvas.SetTop(lbl, ly - th / 2); }
                else if (a > Math.PI / 4 && a <= 3 * Math.PI / 4)
                    { Canvas.SetLeft(lbl, lx - tw / 2); Canvas.SetTop(lbl, ly + 2); }
                else if (a > -3 * Math.PI / 4 && a <= -Math.PI / 4)
                    { Canvas.SetLeft(lbl, lx - tw / 2); Canvas.SetTop(lbl, ly - th - 2); }
                else
                    { Canvas.SetLeft(lbl, lx - tw - 4); Canvas.SetTop(lbl, ly - th / 2); }

                HaritaRadarCanvas.Children.Add(lbl);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // ADR GRAFIK
        // ════════════════════════════════════════════════════════════════════════

        private void AdrGrafikCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawAdrGrafik();

        private void DrawAdrGrafik()
        {
            var AdrGrafikCanvas = FindName("AdrGrafikCanvas") as Canvas;
            if (AdrGrafikCanvas == null || VM == null) return;
            AdrGrafikCanvas.Children.Clear();

            var noktalar = VM.AdrDetay?.AdrNoktalari;
            if (noktalar == null || noktalar.Count < 2) return;

            double w = AdrGrafikCanvas.ActualWidth;
            double h = AdrGrafikCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double maxAdr = noktalar.Max(n => n.Adr);
            double minAdr = noktalar.Min(n => n.Adr);
            double range = Math.Max(maxAdr - minAdr, 10);
            double padMin = Math.Max(0, minAdr - range * 0.1);
            double padMax = maxAdr + range * 0.1;
            if (padMax <= padMin) padMax = padMin + 10;

            double stepX = w / Math.Max(noktalar.Count - 1, 1);

            var pts = noktalar.Select((n, i) => new Point(
                i * stepX,
                h - ((n.Adr - padMin) / (padMax - padMin)) * (h - 4)
            )).ToList();

            var dolguPts = pts.ToList();
            dolguPts.Add(new Point(pts.Last().X, h));
            dolguPts.Add(new Point(pts.First().X, h));
            var dolgu = new Polygon
            {
                Points = new PointCollection(dolguPts),
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(60, 0xFF, 0x8C, 0x00), 0.0),
                        new GradientStop(Color.FromArgb(10, 0xFF, 0x8C, 0x00), 1.0)
                    }
                }
            };
            AdrGrafikCanvas.Children.Add(dolgu);

            for (int i = 0; i < pts.Count - 1; i++)
            {
                AdrGrafikCanvas.Children.Add(new Line
                {
                    X1 = pts[i].X, Y1 = pts[i].Y,
                    X2 = pts[i + 1].X, Y2 = pts[i + 1].Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)),
                    StrokeThickness = 2
                });
            }

            for (int i = 0; i < pts.Count; i++)
            {
                var dot = new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00))
                };
                Canvas.SetLeft(dot, pts[i].X - 2);
                Canvas.SetTop(dot, pts[i].Y - 2);
                AdrGrafikCanvas.Children.Add(dot);
            }

            for (int i = 0; i <= 4; i++)
            {
                double ratio = i / 4.0;
                double y = h - ratio * (h - 4);
                double adrVal = padMin + (padMax - padMin) * ratio;
                AdrGrafikCanvas.Children.Add(new Line
                {
                    X1 = 0, Y1 = y, X2 = w, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 3 }
                });
                var lbl = new TextBlock
                {
                    Text = $"{(int)adrVal}",
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255))
                };
                Canvas.SetLeft(lbl, 2);
                Canvas.SetTop(lbl, y - 6);
                AdrGrafikCanvas.Children.Add(lbl);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // CANLI MAC
        // ════════════════════════════════════════════════════════════════════════

        private void SikayetEt_Tikla(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MessageBox.Show("Yakinda", "Sikayet Et", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void CanliMacBaslik_Tikla(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (VM == null) return;
            await VM.YukleCanliMacAsync();
            if (!VM.CanliMacVisible)
                MessageBox.Show("Su an mac oynamiyorsun.", "Canli Mac", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CanliMacAnimasyonuBaslat()
        {
            if (VM == null || !VM.CanliMacVisible) return;

            var canliNokta    = FindName("CanliNokta")    as UIElement;
            var benimTakim    = FindName("BenimTakimPanel")  as FrameworkElement;
            var rakipTakim    = FindName("RakipTakimPanel")  as FrameworkElement;

            var pulse = new DoubleAnimation(0.2, 1.0, new Duration(TimeSpan.FromMilliseconds(800)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            if (canliNokta != null)
            {
                canliNokta.BeginAnimation(UIElement.OpacityProperty, null);
                canliNokta.BeginAnimation(UIElement.OpacityProperty, pulse);
            }

            if (benimTakim != null)
            {
                benimTakim.BeginAnimation(UIElement.OpacityProperty, null);
                benimTakim.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(400))) { BeginTime = TimeSpan.Zero });
            }
            if (rakipTakim != null)
            {
                rakipTakim.BeginAnimation(UIElement.OpacityProperty, null);
                rakipTakim.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(400))) { BeginTime = TimeSpan.FromMilliseconds(200) });
            }

            Dispatcher.InvokeAsync(CanliMacStaggerAnimasyonu, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void CanliMacStaggerAnimasyonu()
        {
            if (VM == null) return;

            var benimTakim = FindName("BenimTakimPanel") as FrameworkElement;
            var rakipTakim = FindName("RakipTakimPanel") as FrameworkElement;

            var benimItems = benimTakim is StackPanel benimSP
                ? GetItemsControlChildren(benimSP)
                : Array.Empty<ContentPresenter>();
            var rakipItems = rakipTakim is StackPanel rakipSP
                ? GetItemsControlChildren(rakipSP)
                : Array.Empty<ContentPresenter>();

            int index = 0;
            foreach (var item in benimItems)
            {
                var border = OyuncuKartBul(item);
                if (border == null) continue;

                border.RenderTransformOrigin = new Point(0.5, 0.5);
                border.Opacity = 0;
                var anim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)))
                {
                    BeginTime = TimeSpan.FromMilliseconds(index * 60)
                };
                border.BeginAnimation(UIElement.OpacityProperty, anim);

                var translate = new TranslateTransform(-15, 0);
                border.RenderTransform = translate;
                var slideAnim = new DoubleAnimation(-15, 0, new Duration(TimeSpan.FromMilliseconds(350)))
                {
                    BeginTime = TimeSpan.FromMilliseconds(index * 60),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                translate.BeginAnimation(TranslateTransform.XProperty, slideAnim);

                index++;
            }

            index = 0;
            foreach (var item in rakipItems)
            {
                var border = OyuncuKartBul(item);
                if (border == null) continue;

                border.RenderTransformOrigin = new Point(0.5, 0.5);
                border.Opacity = 0;
                var anim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)))
                {
                    BeginTime = TimeSpan.FromMilliseconds(index * 60 + 200)
                };
                border.BeginAnimation(UIElement.OpacityProperty, anim);

                var translate = new TranslateTransform(15, 0);
                border.RenderTransform = translate;
                var slideAnim = new DoubleAnimation(15, 0, new Duration(TimeSpan.FromMilliseconds(350)))
                {
                    BeginTime = TimeSpan.FromMilliseconds(index * 60 + 200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                translate.BeginAnimation(TranslateTransform.XProperty, slideAnim);

                index++;
            }
        }

        private static ContentPresenter[] GetItemsControlChildren(StackPanel panel)
        {
            var itemsControl = panel.Children.OfType<ItemsControl>().FirstOrDefault();
            if (itemsControl == null) return Array.Empty<ContentPresenter>();

            var gen = itemsControl.ItemContainerGenerator;
            var list = new List<ContentPresenter>();
            int count = gen.Items.Count;
            for (int i = 0; i < count; i++)
            {
                if (gen.ContainerFromIndex(i) is ContentPresenter cp)
                    list.Add(cp);
            }
            return list.ToArray();
        }

        private static Border OyuncuKartBul(ContentPresenter presenter)
        {
            if (presenter?.ContentTemplate == null) return null;
            return presenter.ContentTemplate.FindName("OyuncuKart", presenter) as Border;
        }
    }
}
