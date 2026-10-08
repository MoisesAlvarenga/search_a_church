import 'package:bloc_test/bloc_test.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/maps/core/geolocation_service.dart';
import 'package:search_a_church_app/features/maps/data/map_repository.dart';
import 'package:search_a_church_app/features/maps/data/models/church_map_item_model.dart';
import 'package:search_a_church_app/features/maps/data/models/map_search_response_model.dart';
import 'package:search_a_church_app/features/maps/presentation/cubit/map_cubit.dart';
import 'package:search_a_church_app/features/maps/presentation/cubit/map_state.dart';

class MockMapRepository extends Mock implements IMapRepository {}

class MockGeolocationService extends Mock implements GeolocationService {}

void main() {
  late MockMapRepository mockRepository;
  late MockGeolocationService mockGeoService;

  const sampleChurch = ChurchMapItemModel(
    id: 'church-1',
    placeId: 'ChIJ_1',
    name: 'Igreja da Sé',
    address: 'Praça da Sé, 1',
    latitude: -23.5505,
    longitude: -46.6333,
    distanceKm: 0.1,
    source: ChurchSource.app,
    isRegistered: true,
    isVerifiedRepresentative: true,
    canClaim: false,
  );

  const sampleResponse = MapSearchResponseModel(
    results: [sampleChurch],
    centerLatitude: -23.5505,
    centerLongitude: -46.6333,
    appliedRadiusKm: 5.0,
    isDegraded: false,
    degradedMessage: null,
  );

  setUp(() {
    mockRepository = MockMapRepository();
    mockGeoService = MockGeolocationService();
  });

  group('MapCubit', () {
    test('initial state is MapInitial', () {
      final cubit = MapCubit(
        repository: mockRepository,
        geolocationService: mockGeoService,
      );
      expect(cubit.state, const MapInitial());
      cubit.close();
    });

    blocTest<MapCubit, MapState>(
      'init determines user position and searches churches',
      build: () {
        when(() => mockGeoService.determinePosition()).thenAnswer(
          (_) async => const UserLocationResult.success(
            latitude: -23.5505,
            longitude: -46.6333,
          ),
        );
        when(() => mockRepository.searchNearby(
              latitude: -23.5505,
              longitude: -46.6333,
              radiusKm: 5.0,
            )).thenAnswer((_) async => sampleResponse);

        return MapCubit(
          repository: mockRepository,
          geolocationService: mockGeoService,
        );
      },
      act: (cubit) => cubit.init(),
      expect: () => [
        const MapLoading(),
        const MapLoaded(
          churches: [sampleChurch],
          centerLatitude: -23.5505,
          centerLongitude: -46.6333,
          radiusKm: 5.0,
          isDegraded: false,
        ),
      ],
    );

    blocTest<MapCubit, MapState>(
      'searchChurches emits [MapLoading, MapLoaded] with isDegraded true when provider degrades gracefully',
      build: () {
        const degradedResponse = MapSearchResponseModel(
          results: [sampleChurch],
          centerLatitude: -23.5505,
          centerLongitude: -46.6333,
          appliedRadiusKm: 5.0,
          isDegraded: true,
          degradedMessage: 'Provedor externo indisponível.',
        );

        when(() => mockRepository.searchNearby(
              latitude: -23.5505,
              longitude: -46.6333,
              radiusKm: 5.0,
            )).thenAnswer((_) async => degradedResponse);

        return MapCubit(
          repository: mockRepository,
          geolocationService: mockGeoService,
        );
      },
      act: (cubit) => cubit.searchChurches(
        latitude: -23.5505,
        longitude: -46.6333,
      ),
      expect: () => [
        const MapLoading(),
        const MapLoaded(
          churches: [sampleChurch],
          centerLatitude: -23.5505,
          centerLongitude: -46.6333,
          radiusKm: 5.0,
          isDegraded: true,
          degradedMessage: 'Provedor externo indisponível.',
        ),
      ],
    );

    blocTest<MapCubit, MapState>(
      'searchChurches emits [MapLoading, MapErrorGraceful] on DioException',
      build: () {
        when(() => mockRepository.searchNearby(
              latitude: any(named: 'latitude'),
              longitude: any(named: 'longitude'),
              radiusKm: any(named: 'radiusKm'),
            )).thenThrow(DioException(
          requestOptions: RequestOptions(path: '/map/search'),
          response: Response(
            data: {'message': 'Servidor temporariamente indisponível.'},
            statusCode: 503,
            requestOptions: RequestOptions(path: '/map/search'),
          ),
        ));

        return MapCubit(
          repository: mockRepository,
          geolocationService: mockGeoService,
        );
      },
      act: (cubit) => cubit.searchChurches(
        latitude: -23.55,
        longitude: -46.63,
      ),
      expect: () => [
        const MapLoading(),
        const MapErrorGraceful(
          message: 'Servidor temporariamente indisponível.',
        ),
      ],
    );

    test('selectChurch and clearSelection update MapLoaded state', () async {
      final cubit = MapCubit(
        repository: mockRepository,
        geolocationService: mockGeoService,
      );

      when(() => mockRepository.searchNearby(
            latitude: any(named: 'latitude'),
            longitude: any(named: 'longitude'),
            radiusKm: any(named: 'radiusKm'),
          )).thenAnswer((_) async => sampleResponse);

      await cubit.searchChurches(latitude: -23.55, longitude: -46.63);

      expect(cubit.state, isA<MapLoaded>());
      expect((cubit.state as MapLoaded).selectedChurch, isNull);

      cubit.selectChurch(sampleChurch);
      expect((cubit.state as MapLoaded).selectedChurch, sampleChurch);

      cubit.clearSelection();
      expect((cubit.state as MapLoaded).selectedChurch, isNull);

      await cubit.close();
    });

    test('onCameraMoved applies debounce before making search', () async {
      final cubit = MapCubit(
        repository: mockRepository,
        geolocationService: mockGeoService,
      );

      when(() => mockRepository.searchNearby(
            latitude: any(named: 'latitude'),
            longitude: any(named: 'longitude'),
            radiusKm: any(named: 'radiusKm'),
          )).thenAnswer((_) async => sampleResponse);

      // Dispara 3 movimentos consecutivos
      cubit.onCameraMoved(
        latitude: -23.551,
        longitude: -46.631,
        debounceDuration: const Duration(milliseconds: 50),
      );
      cubit.onCameraMoved(
        latitude: -23.552,
        longitude: -46.632,
        debounceDuration: const Duration(milliseconds: 50),
      );
      cubit.onCameraMoved(
        latitude: -23.553,
        longitude: -46.633,
        debounceDuration: const Duration(milliseconds: 50),
      );

      // Imediatamente antes do timer expirar: nenhuma chamada feita
      verifyNever(() => mockRepository.searchNearby(
            latitude: any(named: 'latitude'),
            longitude: any(named: 'longitude'),
            radiusKm: any(named: 'radiusKm'),
          ));

      // Aguarda o debounce expirar
      await Future<void>.delayed(const Duration(milliseconds: 80));

      // Apenas a última posição foi pesquisada (chamada única)
      verify(() => mockRepository.searchNearby(
            latitude: -23.553,
            longitude: -46.633,
            radiusKm: 5.0,
          )).called(1);

      await cubit.close();
    });
  });
}
