using System.Collections.Generic;

namespace ValorantAutoClicker.Models
{
    // ─── Özet İstatistikler ───────────────────────────────────────────────────────
    public class AnalizOzet
    {
        public int    ToplamMac      { get; set; }
        public double KazanmaOrani   { get; set; }   // 0-100
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
        public int    RrDegisim     { get; set; }   // + veya -
        public int    RrSonrasi     { get; set; }   // maç sonrası toplam RR
        public string Mod           { get; set; }
        public int    RoundOynanan  { get; set; }
        public int    HeadshotCount { get; set; }
        public int    BodyshotCount { get; set; }
        public int    LegshotCount  { get; set; }
        public int    FlashedCount  { get; set; }
        public int    EnemiesFlashed { get; set; }
        public int    FirstDeathCount { get; set; }
        public int    ClutchesWon  { get; set; }
        public int    ClutchesTotal { get; set; }
        public int    ACS           { get; set; }
        public long   MacZaman      { get; set; }

        public double Kda => Deaths == 0
            ? Kills + Assists
            : (Kills + Assists) / (double)Deaths;

        public string KdaText => $"{Kills}/{Deaths}/{Assists}";
        public string RrText  => RrDegisim >= 0 ? $"+{RrDegisim}" : $"{RrDegisim}";
    }

    // ─── Grafik Noktası ──────────────────────────────────────────────────────────
    public class RrGrafikNokta
    {
        public int    MacIndex   { get; set; }
        public int    Rr         { get; set; }
        public bool   RankAtladi { get; set; }
        public string RankAdi    { get; set; }   // "Platin'e yükseldi" gibi
    }

    // ─── Performans Metrikleri (BÖLÜM 2) ─────────────────────────────────────────
    public class PerformansMetrik
    {
        public string Adi      { get; set; }
        public double Deger    { get; set; }
        public double Degisim  { get; set; }   // + veya - değişim
        public double Maksimum { get; set; }   // Progress bar için max değer
        public string Format   { get; set; } = "F1";  // "F0", "F1", "F2", "%"

        // Hesaplanan gösterim property'leri
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

    // ─── ELO Grafik Noktası (BÖLÜM 4) ─────────────────────────────────────────────
    public class EloGrafikNokta
    {
        public int MacIndex { get; set; }
        public int Elo { get; set; }
        public string RankAdi { get; set; }
    }

    // ─── ELO Özet (BÖLÜM 4) ───────────────────────────────────────────────────────
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

    // ─── Harita İstatistikleri (BÖLÜM 5) ─────────────────────────────────────────
    public class HaritaIstatistik
    {
        public string HaritaAdi      { get; set; }
        public int    OynanmaSayisi  { get; set; }   // kaç maç oynandı
        public double GalibiyetOrani { get; set; }   // 0-100 yüzde
        public double ADR            { get; set; }   // ortalama hasar
        public List<bool> SonMaclar  { get; set; } = new();  // son 3 maç: true=kazandı
    }

    // ─── Aktivite Takvimi ─────────────────────────────────────────────────────────
    public class AktiviteHucre
    {
        public int    Yil       { get; set; }
        public int    Ay        { get; set; }
        public int    Gun       { get; set; }
        public int    MacSayisi { get; set; }
        public int    Galibiyet { get; set; }
        public string TarihText { get; set; }
        // 0=hic, 1=az, 2=orta, 3=cok
        public int    Yogunluk  { get; set; }
    }

    public class AktiviteOzet
    {
        public List<int> GunlukOrtalama  { get; set; } = new();
        public int       ToplamGun       { get; set; }
        public int       ToplamMac       { get; set; }
        public string    EnAktifGun      { get; set; }
        public int       EnFazlaMac      { get; set; }
    }

    // ─── Saatlik / Haftalik Aktivite ──────────────────────────────────────────────
    public class SaatlikAktivite
    {
        public int Saat      { get; set; }   // 0-23
        public int MacSayisi { get; set; }
        public int Galibiyet { get; set; }
    }

    public class HaftalikAktivite
    {
        public int    GunIndex { get; set; }   // 0=Pzt, 6=Paz
        public string GunAdi   { get; set; }
        public int    MacSayisi { get; set; }
        public int    Galibiyet { get; set; }
    }

    public class AktiviteGrafikOzet
    {
        public double OrtalamaGunlukMac   { get; set; }
        public double OrtalamaHaftalikMac { get; set; }
        public double GunlukDegisim       { get; set; }  // onceki doneme gore
        public double HaftalikDegisim     { get; set; }
    }

    // ─── Takvim Haftasi (satir) ───────────────────────────────────────────────────
    public class TakvimHaftasi
    {
        // 7 gun, null = o ay disinda
        public AktiviteHucre[] Gunler { get; set; } = new AktiviteHucre[7];
    }

    // ─── Takvim Ay Baslik ─────────────────────────────────────────────────────────
    public class TakvimAyBaslik
    {
        public string AyAdi    { get; set; }
        public int    SutunPos { get; set; }  // kac sutun offseti
        public int    Genislik { get; set; }  // kac hafta kapliyor
    }
}
