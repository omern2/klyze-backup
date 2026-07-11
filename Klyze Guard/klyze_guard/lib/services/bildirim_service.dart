import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class Bildirim {
  final String id;
  final String ip;
  final int timestamp;
  final String type;
  final String severity;
  final String message;
  final String path;
  bool okundu;
  final String details;

  Bildirim({
    required this.id,
    required this.ip,
    required this.timestamp,
    required this.type,
    required this.severity,
    required this.message,
    required this.path,
    required this.okundu,
    required this.details,
  });

  factory Bildirim.fromJson(String id, Map<String, dynamic> json) {
    return Bildirim(
      id: id,
      ip: json['ip'] as String? ?? '',
      timestamp: json['timestamp'] as int? ?? 0,
      type: json['type'] as String? ?? 'unknown',
      severity: json['severity'] as String? ?? 'low',
      message: json['message'] as String? ?? '',
      path: json['path'] as String? ?? '/',
      okundu: json['okundu'] as bool? ?? false,
      details: json['details'] as String? ?? '',
    );
  }

  String get zaman {
    if (timestamp == 0) return '';
    final dt = DateTime.fromMillisecondsSinceEpoch(timestamp);
    final now = DateTime.now();
    final diff = now.difference(dt);
    if (diff.inSeconds < 60) return '${diff.inSeconds}s önce';
    if (diff.inMinutes < 60) return '${diff.inMinutes}d önce';
    if (diff.inHours < 24) return '${diff.inHours}s önce';
    return '${dt.day}/${dt.month} ${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
  }
}

class BildirimService {
  static const String _baseUrl = 'https://klyzegg-default-rtdb.firebaseio.com';

  static List<Bildirim> _bildirimler = [];
  static int _lastCheck = 0;
  static int _okunmamisSayisi = 0;
  static Timer? _timer;
  static List<void Function(Bildirim)> _listeners = [];

  static List<Bildirim> get bildirimler => List.unmodifiable(_bildirimler);
  static int get okunmamisSayisi => _okunmamisSayisi;

  static void addListener(void Function(Bildirim) listener) {
    _listeners.add(listener);
  }

  static void removeListener(void Function(Bildirim) listener) {
    _listeners.remove(listener);
  }

  static void baslat() {
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 8), (_) => _yeniBildirimleriKontrolEt());
    _yeniBildirimleriKontrolEt();
  }

  static void durdur() {
    _timer?.cancel();
    _timer = null;
  }

  static Future<List<Bildirim>> tumunuGetir({int limit = 100}) async {
    try {
      final uri = Uri.parse('$_baseUrl/bildirimler.json').replace(
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
          .map((e) => Bildirim.fromJson(e.key, e.value as Map<String, dynamic>))
          .toList();
      list.sort((a, b) => b.timestamp.compareTo(a.timestamp));
      return list;
    } catch (e) {
      debugPrint('Bildirim servis hatasi: $e');
      return [];
    }
  }

  static Future<void> _yeniBildirimleriKontrolEt() async {
    try {
      final yeni = await tumunuGetir(limit: 20);
      if (yeni.isEmpty) return;

      if (_bildirimler.isEmpty) {
        _bildirimler = yeni;
        _okunmamisSayisi = yeni.where((b) => !b.okundu).length;
        return;
      }

      final eskiIds = _bildirimler.map((b) => b.id).toSet();
      final yeniBildirimler = yeni.where((b) => !eskiIds.contains(b.id)).toList();

      if (yeniBildirimler.isNotEmpty) {
        _bildirimler = [...yeniBildirimler, ..._bildirimler];
        if (_bildirimler.length > 200) _bildirimler = _bildirimler.sublist(0, 200);
        _okunmamisSayisi += yeniBildirimler.length;

        for (final b in yeniBildirimler) {
          for (final listener in _listeners) {
            listener(b);
          }
        }
      }
    } catch (e) {
      debugPrint('Bildirim kontrol hatasi: $e');
    }
  }

  static Future<void> okunduIsaretle(String id) async {
    try {
      await http.put(
        Uri.parse('$_baseUrl/bildirimler/$id/okundu.json'),
        body: 'true',
      );
      for (int i = 0; i < _bildirimler.length; i++) {
        if (_bildirimler[i].id == id) {
          _bildirimler[i].okundu = true;
          break;
        }
      }
      _okunmamisSayisi = _bildirimler.where((b) => !b.okundu).length;
    } catch (e) {
      debugPrint('Okundu hatasi: $e');
    }
  }

  static Future<void> tumunuOkunduYap() async {
    try {
      for (final b in _bildirimler.where((b) => !b.okundu)) {
        await http.put(
          Uri.parse('$_baseUrl/bildirimler/${b.id}/okundu.json'),
          body: 'true',
        );
        b.okundu = true;
      }
      _okunmamisSayisi = 0;
    } catch (e) {
      debugPrint('Toplu okundu hatasi: $e');
    }
  }

  static void resetOkunmamis() {
    _okunmamisSayisi = 0;
  }
}
