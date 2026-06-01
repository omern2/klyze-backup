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
    /// int (count) → Visibility. Count > 0 → Visible, else Collapsed.
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public static readonly CountToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
                return count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            if (value is System.Collections.ICollection col)
                return col.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            return System.Windows.Visibility.Collapsed;
        }

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

    /// <summary>
    /// DetayPanelTipi enum → Visibility dönüştürücü.
    /// ConverterParameter ile eşleşen tip Visible, diğerleri Collapsed.
    /// </summary>
    public class DetayTipiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.DetayPanelTipi tip && parameter is string paramStr)
            {
                if (int.TryParse(paramStr, out int hedef))
                    return (int)tip == hedef ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// bool → SolidColorBrush (True=Green, False=Red)
    /// </summary>
    public class BoolToRenkConverter : IValueConverter
    {
        private static readonly SolidColorBrush Yesil = new(Color.FromRgb(0x00, 0xD2, 0x6A));
        private static readonly SolidColorBrush Kirmizi = new(Color.FromRgb(0xFF, 0x46, 0x55));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Yesil : Kirmizi;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// double → double dönüştürücü (bar yüksekliği için).
    /// Değer 0 ise 1 döndürür (görünürlük için).
    /// </summary>
    public class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
                return Math.Max(1.0, d);
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Clutch genişlik converter. Sabit genişlik değerleri döndürür.
    /// ConverterParameter 0-4 arası indeks.
    /// </summary>
    /// <summary>
    /// Tek değer için performans çubuğu genişliği/yüksekliği.
    /// values[0] = 0-100 arası yüzde, ConverterParameter = maksimum piksel değeri.
    /// return = (value / 100) * maxPixel
    /// </summary>
    public class PerformansBarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double deger = System.Convert.ToDouble(value);
            double maxPiksel = parameter != null ? System.Convert.ToDouble(parameter) : 140.0;
            double oran = Math.Max(0, Math.Min(1, deger / 100.0));
            return oran * maxPiksel;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// double (KD oranı) → SolidColorBrush
    /// >= 1.5 → Yesil (#00D26A)
    /// >= 1.0 → Gri (#555555)
    /// < 1.0 → Kirmizi (#FF4655)
    /// </summary>
    public class MacKdRenkConverter : IValueConverter
    {
        private static readonly SolidColorBrush Yesil   = new(Color.FromRgb(0x00, 0xD2, 0x6A));
        private static readonly SolidColorBrush Gri     = new(Color.FromRgb(0x55, 0x55, 0x55));
        private static readonly SolidColorBrush Kirmizi = new(Color.FromRgb(0xFF, 0x46, 0x55));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double kd)
            {
                if (kd >= 1.5) return Yesil;
                if (kd >= 1.0) return Gri;
                return Kirmizi;
            }
            return Gri;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ClutchGenislikConverter : IValueConverter
    {
        private static readonly double[] Genislikler = { 120, 100, 80, 60, 40 };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count && parameter is string paramStr && int.TryParse(paramStr, out int idx))
            {
                if (idx >= 0 && idx < Genislikler.Length)
                    return count > 0 ? Genislikler[idx] : 0.0;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}