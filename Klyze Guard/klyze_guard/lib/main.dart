import 'package:flutter/material.dart';
import 'screens/anasayfa.dart';

void main() {
  runApp(const KlyzeGuard());
}

class KlyzeGuard extends StatelessWidget {
  const KlyzeGuard({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Klyze Guard',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        brightness: Brightness.dark,
        scaffoldBackgroundColor: const Color(0xFF0A0A0A),
        primaryColor: Colors.cyanAccent,
        colorScheme: const ColorScheme.dark(
          primary: Colors.cyanAccent,
          surface: Color(0xFF1A1A2E),
        ),
        appBarTheme: const AppBarTheme(
          backgroundColor: Color(0xFF0A0A0A),
          elevation: 0,
        ),
        drawerTheme: const DrawerThemeData(
          backgroundColor: Color(0xFF0F0F0F),
        ),
      ),
      home: const AnaSayfa(),
    );
  }
}
