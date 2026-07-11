import 'dart:async';
import 'package:flutter/material.dart';
import 'ipler.dart';
import 'guvenlik_duvari.dart';
import 'kill_switch_screen.dart';
import 'firebase_log_screen.dart';
import '../services/bildirim_service.dart';

class AnaSayfa extends StatefulWidget {
  const AnaSayfa({super.key});

  @override
  State<AnaSayfa> createState() => _AnaSayfaState();
}

class _AnaSayfaState extends State<AnaSayfa> {
  int _bildirimSayisi = 0;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    BildirimService.baslat();
    _bildirimSayisi = BildirimService.okunmamisSayisi;
    _timer = Timer.periodic(const Duration(seconds: 3), (_) {
      if (mounted) {
        setState(() {
          _bildirimSayisi = BildirimService.okunmamisSayisi;
        });
      }
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    BildirimService.durdur();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF0A0A0A),
      appBar: AppBar(
        title: const Text('Klyze Guard',
          style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold, letterSpacing: 2)),
        centerTitle: true,
        actions: [
          if (_bildirimSayisi > 0)
            Padding(
              padding: const EdgeInsets.only(right: 12),
              child: GestureDetector(
                onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const GuvenlikDuvariEkrani())),
                child: Stack(
                  children: [
                    const Icon(Icons.notifications_outlined, color: Colors.cyanAccent, size: 26),
                    Positioned(
                      right: 0, top: 0,
                      child: Container(
                        padding: const EdgeInsets.all(4),
                        decoration: const BoxDecoration(color: Colors.redAccent, shape: BoxShape.circle),
                        child: Text('$_bildirimSayisi', style: const TextStyle(color: Colors.white, fontSize: 9, fontWeight: FontWeight.bold)),
                      ),
                    ),
                  ],
                ),
              ),
            ),
        ],
      ),
      drawer: _buildDrawer(context),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
          child: Column(
            children: [
              const Spacer(flex: 2),
              _buildLogo(),
              const Spacer(flex: 2),
              _menuItem(context,
                icon: Icons.language,
                title: 'Site Güvenlik Paneli',
                subtitle: 'Vercel deploy takibi, IP analizi, grafikler',
                color: Colors.cyanAccent,
                onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const IPlerScreen())),
              ),
              const SizedBox(height: 12),
              _menuItem(context,
                icon: Icons.shield,
                title: 'Güvenlik Duvarı',
                subtitle: 'Tehdit tespit ve canlı akış',
                color: Colors.redAccent,
                badge: _bildirimSayisi,
                onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const GuvenlikDuvariEkrani())),
              ),
              const SizedBox(height: 12),
              _menuItem(context,
                icon: Icons.flash_on,
                title: 'Kill Switch',
                subtitle: 'Siteyi acil kapat / offline al',
                color: Colors.orangeAccent,
                onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const KillSwitchScreen())),
              ),
              const SizedBox(height: 12),
              _menuItem(context,
                icon: Icons.security,
                title: 'Firebase Güvenlik',
                subtitle: 'Tüm Firebase işlem logları',
                color: Colors.redAccent,
                onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const FirebaseLogScreen())),
              ),
              const Spacer(flex: 3),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildLogo() {
    return Column(
      children: [
        Container(
          width: 80,
          height: 80,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: const Color(0xFF1A1A2E),
            border: Border.all(color: Colors.cyanAccent.withOpacity(0.3), width: 2),
          ),
          child: const Icon(Icons.shield_outlined, color: Colors.cyanAccent, size: 40),
        ),
        const SizedBox(height: 16),
        const Text('KLYZE GUARD',
          style: TextStyle(
            color: Colors.white,
            fontSize: 24,
            fontWeight: FontWeight.bold,
            letterSpacing: 6,
          )),
        const SizedBox(height: 6),
        Text('Güvenlik Yönetim Paneli',
          style: TextStyle(color: Colors.grey, fontSize: 13, letterSpacing: 1)),
        const SizedBox(height: 4),
        Text('v1.0.0',
          style: TextStyle(color: Colors.white24, fontSize: 11)),
      ],
    );
  }

  Widget _menuItem(
    BuildContext context, {
    required IconData icon,
    required String title,
    required String subtitle,
    required Color color,
    VoidCallback? onTap,
    bool disabled = false,
    int badge = 0,
  }) {
    return GestureDetector(
      onTap: disabled ? null : onTap,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(18),
        decoration: BoxDecoration(
          color: disabled ? const Color(0xFF0A0A0A) : const Color(0xFF12121A),
          borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: disabled ? Colors.white.withOpacity(0.08) : color.withOpacity(0.2),
            ),
        ),
        child: Row(
          children: [
            Stack(
              children: [
                Icon(icon, color: disabled ? Colors.grey : color, size: 26),
                if (badge > 0)
                  Positioned(
                    right: -6, top: -6,
                    child: Container(
                      padding: const EdgeInsets.all(3),
                      decoration: const BoxDecoration(color: Colors.redAccent, shape: BoxShape.circle),
                      child: Text('$badge', style: const TextStyle(color: Colors.white, fontSize: 8, fontWeight: FontWeight.bold)),
                    ),
                  ),
              ],
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Text(title,
                        style: TextStyle(
                          color: disabled ? Colors.grey : Colors.white,
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                        )),
                      if (badge > 0) ...[
                        const SizedBox(width: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
                          decoration: BoxDecoration(
                            color: Colors.redAccent.withOpacity(0.15),
                            borderRadius: BorderRadius.circular(4),
                          ),
                          child: Text('+$badge', style: const TextStyle(color: Colors.redAccent, fontSize: 9, fontWeight: FontWeight.bold)),
                        ),
                      ],
                    ],
                  ),
                  const SizedBox(height: 2),
                  Text(subtitle,
                    style: const TextStyle(color: Colors.grey, fontSize: 12)),
                ],
              ),
            ),
            if (!disabled)
              Icon(Icons.chevron_right, color: color, size: 22),
          ],
        ),
      ),
    );
  }

  Widget _buildDrawer(BuildContext context) {
    return Drawer(
      child: Container(
        color: const Color(0xFF0F0F0F),
        child: ListView(
          padding: EdgeInsets.zero,
          children: [
            DrawerHeader(
              decoration: const BoxDecoration(color: Color(0xFF0A0A0A)),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  const Icon(Icons.shield_outlined, color: Colors.cyanAccent, size: 36),
                  const SizedBox(height: 12),
                  const Text('KLYZE GUARD',
                    style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold, letterSpacing: 2)),
                  const SizedBox(height: 4),
                  Text('v1.0.0', style: const TextStyle(color: Colors.grey, fontSize: 12)),
                ],
              ),
            ),
            _drawerItem(Icons.language, 'Site IP Takip', () {
              Navigator.pop(context);
              Navigator.push(context, MaterialPageRoute(builder: (_) => const IPlerScreen()));
            }),
            _drawerItem(Icons.shield, 'Güvenlik Duvarı', () {
              Navigator.pop(context);
              Navigator.push(context, MaterialPageRoute(builder: (_) => const GuvenlikDuvariEkrani()));
            }),
            _drawerItem(Icons.flash_on, 'Kill Switch', () {
              Navigator.pop(context);
              Navigator.push(context, MaterialPageRoute(builder: (_) => const KillSwitchScreen()));
            }),
            _drawerItem(Icons.security, 'Firebase Güvenlik', () {
              Navigator.pop(context);
              Navigator.push(context, MaterialPageRoute(builder: (_) => const FirebaseLogScreen()));
            }),
          ],
        ),
      ),
    );
  }

  Widget _drawerItem(IconData icon, String title, VoidCallback? onTap, {bool disabled = false}) {
    return ListTile(
      leading: Icon(icon, color: disabled ? Colors.grey : Colors.cyanAccent, size: 20),
      title: Text(title,
        style: TextStyle(
          color: disabled ? Colors.grey : Colors.white70,
          fontSize: 14,
        )),
      onTap: disabled ? null : onTap,
    );
  }
}
