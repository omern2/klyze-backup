import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:fl_chart/fl_chart.dart';
import '../services/tehdit_service.dart';
import '../services/geo_service.dart';
import '../services/bildirim_service.dart';

class GuvenlikDuvari extends StatefulWidget {
  const GuvenlikDuvari({super.key});

  @override
  State<GuvenlikDuvari> createState() => _GuvenlikDuvariState();
}

class _GuvenlikDuvariState extends State<GuvenlikDuvari> {
  ThreatStats? _stats;
  bool _loading = true;
  String? _hata;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _loadData();
    _timer = Timer.periodic(const Duration(seconds: 10), (_) => _loadData());
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _loadData() async {
    try {
      final stats = await TehditService.getStats();
      if (mounted) setState(() { _stats = stats; _loading = false; _hata = null; });
    } catch (e) {
      if (mounted) setState(() { _hata = '$e'; _loading = false; });
    }
  }

  Color _severityColor(String s) {
    switch (s) {
      case 'critical': return Colors.redAccent;
      case 'high': return Colors.orangeAccent;
      case 'medium': return Colors.amber;
      case 'low': return Colors.blueAccent;
      default: return Colors.grey;
    }
  }

  Color _scoreColor(String score) {
    switch (score) {
      case 'A+': return Colors.greenAccent;
      case 'A': return Colors.lightGreenAccent;
      case 'B': return Colors.amber;
      case 'C': return Colors.orangeAccent;
      default: return Colors.redAccent;
    }
  }

  String countryFlag(String code) {
    if (code.isEmpty) return '';
    final first = code.codeUnitAt(0) - 0x61 + 0x1F1E6;
    final second = code.codeUnitAt(1) - 0x61 + 0x1F1E6;
    return String.fromCharCodes([first, second]);
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator(color: Colors.cyanAccent));
    }

    final stats = _stats;
    if (stats == null || stats.total == 0) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 64, height: 64,
              decoration: BoxDecoration(color: Colors.green.withOpacity(0.08), borderRadius: BorderRadius.circular(20)),
              child: const Icon(Icons.check_circle_outline, color: Colors.greenAccent, size: 32),
            ),
            const SizedBox(height: 16),
            const Text('Güvenlik Duvarı Aktif', style: TextStyle(color: Colors.white54, fontSize: 16, fontWeight: FontWeight.w600)),
            const SizedBox(height: 8),
            const Text('Henüz tehdit tespit edilmedi', style: TextStyle(color: Colors.grey, fontSize: 13)),
            const SizedBox(height: 24),
            OutlinedButton.icon(
              onPressed: _loadData,
              icon: const Icon(Icons.refresh, size: 16),
              label: const Text('Yenile'),
              style: OutlinedButton.styleFrom(foregroundColor: Colors.cyanAccent, side: const BorderSide(color: Colors.cyanAccent)),
            ),
          ],
        ),
      );
    }

    final gunluk = stats.byDay.entries.toList()..sort((a, b) => a.key.compareTo(b.key));
    final maxGun = gunluk.isEmpty ? 1.0 : gunluk.map((e) => e.value).reduce(max).toDouble();

    return RefreshIndicator(
      onRefresh: _loadData,
      color: Colors.cyanAccent,
      backgroundColor: const Color(0xFF1A1A2E),
      child: ListView(
        padding: const EdgeInsets.only(top: 12, bottom: 40),
        children: [
          _buildOzet(stats),
          _buildGrafik(gunluk, maxGun),
          _buildTurDagilimi(stats.byType),
          _buildAktifIpler(stats.byIp),
          _buildSonTehditler(stats.recent),
        ],
      ),
    );
  }

  Widget _buildOzet(ThreatStats stats) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: Row(
        children: [
          _ozetKart(Icons.warning_amber_rounded, '${stats.total}', 'Toplam Tehdit', Colors.redAccent),
          const SizedBox(width: 8),
          _ozetKart(Icons.today, '${stats.today}', 'Bugün', stats.today > 5 ? Colors.orangeAccent : Colors.greenAccent),
          const SizedBox(width: 8),
          _ozetKart(Icons.gpp_bad, '${stats.byIp.length}', 'Saldıran IP', Colors.amber),
          const SizedBox(width: 8),
          _ozetKart(Icons.shield, stats.securityScore, 'Skor', _scoreColor(stats.securityScore)),
        ],
      ),
    );
  }

  Widget _ozetKart(IconData icon, String value, String label, Color color) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
          color: const Color(0xFF12121A),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: color.withOpacity(0.2)),
        ),
        child: Column(
          children: [
            Icon(icon, color: color, size: 18),
            const SizedBox(height: 4),
            Text(value, style: TextStyle(color: color, fontSize: 16, fontWeight: FontWeight.bold)),
            Text(label, style: const TextStyle(color: Colors.grey, fontSize: 9)),
          ],
        ),
      ),
    );
  }

  Widget _buildGrafik(List<MapEntry<String, int>> gunluk, double maxGun) {
    if (gunluk.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: const Color(0xFF12121A),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: Colors.white10),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('TEHDİT ZAMAN ÇİZGİSİ', style: TextStyle(color: Colors.grey, fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 1)),
            const SizedBox(height: 20),
            SizedBox(
              height: 160,
              child: BarChart(
                BarChartData(
                  alignment: BarChartAlignment.spaceAround,
                  maxY: (maxGun * 1.3).ceilToDouble(),
                  barTouchData: BarTouchData(
                    touchTooltipData: BarTouchTooltipData(
                      getTooltipItem: (group, _, rod, __) => BarTooltipItem(
                        '${gunluk[group.x.toInt()].key}\n${rod.toY.toInt()} tehdit',
                        const TextStyle(color: Colors.white, fontSize: 12),
                      ),
                    ),
                  ),
                  titlesData: FlTitlesData(
                    show: true,
                    bottomTitles: AxisTitles(
                      sideTitles: SideTitles(
                        showTitles: true, reservedSize: 20,
                        getTitlesWidget: (v, _) {
                          final idx = v.toInt();
                          if (idx < 0 || idx >= gunluk.length) return const SizedBox.shrink();
                          return Text(gunluk[idx].key, style: const TextStyle(color: Colors.grey, fontSize: 9));
                        },
                      ),
                    ),
                    leftTitles: AxisTitles(
                      sideTitles: SideTitles(
                        showTitles: true, reservedSize: 28,
                        getTitlesWidget: (v, _) {
                          if (v == 0) return const SizedBox.shrink();
                          return Text(v.toInt().toString(), style: const TextStyle(color: Colors.grey, fontSize: 10));
                        },
                      ),
                    ),
                    topTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                    rightTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                  ),
                  gridData: FlGridData(
                    show: true, drawVerticalLine: false,
                    getDrawingHorizontalLine: (v) => FlLine(color: Colors.white10, strokeWidth: 1),
                  ),
                  borderData: FlBorderData(show: false),
                  barGroups: gunluk.asMap().entries.map((e) => BarChartGroupData(
                    x: e.key,
                    barRods: [BarChartRodData(
                      toY: e.value.value.toDouble(),
                      color: e.value.value > 5 ? Colors.redAccent : e.value.value > 2 ? Colors.orangeAccent : Colors.cyanAccent,
                      width: 20,
                      borderRadius: const BorderRadius.vertical(top: Radius.circular(4)),
                    )],
                  )).toList(),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildTurDagilimi(Map<String, int> byType) {
    if (byType.isEmpty) return const SizedBox.shrink();
    final entries = byType.entries.toList()..sort((a, b) => b.value.compareTo(a.value));
    final total = byType.values.fold(0, (a, b) => a + b);
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: const Color(0xFF12121A),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: Colors.white10),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('SALDIRI TÜRLERİ', style: TextStyle(color: Colors.grey, fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 1)),
            const SizedBox(height: 14),
            ...entries.map((e) {
              final oran = e.value / total;
              final turAdi = _threatName(e.key);
              return Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Text(turAdi, style: const TextStyle(color: Colors.white70, fontSize: 12)),
                        const Spacer(),
                        Text('${e.value}',
                          style: TextStyle(
                            color: _threatColor(e.key), fontSize: 12, fontWeight: FontWeight.w600)),
                      ],
                    ),
                    const SizedBox(height: 4),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(3),
                      child: LinearProgressIndicator(
                        value: oran,
                        backgroundColor: Colors.white10,
                        valueColor: AlwaysStoppedAnimation(_threatColor(e.key)),
                        minHeight: 5,
                      ),
                    ),
                  ],
                ),
              );
            }),
          ],
        ),
      ),
    );
  }

  Widget _buildAktifIpler(Map<String, int> byIp) {
    if (byIp.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: const Color(0xFF12121A),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: Colors.white10),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('EN AKTİF SALDIRGAN IP\'LER', style: TextStyle(color: Colors.grey, fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 1)),
            const SizedBox(height: 12),
            ...byIp.entries.take(8).map((e) => Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Row(
                children: [
                  Container(
                    width: 8, height: 8,
                    decoration: BoxDecoration(color: Colors.redAccent, shape: BoxShape.circle),
                  ),
                  const SizedBox(width: 10),
                  Text(e.key, style: const TextStyle(color: Colors.white70, fontFamily: 'monospace', fontSize: 12)),
                  const Spacer(),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                    decoration: BoxDecoration(
                      color: Colors.redAccent.withOpacity(0.15),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Text('${e.value}', style: const TextStyle(color: Colors.redAccent, fontSize: 11, fontWeight: FontWeight.w600)),
                  ),
                ],
              ),
            )),
          ],
        ),
      ),
    );
  }

  Widget _buildSonTehditler(List<ThreatRecord> recent) {
    if (recent.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Padding(
            padding: EdgeInsets.only(left: 4, bottom: 8),
            child: Text('SON TEHDİTLER', style: TextStyle(color: Colors.grey, fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 1)),
          ),
          ...recent.take(30).map((t) => _tehditKart(t)),
        ],
      ),
    );
  }

  Widget _tehditKart(ThreatRecord t) {
    final tur = t.threats.isNotEmpty ? t.threats.first.type : 'unknown';
    final detay = t.threats.isNotEmpty ? t.threats.first.detail : 'Şüpheli aktivite';
    final dt = DateTime.fromMillisecondsSinceEpoch(t.timestamp);
    final saat = '${dt.hour.toString().padLeft(2,'0')}:${dt.minute.toString().padLeft(2,'0')}:${dt.second.toString().padLeft(2,'0')}';

    return GestureDetector(
      onTap: () => _showThreatDetail(context, t),
      child: Container(
        margin: const EdgeInsets.only(bottom: 4),
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: const Color(0xFF12121A),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: _severityColor(t.severity).withOpacity(0.15)),
        ),
        child: Row(
          children: [
            Container(
              width: 36, height: 36,
              decoration: BoxDecoration(
                color: _severityColor(t.severity).withOpacity(0.1),
                borderRadius: BorderRadius.circular(10),
              ),
              child: Icon(_threatIcon(tur), color: _severityColor(t.severity), size: 18),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Text(t.ip, style: const TextStyle(color: Colors.white, fontSize: 12, fontFamily: 'monospace', fontWeight: FontWeight.w600)),
                      const SizedBox(width: 6),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
                        decoration: BoxDecoration(
                          color: _severityColor(t.severity).withOpacity(0.15),
                          borderRadius: BorderRadius.circular(4),
                        ),
                        child: Text(t.severity.toUpperCase(), style: TextStyle(color: _severityColor(t.severity), fontSize: 8, fontWeight: FontWeight.w600)),
                      ),
                    ],
                  ),
                  const SizedBox(height: 2),
                  Text(detay, style: const TextStyle(color: Colors.white38, fontSize: 10), maxLines: 1, overflow: TextOverflow.ellipsis),
                  const SizedBox(height: 2),
                  Row(
                    children: [
                      Icon(Icons.access_time, size: 8, color: Colors.grey),
                      const SizedBox(width: 3),
                      Text(saat, style: const TextStyle(color: Colors.grey, fontSize: 9)),
                      const SizedBox(width: 8),
                      Icon(Icons.route, size: 8, color: Colors.grey),
                      const SizedBox(width: 3),
                      Expanded(child: Text(t.path, style: const TextStyle(color: Colors.grey, fontSize: 9), maxLines: 1, overflow: TextOverflow.ellipsis)),
                    ],
                  ),
                ],
              ),
            ),
            Icon(Icons.chevron_right, color: Colors.white24, size: 16),
          ],
        ),
      ),
    );
  }

  void _showThreatDetail(BuildContext context, ThreatRecord t) {
    showModalBottomSheet(
      context: context,
      backgroundColor: const Color(0xFF0F0F0F),
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
      builder: (_) {
        return FutureBuilder<IpGeoInfo?>(
          future: GeoService.lookup(t.ip),
          builder: (ctx, snap) {
            final geo = snap.data;
            return Padding(
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 32),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Center(child: Container(width: 40, height: 4, decoration: BoxDecoration(color: Colors.white24, borderRadius: BorderRadius.circular(2)))),
                  const SizedBox(height: 16),
                  Row(
                    children: [
                      Container(
                        width: 44, height: 44,
                        decoration: BoxDecoration(
                          color: _severityColor(t.severity).withOpacity(0.1),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Icon(_threatIcon(t.threats.isNotEmpty ? t.threats.first.type : 'unknown'), color: _severityColor(t.severity), size: 22),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(t.ip, style: const TextStyle(color: Colors.white, fontSize: 16, fontWeight: FontWeight.bold, fontFamily: 'monospace')),
                            Row(
                              children: [
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
                                  decoration: BoxDecoration(
                                    color: _severityColor(t.severity).withOpacity(0.15),
                                    borderRadius: BorderRadius.circular(4),
                                  ),
                                  child: Text(t.severity.toUpperCase(), style: TextStyle(color: _severityColor(t.severity), fontSize: 10, fontWeight: FontWeight.w600)),
                                ),
                                const SizedBox(width: 6),
                                Text('Tehdit Detayı', style: TextStyle(color: Colors.grey, fontSize: 11)),
                              ],
                            ),
                          ],
                        ),
                      ),
                      if (t.blocked)
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                          decoration: BoxDecoration(
                            color: Colors.red.withOpacity(0.15),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: const Text('ENGELLENDİ', style: TextStyle(color: Colors.redAccent, fontSize: 9, fontWeight: FontWeight.bold)),
                        ),
                    ],
                  ),
                  const SizedBox(height: 20),
                  if (geo != null) ...[
                    _detaySatir(Icons.public, 'Konum', '${countryFlag(geo.countryCode)} ${geo.location}'),
                    const Divider(color: Colors.white10, height: 1),
                  ],
                  _detaySatir(Icons.access_time, 'Zaman', '${dtString(t.timestamp)}'),
                  const Divider(color: Colors.white10, height: 1),
                  _detaySatir(Icons.route_outlined, 'Sayfa', t.path),
                  const Divider(color: Colors.white10, height: 1),
                  _detaySatir(Icons.http, 'Metod', t.method),
                  const Divider(color: Colors.white10, height: 1),
                  ...t.threats.map((th) => Column(
                    children: [
                      _detaySatir(Icons.warning_amber_rounded, _threatName(th.type), th.detail, c: _severityColor(th.severity)),
                      const Divider(color: Colors.white10, height: 1),
                    ],
                  )),
                  _detaySatir(Icons.fingerprint, 'Kayıt ID', t.id.replaceAll('threat_', ''), mono: true),
                ],
              ),
            );
          },
        );
      },
    );
  }

  Widget _detaySatir(IconData icon, String label, String value, {bool mono = false, Color? c}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 15, color: c ?? Colors.cyanAccent),
          const SizedBox(width: 10),
          SizedBox(width: 80, child: Text(label, style: const TextStyle(color: Colors.white38, fontSize: 11))),
          Expanded(
            child: Text(value, style: TextStyle(color: Colors.white70, fontSize: 12, fontFamily: mono ? 'monospace' : null)),
          ),
        ],
      ),
    );
  }

  IconData _threatIcon(String type) {
    switch (type) {
      case 'sql_injection': return Icons.storage;
      case 'xss': return Icons.code;
      case 'path_traversal': return Icons.folder_open;
      case 'scanner': return Icons.radar;
      case 'bad_ua': return Icons.android;
      case 'rate_limit': return Icons.speed;
      case 'large_payload': return Icons.cloud_upload;
      case 'bad_method': return Icons.http;
      case 'prototype_pollution': return Icons.bug_report;
      default: return Icons.warning_amber_rounded;
    }
  }

  Color _threatColor(String type) {
    switch (type) {
      case 'sql_injection': return Colors.redAccent;
      case 'xss': return Colors.redAccent;
      case 'path_traversal': return Colors.orangeAccent;
      case 'scanner': return Colors.orangeAccent;
      case 'rate_limit': return Colors.amber;
      case 'bad_ua': return Colors.amber;
      default: return Colors.cyanAccent;
    }
  }

  String _threatName(String type) {
    switch (type) {
      case 'sql_injection': return 'SQL Enjeksiyon';
      case 'xss': return 'XSS Saldırısı';
      case 'path_traversal': return 'Path Tarama';
      case 'scanner': return 'Güvenlik Tarayıcı';
      case 'bad_ua': return 'Şüpheli Bot';
      case 'rate_limit': return 'Rate-Limit Aşımı';
      case 'large_payload': return 'Büyük Payload';
      case 'bad_method': return 'Anormal Metod';
      case 'prototype_pollution': return 'Prototype Pollution';
      default: return type;
    }
  }

  String dtString(int ts) {
    if (ts == 0) return 'Bilinmiyor';
    final dt = DateTime.fromMillisecondsSinceEpoch(ts);
    final ay = ['Ocak','Şubat','Mart','Nisan','Mayıs','Haziran','Temmuz','Ağustos','Eylül','Ekim','Kasım','Aralık'];
    return '${dt.day} ${ay[dt.month - 1]} ${dt.year} ${dt.hour.toString().padLeft(2,'0')}:${dt.minute.toString().padLeft(2,'0')}:${dt.second.toString().padLeft(2,'0')}';
  }
}

class GuvenlikDuvariEkrani extends StatefulWidget {
  const GuvenlikDuvariEkrani({super.key});

  @override
  State<GuvenlikDuvariEkrani> createState() => _GuvenlikDuvariEkraniState();
}

class _GuvenlikDuvariEkraniState extends State<GuvenlikDuvariEkrani> with TickerProviderStateMixin {
  final _scaffoldKey = GlobalKey<ScaffoldState>();
  List<Bildirim> _canliBildirimler = [];
  AnimationController? _animController;
  Timer? _canliTimer;

  @override
  void initState() {
    super.initState();
    _animController = AnimationController(vsync: this, duration: const Duration(milliseconds: 300));
    BildirimService.addListener(_yeniBildirimGeldi);

    _canliTimer = Timer.periodic(const Duration(seconds: 5), (_) {
      if (mounted && _canliBildirimler.isNotEmpty) {
        setState(() {
          if (_canliBildirimler.length > 5) {
            _canliBildirimler = _canliBildirimler.sublist(0, 5);
          }
        });
      }
    });
  }

  @override
  void dispose() {
    BildirimService.removeListener(_yeniBildirimGeldi);
    _animController?.dispose();
    _canliTimer?.cancel();
    super.dispose();
  }

  void _yeniBildirimGeldi(Bildirim b) {
    if (!mounted) return;
    setState(() {
      _canliBildirimler.insert(0, b);
      if (_canliBildirimler.length > 20) {
        _canliBildirimler = _canliBildirimler.sublist(0, 20);
      }
    });
    _animController?.forward(from: 0);
    Future.delayed(const Duration(seconds: 6), () {
      if (mounted) {
        setState(() {
          _canliBildirimler.remove(b);
        });
      }
    });
  }

  Color _severityColor(String s) {
    switch (s) {
      case 'critical': return Colors.redAccent;
      case 'high': return Colors.orangeAccent;
      case 'medium': return Colors.amber;
      case 'low': return Colors.blueAccent;
      default: return Colors.grey;
    }
  }

  IconData _typeIcon(String type) {
    switch (type) {
      case 'sql_injection': return Icons.storage;
      case 'xss': return Icons.code;
      case 'path_traversal': return Icons.folder_open;
      case 'scanner': return Icons.radar;
      case 'bad_ua': return Icons.android;
      case 'rate_limit': return Icons.speed;
      case 'large_payload': return Icons.cloud_upload;
      case 'bad_method': return Icons.http;
      case 'prototype_pollution': return Icons.bug_report;
      case 'sensitive_file': return Icons.description;
      default: return Icons.warning_amber_rounded;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      key: _scaffoldKey,
      backgroundColor: const Color(0xFF0A0A0A),
      appBar: AppBar(
        backgroundColor: const Color(0xFF0A0A0A),
        iconTheme: const IconThemeData(color: Colors.white),
        title: const Text('Güvenlik Duvarı',
          style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 16)),
        actions: [
          if (_canliBildirimler.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: Colors.redAccent.withOpacity(0.15),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Container(width: 6, height: 6, decoration: const BoxDecoration(color: Colors.redAccent, shape: BoxShape.circle)),
                    const SizedBox(width: 4),
                    Text('CANLI', style: const TextStyle(color: Colors.redAccent, fontSize: 10, fontWeight: FontWeight.bold)),
                  ],
                ),
              ),
            ),
          IconButton(
            icon: const Icon(Icons.notifications_off_outlined, color: Colors.grey, size: 20),
            onPressed: () => BildirimService.tumunuOkunduYap().then((_) => setState(() {})),
            tooltip: 'Bildirimleri temizle',
          ),
        ],
      ),
      body: Stack(
        children: [
          const GuvenlikDuvari(),
          if (_canliBildirimler.isNotEmpty)
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              child: _buildCanliBildirimBar(),
            ),
        ],
      ),
    );
  }

  Widget _buildCanliBildirimBar() {
    return Container(
      padding: const EdgeInsets.only(top: 4),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: _canliBildirimler.take(4).toList().asMap().entries.map((e) {
          final b = e.value;
          final isCritical = b.severity == 'critical' || b.severity == 'high';
          return AnimatedBuilder(
            animation: _animController!,
            builder: (ctx, child) {
              final progress = _animController!.value;
              final offset = (1 - progress) * 40;
              return Transform.translate(
                offset: Offset(0, offset * (e.key + 1).toDouble()),
                child: Opacity(
                  opacity: progress.clamp(0.2, 1.0),
                  child: child,
                ),
              );
            },
            child: Dismissible(
              key: Key(b.id),
              direction: DismissDirection.endToStart,
              onDismissed: (_) {
                setState(() => _canliBildirimler.remove(b));
                BildirimService.okunduIsaretle(b.id);
              },
              background: Container(color: Colors.redAccent.withOpacity(0.3)),
              child: GestureDetector(
                onTap: () => BildirimService.okunduIsaretle(b.id),
                child: Container(
                  margin: const EdgeInsets.fromLTRB(12, 2, 12, 2),
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                  decoration: BoxDecoration(
                    color: isCritical ? Colors.red.withOpacity(0.12) : const Color(0xFF12121A),
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(
                      color: _severityColor(b.severity).withOpacity(isCritical ? 0.4 : 0.15),
                    ),
                  ),
                  child: Row(
                    children: [
                      Icon(_typeIcon(b.type), color: _severityColor(b.severity), size: 16),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Row(
                              children: [
                                Text(b.ip, style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w600, fontFamily: 'monospace')),
                                const SizedBox(width: 4),
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
                                  decoration: BoxDecoration(
                                    color: _severityColor(b.severity).withOpacity(0.15),
                                    borderRadius: BorderRadius.circular(3),
                                  ),
                                  child: Text(b.severity.toUpperCase(), style: TextStyle(color: _severityColor(b.severity), fontSize: 7, fontWeight: FontWeight.bold)),
                                ),
                              ],
                            ),
                            const SizedBox(height: 1),
                            Text(
                              b.message.length > 60 ? '${b.message.substring(0, 60)}...' : b.message,
                              style: const TextStyle(color: Colors.white38, fontSize: 9),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 4),
                      Text(b.zaman, style: const TextStyle(color: Colors.grey, fontSize: 8)),
                    ],
                  ),
                ),
              ),
            ),
          );
        }).toList(),
      ),
    );
  }
}


