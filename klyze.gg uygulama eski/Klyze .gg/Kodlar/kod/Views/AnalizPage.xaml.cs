using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.ViewModels;

namespace ValorantAutoClicker.Views
{
    public partial class AnalizPage : UserControl
    {
        private AnalizViewModel VM => DataContext as AnalizViewModel;

        // Hucre boyutu (takvim)
        private const double HucreBoy = 11;
        private const double HucreAra = 2;

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
            if (e.OldValue is AnalizViewModel oldVm)
            {
                oldVm.GrafikCizilecek         -= OnGrafikCizilecek;
                oldVm.EloGrafikCizilecek      -= OnEloGrafikCizilecek;
                oldVm.AktiviteGrafikCizilecek -= OnAktiviteGrafikCizilecek;
            }
            if (e.NewValue is AnalizViewModel newVm)
            {
                newVm.GrafikCizilecek         += OnGrafikCizilecek;
                newVm.EloGrafikCizilecek      += OnEloGrafikCizilecek;
                newVm.AktiviteGrafikCizilecek += OnAktiviteGrafikCizilecek;
            }
            if (VM != null && !VM.HasData && !VM.IsLoading && !VM.HasError)
                await VM.YukleAsync();
        }

        private void OnGrafikCizilecek() { }

        private void OnEloGrafikCizilecek()
            => Dispatcher.Invoke(() => { DrawEloGrafik(); DrawEloXEksen(); });

        private void OnAktiviteGrafikCizilecek()
            => Dispatcher.Invoke(() =>
            {
                DrawSaatlikGrafik();
                DrawHaftalikGrafik();
                DrawTakvim();
                DrawTakvimAyBasliklari();
            });

        // ════════════════════════════════════════════════════════════════════════
        // ELO GRAFİK
        // ════════════════════════════════════════════════════════════════════════

        private void EloCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawEloGrafik();
        private void EloXEksen_SizeChanged(object sender, SizeChangedEventArgs e) => DrawEloXEksen();

        private void DrawEloGrafik()
        {
            if (EloCanvas == null || VM == null) return;
            EloCanvas.Children.Clear();

            var noktalar = VM.EloGrafikNoktalari?.ToList();
            if (noktalar == null || noktalar.Count < 2) return;

            double w = EloCanvas.ActualWidth;
            double h = EloCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            int minElo = noktalar.Min(n => n.Elo);
            int maxElo = noktalar.Max(n => n.Elo);
            int range  = Math.Max(maxElo - minElo, 100);
            int padMin = minElo - (int)(range * 0.1);
            int padMax = maxElo + (int)(range * 0.1);
            if (padMax <= padMin) padMax = padMin + 100;

            double stepX = w / Math.Max(noktalar.Count - 1, 1);

            var pts = noktalar.Select((n, i) => new Point(
                i * stepX,
                h - ((n.Elo - padMin) / (double)(padMax - padMin)) * (h - 8) - 4
            )).ToList();

            // Dolgu
            var dolguPts = pts.ToList();
            dolguPts.Add(new Point(pts.Last().X, h));
            dolguPts.Add(new Point(pts.First().X, h));
            var dolgu = new Polygon
            {
                Points = new PointCollection(dolguPts),
                Stroke = null,
                Opacity = 0,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint   = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(120, 0xFF, 0x6B, 0x00), 0.0),
                        new GradientStop(Color.FromArgb(40,  0xFF, 0x30, 0x00), 0.5),
                        new GradientStop(Color.FromArgb(0,   0xFF, 0x20, 0x00), 1.0)
                    }
                }
            };
            EloCanvas.Children.Add(dolgu);

            // Cizgi
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var line = new Line
                {
                    X1 = pts[i].X, Y1 = pts[i].Y,
                    X2 = pts[i+1].X, Y2 = pts[i+1].Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x00)),
                    StrokeThickness = 2,
                    Opacity = 0
                };
                EloCanvas.Children.Add(line);
                line.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(250)))
                    { BeginTime = TimeSpan.FromMilliseconds(i * 30) });
            }

            dolgu.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(400)))
                { BeginTime = TimeSpan.FromMilliseconds(pts.Count * 30) });

            // Noktalar
            foreach (var pt in pts)
            {
                var dot = new Ellipse { Width = 5, Height = 5, Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)) };
                Canvas.SetLeft(dot, pt.X - 2.5);
                Canvas.SetTop(dot,  pt.Y - 2.5);
                EloCanvas.Children.Add(dot);
            }

            // Kilavuz + ELO etiketleri
            for (int i = 0; i <= 4; i++)
            {
                double ratio = i / 4.0;
                int    elo   = padMin + (int)((padMax - padMin) * ratio);
                double y     = h - ratio * (h - 8) - 4;
                var lbl = new TextBlock { Text = elo.ToString(), FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)) };
                Canvas.SetLeft(lbl, 2); Canvas.SetTop(lbl, y - 8);
                EloCanvas.Children.Add(lbl);
                EloCanvas.Children.Add(new Line
                {
                    X1 = 0, Y1 = y, X2 = w, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 4 }
                });
            }
        }

        private void DrawEloXEksen()
        {
            if (EloXEksen == null || VM == null) return;
            EloXEksen.Children.Clear();
            var noktalar = VM.EloGrafikNoktalari?.ToList();
            if (noktalar == null || noktalar.Count < 2) return;
            double w = EloXEksen.ActualWidth;
            if (w <= 0) return;
            double stepX = w / Math.Max(noktalar.Count - 1, 1);
            int adim = Math.Max(1, noktalar.Count / 5);
            for (int i = 0; i < noktalar.Count; i += adim)
            {
                var lbl = new TextBlock { Text = noktalar[i].MacIndex.ToString(), FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)) };
                Canvas.SetLeft(lbl, i * stepX - 6);
                Canvas.SetTop(lbl, 0);
                EloXEksen.Children.Add(lbl);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // SAATLİK GRAFİK (çubuk + çizgi)
        // ════════════════════════════════════════════════════════════════════════

        private void SaatlikCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawSaatlikGrafik();

        private void DrawSaatlikGrafik()
        {
            if (SaatlikCanvas == null || VM == null) return;
            SaatlikCanvas.Children.Clear();

            var veri = VM.SaatlikAktiviteler?.ToList();
            if (veri == null || !veri.Any()) return;

            double w = SaatlikCanvas.ActualWidth;
            double h = SaatlikCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            int maxMac = Math.Max(1, veri.Max(v => v.MacSayisi));
            double cubukW = w / 24.0;

            // Çubuklar (beyaz — oynanan maçlar)
            for (int i = 0; i < 24; i++)
            {
                double cubukH = (veri[i].MacSayisi / (double)maxMac) * (h - 10);
                if (cubukH < 1) cubukH = 1;
                var rect = new Rectangle
                {
                    Width  = Math.Max(1, cubukW - 2),
                    Height = cubukH,
                    Fill   = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    RadiusX = 2, RadiusY = 2
                };
                Canvas.SetLeft(rect, i * cubukW + 1);
                Canvas.SetTop(rect,  h - cubukH);
                SaatlikCanvas.Children.Add(rect);
            }

            // Çizgi (yeşil — galibiyetler)
            var galPts = veri.Select((v, i) => new Point(
                i * cubukW + cubukW / 2,
                h - (v.Galibiyet / (double)maxMac) * (h - 10) - 5
            )).ToList();

            for (int i = 0; i < galPts.Count - 1; i++)
            {
                SaatlikCanvas.Children.Add(new Line
                {
                    X1 = galPts[i].X, Y1 = galPts[i].Y,
                    X2 = galPts[i+1].X, Y2 = galPts[i+1].Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0x6A)),
                    StrokeThickness = 1.5
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // HAFTALIK GRAFİK (çubuk + çizgi)
        // ════════════════════════════════════════════════════════════════════════

        private void HaftalikCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawHaftalikGrafik();

        private void DrawHaftalikGrafik()
        {
            if (HaftalikCanvas == null || VM == null) return;
            HaftalikCanvas.Children.Clear();

            var veri = VM.HaftalikAktiviteler?.ToList();
            if (veri == null || !veri.Any()) return;

            double w = HaftalikCanvas.ActualWidth;
            double h = HaftalikCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            int maxMac = Math.Max(1, veri.Max(v => v.MacSayisi));
            double cubukW = w / 7.0;

            // Çubuklar
            for (int i = 0; i < 7; i++)
            {
                double cubukH = (veri[i].MacSayisi / (double)maxMac) * (h - 10);
                if (cubukH < 1) cubukH = 1;
                var rect = new Rectangle
                {
                    Width  = Math.Max(1, cubukW - 4),
                    Height = cubukH,
                    Fill   = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    RadiusX = 2, RadiusY = 2
                };
                Canvas.SetLeft(rect, i * cubukW + 2);
                Canvas.SetTop(rect,  h - cubukH);
                HaftalikCanvas.Children.Add(rect);
            }

            // Çizgi (yeşil)
            var galPts = veri.Select((v, i) => new Point(
                i * cubukW + cubukW / 2,
                h - (v.Galibiyet / (double)maxMac) * (h - 10) - 5
            )).ToList();

            for (int i = 0; i < galPts.Count - 1; i++)
            {
                HaftalikCanvas.Children.Add(new Line
                {
                    X1 = galPts[i].X, Y1 = galPts[i].Y,
                    X2 = galPts[i+1].X, Y2 = galPts[i+1].Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0x6A)),
                    StrokeThickness = 1.5
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // AKTİVİTE TAKVİMİ (GitHub tarzı hücreler)
        // ════════════════════════════════════════════════════════════════════════

        private void TakvimCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawTakvim();
        private void TakvimAyCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawTakvimAyBasliklari();

        private static Color YogunlukRenk(int yogunluk) => yogunluk switch
        {
            1 => Color.FromRgb(0x3D, 0x1A, 0x5C),   // koyu mor — az
            2 => Color.FromRgb(0x7B, 0x2F, 0xBE),   // parlak mor — orta
            3 => Color.FromRgb(0xC8, 0x40, 0xE9),   // parlak mor/pembe — cok
            4 => Color.FromRgb(0xFF, 0x2D, 0x9B),   // fusya — en cok
            _ => Color.FromRgb(0x25, 0x25, 0x25)    // gri — hic
        };

        private void DrawTakvim()
        {
            if (TakvimCanvas == null || VM == null) return;
            TakvimCanvas.Children.Clear();

            var haftalar = VM.TakvimHaftalari?.ToList();
            if (haftalar == null || !haftalar.Any()) return;

            double adim = HucreBoy + HucreAra;

            for (int haftaIdx = 0; haftaIdx < haftalar.Count; haftaIdx++)
            {
                var hafta = haftalar[haftaIdx];
                for (int gunIdx = 0; gunIdx < 7; gunIdx++)
                {
                    var hucre = hafta.Gunler[gunIdx];
                    if (hucre == null) continue;

                    // 4 seviye: 1=az, 2=orta, 3=cok, 4=fusya (maxMac'e gore)
                    int yogunluk = hucre.Yogunluk;
                    // Fusya: en yüksek yogunluk (3) → bazılarını 4'e yükselt
                    if (yogunluk == 3 && hucre.MacSayisi >= 5) yogunluk = 4;

                    var rect = new Rectangle
                    {
                        Width   = HucreBoy,
                        Height  = HucreBoy,
                        Fill    = new SolidColorBrush(YogunlukRenk(yogunluk)),
                        RadiusX = 2,
                        RadiusY = 2,
                        ToolTip = $"{hucre.TarihText}: {hucre.MacSayisi} mac"
                    };
                    Canvas.SetLeft(rect, haftaIdx * adim);
                    Canvas.SetTop(rect,  gunIdx   * adim);
                    TakvimCanvas.Children.Add(rect);
                }
            }
        }

        private void DrawTakvimAyBasliklari()
        {
            if (TakvimAyCanvas == null || VM == null) return;
            TakvimAyCanvas.Children.Clear();

            var aylar = VM.TakvimAyBasliklari?.ToList();
            if (aylar == null || !aylar.Any()) return;

            double adim = HucreBoy + HucreAra;

            foreach (var ay in aylar)
            {
                var lbl = new TextBlock
                {
                    Text       = ay.AyAdi,
                    FontSize   = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66))
                };
                Canvas.SetLeft(lbl, ay.SutunPos * adim);
                Canvas.SetTop(lbl,  0);
                TakvimAyCanvas.Children.Add(lbl);
            }
        }
    }
}
