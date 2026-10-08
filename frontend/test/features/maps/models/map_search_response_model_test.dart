import 'package:flutter_test/flutter_test.dart';
import 'package:search_a_church_app/features/maps/data/models/church_map_item_model.dart';
import 'package:search_a_church_app/features/maps/data/models/map_search_response_model.dart';

void main() {
  group('MapSearchResponseModel', () {
    test('fromJson and toJson serialize full response correctly', () {
      final json = {
        'results': [
          {
            'id': 'church-1',
            'placeId': 'ChIJ_1',
            'name': 'Igreja Batista',
            'address': 'Rua A',
            'latitude': -23.55,
            'longitude': -46.63,
            'distanceKm': 0.8,
            'source': 'app',
            'isRegistered': true,
            'isVerifiedRepresentative': true,
            'ratingAverage': 5.0,
            'reviewCount': 10,
            'canClaim': false,
          }
        ],
        'centerLatitude': -23.5505,
        'centerLongitude': -46.6333,
        'appliedRadiusKm': 5.0,
        'isDegraded': false,
        'degradedMessage': null,
      };

      final model = MapSearchResponseModel.fromJson(json);

      expect(model.results, hasLength(1));
      expect(model.results.first.name, 'Igreja Batista');
      expect(model.centerLatitude, -23.5505);
      expect(model.centerLongitude, -46.6333);
      expect(model.appliedRadiusKm, 5.0);
      expect(model.isDegraded, isFalse);
      expect(model.degradedMessage, isNull);

      final outJson = model.toJson();
      expect(outJson['appliedRadiusKm'], 5.0);
      expect(outJson['isDegraded'], isFalse);
    });

    test('fromJson handles degraded external response correctly', () {
      final json = {
        'results': <Map<String, dynamic>>[],
        'centerLatitude': -23.55,
        'centerLongitude': -46.63,
        'appliedRadiusKm': 10.0,
        'isDegraded': true,
        'degradedMessage': 'Provedor indisponível',
      };

      final model = MapSearchResponseModel.fromJson(json);

      expect(model.results, isEmpty);
      expect(model.isDegraded, isTrue);
      expect(model.degradedMessage, 'Provedor indisponível');
    });
  });
}
