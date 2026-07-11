import 'package:flutter_test/flutter_test.dart';
import 'package:klyze_guard/main.dart';

void main() {
  testWidgets('Uygulama baslatma testi', (WidgetTester tester) async {
    await tester.pumpWidget(const KlyzeGuard());
    expect(find.text('KLYZE GUARD'), findsOneWidget);
  });
}
