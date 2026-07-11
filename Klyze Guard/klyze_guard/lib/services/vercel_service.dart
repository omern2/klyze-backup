import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class VercelDeploy {
  final String id;
  final String state;
  final int createdAt;
  final String? source;
  final String? url;

  VercelDeploy({
    required this.id,
    required this.state,
    required this.createdAt,
    this.source,
    this.url,
  });

  factory VercelDeploy.fromJson(Map<String, dynamic> json) {
    return VercelDeploy(
      id: json['id'] as String? ?? json['uid'] as String? ?? '',
      state: json['state'] as String? ?? 'unknown',
      createdAt: json['createdAt'] as int? ?? 0,
      source: json['source'] as String?,
      url: json['url'] as String?,
    );
  }
}

class VercelService {
  static String get _token =>
      const String.fromEnvironment('VERCEL_TOKEN');
  static String get _projectId =>
      const String.fromEnvironment('VERCEL_PROJECT_ID',
          defaultValue: 'prj_zgn0AWDjMyzWHWJVaHUhwFyfAFqb');
  static String get _teamId =>
      const String.fromEnvironment('VERCEL_TEAM_ID');

  static Map<String, String> _headers() {
    final h = <String, String>{'Authorization': 'Bearer $_token'};
    if (_teamId.isNotEmpty) h['x-vercel-team-id'] = _teamId;
    return h;
  }

  static Future<VercelDeploy?> getLastDeploy() async {
    try {
      final params = <String, String>{
        'projectId': _projectId,
        'limit': '1',
      };
      if (_teamId.isNotEmpty) params['teamId'] = _teamId;

      final uri = Uri.parse('https://api.vercel.com/v6/deployments').replace(
        queryParameters: params,
      );

      final response = await http
          .get(uri, headers: _headers())
          .timeout(const Duration(seconds: 10));

      if (response.statusCode == 401) {
        debugPrint('Vercel yetki hatasi: Token gecersiz.');
        return null;
      }

      if (response.statusCode != 200) {
        debugPrint('Vercel hata: ${response.statusCode} ${response.body}');
        return null;
      }

      final data = json.decode(response.body) as Map<String, dynamic>?;
      final deployments = data?['deployments'] as List?;
      if (deployments == null || deployments.isEmpty) return null;

      return VercelDeploy.fromJson(deployments[0]);
    } catch (e) {
      debugPrint('Vercel servis hatasi: $e');
      return null;
    }
  }
}
