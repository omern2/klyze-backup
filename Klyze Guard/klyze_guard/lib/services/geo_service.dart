import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class IpGeoInfo {
  final String country;
  final String countryCode;
  final String region;
  final String city;
  final String isp;
  final String org;
  final double? lat;
  final double? lon;

  IpGeoInfo({
    required this.country,
    required this.countryCode,
    required this.region,
    required this.city,
    required this.isp,
    required this.org,
    this.lat,
    this.lon,
  });

  factory IpGeoInfo.fromJson(Map<String, dynamic> json) {
    return IpGeoInfo(
      country: json['country'] as String? ?? 'Bilinmiyor',
      countryCode: (json['countryCode'] as String? ?? '').toLowerCase(),
      region: json['regionName'] as String? ?? '',
      city: json['city'] as String? ?? '',
      isp: json['isp'] as String? ?? '',
      org: json['org'] as String? ?? '',
      lat: (json['lat'] as num?)?.toDouble(),
      lon: (json['lon'] as num?)?.toDouble(),
    );
  }

  String get location {
    final parts = <String>[];
    if (city.isNotEmpty) parts.add(city);
    if (region.isNotEmpty) parts.add(region);
    if (country.isNotEmpty) parts.add(country);
    return parts.isNotEmpty ? parts.join(', ') : 'Bilinmiyor';
  }
}

class GeoService {
  static final Map<String, IpGeoInfo> _cache = {};

  static Future<IpGeoInfo?> lookup(String ip) async {
    if (ip == 'Bilinmiyor' || ip.isEmpty || ip == '0.0.0.0') return null;

    if (_cache.containsKey(ip)) return _cache[ip];

    try {
      final uri = Uri.parse('http://ip-api.com/json/$ip?fields=status,country,countryCode,regionName,city,isp,org,lat,lon');
      final response = await http.get(uri).timeout(const Duration(seconds: 5));

      if (response.statusCode != 200) return null;

      final data = json.decode(response.body) as Map<String, dynamic>?;
      if (data == null || data['status'] != 'success') return null;

      final info = IpGeoInfo.fromJson(data);
      _cache[ip] = info;
      return info;
    } catch (e) {
      debugPrint('Geo servis hatasi: $e');
      return null;
    }
  }
}
