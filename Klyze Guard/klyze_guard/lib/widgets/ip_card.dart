import 'package:flutter/material.dart';
import '../services/firebase_service.dart';
import '../services/geo_service.dart';

class IpCard extends StatelessWidget {
  final IpRecord record;

  const IpCard({super.key, required this.record});

  String _formatDate(int ts) {
    if (ts == 0) return 'Bilinmiyor';
    final dt = DateTime.fromMillisecondsSinceEpoch(ts);
    final ay = ['Oca','Şub','Mar','Nis','May','Haz',
                'Tem','Ağu','Eyl','Eki','Kas','Ara'];
    final gun = dt.day.toString().padLeft(2, '0');
    final saat = '${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
    return '$gun ${ay[dt.month - 1]} $saat';
  }

  String _formatFullDate(int ts) {
    if (ts == 0) return 'Bilinmiyor';
    final dt = DateTime.fromMillisecondsSinceEpoch(ts);
    final ay = ['Ocak','Şubat','Mart','Nisan','Mayıs','Haziran',
                'Temmuz','Ağustos','Eylül','Ekim','Kasım','Aralık'];
    return '${dt.day} ${ay[dt.month - 1]} ${dt.year}  '
        '${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}:${dt.second.toString().padLeft(2, '0')}';
  }

  String? _parseBrowser(String ua) {
    if (ua.isEmpty) return null;
    if (ua.contains('Chrome/') && !ua.contains('Edg/') && !ua.contains('OPR/')) return 'Chrome';
    if (ua.contains('Firefox/') && !ua.contains('Seamonkey/')) return 'Firefox';
    if (ua.contains('Safari/') && !ua.contains('Chrome/')) return 'Safari';
    if (ua.contains('Edg/')) return 'Edge';
    if (ua.contains('OPR/') || ua.contains('Opera/')) return 'Opera';
    if (ua.contains('Trident/') || ua.contains('MSIE')) return 'Internet Explorer';
    return null;
  }

  String? _parseOs(String ua) {
    if (ua.isEmpty) return null;
    if (ua.contains('Windows NT 10')) return 'Windows 10/11';
    if (ua.contains('Windows NT 6.3')) return 'Windows 8.1';
    if (ua.contains('Windows NT 6.1')) return 'Windows 7';
    if (ua.contains('Android')) return 'Android';
    if (ua.contains('iPhone') || ua.contains('iPad')) return 'iOS';
    if (ua.contains('Mac OS X')) return 'macOS';
    if (ua.contains('Linux')) return 'Linux';
    return null;
  }

  String countryFlag(String code) {
    if (code.isEmpty) return '';
    final first = code.codeUnitAt(0) - 0x61 + 0x1F1E6;
    final second = code.codeUnitAt(1) - 0x61 + 0x1F1E6;
    return String.fromCharCodes([first, second]);
  }

  void _showDetails(BuildContext context) {
    showModalBottomSheet(
      context: context,
      backgroundColor: const Color(0xFF0F0F0F),
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
      builder: (_) {
        return FutureBuilder<IpGeoInfo?>(
          future: GeoService.lookup(record.ip),
          builder: (ctx, snapshot) {
            final geo = snapshot.data;
            final loading = snapshot.connectionState == ConnectionState.waiting;

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
                        width: 48, height: 48,
                        decoration: BoxDecoration(color: const Color(0xFF1A1A2E), borderRadius: BorderRadius.circular(14)),
                        child: const Icon(Icons.language, color: Colors.cyanAccent, size: 24),
                      ),
                      const SizedBox(width: 14),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(record.ip, style: const TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold, fontFamily: 'monospace', letterSpacing: 0.5)),
                          const SizedBox(height: 2),
                          Text('IP Adresi Detayı', style: TextStyle(color: Colors.grey, fontSize: 12)),
                        ],
                      ),
                    ],
                  ),
                  const SizedBox(height: 24),
                  if (loading)
                    const Padding(
                      padding: EdgeInsets.only(bottom: 8),
                      child: Row(children: [SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.cyanAccent)), SizedBox(width: 10), Text('Konum bilgisi alınıyor...', style: TextStyle(color: Colors.grey, fontSize: 12))]),
                    ),
                  if (geo != null) ...[
                    _detailItem(Icons.public, 'Konum', '${countryFlag(geo.countryCode)} ${geo.location}', flag: true),
                    const Divider(color: Colors.white10, height: 1),
                    if (geo.isp.isNotEmpty) ...[
                      _detailItem(Icons.business, 'ISS', geo.isp),
                      const Divider(color: Colors.white10, height: 1),
                    ],
                    if (geo.org.isNotEmpty) ...[
                      _detailItem(Icons.apartment, 'Kurum', geo.org),
                      const Divider(color: Colors.white10, height: 1),
                    ],
                  ],
                  _detailItem(Icons.access_time, 'Zaman', _formatFullDate(record.timestamp)),
                  const Divider(color: Colors.white10, height: 1),
                  _detailItem(Icons.route_outlined, 'Sayfa', record.path),
                  const Divider(color: Colors.white10, height: 1),
                  _detailItem(Icons.computer_outlined, 'İşletim Sistemi', _parseOs(record.userAgent) ?? 'Bilinmiyor'),
                  const Divider(color: Colors.white10, height: 1),
                  _detailItem(Icons.language_outlined, 'Tarayıcı', _parseBrowser(record.userAgent) ?? 'Bilinmiyor'),
                  const Divider(color: Colors.white10, height: 1),
                  _detailItem(Icons.info_outline, 'User-Agent', record.userAgent.isNotEmpty ? record.userAgent : 'Yok', multiLine: true),
                  const Divider(color: Colors.white10, height: 1),
                  _detailItem(Icons.fingerprint, 'Kayıt ID', record.id, mono: true),
                ],
              ),
            );
          },
        );
      },
    );
  }

  Widget _detailItem(IconData icon, String label, String value, {bool multiLine = false, bool mono = false, bool flag = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 10),
      child: Row(
        crossAxisAlignment: multiLine ? CrossAxisAlignment.start : CrossAxisAlignment.center,
        children: [
          Icon(icon, size: 16, color: Colors.cyanAccent),
          const SizedBox(width: 10),
          SizedBox(width: 90, child: Text(label, style: const TextStyle(color: Colors.white38, fontSize: 12))),
          Expanded(
            child: Text(
              value,
              style: TextStyle(
                color: Colors.white70,
                fontSize: flag ? 16 : 13,
                fontFamily: mono ? 'monospace' : null,
              ),
            ),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () => _showDetails(context),
      child: Container(
        margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 3),
        decoration: BoxDecoration(
          color: const Color(0xFF12121A),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: Colors.white.withOpacity(0.08)),
        ),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            children: [
              Container(
                width: 42,
                height: 42,
                decoration: BoxDecoration(
                  color: const Color(0xFF1A1A2E),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: const Icon(Icons.language, color: Colors.cyanAccent, size: 20),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Text(
                          record.ip,
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 14,
                            fontWeight: FontWeight.w600,
                            fontFamily: 'monospace',
                            letterSpacing: 0.5,
                          ),
                        ),
                        const SizedBox(width: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                          decoration: BoxDecoration(
                            color: Colors.cyanAccent.withOpacity(0.1),
                            borderRadius: BorderRadius.circular(4),
                          ),
                          child: Text(
                            record.path,
                            style: const TextStyle(color: Colors.cyanAccent, fontSize: 9, fontWeight: FontWeight.w500),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Row(
                      children: [
                        Icon(Icons.access_time, size: 10, color: Colors.grey),
                        const SizedBox(width: 4),
                        Text(
                          _formatDate(record.timestamp),
                          style: const TextStyle(color: Colors.grey, fontSize: 11),
                        ),
                        if (record.userAgent.isNotEmpty) ...[
                          const SizedBox(width: 12),
                          Expanded(
                            child: Text(
                              record.userAgent,
                              style: const TextStyle(color: Colors.white24, fontSize: 10),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ],
                ),
              ),
              Icon(Icons.chevron_right, color: Colors.white24, size: 18),
            ],
          ),
        ),
      ),
    );
  }
}
