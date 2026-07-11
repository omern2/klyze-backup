using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ValorantAutoClicker.Helpers;

namespace ValorantAutoClicker.Models
{
    // ─── Özet İstatistikler ───────────────────────────────────────────────────────
    public class AnalizOzet
    {
        public int    ToplamMac      { get; set; }
        public double KazanmaOrani   { get; set; }
        public double OrtalamaKda    { get; set; }
        public double OrtalamaHasar  { get; set; }
    }

    // ─── Tek Maç ─────────────────────────────────────────────────────────────────
    public class AnalizMac
    {
        public string MapAdi        { get; set; }
        public string AjanAdi       { get; set; }
        public bool   Kazandi       { get; set; }
        public int    Kills         { get; set; }
        public int    Deaths        { get; set; }
        public int    Assists       { get; set; }
        public double Hasar         { get; set; }
        public int    RrDegisim     { get; set; }
        public int    RrSonrasi     { get; set; }
        public string Mod           { get; set; }
        public int    RoundOynanan  { get; set; }
        public int    HeadshotCount { get; set; }
        public int    BodyshotCount { get; set; }
        public int    LegshotCount  { get; set; }
        public long   MacZaman      { get; set; }
        public string MacSkoru      { get; set; }
        public string MatchId       { get; set; }

        private static readonly Dictionary<string, string> ModAdlari = new(StringComparer.OrdinalIgnoreCase)
        {
            {"competitive", "Dereceli"},
            {"unrated", "Derecesiz"},
            {"standard", "Derecesiz"},
            {"spikerush", "Spike Rush"},
            {"deathmatch", "Deathmatch"},
            {"escalation", "Escalation"},
            {"swiftplay", "Swift Play"},
            {"replication", "Replication"},
            {"onefa", "Custom"},
            {"snowball", "Snowball"},
        };

        public string TarihText => MacZaman > 0
            ? DateTimeOffset.FromUnixTimeSeconds(MacZaman).LocalDateTime.ToString("dd MMM yyyy HH:mm")
            : "";

        public string TarihKisa => MacZaman > 0
            ? DateTimeOffset.FromUnixTimeSeconds(MacZaman).LocalDateTime.ToString("dd MMM")
            : "";

        public string AdrText => RoundOynanan > 0 ? $"{(int)(Hasar / RoundOynanan)}" : "0";

        public double Kda => Deaths == 0
            ? Kills + Assists
            : (Kills + Assists) / (double)Deaths;

        public string KdaText => $"{Kills}/{Deaths}/{Assists}";

        public bool   IsCompetitive => string.Equals(Mod, "competitive", StringComparison.OrdinalIgnoreCase);
        public string ModText       => ModAdlari.TryGetValue(Mod ?? "", out var ad) ? ad : Mod ?? "";
        public string RrText        => IsCompetitive ? (RrDegisim >= 0 ? $"+{RrDegisim}" : $"{RrDegisim}") : "-";

        public string RankAdi => IsCompetitive ? RankIkonHelper.RrdenRankAdi(RrSonrasi) : "";
        public ImageSource RankIkonKaynak => IsCompetitive ? RankIkonHelper.RankIkonRr(RrSonrasi) : null;
    }

    // ─── Mac Gecmisi (UI Model) ───────────────────────────────────────────────
    public partial class MacGecmisiItem : ObservableObject
    {
        public string MatchId      { get; set; }
        public string HaritaAdi    { get; set; }
        public string AjanAdi      { get; set; }
        public bool   Kazandi      { get; set; }
        public long   MacZaman     { get; set; }
        public int    Kills        { get; set; }
        public int    Deaths       { get; set; }
        public int    Assists      { get; set; }
        public double Hasar        { get; set; }
        public int    RoundOynanan { get; set; }
        public string Mod          { get; set; }
        public string MacSkoru     { get; set; }
        public int RrDegisim    { get; set; }
        public int RrSonrasi    { get; set; }
        public int Tier         { get; set; }
        public int EloDeger     { get; set; }
        public bool   MmrVar       { get; set; }

        [ObservableProperty] private double _opacity = 1;

        // ── Computed Display ──

        private static readonly Dictionary<string, string> ModAdlari = new(StringComparer.OrdinalIgnoreCase)
        {
            {"competitive", "Dereceli"},
            {"unrated", "Derecesiz"},
            {"standard", "Derecesiz"},
            {"spikerush", "Spike Rush"},
            {"deathmatch", "Deathmatch"},
            {"escalation", "Escalation"},
            {"swiftplay", "Swift Play"},
            {"replication", "Replication"},
            {"onefa", "Custom"},
            {"snowball", "Snowball"},
        };

        public string ModText => ModAdlari.TryGetValue(Mod ?? "", out var ad) ? ad : Mod ?? "";

        public bool IsCompetitive => string.Equals(Mod, "competitive", StringComparison.OrdinalIgnoreCase);

        public string TarihUst
        {
            get
            {
                if (MacZaman <= 0) return "";
                var dt = DateTimeOffset.FromUnixTimeSeconds(MacZaman).LocalDateTime;
                var gunAdi = dt.ToString("ddd", new System.Globalization.CultureInfo("tr-TR"));
                return $"{gunAdi} {dt:dd MMM}";
            }
        }

        public string TarihAlt
        {
            get
            {
                if (MacZaman <= 0) return "";
                return DateTimeOffset.FromUnixTimeSeconds(MacZaman).LocalDateTime.ToString("HH:mm");
            }
        }

        public string SkorKutucukText => Kazandi ? "W" : "K";

        public string SkorText
        {
            get
            {
                if (string.IsNullOrEmpty(MacSkoru)) return "";
                var parts = MacSkoru.Split('-', ':');
                if (parts.Length == 2) return $"{parts[0].Trim()} : {parts[1].Trim()}";
                return MacSkoru;
            }
        }

        public string SkorBizim => MacSkoru?.Split('-', ':').FirstOrDefault()?.Trim() ?? "";
        public string SkorOnlar => MacSkoru?.Split('-', ':').Skip(1).FirstOrDefault()?.Trim() ?? "";

        public string EloText => MmrVar ? $"{EloDeger}" : "";
        public bool EloVisible => MmrVar;

        public string RrText => MmrVar && IsCompetitive
            ? (RrDegisim >= 0 ? $"+{RrDegisim}" : $"{RrDegisim}")
            : "";
        public bool RrVisible => MmrVar && IsCompetitive && RrDegisim != 0;
        public bool RrPozitif => RrDegisim > 0;
        public bool RrNegatif => RrDegisim < 0;

        public double KdOran => Deaths == 0 ? Kills : Kills / (double)Deaths;
        public string KdText => $"{KdOran:F2}";
        public string KdaText => $"{Kills} / {Deaths} / {Assists}";
        public string AdrText => RoundOynanan > 0 ? $"{Hasar / RoundOynanan:F1}" : "0";

        public bool KdYuksek => KdOran >= 1.5;
        public bool KdOrta => KdOran >= 1.0 && KdOran < 1.5;
        public bool KdDusuk => KdOran < 1.0;

        public SolidColorBrush SkorBizimRenk => new(Kazandi ? Colors.White : Color.FromRgb(0x88, 0x88, 0x88));
        public SolidColorBrush SkorOnlarRenk => new(Kazandi ? Color.FromRgb(0x88, 0x88, 0x88) : Colors.White);
    }
    public class RrGrafikNokta
    {
        public int    MacIndex   { get; set; }
        public int    Rr         { get; set; }
        public bool   RankAtladi { get; set; }
        public string RankAdi    { get; set; }
    }

    // ─── Performans Metrikleri ────────────────────────────────────────────────────
    public class PerformansMetrik : ObservableObject
    {
        public string Adi      { get; set; }
        public double Deger    { get; set; }
        public double Degisim  { get; set; }
        public double Maksimum { get; set; }
        public string Format   { get; set; } = "F1";
        public int    Tip      { get; set; }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedBackground));
                }
            }
        }

        public string SelectedBackground => Selected ? "#2A2A2A" : "Transparent";

        public bool   PozitifDegisim => Degisim >= 0;

        public string DegerText
        {
            get
            {
                if (Format == "%")  return $"{Deger:F0}%";
                if (Format == "F0") return $"{Deger:F0}";
                if (Format == "F2") return $"{Deger:F2}";
                return $"{Deger:F1}";
            }
        }

        public string DegisimText
        {
            get
            {
                if (Degisim == 0) return "";
                return Degisim > 0 ? $"+{Degisim:F1}" : $"{Degisim:F1}";
            }
        }

        public string DegisimOk
        {
            get
            {
                if (Degisim == 0) return "";
                return Degisim > 0 ? "▲" : "▼";
            }
        }

        public string DegisimRenk
        {
            get
            {
                if (Degisim > 0) return "#00D26A";
                if (Degisim < 0) return "#FF4655";
                return "#555555";
            }
        }
    }

    // ─── ELO ──────────────────────────────────────────────────────────────────────
    public class EloGrafikNokta
    {
        public int MacIndex { get; set; }
        public int Elo { get; set; }
        public int Tier { get; set; }
        public bool RankAtladi { get; set; }
        public string RankAdi { get; set; }
        public bool Kazandi { get; set; }
        public long MacZaman { get; set; }
        public string MatchId { get; set; }
    }

    public class EloOzet
    {
        public int ToplamEloFarki { get; set; }
        public int EnYuksekElo { get; set; }
        public int EnDusukElo { get; set; }
        public double GalibiyetYuzdesi { get; set; }
        public int ToplamMac { get; set; }
        public int Galibiyet { get; set; }
        public int Maglubiyet { get; set; }
    }

    // ─── Harita İstatistikleri ──────────────────────────────────────────────────
    public class HaritaIstatistik
    {
        public string HaritaAdi      { get; set; }
        public int    OynanmaSayisi  { get; set; }
        public int    GalibiyetSayisi { get; set; }
        public double GalibiyetOrani { get; set; }
        public double OynanmaOrani   { get; set; }
        public double ADR            { get; set; }
        public double OrtalamaKill   { get; set; }
        public double OrtalamaDeath  { get; set; }
        public double OrtalamaAsist  { get; set; }
        public double HeadshotOrani  { get; set; }
        public List<bool> SonMaclar  { get; set; } = new();
    }

    // ─── Aktivite ────────────────────────────────────────────────────────────────
    public class AktiviteHucre
    {
        public int    Yil       { get; set; }
        public int    Ay        { get; set; }
        public int    Gun       { get; set; }
        public int    MacSayisi { get; set; }
        public int    Galibiyet { get; set; }
        public string TarihText { get; set; }
        public int    Yogunluk  { get; set; }
        public double KazanmaYuzdesi => MacSayisi > 0 ? Math.Round(Galibiyet * 100.0 / MacSayisi, 1) : 0;
    }

    public class SaatlikAktivite
    {
        public int Saat      { get; set; }
        public int MacSayisi { get; set; }
        public int Galibiyet { get; set; }
        public double KazanmaYuzdesi => MacSayisi > 0 ? Math.Round(Galibiyet * 100.0 / MacSayisi, 1) : 0;
    }

    public class HaftalikAktivite
    {
        public int    GunIndex { get; set; }
        public string GunAdi   { get; set; }
        public int    MacSayisi { get; set; }
        public int    Galibiyet { get; set; }
        public double KazanmaYuzdesi => MacSayisi > 0 ? Math.Round(Galibiyet * 100.0 / MacSayisi, 1) : 0;
    }

    public class AktiviteGrafikOzet
    {
        public double OrtalamaGunlukMac   { get; set; }
        public double OrtalamaHaftalikMac { get; set; }
        public double GunlukDegisim       { get; set; }
        public double HaftalikDegisim     { get; set; }
    }

    public class TakvimHaftasi
    {
        public AktiviteHucre[] Gunler { get; set; } = new AktiviteHucre[7];
    }

    public class TakvimAyBaslik
    {
        public string AyAdi    { get; set; }
        public int    SutunPos { get; set; }
        public int    Genislik { get; set; }
    }

    public class AktiviteTooltipVeri
    {
        public string Baslik { get; set; }
        public string Oynanan { get; set; }
        public string Kazanilan { get; set; }
        public string KazanmaYuzdesi { get; set; }
    }

    // ─── Detay Panel Modelleri ────────────────────────────────────────────────────
    public enum DetayPanelTipi
    {
        None = -1,
        KazanmaOrani = 0,
        ADR = 1,
        KD = 2,
        Headshot = 3,
        Entry = 4,
        ACS = 5
    }

    public partial class KazanmaOraniDetay : ObservableObject
    {
        [ObservableProperty] private double _son10MacOrani;
        [ObservableProperty] private double _son20MacOrani;
        [ObservableProperty] private double _son50MacOrani;
        [ObservableProperty] private int _kazanilanMac;
        [ObservableProperty] private int _kaybedilenMac;
        [ObservableProperty] private double _kazanilanYukseklik;
        [ObservableProperty] private double _kaybedilenYukseklik;
        [ObservableProperty] private string _enCokKazanilanHarita = "";
        [ObservableProperty] private string _enCokKaybedilenHarita = "";
    }

    public partial class AdrDetay : ObservableObject
    {
        public List<AdrGrafikNokta> AdrNoktalari { get; set; } = new();
        [ObservableProperty] private double _enYuksekAdr;
        [ObservableProperty] private double _enDusukAdr;
        [ObservableProperty] private double _ortalamaAdr;
        public List<HaritaAdrBilgi> HaritaAdrListe { get; set; } = new();
    }

    public class AdrGrafikNokta
    {
        public int Index { get; set; }
        public double Adr { get; set; }
    }

    public class HaritaAdrBilgi
    {
        public string HaritaAdi { get; set; } = "";
        public double Adr { get; set; }
    }

    public partial class KrDetay : ObservableObject
    {
        [ObservableProperty] private int _toplamKill;
        [ObservableProperty] private int _toplamDeath;
        [ObservableProperty] private int _toplamAsist;
        [ObservableProperty] private string _karsilastirmaText = "";
        [ObservableProperty] private bool _karsilastirmaIyi;
    }

    public partial class HeadshotDetay : ObservableObject
    {
        [ObservableProperty] private int _toplamHeadshot;
        [ObservableProperty] private int _toplamBodyshot;
        [ObservableProperty] private int _toplamLegshot;
        [ObservableProperty] private double _headshotYuzdesi;
        public List<HaritaHeadshotBilgi> HaritaListe { get; set; } = new();
    }

    public class HaritaHeadshotBilgi
    {
        public string HaritaAdi { get; set; } = "";
        public double HeadshotOrani { get; set; }
    }

    public partial class KdDetay : ObservableObject
    {
        [ObservableProperty] private int _toplamKill;
        [ObservableProperty] private int _toplamDeath;
        [ObservableProperty] private double _kdOrani;
        [ObservableProperty] private string _karsilastirmaText = "";
        [ObservableProperty] private bool _karsilastirmaIyi;
    }

    public partial class BitisBasarisiDetay : ObservableObject
    {
        [ObservableProperty] private int _clutch1v1;
        [ObservableProperty] private int _clutch1v1Basarili;
        [ObservableProperty] private int _clutch1v2;
        [ObservableProperty] private int _clutch1v2Basarili;
        [ObservableProperty] private int _clutch1v3;
        [ObservableProperty] private int _clutch1v3Basarili;
        [ObservableProperty] private int _clutch1v4;
        [ObservableProperty] private int _clutch1v4Basarili;
        [ObservableProperty] private int _clutch1v5;
        [ObservableProperty] private int _clutch1v5Basarili;
        [ObservableProperty] private double _toplamBasariOrani;
    }

    public partial class GirisBasarisiDetay : ObservableObject
    {
        [ObservableProperty] private int _ilkKanAlinanMac;
        [ObservableProperty] private int _toplamMac;
        [ObservableProperty] private double _basariOrani;
        public List<HaritaGirisBilgi> HaritaListe { get; set; } = new();
    }

    public class HaritaGirisBilgi
    {
        public string HaritaAdi { get; set; } = "";
        public double BasariOrani { get; set; }
    }

    public partial class MultiKillDetay : ObservableObject
    {
        [ObservableProperty] private int _ace;
        [ObservableProperty] private int _fourK;
        [ObservableProperty] private int _threeK;
        [ObservableProperty] private int _twoK;
        [ObservableProperty] private int _oneK;
        public List<MultiKillItem> MultiKillListe { get; set; } = new();
    }

    public class MultiKillItem
    {
        public string HaritaAdi { get; set; } = "";
        public string Tur { get; set; } = "";
        public int KillSayisi { get; set; }
        public string MacText { get; set; } = "";
        public long MacZaman { get; set; }
    }

    // ─── Filtre Modelleri ────────────────────────────────────────────────────────
    public class FiltreSecenek
    {
        public string Deger { get; set; }
        public string Etiket { get; set; }
    }

    public enum FiltreOyunModu
    {
        [System.ComponentModel.Description("Tüm Modlar")]
        TumModlar,
        Dereceli,
        Derecesiz,
        SpikeRush,
        Deathmatch,
        Escalation,
        SwiftPlay,
        Ozel
    }

    public enum FiltreAralik
    {
        [System.ComponentModel.Description("Tüm Zamanlar")]
        TumZamanlar,
        BuHafta,
        BuAy,
        Son30Gun,
        Son90Gun,
        BuSezon
    }

    public enum FiltreHarita
    {
        [System.ComponentModel.Description("Tüm Haritalar")]
        TumHaritalar,
        Ascent, Bind, Haven, Split, Icebox,
        Breeze, Fracture, Pearl, Lotus, Sunset, Abyss
    }

    public enum FiltreSiralama
    {
        [System.ComponentModel.Description("En Yeni")]
        EnYeni,
        [System.ComponentModel.Description("En Eski")]
        EnEski,
        [System.ComponentModel.Description("En Yüksek KDA")]
        EnYuksekKDA,
        [System.ComponentModel.Description("En Yüksek Hasar")]
        EnYuksekHasar
    }

    // ─── Canli Mac ─────────────────────────────────────────────────────────────────
    public class LiveMatchData
    {
        public string Map { get; set; } = "";
        public string Mode { get; set; } = "";
        public string GameType { get; set; } = "";
        public long EstimatedTime { get; set; }
        public string ServerName { get; set; } = "";
        public LiveMatchTeam RedTeam { get; set; } = new();
        public LiveMatchTeam BlueTeam { get; set; } = new();
    }

    public class LiveMatchTeam
    {
        public string TeamName { get; set; } = "";
        public ObservableCollection<LiveMatchPlayer> Players { get; set; } = new();
    }

    public class LiveMatchPlayer : ObservableObject
    {
        private string _agent = "";
        public string Agent
        {
            get => _agent;
            set => SetProperty(ref _agent, value);
        }

        private string _name = "";
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _tag = "";
        public string Tag
        {
            get => _tag;
            set => SetProperty(ref _tag, value);
        }

        private string _countryCode = "";
        public string CountryCode
        {
            get => _countryCode;
            set => SetProperty(ref _countryCode, value);
        }

        private string _rank = "";
        public string Rank
        {
            get => _rank;
            set
            {
                if (SetProperty(ref _rank, value))
                    OnPropertyChanged(nameof(RankIkonKaynak));
            }
        }

        public ImageSource RankIkonKaynak => RankIkonHelper.RankIkon(_rank);

        public ImageSource RankIkonFromTierKaynak => RankIkonHelper.RankIkonFromTier(_tier, 20);

        private string _playerCardUrl = "";
        public string PlayerCardUrl
        {
            get => _playerCardUrl;
            set => SetProperty(ref _playerCardUrl, value);
        }

        private int _tier;
        public int Tier
        {
            get => _tier;
            set
            {
                if (SetProperty(ref _tier, value))
                    OnPropertyChanged(nameof(RankIkonFromTierKaynak));
            }
        }

        private int _elo;
        public int Elo
        {
            get => _elo;
            set => SetProperty(ref _elo, value);
        }

        private int _kills;
        public int Kills
        {
            get => _kills;
            set
            {
                if (SetProperty(ref _kills, value))
                    OnPropertyChanged(nameof(KdaText));
            }
        }

        private int _deaths;
        public int Deaths
        {
            get => _deaths;
            set
            {
                if (SetProperty(ref _deaths, value))
                    OnPropertyChanged(nameof(KdaText));
            }
        }

        private int _assists;
        public int Assists
        {
            get => _assists;
            set
            {
                if (SetProperty(ref _assists, value))
                    OnPropertyChanged(nameof(KdaText));
            }
        }

        public string KdaText => HasKda ? $"{Kills}/{Deaths}/{Assists}" : "--/--/--";

        private bool _hasKda = true;
        public bool HasKda
        {
            get => _hasKda;
            set
            {
                if (SetProperty(ref _hasKda, value))
                    OnPropertyChanged(nameof(KdaText));
            }
        }

        private string _puuid = "";
        public string Puuid
        {
            get => _puuid;
            set => SetProperty(ref _puuid, value);
        }

        private bool _isCurrentUser;
        public bool IsCurrentUser
        {
            get => _isCurrentUser;
            set => SetProperty(ref _isCurrentUser, value);
        }

        private bool _match1Win;
        public bool Match1Win
        {
            get => _match1Win;
            set { if (SetProperty(ref _match1Win, value)) OnPropertyChanged(nameof(Match1Brush)); }
        }

        private bool _match2Win;
        public bool Match2Win
        {
            get => _match2Win;
            set { if (SetProperty(ref _match2Win, value)) OnPropertyChanged(nameof(Match2Brush)); }
        }

        private bool _match3Win;
        public bool Match3Win
        {
            get => _match3Win;
            set { if (SetProperty(ref _match3Win, value)) OnPropertyChanged(nameof(Match3Brush)); }
        }

        private bool _match4Win;
        public bool Match4Win
        {
            get => _match4Win;
            set { if (SetProperty(ref _match4Win, value)) OnPropertyChanged(nameof(Match4Brush)); }
        }

        public SolidColorBrush Match1Brush => Match1Win ? _yesil : _kirmizi;
        public SolidColorBrush Match2Brush => Match2Win ? _yesil : _kirmizi;
        public SolidColorBrush Match3Brush => Match3Win ? _yesil : _kirmizi;
        public SolidColorBrush Match4Brush => Match4Win ? _yesil : _kirmizi;
        private static readonly SolidColorBrush _yesil = new(System.Windows.Media.Color.FromRgb(0x22, 0xC5, 0x5E));
        private static readonly SolidColorBrush _kirmizi = new(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
    }

    // ─── Maç Detay ───────────────────────────────────────────────────────────
    public partial class MacDetay
    {
        public string MapAdi { get; set; }
        public string Mod { get; set; }
        public long MacZaman { get; set; }
        public int SkorBizim { get; set; }
        public int SkorOnlar { get; set; }
        public bool Kazandi { get; set; }
        public string MatchId { get; set; }
        public List<MacDetayOyuncu> TumOyuncular { get; set; } = new();
        public List<MacDetayOyuncu> TakimOyuncular { get; set; } = new();
        public List<MacDetayOyuncu> Rakipler { get; set; } = new();
        public MacDetayOyuncu Ben { get; set; }
    }

    public partial class MacDetayOyuncu : ObservableObject
    {
        public string Puuid { get; set; }
        public string Ad { get; set; }
        public string Tag { get; set; }
        public int Tier { get; set; }
        public string TierAdi { get; set; }
        public string KartUrl { get; set; }
        public string AjanUrl { get; set; }
        public string AjanAdi { get; set; }
        public bool TakimKirmizi { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int Headshots { get; set; }
        public int Bodyshots { get; set; }
        public int Legshots { get; set; }
        public double Hasar { get; set; }
        public int HasarAlan { get; set; }
        public int RoundOynanan { get; set; }
        public double Adr => RoundOynanan > 0 ? Hasar / RoundOynanan : 0;
        public double KdOran => Deaths == 0 ? Kills : Math.Round(Kills / (double)Deaths, 2);
        public double KrOran => RoundOynanan > 0 ? Math.Round(Kills / (double)RoundOynanan, 2) : 0;
        public double HsYuzde
        {
            get
            {
                var toplam = Headshots + Bodyshots + Legshots;
                return toplam > 0 ? Math.Round(Headshots / (double)toplam * 100, 1) : 0;
            }
        }
        public int Kill4k { get; set; }
        public int Kill3k { get; set; }
        public int Kill2k { get; set; }
        public int Kill5k { get; set; }
        public int IlkOldurme { get; set; }
        public double Swing { get; set; }
        public double Kast { get; set; }
        public bool IsCurrentUser { get; set; }
        public bool IsMvp { get; set; }
        public int MvpSayisi { get; set; }

        public string KdText => $"{KdOran:F2}";
        public string AdrText => $"{(int)Math.Round(Adr)}";
        public string KrText => $"{KrOran:F2}";
        public string HsText => $"{Headshots}";
        public string HsYuzdeText => $"{HsYuzde}%";
        public string SwingText => Swing >= 0 ? $"+{Swing:F2}%" : $"{Swing:F2}%";
        public SolidColorBrush SwingRenk => Swing >= 0 ? new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)) : new SolidColorBrush(Color.FromRgb(0xFF, 0x46, 0x55));
        public string Kill5kText => Kill5k > 0 ? $"{Kill5k}" : "-";
        public string Kill4kText => Kill4k > 0 ? $"{Kill4k}" : "-";
        public string Kill3kText => Kill3k > 0 ? $"{Kill3k}" : "-";
        public string Kill2kText => Kill2k > 0 ? $"{Kill2k}" : "-";
        public string KastText => $"{Kast:F1}%";
        public string KdaText => $"{Kills}/{Deaths}/{Assists}";
        public string MvpSayisiText => MvpSayisi > 0 ? $"{MvpSayisi}" : "-";

        public ImageSource RankIkonKaynak => RankIkonHelper.RankIkonFromTier(Tier, 20);

        /// Evaluation bar properties (54x28px)
        public double DegerlendirmeBarGenislik => Math.Min(KdOran / 1.5, 1.0) * 54;
        public string DegerlendirmeText => $"{KdOran:F2}";
        public SolidColorBrush DegerlendirmeRenk
        {
            get
            {
                if (KdOran >= 1.4)
                    return new SolidColorBrush(Color.FromRgb(0x14, 0xD9, 0x66)); // full green #14d966
                if (KdOran >= 1.1)
                    return new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)); // neutral dark
                if (KdOran >= 0.9)
                    return new SolidColorBrush(Color.FromRgb(0x3A, 0x20, 0x20)); // dark red tint
                return new SolidColorBrush(Color.FromRgb(0x1A, 0x10, 0x10)); // transparent dark
            }
        }
        public SolidColorBrush DegerlendirmeTextRenk
        {
            get
            {
                if (KdOran >= 1.4)
                    return new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00)); // black text on green
                if (KdOran >= 0.9)
                    return new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)); // white
                return new SolidColorBrush(Color.FromRgb(0xCC, 0x44, 0x44)); // dark red
            }
        }

        // Edge indicator color (right-end)
        public SolidColorBrush EdgeRenk => TakimKirmizi
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x46, 0x55))
            : new SolidColorBrush(Color.FromRgb(0x00, 0xF3, 0xFF));
    }

    public partial class MacDetay
    {
        public MacDetayOyuncu HighlightKillsPlayer => TumOyuncular?.OrderByDescending(p => p.Kills).FirstOrDefault();
        public MacDetayOyuncu HighlightAdrPlayer => TumOyuncular?.OrderByDescending(p => p.Adr).FirstOrDefault();
        public MacDetayOyuncu HighlightKastPlayer => TumOyuncular?.OrderByDescending(p => p.Kast).FirstOrDefault();
        public MacDetayOyuncu HighlightIlkOldurmePlayer => TumOyuncular?.OrderByDescending(p => p.IlkOldurme).FirstOrDefault();

        public string HighlightKills => HighlightKillsPlayer?.Kills.ToString() ?? "0";
        public string HighlightAdr => HighlightAdrPlayer?.AdrText ?? "0";
        public string HighlightKast => HighlightKastPlayer?.KastText ?? "0%";
        public string HighlightIlkOldurme => HighlightIlkOldurmePlayer?.IlkOldurme.ToString() ?? "0";

        public int IlkYariBizim { get; set; }
        public int IkinciYariBizim { get; set; }
        public int IlkYariOnlar { get; set; }
        public int IkinciYariOnlar { get; set; }
        public string IlkYariText => $"İlk Yarı: {IlkYariBizim}";
        public string IkinciYariText => $"İkinci Yarı: {IkinciYariBizim}";

        public string TakimOrtEloText => $"Takım Ort. {TakimOrtElo}";
        public string RakipOrtEloText => $"Takım Ort. {RakipOrtElo}";

        // Average rank emblem for center divider
        public ImageSource TakimOrtRankIkon
        {
            get
            {
                if (TakimOyuncular?.Any() != true) return null;
                var ortTier = (int)TakimOyuncular.Average(p => p.Tier);
                return RankIkonHelper.RankIkonFromTier(ortTier, 18);
            }
        }
        public ImageSource RakipOrtRankIkon
        {
            get
            {
                if (Rakipler?.Any() != true) return null;
                var ortTier = (int)Rakipler.Average(p => p.Tier);
                return RankIkonHelper.RankIkonFromTier(ortTier, 18);
            }
        }

        public int TakimOrtElo => TakimOyuncular?.Any() == true
            ? (int)TakimOyuncular.Average(p => p.Tier * 100 + (int)p.Adr) : 0;
        public int RakipOrtElo => Rakipler?.Any() == true
            ? (int)Rakipler.Average(p => p.Tier * 100 + (int)p.Adr) : 0;
    }
}
