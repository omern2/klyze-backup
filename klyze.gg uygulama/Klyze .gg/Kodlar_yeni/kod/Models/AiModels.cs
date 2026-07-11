using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ValorantAutoClicker.Models
{
    public partial class AiSohbetMesaj : ObservableObject
    {
        [ObservableProperty]
        private string _icerik = "";

        [ObservableProperty]
        private string _streamingMetin = "";

        [ObservableProperty]
        private bool _kullaniciMesaji;

        [ObservableProperty]
        private DateTime _zaman = DateTime.Now;

        [ObservableProperty]
        private bool _yukleniyor;

        [ObservableProperty]
        private bool _streamAktif;

        [ObservableProperty]
        private string _durumEtiketi = "";
    }

    public partial class AiSohbet : ObservableObject
    {
        [ObservableProperty]
        private string _id = Guid.NewGuid().ToString("N")[..8];

        [ObservableProperty]
        private string _baslik = "Yeni Sohbet";

        [ObservableProperty]
        private DateTime _olusturulma = DateTime.Now;

        [ObservableProperty]
        private bool _aktif;

        public ObservableCollection<AiSohbetMesaj> Mesajlar { get; set; } = new();
    }

    public class GroqIstek
    {
        public string model { get; set; } = "meta-llama/llama-4-scout-17b-16e-instruct";
        public GroqMesaj[] messages { get; set; }
        public int max_tokens { get; set; } = 500;
        public double temperature { get; set; } = 0.7;
    }

    public class GroqMesaj
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class GroqYanit
    {
        public GroqSecim[] choices { get; set; }
        public GroqKullanim usage { get; set; }
    }

    public class GroqSecim
    {
        public GroqMesaj message { get; set; }
    }

    public class GroqKullanim
    {
        public int total_tokens { get; set; }
    }
}
