using System;

namespace ValorantAutoClicker.Models
{
    public class CrosshairSettings
    {
        public string Color { get; set; } = "Red";
        public int InnerLength { get; set; } = 8;
        public int InnerThickness { get; set; } = 2;
        public int InnerGap { get; set; } = 2;
        public bool OuterLines { get; set; } = false;
        public int OuterLength { get; set; } = 4;
        public int OuterThickness { get; set; } = 1;
        public int OuterGap { get; set; } = 2;
        public bool CenterDot { get; set; } = false;
        public int CenterDotSize { get; set; } = 2;
        public double Scale { get; set; } = 1.0;
        public int Opacity { get; set; } = 100;

        public CrosshairSettings Clone()
        {
            return new CrosshairSettings
            {
                Color = this.Color,
                InnerLength = this.InnerLength,
                InnerThickness = this.InnerThickness,
                InnerGap = this.InnerGap,
                OuterLines = this.OuterLines,
                OuterLength = this.OuterLength,
                OuterThickness = this.OuterThickness,
                OuterGap = this.OuterGap,
                CenterDot = this.CenterDot,
                CenterDotSize = this.CenterDotSize,
                Scale = this.Scale,
                Opacity = this.Opacity
            };
        }
    }
}
