import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class IpRecord {
  final String id;
  final String ip;
  final int timestamp;
  final String userAgent;
  final String path;

  IpRecord({
    required this.id,
    required this.ip,
    required this.timestamp,
    required this.userAgent,
    required this.path,
  });

  factory IpRecord.fromJson(String id, Map<String, dynamic> json) {
    return IpRecord(
      id: id,
      ip: json['ip'] as String? ?? 'Bilinmiyor',
      timestamp: json['timestamp'] as int? ?? 0,
      userAgent: json['userAgent'] as String? ?? '',
      path: json['path'] as String? ?? '/',
    );
  }
}

class FirebaseService {
  static const String _baseUrl = 'https://klyzegg-default-rtdb.firebaseio.com';

  static Future<List<IpRecord>> getIps({int limit = 100}) async {
    try {
      final uri = Uri.parse('$_baseUrl/tracking/ips.json').replace(
        queryParameters: {
          'orderBy': '"\$key"',
          'limitToLast': limit.toString(),
        },
      );

      final response = await http
          .get(uri)
          .timeout(const Duration(seconds: 15));

      if (response.statusCode == 401 || response.statusCode == 403) {
        debugPrint('Firebase yetki hatasi: ${response.body}');
        return [];
      }

      if (response.statusCode != 200) {
        debugPrint('Firebase hata: ${response.statusCode} ${response.body}');
        return [];
      }

      final raw = response.body.trim();
      if (raw == 'null' || raw.isEmpty) return [];

      final data = json.decode(raw) as Map<String, dynamic>?;
      if (data == null || data.isEmpty) return [];

      final records = data.entries
          .where((e) => e.value is Map && (e.value as Map).containsKey('ip'))
          .map((e) => IpRecord.fromJson(e.key, e.value as Map<String, dynamic>))
          .toList();

      records.sort((a, b) => b.timestamp.compareTo(a.timestamp));
      return records;
    } catch (e) {
      debugPrint('Firebase servis hatasi: $e');
      return [];
    }
  }

  static Future<bool> saveIp({
    required String ip,
    required String path,
    String userAgent = '',
  }) async {
    try {
      final timestamp = DateTime.now().millisecondsSinceEpoch;
      final id = '${timestamp}_${ip.replaceAll('.', '_')}';
      final uri = Uri.parse('$_baseUrl/tracking/ips/$id.json');

      final response = await http
          .put(uri, body: json.encode({
            'ip': ip,
            'timestamp': timestamp,
            'path': path,
            'userAgent': userAgent,
          }))
          .timeout(const Duration(seconds: 10));

      return response.statusCode == 200;
    } catch (e) {
      debugPrint('Firebase kayit hatasi: $e');
      return false;
    }
  }
}
