using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ValorantAutoClicker.Helpers
{
    /// <summary>
    /// bool (HasWon) → SolidColorBrush dönüştürücü.
    /// True  → AccentGreen (#00D26A)
    /// False → AccentRed   (#FF4655)
    /// </summary>
    public class WinColorConverter : IValueConverter
    {
        public static readonly WinColorConverter Instance = new();

        private static readonly SolidColorBrush Win  = new(Color.FromRgb(0x00, 0xD2, 0x6A));
        private static readonly SolidColorBrush Loss = new(Color.FromRgb(0xFF, 0x46, 0x55));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Win : Loss;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// double (KdRatio) → SolidColorBrush
    /// > 1.0 → AccentGreen
    /// = 1.0 → White
    /// &lt; 1.0 → AccentRed
    /// </summary>
    public class KdColorConverter : IValueConverter
    {
        public static readonly KdColorConverter Instance = new();

        private static readonly SolidColorBrush Positive = new(Color.FromRgb(0x00, 0xD2, 0x6A));
        private static readonly SolidColorBrush Neutral   = new(Colors.White);
        private static readonly SolidColorBrush Negative  = new(Color.FromRgb(0xFF, 0x46, 0x55));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double kd)
            {
                if (kd > 1.0) return Positive;
                if (kd < 1.0) return Negative;
            }
            return Neutral;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// double (WinRate) → SolidColorBrush
    /// > 50 → AccentGreen
    /// = 50 → White
    /// &lt; 50 → AccentRed
    /// </summary>
    public class WinRateColorConverter : IValueConverter
    {
        public static readonly WinRateColorConverter Instance = new();

        private static readonly SolidColorBrush Positive = new(Color.FromRgb(0x00, 0xD2, 0x6A));
        private static readonly SolidColorBrush Neutral   = new(Colors.White);
        private static readonly SolidColorBrush Negative  = new(Color.FromRgb(0xFF, 0x46, 0x55));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double wr)
            {
                if (wr > 50.0) return Positive;
                if (wr < 50.0) return Negative;
            }
            return Neutral;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// string (null/empty) → Visibility.Collapsed, dolu → Visible
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public static readonly StringToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrEmpty(value as string)
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// bool → Visibility (False = Visible, True = Collapsed) — ters çevirici
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public static readonly InverseBoolToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// bool → bool (ters çevirici — IsEnabled için)
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public static readonly InverseBoolConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : true;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
    }

    /// <summary>
    /// Progress bar genişliği hesapla (değer / maksimum * 100%)
    /// </summary>
    public class ProgressConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is double deger && values[1] is double max && max > 0)
            {
                double oran = (deger / max) * 160; // Max width
                return Math.Max(0, Math.Min(160, oran));
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Aktivite takvimi hücre rengi (maç sayısına göre)
    /// </summary>
    public class AktiviteRenkConverter : IValueConverter
    {
        private static readonly SolidColorBrush Renk0 = new(Color.FromRgb(0x1A, 0x1A, 0x1A));  // Hiç oynanmadı
        private static readonly SolidColorBrush Renk1 = new(Color.FromRgb(0x35, 0x35, 0x35));  // Az
        private static readonly SolidColorBrush Renk2 = new(Color.FromRgb(0x55, 0x55, 0x55));  // Orta
        private static readonly SolidColorBrush Renk3 = new(Color.FromRgb(0x88, 0x88, 0x88));  // Çok
        private static readonly SolidColorBrush Renk4 = new(Color.FromRgb(0xFF, 0xFF, 0xFF));  // En çok

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int macSayisi)
            {
                if (macSayisi == 0) return Renk0;
                if (macSayisi == 1) return Renk1;
                if (macSayisi <= 3) return Renk2;
                if (macSayisi <= 6) return Renk3;
                return Renk4;
            }
            return Renk0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Performans çubuğu rengi: değer/maksimum oranına göre kırmızı→sarı→yeşil
    /// </summary>
    public class PerformansRenkConverter : IMultiValueConverter
    {
        public static readonly PerformansRenkConverter Instance = new();

        private static readonly SolidColorBrush Kirmizi = new(Color.FromRgb(0xFF, 0x46, 0x55));
        private static readonly SolidColorBrush Sari    = new(Color.FromRgb(0xFF, 0xC0, 0x30));
        private static readonly SolidColorBrush Yesil   = new(Color.FromRgb(0x00, 0xD2, 0x6A));

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is double deger && values[1] is double max && max > 0)
            {
                double oran = deger / max;
                if (oran >= 0.66) return Yesil;
                if (oran >= 0.33) return Sari;
                return Kirmizi;
            }
            return Kirmizi;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Performans çubuğu genişliği: değer/maksimum * containerWidth
    /// ConverterParameter = container genişliği (double)
    /// </summary>
    public class PerformansGenislikConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is double deger && values[1] is double max && max > 0)
            {
                double containerW = parameter != null ? System.Convert.ToDouble(parameter) : 140.0;
                double oran = Math.Max(0, Math.Min(1, deger / max));
                return oran * containerW;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}