import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';

class KillSwitchScreen extends StatefulWidget {
  const KillSwitchScreen({super.key});

  @override
  State<KillSwitchScreen> createState() => _KillSwitchScreenState();
}

class _KillSwitchScreenState extends State<KillSwitchScreen> with SingleTickerProviderStateMixin {
  bool _aktif = false;
  bool _yukleniyor = false;
  int _sure = 0;
  Timer? _sayac;
  Timer? _pollTimer;
  AnimationController? _animController;

  @override
  void initState() {
    super.initState();
    _animController = AnimationController(vsync: this, duration: const Duration(milliseconds: 800));
    _durumKontrol();
    _pollTimer = Timer.periodic(const Duration(seconds: 10), (_) => _durumKontrol());
  }

  @override
  void dispose() {
    _sayac?.cancel();
    _pollTimer?.cancel();
    _animController?.dispose();
    super.dispose();
  }

  Future<void> _durumKontrol() async {
    try {
      final client = HttpClient();
      final request = await client.getUrl(Uri.parse('https://klyzegg-default-rtdb.firebaseio.com/killswitch.json'));
      final response = await request.close();
      if (response.statusCode == 200) {
        final body = await response.transform(utf8.decoder).join();
        if (body == 'true' && mounted) {
          setState(() { _aktif = true; });
          _animController?.forward();
        }
      }
      client.close();
    } catch (_) {}
  }

  Future<void> _toggle() async {
    if (_aktif) {
      await _kapat();
    } else {
      _ac();
    }
  }

  void _ac() {
    setState(() { _yukleniyor = true; _sure = 5; });
    _sayac = Timer.periodic(const Duration(seconds: 1), (t) {
      setState(() { _sure--; });
      if (_sure <= 0) {
        t.cancel();
        _aktiflestir();
      }
    });
  }

  Future<void> _aktiflestir() async {
    try {
      final client = HttpClient();
      final request = await client.putUrl(Uri.parse('https://klyzegg-default-rtdb.firebaseio.com/killswitch.json'));
      request.headers.contentType = ContentType.json;
      request.write('true');
      await request.close();
      client.close();
    } catch (_) {}
    if (mounted) {
      setState(() { _aktif = true; _yukleniyor = false; });
      _animController?.forward();
    }
  }

  Future<void> _kapat() async {
    try {
      final client = HttpClient();
      final request = await client.putUrl(Uri.parse('https://klyzegg-default-rtdb.firebaseio.com/killswitch.json'));
      request.headers.contentType = ContentType.json;
      request.write('false');
      await request.close();
      client.close();
    } catch (_) {}
    if (mounted) {
      setState(() { _aktif = false; _yukleniyor = false; _sure = 0; });
      _animController?.reverse();
    }
  }

  String _siteDurumu() => _aktif ? 'OFFLINE' : 'AKTIF';
  Color _siteRengi() => _aktif ? Colors.redAccent : Colors.greenAccent;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF0A0A0A),
      appBar: AppBar(
        backgroundColor: const Color(0xFF0A0A0A),
        iconTheme: const IconThemeData(color: Colors.white),
        title: const Text('Kill Switch',
          style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 16)),
      ),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              GestureDetector(
                onTap: _yukleniyor ? null : _toggle,
                child: Container(
                  width: 200, height: 200,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    border: Border.all(color: _siteRengi().withOpacity(0.3), width: 3),
                    boxShadow: [
                      BoxShadow(color: _siteRengi().withOpacity(0.15), blurRadius: 40, spreadRadius: 5),
                    ],
                  ),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(Icons.power_settings_new, color: _siteRengi(), size: 56),
                      const SizedBox(height: 8),
                      Text(
                        _yukleniyor ? '$_sure' : (_aktif ? 'KAPAT' : 'AC'),
                        style: TextStyle(
                          color: _siteRengi(), fontSize: _yukleniyor ? 32 : 16, fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 40),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                decoration: BoxDecoration(
                  color: _aktif ? Colors.red.withOpacity(0.1) : Colors.green.withOpacity(0.1),
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: _siteRengi().withOpacity(0.3)),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Container(
                      width: 10, height: 10,
                      decoration: BoxDecoration(
                        color: _siteRengi(),
                        shape: BoxShape.circle,
                        boxShadow: [BoxShadow(color: _siteRengi(), blurRadius: 8)],
                      ),
                    ),
                    const SizedBox(width: 12),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('klyzegg.vercel.app',
                          style: TextStyle(color: Colors.white70, fontSize: 11, fontFamily: 'monospace')),
                        const SizedBox(height: 2),
                        Text(_siteDurumu(),
                          style: TextStyle(color: _siteRengi(), fontSize: 20, fontWeight: FontWeight.bold, letterSpacing: 2)),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: const Color(0xFF12121A),
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: Colors.white10),
                ),
                child: Column(
                  children: [
                    Row(
                      children: [
                        Icon(Icons.info_outline, color: Colors.grey, size: 16),
                        const SizedBox(width: 8),
                        const Text('NASIL CALISIR?',
                          style: TextStyle(color: Colors.grey, fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 1)),
                      ],
                    ),
                    const SizedBox(height: 12),
                    const Text(
                      'Kill Switch aktif edildiginde klyzegg.vercel.app sitesi offline moda gecer. '
                      'Ziyaretciler sadece bakim sayfasini gorur. Gercek site icerigi gizlenir.',
                      style: TextStyle(color: Colors.white54, fontSize: 12, height: 1.5),
                    ),
                    const SizedBox(height: 12),
                    if (_aktif)
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: Colors.red.withOpacity(0.08),
                          borderRadius: BorderRadius.circular(10),
                          border: Border.all(color: Colors.redAccent.withOpacity(0.2)),
                        ),
                        child: Row(
                          children: [
                            Icon(Icons.shield, color: Colors.redAccent, size: 16),
                            const SizedBox(width: 8),
                            const Expanded(
                              child: Text('&lt; Site koruma altinda. Tum saldirilar engelleniyor.',
                                style: TextStyle(color: Colors.redAccent, fontSize: 11)),
                            ),
                          ],
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}