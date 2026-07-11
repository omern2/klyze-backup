import 'dart:async';
import 'package:flutter/material.dart';
import '../services/audit_service.dart';

class FirebaseLogScreen extends StatefulWidget {
  const FirebaseLogScreen({super.key});

  @override
  State<FirebaseLogScreen> createState() => _FirebaseLogScreenState();
}

class _FirebaseLogScreenState extends State<FirebaseLogScreen> {
  Timer? _timer;
  String _filtre = 'all';

  @override
  void initState() {
    super.initState();
    AuditService.baslat();
    _timer = Timer.periodic(const Duration(seconds: 5), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    AuditService.durdur();
    super.dispose();
  }

  List<AuditEntry> _filtrelenmis() {
    final loglar = AuditService.loglar;
    if (_filtre == 'all') return loglar;
    return loglar.where((e) => e.severity == _filtre).toList();
  }

  Color _severityColor(String s) {
    switch (s) {
      case 'critical': return Colors.redAccent;
      case 'warning': return Colors.orangeAccent;
      case 'info': return Colors.cyanAccent;
      default: return Colors.grey;
    }
  }

  IconData _actionIcon(String action) {
    switch (action) {
      case 'sil': return Icons.delete_outline;
      case 'create': return Icons.add_circle_outline;
      case 'update': return Icons.edit_outlined;
      case 'toplu': return Icons.warning_amber_rounded;
      default: return Icons.info_outline;
    }
  }

  @override
  Widget build(BuildContext context) {
    final loglar = _filtrelenmis();
    final bugun = AuditService.bugunSayisi();
    final kritik = AuditService.kritikSayisi();

    return Scaffold(
      backgroundColor: const Color(0xFF0A0A0A),
      appBar: AppBar(
        backgroundColor: const Color(0xFF0A0A0A),
        iconTheme: const IconThemeData(color: Colors.white),
        title: Text(
          'Firebase Güvenlik',
          style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 16),
        ),
        actions: [
          PopupMenuButton<String>(
            icon: const Icon(Icons.filter_list, color: Colors.grey, size: 20),
            onSelected: (v) => setState(() => _filtre = v),
            color: const Color(0xFF1A1A2E),
            itemBuilder: (_) => [
              PopupMenuItem(value: 'all', child: Text('Tümü', style: TextStyle(color: _filtre == 'all' ? Colors.cyanAccent : Colors.white70))),
              PopupMenuItem(value: 'critical', child: Text('Critical', style: TextStyle(color: _filtre == 'critical' ? Colors.redAccent : Colors.white70))),
              PopupMenuItem(value: 'warning', child: Text('Warning', style: TextStyle(color: _filtre == 'warning' ? Colors.orangeAccent : Colors.white70))),
              PopupMenuItem(value: 'info', child: Text('Info', style: TextStyle(color: _filtre == 'info' ? Colors.cyanAccent : Colors.white70))),
            ],
          ),
        ],
      ),
      body: Column(
        children: [
          _buildOzet(bugun, kritik, AuditService.loglar.length),
          Expanded(child: _buildLogListesi(loglar)),
        ],
      ),
    );
  }

  Widget _buildOzet(int bugun, int kritik, int toplam) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      child: Row(
        children: [
          _ozetKart(Icons.today, '$bugun', 'Bugün', Colors.cyanAccent),
          const SizedBox(width: 6),
          _ozetKart(Icons.warning_amber_rounded, '$kritik', 'Kritik', Colors.redAccent),
          const SizedBox(width: 6),
          _ozetKart(Icons.list, '$toplam', 'Toplam', Colors.grey),
        ],
      ),
    );
  }

  Widget _ozetKart(IconData icon, String value, String label, Color color) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 6),
        decoration: BoxDecoration(
          color: const Color(0xFF12121A),
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: color.withOpacity(0.2)),
        ),
        child: Column(
          children: [
            Icon(icon, color: color, size: 16),
            const SizedBox(height: 2),
            Text(value, style: TextStyle(color: color, fontSize: 14, fontWeight: FontWeight.bold)),
            Text(label, style: const TextStyle(color: Colors.grey, fontSize: 8)),
          ],
        ),
      ),
    );
  }

  Widget _buildLogListesi(List<AuditEntry> loglar) {
    if (loglar.isEmpty) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 64, height: 64,
              decoration: BoxDecoration(
                color: Colors.green.withOpacity(0.08),
                borderRadius: BorderRadius.circular(20),
              ),
              child: const Icon(Icons.check_circle_outline, color: Colors.greenAccent, size: 32),
            ),
            const SizedBox(height: 16),
            const Text('Firebase güvende',
              style: TextStyle(color: Colors.white54, fontSize: 16, fontWeight: FontWeight.w600)),
            const SizedBox(height: 8),
            Text('Henüz işlem kaydı yok.\n10 saniyede bir otomatik güncellenir.',
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.grey, fontSize: 12, height: 1.4)),
          ],
        ),
      );
    }

    return ListView.builder(
      padding: const EdgeInsets.fromLTRB(12, 4, 12, 24),
      itemCount: loglar.length,
      itemBuilder: (_, i) => _logItem(loglar[i]),
    );
  }

  Widget _logItem(AuditEntry entry) {
    final severity = entry.severity;
    final isImportant = severity == 'critical' || severity == 'warning';

    return GestureDetector(
      onTap: () => _showDetail(context, entry),
      child: Container(
        margin: const EdgeInsets.only(bottom: 4),
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
          color: severity == 'critical' ? Colors.red.withOpacity(0.06) :
                 severity == 'warning' ? Colors.orange.withOpacity(0.05) :
                 const Color(0xFF12121A),
          borderRadius: BorderRadius.circular(10),
          border: Border.all(
            color: _severityColor(severity).withOpacity(isImportant ? 0.25 : 0.1),
          ),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              padding: const EdgeInsets.all(6),
              decoration: BoxDecoration(
                color: _severityColor(severity).withOpacity(0.1),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Icon(_actionIcon(entry.action), color: _severityColor(severity), size: 14),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
                        decoration: BoxDecoration(
                          color: _severityColor(severity).withOpacity(0.15),
                          borderRadius: BorderRadius.circular(3),
                        ),
                        child: Text(severity.toUpperCase(),
                          style: TextStyle(color: _severityColor(severity), fontSize: 8, fontWeight: FontWeight.bold)),
                      ),
                      const SizedBox(width: 4),
                      Expanded(
                        child: Text(entry.message,
                          style: TextStyle(
                            color: severity == 'critical' ? Colors.redAccent.shade200 : Colors.white70,
                            fontSize: 11, fontWeight: isImportant ? FontWeight.w600 : FontWeight.normal,
                          ),
                          maxLines: 2, overflow: TextOverflow.ellipsis),
                      ),
                    ],
                  ),
                  const SizedBox(height: 2),
                  Row(
                    children: [
                      Icon(Icons.access_time, size: 8, color: Colors.grey),
                      const SizedBox(width: 2),
                      Text(entry.zaman, style: const TextStyle(color: Colors.grey, fontSize: 9)),
                      const SizedBox(width: 6),
                      Icon(Icons.folder_outlined, size: 8, color: Colors.grey),
                      const SizedBox(width: 2),
                      Expanded(
                        child: Text(entry.node,
                          style: const TextStyle(color: Colors.grey, fontSize: 9),
                          overflow: TextOverflow.ellipsis),
                      ),
                    ],
                  ),
                ],
              ),
            ),
            Icon(Icons.chevron_right, color: Colors.white24, size: 14),
          ],
        ),
      ),
    );
  }

  void _showDetail(BuildContext context, AuditEntry entry) {
    showModalBottomSheet(
      context: context,
      backgroundColor: const Color(0xFF0F0F0F),
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
      builder: (_) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Center(child: Container(width: 40, height: 4, decoration: BoxDecoration(color: Colors.white24, borderRadius: BorderRadius.circular(2)))),
              const SizedBox(height: 20),
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: _severityColor(entry.severity).withOpacity(0.1),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Icon(_actionIcon(entry.action), color: _severityColor(entry.severity), size: 22),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(entry.message,
                          style: const TextStyle(color: Colors.white, fontSize: 14, fontWeight: FontWeight.w600)),
                        const SizedBox(height: 2),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
                          decoration: BoxDecoration(
                            color: _severityColor(entry.severity).withOpacity(0.15),
                            borderRadius: BorderRadius.circular(4),
                          ),
                          child: Text(entry.severity.toUpperCase(),
                            style: TextStyle(color: _severityColor(entry.severity), fontSize: 10, fontWeight: FontWeight.w600)),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 20),
              _detaySatir(Icons.folder, 'Node', entry.node),
              const Divider(color: Colors.white10, height: 1),
              _detaySatir(Icons.vpn_key, 'Key', entry.key),
              const Divider(color: Colors.white10, height: 1),
              _detaySatir(Icons.route_outlined, 'Path', entry.path),
              const Divider(color: Colors.white10, height: 1),
              _detaySatir(Icons.person_outline, 'Kullanıcı', entry.kisaUid),
              const Divider(color: Colors.white10, height: 1),
              if (entry.previousValue != null) ...[
                _detaySatir(Icons.arrow_back, 'Eski Değer', entry.previousValue!, mono: true),
                const Divider(color: Colors.white10, height: 1),
              ],
              if (entry.newValue != null) ...[
                _detaySatir(Icons.arrow_forward, 'Yeni Değer', entry.newValue!, mono: true),
                const Divider(color: Colors.white10, height: 1),
              ],
              _detaySatir(Icons.access_time, 'Zaman', _dtStr(entry.timestamp)),
            ],
          ),
        );
      },
    );
  }

  Widget _detaySatir(IconData icon, String label, String value, {bool mono = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 14, color: Colors.cyanAccent),
          const SizedBox(width: 8),
          SizedBox(width: 80, child: Text(label, style: const TextStyle(color: Colors.white38, fontSize: 11))),
          Expanded(
            child: Text(value, style: TextStyle(color: Colors.white70, fontSize: 11, fontFamily: mono ? 'monospace' : null)),
          ),
        ],
      ),
    );
  }

  String _dtStr(int ts) {
    if (ts == 0) return 'Bilinmiyor';
    final dt = DateTime.fromMillisecondsSinceEpoch(ts);
    final ay = ['Ocak','Subat','Mart','Nisan','Mayis','Haziran','Temmuz','Agustos','Eylul','Ekim','Kasim','Aralik'];
    return '${dt.day} ${ay[dt.month - 1]} ${dt.year} ${dt.hour.toString().padLeft(2,'0')}:${dt.minute.toString().padLeft(2,'0')}:${dt.second.toString().padLeft(2,'0')}';
  }
}