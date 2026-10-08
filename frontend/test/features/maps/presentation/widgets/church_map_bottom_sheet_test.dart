import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:search_a_church_app/features/maps/data/models/church_map_item_model.dart';
import 'package:search_a_church_app/features/maps/presentation/widgets/church_map_bottom_sheet.dart';

void main() {
  group('ChurchMapBottomSheet Widget Tests', () {
    const appChurch = ChurchMapItemModel(
      id: 'church-1',
      name: 'Igreja Batista Central',
      address: 'Av. Paulista, 1000 - Bela Vista, SP',
      latitude: -23.56,
      longitude: -46.65,
      distanceKm: 1.25,
      source: ChurchSource.app,
      isRegistered: true,
      isVerifiedRepresentative: true,
      ratingAverage: 4.8,
      reviewCount: 32,
      canClaim: false,
    );

    const mapsChurch = ChurchMapItemModel(
      id: 'church-2',
      placeId: 'ChIJ456789',
      name: 'Capela Comunitária Externa',
      address: 'Rua das Flores, 50 - Centro, SP',
      latitude: -23.55,
      longitude: -46.63,
      distanceKm: 3.4,
      source: ChurchSource.maps,
      isRegistered: false,
      isVerifiedRepresentative: false,
      ratingAverage: 4.2,
      reviewCount: 8,
      canClaim: true,
    );

    testWidgets('renders App source church with Cadastrada and Verified badges',
        (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: ChurchMapBottomSheet(church: appChurch),
          ),
        ),
      );

      expect(find.byKey(const Key('church_map_bottom_sheet')), findsOneWidget);
      expect(find.text('Igreja Batista Central'), findsOneWidget);
      expect(find.text('Av. Paulista, 1000 - Bela Vista, SP'), findsOneWidget);
      expect(find.byKey(const Key('church_badge_app')), findsOneWidget);
      expect(find.text('Cadastrada'), findsOneWidget);
      expect(find.byKey(const Key('church_badge_verified')), findsOneWidget);
      expect(find.text('Representante Verificado'), findsOneWidget);
      expect(find.text('1.3 km'), findsOneWidget);
      expect(find.text('4.8 (32)'), findsOneWidget);
      expect(find.byKey(const Key('claim_church_button')), findsNothing);
    });

    testWidgets(
        'renders Maps source church with Nao Cadastrada badge and Claim CTA button',
        (tester) async {
      ChurchMapItemModel? claimedChurch;

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: ChurchMapBottomSheet(
              church: mapsChurch,
              onClaim: (church) {
                claimedChurch = church;
              },
            ),
          ),
        ),
      );

      expect(find.text('Capela Comunitária Externa'), findsOneWidget);
      expect(find.byKey(const Key('church_badge_maps')), findsOneWidget);
      expect(find.text('Não cadastrada'), findsOneWidget);
      expect(find.byKey(const Key('church_badge_app')), findsNothing);
      expect(find.byKey(const Key('claim_church_button')), findsOneWidget);
      expect(find.text('Reivindicar esta igreja'), findsOneWidget);

      await tester.tap(find.byKey(const Key('claim_church_button')));
      await tester.pumpAndSettle();

      expect(claimedChurch, isNotNull);
      expect(claimedChurch?.id, 'church-2');
      expect(claimedChurch?.placeId, 'ChIJ456789');
      expect(claimedChurch?.name, 'Capela Comunitária Externa');
    });

    testWidgets('calls onClose callback when close button is tapped',
        (tester) async {
      var closed = false;

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: ChurchMapBottomSheet(
              church: appChurch,
              onClose: () {
                closed = true;
              },
            ),
          ),
        ),
      );

      final closeButton = find.byKey(const Key('church_bottom_sheet_close_button'));
      expect(closeButton, findsOneWidget);

      await tester.tap(closeButton);
      await tester.pumpAndSettle();

      expect(closed, isTrue);
    });

    testWidgets('navigates to /claim route when onClaim callback is null',
        (tester) async {
      Map<String, dynamic>? routeArguments;

      await tester.pumpWidget(
        MaterialApp(
          initialRoute: '/',
          routes: {
            '/': (context) => const Scaffold(
                  body: ChurchMapBottomSheet(church: mapsChurch),
                ),
            '/claim': (context) {
              routeArguments = ModalRoute.of(context)!.settings.arguments
                  as Map<String, dynamic>?;
              return const Scaffold(body: Text('Claim Route Screen'));
            },
          },
        ),
      );

      await tester.tap(find.byKey(const Key('claim_church_button')));
      await tester.pumpAndSettle();

      expect(find.text('Claim Route Screen'), findsOneWidget);
      expect(routeArguments, isNotNull);
      expect(routeArguments?['placeId'], 'ChIJ456789');
      expect(routeArguments?['name'], 'Capela Comunitária Externa');
      expect(routeArguments?['address'], 'Rua das Flores, 50 - Centro, SP');
      expect(routeArguments?['latitude'], -23.55);
      expect(routeArguments?['longitude'], -46.63);
    });
  });
}
