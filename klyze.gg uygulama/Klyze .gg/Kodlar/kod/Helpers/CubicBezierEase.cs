using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace ValorantAutoClicker.Helpers
{
    public class CubicBezierEase : EasingFunctionBase
    {
        public static readonly DependencyProperty X1Property = DependencyProperty.Register(nameof(X1), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(0.34));
        public static readonly DependencyProperty Y1Property = DependencyProperty.Register(nameof(Y1), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(1.56));
        public static readonly DependencyProperty X2Property = DependencyProperty.Register(nameof(X2), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(0.64));
        public static readonly DependencyProperty Y2Property = DependencyProperty.Register(nameof(Y2), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(1.0));

        public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }
        public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }
        public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }
        public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }

        public static readonly CubicBezierEase Spring = new CubicBezierEase
        {
            X1 = 0.34, Y1 = 1.56, X2 = 0.64, Y2 = 1.0,
            EasingMode = EasingMode.EaseIn
        };

        public static readonly CubicBezierEase Smooth = new CubicBezierEase
        {
            X1 = 0.34, Y1 = 1.56, X2 = 0.64, Y2 = 1.0,
            EasingMode = EasingMode.EaseIn
        };

        protected override Freezable CreateInstanceCore() => new CubicBezierEase();

        protected override double EaseInCore(double normalizedTime)
        {
            double t = normalizedTime;
            for (int i = 0; i < 12; i++)
            {
                double x = SampleCurveX(t);
                double dx = SampleCurveDerivativeX(t);
                if (Math.Abs(dx) < 1e-7) break;
                t -= (x - normalizedTime) / dx;
            }
            t = Math.Clamp(t, 0, 1);
            return SampleCurveY(t);
        }

        private double SampleCurveX(double t) =>
            3 * (1 - t) * (1 - t) * t * X1 +
            3 * (1 - t) * t * t * X2 +
            t * t * t;

        private double SampleCurveDerivativeX(double t) =>
            3 * (1 - t) * (1 - t) * X1 +
            6 * (1 - t) * t * (X2 - X1) +
            3 * t * t * (1 - X2);

        private double SampleCurveY(double t) =>
            3 * (1 - t) * (1 - t) * t * Y1 +
            3 * (1 - t) * t * t * Y2 +
            t * t * t;
    }
}
