import 'package:flutter_test/flutter_test.dart';
import 'package:search_a_church_app/features/maps/data/models/church_map_item_model.dart';

void main() {
  group('ChurchMapItemModel', () {
    test('fromJson and toJson serialize correctly for App source', () {
      final json = {
        'id': 'church-uuid-123',
        'placeId': 'ChIJ_app_123',
        'name': 'Igreja Presbiteriana Central',
        'address': 'Rua das Flores, 50',
        'latitude': -23.551,
        'longitude': -46.634,
        'distanceKm': 1.25,
        'source': 'App',
        'isRegistered': true,
        'isVerifiedRepresentative': true,
        'ratingAverage': 4.7,
        'reviewCount': 50,
        'canClaim': false,
      };

      final model = ChurchMapItemModel.fromJson(json);

      expect(model.id, 'church-uuid-123');
      expect(model.placeId, 'ChIJ_app_123');
      expect(model.name, 'Igreja Presbiteriana Central');
      expect(model.source, ChurchSource.app);
      expect(model.isRegistered, isTrue);
      expect(model.isVerifiedRepresentative, isTrue);
      expect(model.ratingAverage, 4.7);
      expect(model.reviewCount, 50);
      expect(model.canClaim, isFalse);

      final outputJson = model.toJson();
      expect(outputJson['source'], 'app');
      expect(outputJson['name'], 'Igreja Presbiteriana Central');
      expect(outputJson['canClaim'], isFalse);
    });

    test('fromJson and toJson serialize correctly for Maps source with int enum', () {
      final json = {
        'id': 'maps_ChIJ_external',
        'placeId': 'ChIJ_external',
        'name': 'Comunidade Externa',
        'address': 'Av. Paulista, 1000',
        'latitude': -23.561,
        'longitude': -46.655,
        'distanceKm': 2.5,
        'source': 1, // int 1 -> maps
        'isRegistered': false,
        'isVerifiedRepresentative': false,
        'ratingAverage': 4.2,
        'reviewCount': 15,
        'canClaim': true,
      };

      final model = ChurchMapItemModel.fromJson(json);

      expect(model.id, 'maps_ChIJ_external');
      expect(model.source, ChurchSource.maps);
      expect(model.isRegistered, isFalse);
      expect(model.canClaim, isTrue);
    });

    test('ChurchSource.fromJson handles string and fallback gracefully', () {
      expect(ChurchSource.fromJson('maps'), ChurchSource.maps);
      expect(ChurchSource.fromJson('Maps'), ChurchSource.maps);
      expect(ChurchSource.fromJson('app'), ChurchSource.app);
      expect(ChurchSource.fromJson('unknown'), ChurchSource.app);
      expect(ChurchSource.fromJson(null), ChurchSource.app);
    });

    test('props equality compares all fields properly', () {
      const item1 = ChurchMapItemModel(
        id: '1',
        name: 'Igreja',
        address: 'Rua',
        latitude: 0,
        longitude: 0,
        distanceKm: 0,
        source: ChurchSource.app,
        isRegistered: true,
        isVerifiedRepresentative: true,
        canClaim: false,
      );

      const item2 = ChurchMapItemModel(
        id: '1',
        name: 'Igreja',
        address: 'Rua',
        latitude: 0,
        longitude: 0,
        distanceKm: 0,
        source: ChurchSource.app,
        isRegistered: true,
        isVerifiedRepresentative: true,
        canClaim: false,
      );

      expect(item1, equals(item2));
    });
  });
}
