import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class ThreatDetail {
  final String type;
  final String severity;
  final String detail;

  ThreatDetail({required this.type, required this.severity, required this.detail});

  factory ThreatDetail.fromJson(Map<String, dynamic> json) {
    return ThreatDetail(
      type: json['type'] as String? ?? '',
      severity: json['severity'] as String? ?? 'low',
      detail: json['detail'] as String? ?? '',
    );
  }
}

class ThreatRecord {
  final String id;
  final String ip;
  final int timestamp;
  final String path;
  final String userAgent;
  final String method;
  final String severity;
  final List<ThreatDetail> threats;
  final bool blocked;

  ThreatRecord({
    required this.id,
    required this.ip,
    required this.timestamp,
    required this.path,
    required this.userAgent,
    required this.method,
    required this.severity,
    required this.threats,
    required this.blocked,
  });

  factory ThreatRecord.fromJson(String id, Map<String, dynamic> json) {
    final threatsRaw = json['threats'] as List<dynamic>? ?? [];
    return ThreatRecord(
      id: id,
      ip: json['ip'] as String? ?? '',
      timestamp: json['timestamp'] as int? ?? 0,
      path: json['path'] as String? ?? '/',
      userAgent: json['userAgent'] as String? ?? '',
      method: json['method'] as String? ?? 'GET',
      severity: json['severity'] as String? ?? 'low',
      threats: threatsRaw
          .where((e) => e is Map)
          .map((e) => ThreatDetail.fromJson(e as Map<String, dynamic>))
          .toList(),
      blocked: json['blocked'] as bool? ?? false,
    );
  }
}

class ThreatStats {
  final int total;
  final int today;
  final List<ThreatRecord> recent;
  final Map<String, int> byType;
  final Map<String, int> bySeverity;
  final Map<String, int> byIp;
  final Map<String, int> byDay;

  ThreatStats({
    required this.total,
    required this.today,
    required this.recent,
    required this.byType,
    required this.bySeverity,
    required this.byIp,
    required this.byDay,
  });

  String get securityScore {
    if (total == 0) return 'A+';
    final todayRate = today > 10 ? 1 : today > 5 ? 0.5 : 0;
    final criticalRate = (bySeverity['critical'] ?? 0) / total;
    final score = 100 - (todayRate * 10) - (criticalRate * 50);
    if (score >= 95) return 'A+';
    if (score >= 85) return 'A';
    if (score >= 70) return 'B';
    if (score >= 50) return 'C';
    return 'F';
  }
}

class TehditService {
  static const String _baseUrl = 'https://klyzegg-default-rtdb.firebaseio.com';

  static Future<ThreatStats> getStats() async {
    try {
      final records = await getThreats(limit: 200);
      final now = DateTime.now();
      final todayStart = DateTime(now.year, now.month, now.day).millisecondsSinceEpoch;

      final byType = <String, int>{};
      final bySeverity = <String, int>{};
      final byIp = <String, int>{};
      final byDay = <String, int>{};
      int today = 0;

      for (final r in records) {
        today += (r.timestamp >= todayStart) ? 1 : 0;

        final dt = DateTime.fromMillisecondsSinceEpoch(r.timestamp);
        final dayKey = '${dt.day}/${dt.month}';
        byDay[dayKey] = (byDay[dayKey] ?? 0) + 1;

        if (!byIp.containsKey(r.ip)) byIp[r.ip] = 0;
        byIp[r.ip] = (byIp[r.ip] ?? 0) + 1;

        bySeverity[r.severity] = (bySeverity[r.severity] ?? 0) + 1;

        for (final t in r.threats) {
          byType[t.type] = (byType[t.type] ?? 0) + 1;
        }
        if (r.threats.isEmpty) {
          byType['unknown'] = (byType['unknown'] ?? 0) + 1;
        }
      }

      final sortedIps = byIp.entries.toList()..sort((a, b) => b.value.compareTo(a.value));
      final topIps = Map.fromEntries(sortedIps.take(10));

      return ThreatStats(
        total: records.length,
        today: today,
        recent: records.take(50).toList(),
        byType: byType,
        bySeverity: bySeverity,
        byIp: topIps,
        byDay: byDay,
      );
    } catch (e) {
      debugPrint('Tehdit istatistik hatasi: $e');
      return ThreatStats(
        total: 0, today: 0, recent: [],
        byType: {}, bySeverity: {}, byIp: {}, byDay: {},
      );
    }
  }

  static Future<List<ThreatRecord>> getThreats({int limit = 100}) async {
    try {
      final uri = Uri.parse('$_baseUrl/tracking/threats.json').replace(
        queryParameters: {
          'orderBy': '"\$key"',
          'limitToLast': limit.toString(),
        },
      );

      final response = await http
          .get(uri)
          .timeout(const Duration(seconds: 15));

      if (response.statusCode != 200) return [];
      final raw = response.body.trim();
      if (raw == 'null' || raw.isEmpty) return [];

      final data = json.decode(raw) as Map<String, dynamic>?;
      if (data == null || data.isEmpty) return [];

      final records = data.entries
          .where((e) => e.value is Map)
          .map((e) => ThreatRecord.fromJson(e.key, e.value as Map<String, dynamic>))
          .toList();

      records.sort((a, b) => b.timestamp.compareTo(a.timestamp));
      return records;
    } catch (e) {
      debugPrint('Tehdit servis hatasi: $e');
      return [];
    }
  }
}
