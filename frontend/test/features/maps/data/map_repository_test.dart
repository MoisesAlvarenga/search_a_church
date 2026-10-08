import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/maps/data/map_repository.dart';

class MockDio extends Mock implements Dio {}

void main() {
  late MockDio mockDio;
  late MapRepository repository;

  setUp(() {
    mockDio = MockDio();
    repository = MapRepository(dio: mockDio);
  });

  group('MapRepository', () {
    test('searchNearby calls /map/search with correct queryParameters and parses response', () async {
      final mockData = {
        'results': [
          {
            'id': 'church-1',
            'placeId': 'ChIJ_1',
            'name': 'Igreja Presbiteriana',
            'address': 'Rua Flores, 100',
            'latitude': -23.55,
            'longitude': -46.63,
            'distanceKm': 1.1,
            'source': 'app',
            'isRegistered': true,
            'isVerifiedRepresentative': true,
            'canClaim': false,
          }
        ],
        'centerLatitude': -23.5505,
        'centerLongitude': -46.6333,
        'appliedRadiusKm': 5.0,
        'isDegraded': false,
        'degradedMessage': null,
      };

      when(() => mockDio.get(
            '/map/search',
            queryParameters: any(named: 'queryParameters'),
          )).thenAnswer((_) async => Response(
            data: mockData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/map/search'),
          ));

      final result = await repository.searchNearby(
        latitude: -23.5505,
        longitude: -46.6333,
        radiusKm: 5.0,
        query: 'Presbiteriana',
      );

      expect(result.results, hasLength(1));
      expect(result.results.first.name, 'Igreja Presbiteriana');
      expect(result.appliedRadiusKm, 5.0);

      verify(() => mockDio.get(
            '/map/search',
            queryParameters: {
              'lat': -23.5505,
              'lng': -46.6333,
              'radiusKm': 5.0,
              'query': 'Presbiteriana',
            },
          )).called(1);
    });

    test('getPlaceDetails calls /map/places/{placeId} and parses item', () async {
      final mockPlace = {
        'id': 'maps_ChIJ_2',
        'placeId': 'ChIJ_2',
        'name': 'Igreja Batista',
        'address': 'Av. Paulista',
        'latitude': -23.56,
        'longitude': -46.65,
        'distanceKm': 2.0,
        'source': 'maps',
        'isRegistered': false,
        'isVerifiedRepresentative': false,
        'canClaim': true,
      };

      when(() => mockDio.get('/map/places/ChIJ_2'))
          .thenAnswer((_) async => Response(
                data: mockPlace,
                statusCode: 200,
                requestOptions: RequestOptions(path: '/map/places/ChIJ_2'),
              ));

      final result = await repository.getPlaceDetails('ChIJ_2');

      expect(result, isNotNull);
      expect(result!.name, 'Igreja Batista');
      expect(result.canClaim, isTrue);
    });
  });
}
