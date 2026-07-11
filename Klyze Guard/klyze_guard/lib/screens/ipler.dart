import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:fl_chart/fl_chart.dart';
import '../services/firebase_service.dart';
import '../services/vercel_service.dart';
import '../widgets/ip_card.dart';
import 'guvenlik_duvari.dart';

class IPlerScreen extends StatefulWidget {
  const IPlerScreen({super.key});

  @override
  State<IPlerScreen> createState() => _IPlerScreenState();
}

class _IPlerScreenState extends State<IPlerScreen> {
  List<IpRecord> _records = [];
  VercelDeploy? _lastDeploy;
  bool _loading = true;
  String? _hata;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _loadData();
    _timer = Timer.periodic(const Duration(seconds: 30), (_) => _loadData());
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _loadData() async {
    if (!_loading) setState(() { _hata = null; });
    try {
      final results = await Future.wait([
        FirebaseService.getIps(limit: 200),
        VercelService.getLastDeploy(),
      ]).timeout(const Duration(seconds: 20));
      if (mounted) {
        setState(() {
          _records = results[0] as List<IpRecord>;
          _lastDeploy = results[1] as VercelDeploy?;
          _loading = false;
        });
      }
    } catch (e) {
      if (mounted) setState(() { _hata = 'Veri yüklenemedi: $e'; _loading = false; });
    }
  }

  static const _gunler = [
    'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe',
    'Cuma', 'Cumartesi', 'Pazar'
  ];

  String _gunAdi(DateTime dt) => _gunler[dt.weekday - 1];

  Map<String, int> _getDayStats() {
    final map = <String, int>{};
    final now = DateTime.now();
    for (int i = 6; i >= 0; i--) {
      map[_gunAdi(now.subtract(Duration(days: i)))] = 0;
    }
    for (final r in _records) {
      final dt = DateTime.fromMillisecondsSinceEpoch(r.timestamp);
      final diff = now.difference(dt).inDays;
      if (diff >= 0 && diff <= 6) {
        map[_gunAdi(dt)] = (map[_gunAdi(dt)] ?? 0) + 1;
      }
    }
    return map;
  }

  Map<String, int> _getIpCounts() {
    final map = <String, int>{};
    for (final r in _records) {
      map[r.ip] = (map[r.ip] ?? 0) + 1;
    }
    final sorted = map.entries.toList()..sort((a, b) => b.value.compareTo(a.value));
    return Map.fromEntries(sorted.take(5));
  }

  String _formatDate(int ts) {
    if (ts == 0) return 'Bilinmiyor';
    final dt = DateTime.fromMillisecondsSinceEpoch(ts);
    final ay = ['Oca','Şub','Mar','Nis','May','Haz',
                'Tem','Ağu','Eyl','Eki','Kas','Ara'];
    return '${dt.day} ${ay[dt.month - 1]} ${dt.year} '
        '${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
  }

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        backgroundColor: const Color(0xFF0A0A0A),
        appBar: AppBar(
          backgroundColor: const Color(0xFF0A0A0A),
          iconTheme: const IconThemeData(color: Colors.white),
          title: const Text('Site Güvenlik Paneli',
            style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 16)),
          bottom: PreferredSize(
            preferredSize: const Size.fromHeight(44),
            child: Container(
              margin: const EdgeInsets.symmetric(horizontal: 16),
              decoration: BoxDecoration(
                color: const Color(0xFF1A1A2E),
                borderRadius: BorderRadius.circular(12),
              ),
              child: TabBar(
                indicator: BoxDecoration(
                  color: Colors.cyanAccent.withOpacity(0.15),
                  borderRadius: BorderRadius.circular(12),
                ),
                indicatorSize: TabBarIndicatorSize.tab,
                labelColor: Colors.cyanAccent,
                unselectedLabelColor: Colors.grey,
                labelStyle: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
                unselectedLabelStyle: const TextStyle(fontSize: 12),
                dividerColor: Colors.transparent,
                tabs: const [
                  Tab(text: 'IP TAKİP', icon: Icon(Icons.language, size: 16)),
                  Tab(text: 'GÜVENLİK DUVARI', icon: Icon(Icons.shield, size: 16)),
                ],
              ),
            ),
          ),
        ),
        body: TabBarView(
          children: [
            _buildIpTakip(),
            const GuvenlikDuvari(),
          ],
        ),
      ),
    );
  }

  Widget _buildIpTakip() {
    final dayStats = _getDayStats();
    final topIps = _getIpCounts();
    final maxDayVal = dayStats.values.fold<int>(0, max);
    final tekilIp = _records.map((r) => r.ip).toSet().length;

    if (_loading) {
      return const Center(child: CircularProgressIndicator(color: Colors.cyanAccent));
    }

    return RefreshIndicator(
      onRefresh: _loadData,
      color: Colors.cyanAccent,
      backgroundColor: const Color(0xFF1A1A2E),
      child: ListView(
        padding: const EdgeInsets.only(top: 12, bottom: 40),
        children: [
          if (_hata != null) _buildHata(),
          if (_lastDeploy != null) _buildVercelPanel(),
          _buildOzet(tekilIp),
          if (_records.isEmpty)
            _buildEmptyState()
          else ...[
            _buildChart(dayStats, maxDayVal),
            _buildTopIps(topIps),
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 20, 16, 8),
              child: Text('SON ZİYARETÇİLER',
                style: TextStyle(color: Colors.grey, fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 1)),
            ),
            ..._records.take(50).map((r) => IpCard(record: r)),
          ],
        ],
      ),
    );
  }

  Widget _buildHata() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: Colors.red.withOpacity(0.1),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: Colors.red.withOpacity(0.3)),
        ),
        child: Row(
          children: [
            const Icon(Icons.warning_amber_rounded, color: Colors.redAccent, size: 20),
            const SizedBox(width: 10),
            Expanded(child: Text(_hata!, style: const TextStyle(color: Colors.redAccent, fontSize: 13))),
          ],
        ),
      ),
    );
  }

  Widget _buildVercelPanel() {
    final deploy = _lastDeploy!;
    final isReady = deploy.state == 'READY';
    final deployIdKisa = deploy.id.length > 20
        ? '${deploy.id.substring(0, 10)}...${deploy.id.substring(deploy.id.length - 8)}'
        : deploy.id;

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          gradient: LinearGradient(
            colors: [
              isReady ? const Color(0xFF0D2818) : const Color(0xFF2E0A0A),
              const Color(0xFF0A0A0A),
            ],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          ),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: isReady ? Colors.green.withOpacity(0.3) : Colors.red.withOpacity(0.3)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(isReady ? Icons.check_circle : Icons.error, color: isReady ? Colors.greenAccent : Colors.redAccent, size: 18),
                const SizedBox(width: 8),
                Text('VERCEL DEPLOY DURUMU',
                  style: TextStyle(color: Colors.grey, fontSize: 10, fontWeight: FontWeight.w600, letterSpacing: 1)),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                _vercelInfoItem('Durum', isReady ? 'Aktif ✅' : 'Hatalı ❌', isReady ? Colors.greenAccent : Colors.redAccent),
                const SizedBox(width: 16),
                _vercelInfoItem('Deploy ID', deployIdKisa, Colors.cyanAccent),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                _vercelInfoItem('Son Deploy', _formatDate(deploy.createdAt), Colors.grey),
                const SizedBox(width: 16),
                _vercelInfoItem('Kaynak', deploy.source?.toUpperCase() ?? 'GIT', Colors.grey),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _vercelInfoItem(String label, String value, Color color) {
    return Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(color: Colors.white38, fontSize: 10)),
          const SizedBox(height: 2),
          Text(value, style: TextStyle(color: color, fontSize: 13, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }

  Widget _buildOzet(int tekilIp) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: const Color(0xFF1A1A2E),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: Colors.white10),
        ),
        child: Row(
          children: [
            _ozetItem(Icons.language, '${_records.length}', 'Toplam Ziyaret', Colors.cyanAccent),
            Container(width: 1, height: 40, color: Colors.white10),
            _ozetItem(Icons.people_outline, '$tekilIp', 'Tekil IP', Colors.greenAccent),
            Container(width: 1, height: 40, color: Colors.white10),
            _ozetItem(Icons.route_outlined, _records.isNotEmpty ? '${_records.first.path}' : '-', 'Son Sayfa', Colors.amberAccent),
          ],
        ),
      ),
    );
  }

  Widget _ozetItem(IconData icon, String value, String label, Color color) {
    return Expanded(
      child: Column(
        children: [
          Icon(icon, color: color, size: 20),
          const SizedBox(height: 6),
          Text(value, style: TextStyle(color: color, fontSize: 16, fontWeight: FontWeight.bold)),
          const SizedBox(height: 2),
          Text(label, style: const TextStyle(color: Colors.grey, fontSize: 10)),
        ],
      ),
    );
  }

  Widget _buildEmptyState() {
    return Padding(
      padding: const EdgeInsets.only(top: 40, left: 32, right: 32),
      child: Column(
        children: [
          Container(
            width: 64, height: 64,
            decoration: BoxDecoration(
              color: Colors.cyanAccent.withOpacity(0.08),
              borderRadius: BorderRadius.circular(20),
            ),
            child: const Icon(Icons.wifi_off, color: Colors.grey, size: 28),
          ),
          const SizedBox(height: 20),
          const Text('Henüz ziyaretçi verisi yok',
            style: TextStyle(color: Colors.white54, fontSize: 16, fontWeight: FontWeight.w600)),
          const SizedBox(height: 10),
          Text(
            'klyzegg.vercel.app\'e tracking API\'sini kur:\n\n'
            '1. Projeyi Vercel\'e deploy et (api/track.js ile)\n'
            '2. Web sitene şu kodu ekle:\n'
            '<script>fetch("/api/track",{method:"POST"})</script>\n\n'
            'Veriler Firebase\'e kaydedilip burada görünecek.\n'
            'Sayfa her 30 saniyede otomatik yenilenir.',
            textAlign: TextAlign.center,
            style: const TextStyle(color: Colors.grey, fontSize: 13, height: 1.5)),
          const SizedBox(height: 24),
          OutlinedButton.icon(
            onPressed: _loadData,
            icon: const Icon(Icons.refresh, size: 16),
            label: const Text('Yenile'),
            style: OutlinedButton.styleFrom(
              foregroundColor: Colors.cyanAccent,
              side: const BorderSide(color: Colors.cyanAccent, width: 1),
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildChart(Map<String, int> dayStats, int maxVal) {
    if (maxVal == 0) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: const Color(0xFF1A1A2E),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: Colors.white10),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('7 GÜNLÜK ZİYARET GRAFİĞİ',
              style: TextStyle(color: Colors.grey, fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 1)),
            const SizedBox(height: 20),
            SizedBox(
              height: 200,
              child: BarChart(
                BarChartData(
                  alignment: BarChartAlignment.spaceAround,
                  maxY: (maxVal * 1.4).ceilToDouble(),
                  barTouchData: BarTouchData(
                    touchTooltipData: BarTouchTooltipData(
                      getTooltipItem: (group, groupIndex, rod, rodIndex) {
                        final keys = dayStats.keys.toList();
                        final idx = group.x.toInt();
                        if (idx >= 0 && idx < keys.length) {
                          return BarTooltipItem(
                            '${keys[idx]}\n${rod.toY.toInt()} ziyaret',
                            const TextStyle(color: Colors.white, fontSize: 12),
                          );
                        }
                        return null;
                      },
                    ),
                  ),
                  titlesData: FlTitlesData(
                    show: true,
                    bottomTitles: AxisTitles(
                      sideTitles: SideTitles(
                        showTitles: true,
                        reservedSize: 24,
                        getTitlesWidget: (value, meta) {
                          final keys = dayStats.keys.toList();
                          final idx = value.toInt();
                          if (idx < 0 || idx >= keys.length) return const SizedBox.shrink();
                          return Padding(
                            padding: const EdgeInsets.only(top: 6),
                            child: Text(
                              keys[idx].substring(0, 3),
                              style: const TextStyle(color: Colors.grey, fontSize: 10),
                            ),
                          );
                        },
                      ),
                    ),
                    leftTitles: AxisTitles(
                      sideTitles: SideTitles(
                        showTitles: true,
                        reservedSize: 32,
                        getTitlesWidget: (value, meta) {
                          if (value == 0) return const SizedBox.shrink();
                          return Text(
                            value.toInt().toString(),
                            style: const TextStyle(color: Colors.grey, fontSize: 10),
                          );
                        },
                      ),
                    ),
                    topTitles: AxisTitles(sideTitles: SideTitles(showTitles: false)),
                    rightTitles: AxisTitles(sideTitles: SideTitles(showTitles: false)),
                  ),
                  gridData: FlGridData(
                    show: true,
                    drawVerticalLine: false,
                    getDrawingHorizontalLine: (value) => FlLine(color: Colors.white10, strokeWidth: 1),
                  ),
                  borderData: FlBorderData(show: false),
                  barGroups: dayStats.entries.toList().asMap().entries.map((e) {
                    return BarChartGroupData(
                      x: e.key,
                      barRods: [
                        BarChartRodData(
                          toY: e.value.value.toDouble(),
                          color: Colors.cyanAccent,
                          width: 22,
                          borderRadius: const BorderRadius.only(
                            topLeft: Radius.circular(4),
                            topRight: Radius.circular(4),
                          ),
                        ),
                      ],
                    );
                  }).toList(),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildTopIps(Map<String, int> topIps) {
    if (topIps.isEmpty) return const SizedBox.shrink();
    final maxCount = topIps.values.first;

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: const Color(0xFF1A1A2E),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: Colors.white10),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('EN AKTİF İP ADRESLERİ',
              style: TextStyle(color: Colors.grey, fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 1)),
            const SizedBox(height: 16),
            ...topIps.entries.map((e) {
              final oran = e.value / maxCount;
              return Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Text(e.key, style: const TextStyle(color: Colors.white70, fontFamily: 'monospace', fontSize: 13)),
                        const Spacer(),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                          decoration: BoxDecoration(
                            color: Colors.cyanAccent.withOpacity(0.15),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text('${e.value}', style: const TextStyle(color: Colors.cyanAccent, fontSize: 12, fontWeight: FontWeight.w600)),
                        ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(4),
                      child: LinearProgressIndicator(
                        value: oran,
                        backgroundColor: Colors.white10,
                        valueColor: const AlwaysStoppedAnimation<Color>(Colors.cyanAccent),
                        minHeight: 4,
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
}
