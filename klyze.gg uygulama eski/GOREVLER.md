# Klyze.gg Analiz Modülü Güncelleme Görevleri

## Genel Hedef
Mevcut analiz sayfasını koruyup üzerine gelişmiş istatistikler, grafikler ve aktivite takvimi eklemek. Tema: Siyah-gri-beyaz, animasyonlu.

---

## BÖLÜM 1 — Mevcut Özet Kartları
- [ ] Toplam maç, kazanma oranı, ort. KDA, ort. hasar kartları korunacak
- [ ] Sayfa açılırken soldan sağa stagger animasyonu eklenecek

---

## BÖLÜM 2 — Performans İstatistikleri (3x3 Grid)
- [ ] 9 metrik kutu oluşturulacak (kazanma oranı, ADR, K/R, bitiriş başarısı, giriş başarılı, headshot yüzdesi, flaş başarı, round başına flaş, yardımcı hasar/tur)
- [ ] Her kutuda: büyük değer, küçük değişim değeri (artış/düşüş), metrik adı, renkli ilerleme çubuğu
- [ ] Henrik Dev API maç geçmişinden veriler hesaplanacak
- [ ] Kutular yukarıdan aşağıya stagger animasyonla gelecek

---

## BÖLÜM 3 — Mevcut RR Grafiği
- [ ] Mevcut RR grafiği korunacak
- [ ] Rank atlama noktaları işaretli kalacak
- [ ] Grafik çizilme animasyonu korunacak

---

## BÖLÜM 4 — ELO İlerleme Grafiği
- [ ] Büyük çizgi grafiği oluşturulacak (maç numaraları vs ELO)
- [ ] ELO hesaplama: her rütbenin baz değeri + RR
- [ ] Grafik altı hafif dolgulu olacak
- [ ] Sağ tarafta özet panel: toplam ELO farkı, en yüksek/düşük ELO, galibiyet%, maç sayısı, galibiyet/maglubiyet
- [ ] Soldan sağa çizilme animasyonu

---

## BÖLÜM 5 — Harita İstatistikleri
- [ ] Sol tarafta örümcek/radar grafiği (her harita köşe, oynanan% ve galibiyet%)
- [ ] Sağ tarafta en çok oynanan 3 harita kartı (harita adı, ADR, giriş başarı%, clutch başarı%)
- [ ] Son maç sonuçları W/K harfleriyle
- [ ] Radar grafiği dönerek açılma animasyonu
- [ ] Kartlar sağdan sola kayarak gelecek

---

## BÖLÜM 6 — Aktivite Takvimi
- [ ] Yıllık ısı haritası (12 ay x 7 gün)
- [ ] Renk skalası: koyu gri (yok) → orta gri → beyaz (çok)
- [ ] Altında açıklama satırı
- [ ] Üstte iki küçük grafik: günlük ve haftalık ortalama maç
- [ ] Takvim hücreleri soldan sağa dalga animasyonu

---

## API Entegrasyonu
- [ ] Maç geçmişi endpoint: GET https://api.henrikdev.xyz/valorant/v3/matches/{region}/{name}/{tag}?size=20
- [ ] MMR geçmişi endpoint: GET https://api.henrikdev.xyz/valorant/v1/mmr-history/{region}/{name}/{tag}
- [ ] API anahtarı: HDEV-06d4da7c-c8ae-446d-a653-9277e0ea7cb1
- [ ] Kullanıcı bilgileri data/user.json'dan okunacak

---

## Animasyon Kuralları
- [ ] Sayfa açılınca skeleton loading gösterilecek
- [ ] API cevap verince bölümler sırayla animasyonla yerleşecek (150ms gecikme)
- [ ] Grafikler çizilme animasyonuyla açılacak
- [ ] Kartlar/kutular yukarıdan veya yandan kayarak gelecek
- [ ] Tüm geçişler 200-400ms, cubic-bezier easing

---

## Teknik Altyapı
- [ ] HenrikApiService güncellenecek (matches ve mmr-history endpointleri)
- [ ] AnalizViewModel yeni veriler için genişletilecek
- [ ] Yeni modeller eklenecek (MatchDetail, MMRHistory, vb.)
- [ ] XAML'de yeni grafik componentleri oluşturulacak