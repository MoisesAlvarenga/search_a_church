import 'package:bloc_test/bloc_test.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:google_maps_flutter_platform_interface/google_maps_flutter_platform_interface.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/maps/data/models/church_map_item_model.dart';
import 'package:search_a_church_app/features/maps/presentation/cubit/map_cubit.dart';
import 'package:search_a_church_app/features/maps/presentation/cubit/map_state.dart';
import 'package:search_a_church_app/features/maps/presentation/screens/map_screen.dart';
import 'package:search_a_church_app/features/maps/presentation/widgets/church_map_bottom_sheet.dart';
import 'fake_maps_platform.dart';

class MockMapCubit extends MockCubit<MapState> implements MapCubit {}

void main() {
  late MockMapCubit mockCubit;
  late TestFakeMapsPlatform fakePlatform;

  const appChurch = ChurchMapItemModel(
    id: 'church-1',
    name: 'Igreja Presbiteriana do Brasil',
    address: 'Rua Bela Cintra, 100 - Consolação, SP',
    latitude: -23.551,
    longitude: -46.661,
    distanceKm: 0.8,
    source: ChurchSource.app,
    isRegistered: true,
    isVerifiedRepresentative: true,
    ratingAverage: 4.9,
    reviewCount: 45,
    canClaim: false,
  );

  const mapsChurch = ChurchMapItemModel(
    id: 'church-2',
    placeId: 'ChIJ987654',
    name: 'Templo Comunitário Bíblico',
    address: 'Av. Brigadeiro Luís Antônio, 500 - Bela Vista, SP',
    latitude: -23.558,
    longitude: -46.645,
    distanceKm: 2.1,
    source: ChurchSource.maps,
    isRegistered: false,
    isVerifiedRepresentative: false,
    ratingAverage: 4.0,
    reviewCount: 12,
    canClaim: true,
  );

  setUpAll(() {
    fakePlatform = TestFakeMapsPlatform();
    GoogleMapsFlutterPlatform.instance = fakePlatform;
  });

  setUp(() {
    mockCubit = MockMapCubit();
  });

  Widget createWidgetUnderTest() {
    return MaterialApp(
      home: MapScreen(cubit: mockCubit),
    );
  }

  group('MapScreen Widget Tests', () {
    testWidgets('renders search header, map canvas, and FAB controls in initial state',
        (tester) async {
      when(() => mockCubit.state).thenReturn(const MapInitial());

      await tester.pumpWidget(createWidgetUnderTest());
      await tester.pump();

      expect(find.byKey(const Key('google_map_widget')), findsOneWidget);
      expect(find.byKey(const Key('fake_google_map_canvas')), findsOneWidget);
      expect(find.byKey(const Key('map_search_header')), findsOneWidget);
      expect(find.byKey(const Key('my_location_fab')), findsOneWidget);
      expect(find.byKey(const Key('toggle_list_button')), findsOneWidget);
    });

    testWidgets('displays loading progress indicator when state is MapLoading',
        (tester) async {
      when(() => mockCubit.state).thenReturn(const MapLoading());

      await tester.pumpWidget(createWidgetUnderTest());
      await tester.pump();

      expect(find.byKey(const Key('map_loading_indicator')), findsOneWidget);
    });

    testWidgets('displays empty state card when MapLoaded has no churches',
        (tester) async {
      when(() => mockCubit.state).thenReturn(
        const MapLoaded(
          churches: [],
          centerLatitude: -23.55,
          centerLongitude: -46.63,
          radiusKm: 5.0,
        ),
      );

      await tester.pumpWidget(createWidgetUnderTest());
      await tester.pump();

      expect(find.byKey(const Key('empty_map_message')), findsOneWidget);
      expect(find.text('Nenhuma igreja encontrada nesta região.'), findsOneWidget);
    });

    testWidgets(
        'renders horizontal church cards with App and Maps items and selects church on tap',
        (tester) async {
      when(() => mockCubit.state).thenReturn(
        const MapLoaded(
          churches: [appChurch, mapsChurch],
          centerLatitude: -23.55,
          centerLongitude: -46.63,
          radiusKm: 5.0,
        ),
      );

      await tester.pumpWidget(createWidgetUnderTest());
      await tester.pump();

      expect(find.byKey(const Key('churches_horizontal_list')), findsOneWidget);
      expect(find.byKey(const Key('church_card_church-1')), findsOneWidget);
      expect(find.byKey(const Key('church_card_church-2')), findsOneWidget);
      expect(find.text('Igreja Presbiteriana do Brasil'), findsOneWidget);
      expect(find.text('Templo Comunitário Bíblico'), findsOneWidget);
      expect(find.text('Oficial'), findsOneWidget);
      expect(find.text('Maps'), findsOneWidget);

      await tester.tap(find.byKey(const Key('church_card_church-2')));
      await tester.pump();

      verify(() => mockCubit.selectChurch(mapsChurch)).called(1);
    });

    testWidgets('displays graceful degradation banner when isDegraded is true',
        (tester) async {
      when(() => mockCubit.state).thenReturn(
        const MapLoaded(
          churches: [appChurch],
          centerLatitude: -23.55,
          centerLongitude: -46.63,
          radiusKm: 5.0,
          isDegraded: true,
          degradedMessage:
              'Google Maps temporariamente indisponível. Exibindo igrejas cadastradas.',
        ),
      );

      await tester.pumpWidget(createWidgetUnderTest());
      await tester.pump();

      expect(find.byKey(const Key('degraded_mode_banner')), findsOneWidget);
      expect(
        find.text(
            'Google Maps temporariamente indisponível. Exibindo igrejas cadastradas.'),
        findsOneWidget,
      );
    });

    testWidgets('displays error banner when state is MapErrorGraceful',
        (tester) async {
      when(() => mockCubit.state).thenReturn(
        const MapErrorGraceful(
          message: 'Falha na conexão com o serviço de mapas.',
        ),
      );

      await tester.pumpWidget(createWidgetUnderTest());
      await tester.pump();

      expect(find.byKey(const Key('map_error_banner')), findsOneWidget);
      expect(
        find.text('Falha na conexão com o serviço de mapas.'),
        findsOneWidget,
      );
    });

    testWidgets(
        'renders ChurchMapBottomSheet when selectedChurch is present and clears selection on close',
        (tester) async {
      when(() => mockCubit.state).thenReturn(
        const MapLoaded(
          churches: [appChurch, mapsChurch],
          selectedChurch: mapsChurch,
          centerLatitude: -23.55,
          centerLongitude: -46.63,
          radiusKm: 5.0,
        ),
      );

      await tester.pumpWidget(createWidgetUnderTest());
      await tester.pump();

      expect(find.byType(ChurchMapBottomSheet), findsOneWidget);
      expect(find.text('Templo Comunitário Bíblico'), findsOneWidget);
      expect(find.byKey(const Key('claim_church_button')), findsOneWidget);

      await tester.tap(find.byKey(const Key('church_bottom_sheet_close_button')));
      await tester.pump();

      verify(() => mockCubit.clearSelection()).called(1);
    });

    testWidgets('triggers cubit.init when my_location_fab is tapped',
        (tester) async {
      when(() => mockCubit.state).thenReturn(const MapInitial());
      when(() => mockCubit.init()).thenAnswer((_) async {});

      await tester.pumpWidget(createWidgetUnderTest());
      await tester.pump();

      await tester.tap(find.byKey(const Key('my_location_fab')));
      await tester.pump();

      verify(() => mockCubit.init()).called(1);
    });

    testWidgets(
        'toggles horizontal list visibility when toggle_list_button is tapped',
        (tester) async {
      when(() => mockCubit.state).thenReturn(
        const MapLoaded(
          churches: [appChurch],
          centerLatitude: -23.55,
          centerLongitude: -46.63,
          radiusKm: 5.0,
        ),
      );

      await tester.pumpWidget(createWidgetUnderTest());
      await tester.pump();

      expect(find.byKey(const Key('churches_horizontal_list')), findsOneWidget);

      await tester.tap(find.byKey(const Key('toggle_list_button')));
      await tester.pump();

      expect(find.byKey(const Key('churches_horizontal_list')), findsNothing);

      await tester.tap(find.byKey(const Key('toggle_list_button')));
      await tester.pump();

      expect(find.byKey(const Key('churches_horizontal_list')), findsOneWidget);
    });
  });
}
