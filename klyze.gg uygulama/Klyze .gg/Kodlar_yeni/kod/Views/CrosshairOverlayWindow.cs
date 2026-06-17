using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace ValorantAutoClicker.Views
{
    public class CrosshairOverlayWindow : Window
    {
        private readonly Canvas _canvas;

        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        public CrosshairOverlayWindow()
        {
            _canvas = new Canvas();
            Content = _canvas;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Left = 0;
            Top = 0;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);

            UpdateCrosshairCore();
        }

        private Models.CrosshairSettings _pendingSettings;

        public void UpdateCrosshair(Models.CrosshairSettings settings)
        {
            _pendingSettings = settings;
            if (IsLoaded) UpdateCrosshairCore();
        }

        private void UpdateCrosshairCore()
        {
            if (_pendingSettings == null) return;
            _canvas.Children.Clear();
            _canvas.Width = Width;
            _canvas.Height = Height;

            var s = _pendingSettings;
            var color = ParseColor(s.Color);
            double cx = Width / 2;
            double cy = Height / 2;
            double maxAllowed = Math.Min(cx, cy) - 4;

            int innerSpan = s.InnerGap + s.InnerLength;
            int outerSpan = s.OuterLines ? s.OuterGap + s.OuterLength : 0;
            int totalSpan = innerSpan + outerSpan;
            int dotSpan = s.CenterDot ? s.CenterDotSize / 2 : 0;
            int needed = Math.Max(totalSpan, dotSpan);
            double scale = needed > maxAllowed ? maxAllowed / needed : 1.0;

            byte alpha = (byte)Math.Clamp(s.Opacity * 255 / 100, 0, 255);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));

            double innerLen = s.InnerLength * scale;
            double innerThick = Math.Max(1, s.InnerThickness * scale);
            double innerGap = s.InnerGap * scale;

            AddLine(cx + innerGap, cy, cx + innerGap + innerLen, cy, innerThick, brush);
            AddLine(cx - innerGap - innerLen, cy, cx - innerGap, cy, innerThick, brush);
            AddLine(cx, cy + innerGap, cx, cy + innerGap + innerLen, innerThick, brush);
            AddLine(cx, cy - innerGap - innerLen, cx, cy - innerGap, innerThick, brush);

            if (s.OuterLines)
            {
                double outerLen = s.OuterLength * scale;
                double outerThick = Math.Max(1, s.OuterThickness * scale);
                double outerGap = s.OuterGap * scale;
                double startGap = innerGap + innerLen + outerGap;

                AddLine(cx + startGap, cy, cx + startGap + outerLen, cy, outerThick, brush);
                AddLine(cx - startGap - outerLen, cy, cx - startGap, cy, outerThick, brush);
                AddLine(cx, cy + startGap, cx, cy + startGap + outerLen, outerThick, brush);
                AddLine(cx, cy - startGap - outerLen, cx, cy - startGap, outerThick, brush);
            }

            if (s.CenterDot)
            {
                double dotSize = Math.Max(1, s.CenterDotSize * scale);
                var dot = new Ellipse
                {
                    Width = dotSize,
                    Height = dotSize,
                    Fill = brush
                };
                Canvas.SetLeft(dot, cx - dotSize / 2.0);
                Canvas.SetTop(dot, cy - dotSize / 2.0);
                _canvas.Children.Add(dot);
            }
        }

        private void AddLine(double x1, double y1, double x2, double y2, double thickness, Brush brush)
        {
            _canvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = brush,
                StrokeThickness = thickness
            });
        }

        private static Color ParseColor(string color)
        {
            return color?.ToLower() switch
            {
                "red" => Color.FromRgb(0xFF, 0x46, 0x55),
                "green" => Color.FromRgb(0x00, 0xFF, 0x41),
                "blue" => Color.FromRgb(0x00, 0xD4, 0xFF),
                "yellow" => Color.FromRgb(0xFF, 0xFF, 0x00),
                "purple" => Color.FromRgb(0xFF, 0x00, 0xFF),
                "white" => Color.FromRgb(0xFF, 0xFF, 0xFF),
                "cyan" => Color.FromRgb(0x00, 0xFF, 0xFF),
                "orange" => Color.FromRgb(0xFF, 0x80, 0x00),
                _ => Color.FromRgb(0xFF, 0x46, 0x55)
            };
        }
    }
}
