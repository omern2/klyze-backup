import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class AuditEntry {
  final String id;
  final String action;
  final String node;
  final String childId;
  final String path;
  final String key;
  final String? previousValue;
  final String? newValue;
  final String? authUid;
  final int timestamp;
  final String severity;
  final String message;

  AuditEntry({
    required this.id,
    required this.action,
    required this.node,
    required this.childId,
    required this.path,
    required this.key,
    this.previousValue,
    this.newValue,
    this.authUid,
    required this.timestamp,
    required this.severity,
    required this.message,
  });

  factory AuditEntry.fromJson(String id, Map<String, dynamic> json) {
    return AuditEntry(
      id: id,
      action: json['action'] as String? ?? 'unknown',
      node: json['node'] as String? ?? '',
      childId: json['childId'] as String? ?? '',
      path: json['path'] as String? ?? json['node'] as String? ?? '',
      key: json['key'] as String? ?? '',
      previousValue: json['previousValue'] as String?,
      newValue: json['newValue'] as String?,
      authUid: json['authUid'] as String?,
      timestamp: json['timestamp'] as int? ?? DateTime.now().millisecondsSinceEpoch,
      severity: json['severity'] as String? ?? 'info',
      message: json['message'] as String? ?? '',
    );
  }

  String get zaman {
    if (timestamp == 0) return '';
    final dt = DateTime.fromMillisecondsSinceEpoch(timestamp);
    final now = DateTime.now();
    final diff = now.difference(dt);
    if (diff.inSeconds < 60) return '${diff.inSeconds}s';
    if (diff.inMinutes < 60) return '${diff.inMinutes}d';
    if (diff.inHours < 24) return '${diff.inHours}s';
    return '${dt.day}/${dt.month} ${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
  }

  String get kisaUid {
    if (authUid == null || authUid!.isEmpty) return 'Anonim';
    return authUid!.length > 12 ? '${authUid!.substring(0, 12)}...' : authUid!;
  }
}

class AuditService {
  static const String _baseUrl = 'https://klyzegg-default-rtdb.firebaseio.com';

  static List<AuditEntry> _loglar = [];
  static Timer? _timer;
  static int _sonTimestamp = 0;
  static int _sonSayi = 0;

  static List<AuditEntry> get loglar => List.unmodifiable(_loglar);

  static void baslat() {
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 10), (_) => _guncelle());
    _guncelle();
  }

  static void durdur() {
    _timer?.cancel();
    _timer = null;
  }

  static Future<void> _guncelle() async {
    try {
      final yeni = await getAuditLogs(limit: 50);
      if (yeni.isEmpty) return;

      if (_loglar.isEmpty) {
        _loglar = yeni;
        if (yeni.isNotEmpty) _sonSayi = yeni.first.timestamp;
        return;
      }

      final eskiIds = _loglar.map((e) => e.id).toSet();
      final gercektenYeni = yeni.where((e) => !eskiIds.contains(e.id)).toList();

      if (gercektenYeni.isNotEmpty) {
        _loglar = [...gercektenYeni, ..._loglar];
        if (_loglar.length > 200) _loglar = _loglar.sublist(0, 200);
        _sonSayi = _loglar.first.timestamp;
      }
    } catch (e) {
      debugPrint('Audit guncelleme hatasi: $e');
    }
  }

  static Future<List<AuditEntry>> getAuditLogs({int limit = 100}) async {
    try {
      final uri = Uri.parse('$_baseUrl/audit.json').replace(
        queryParameters: {
          'orderBy': '"\$key"',
          'limitToLast': limit.toString(),
        },
      );
      final response = await http.get(uri).timeout(const Duration(seconds: 15));
      if (response.statusCode != 200) return [];

      final raw = response.body.trim();
      if (raw == 'null' || raw.isEmpty) return [];
      final data = json.decode(raw) as Map<String, dynamic>?;
      if (data == null || data.isEmpty) return [];

      final list = data.entries
          .where((e) => e.value is Map)
          .map((e) => AuditEntry.fromJson(e.key, e.value as Map<String, dynamic>))
          .toList();
      list.sort((a, b) => b.timestamp.compareTo(a.timestamp));
      return list;
    } catch (e) {
      debugPrint('Audit servis hatasi: $e');
      return [];
    }
  }

  static List<AuditEntry> kritikOlanlar() {
    return _loglar.where((e) => e.severity == 'critical').toList();
  }

  static List<AuditEntry> nodeFiltrele(String node) {
    return _loglar.where((e) => e.node == node).toList();
  }

  static int bugunSayisi() {
    final now = DateTime.now();
    final bugun = DateTime(now.year, now.month, now.day).millisecondsSinceEpoch;
    return _loglar.where((e) => e.timestamp >= bugun).length;
  }

  static int kritikSayisi() {
    return _loglar.where((e) => e.severity == 'critical').length;
  }

  static void temizle() {
    _loglar.clear();
  }
}