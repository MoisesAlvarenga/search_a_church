import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/maps/core/geolocation_service.dart';

class MockGeolocatorPlatform extends Mock implements GeolocatorPlatform {}

void main() {
  late MockGeolocatorPlatform mockGeolocator;
  late GeolocationService service;

  final testPosition = Position(
    latitude: -23.5614,
    longitude: -46.6558,
    timestamp: DateTime.now(),
    accuracy: 5.0,
    altitude: 750.0,
    altitudeAccuracy: 1.0,
    heading: 0.0,
    headingAccuracy: 0.0,
    speed: 0.0,
    speedAccuracy: 0.0,
  );

  setUp(() {
    mockGeolocator = MockGeolocatorPlatform();
    service = GeolocationService(geolocator: mockGeolocator);
  });

  group('GeolocationService', () {
    test('returns serviceDisabled with fallback when GPS service is off', () async {
      when(() => mockGeolocator.isLocationServiceEnabled())
          .thenAnswer((_) async => false);

      final result = await service.determinePosition(useFallbackOnFailure: true);

      expect(result.status, LocationStatus.serviceDisabled);
      expect(result.latitude, GeolocationService.defaultLatitude);
      expect(result.longitude, GeolocationService.defaultLongitude);
      expect(result.isSuccess, isFalse);
    });

    test('returns permissionDenied with fallback when user denies permission', () async {
      when(() => mockGeolocator.isLocationServiceEnabled())
          .thenAnswer((_) async => true);
      when(() => mockGeolocator.checkPermission())
          .thenAnswer((_) async => LocationPermission.denied);
      when(() => mockGeolocator.requestPermission())
          .thenAnswer((_) async => LocationPermission.denied);

      final result = await service.determinePosition(useFallbackOnFailure: true);

      expect(result.status, LocationStatus.permissionDenied);
      expect(result.latitude, GeolocationService.defaultLatitude);
      expect(result.longitude, GeolocationService.defaultLongitude);
    });

    test('returns permissionDeniedForever with fallback when permission is permanently denied', () async {
      when(() => mockGeolocator.isLocationServiceEnabled())
          .thenAnswer((_) async => true);
      when(() => mockGeolocator.checkPermission())
          .thenAnswer((_) async => LocationPermission.deniedForever);

      final result = await service.determinePosition(useFallbackOnFailure: true);

      expect(result.status, LocationStatus.permissionDeniedForever);
      expect(result.latitude, GeolocationService.defaultLatitude);
      expect(result.longitude, GeolocationService.defaultLongitude);
    });

    test('returns success with device coordinates when permission granted and position fetched', () async {
      when(() => mockGeolocator.isLocationServiceEnabled())
          .thenAnswer((_) async => true);
      when(() => mockGeolocator.checkPermission())
          .thenAnswer((_) async => LocationPermission.whileInUse);
      when(() => mockGeolocator.getCurrentPosition(
            locationSettings: any(named: 'locationSettings'),
          )).thenAnswer((_) async => testPosition);

      final result = await service.determinePosition();

      expect(result.status, LocationStatus.success);
      expect(result.latitude, -23.5614);
      expect(result.longitude, -46.6558);
      expect(result.isSuccess, isTrue);
    });

    test('falls back to lastKnownPosition when getCurrentPosition throws exception', () async {
      when(() => mockGeolocator.isLocationServiceEnabled())
          .thenAnswer((_) async => true);
      when(() => mockGeolocator.checkPermission())
          .thenAnswer((_) async => LocationPermission.whileInUse);
      when(() => mockGeolocator.getCurrentPosition(
            locationSettings: any(named: 'locationSettings'),
          )).thenThrow(Exception('Timeout fetching GPS'));
      when(() => mockGeolocator.getLastKnownPosition())
          .thenAnswer((_) async => testPosition);

      final result = await service.determinePosition();

      expect(result.status, LocationStatus.success);
      expect(result.latitude, -23.5614);
      expect(result.longitude, -46.6558);
    });

    test('returns error with fallback when both getCurrentPosition and lastKnownPosition fail', () async {
      when(() => mockGeolocator.isLocationServiceEnabled())
          .thenAnswer((_) async => true);
      when(() => mockGeolocator.checkPermission())
          .thenAnswer((_) async => LocationPermission.whileInUse);
      when(() => mockGeolocator.getCurrentPosition(
            locationSettings: any(named: 'locationSettings'),
          )).thenThrow(Exception('Hardware error'));
      when(() => mockGeolocator.getLastKnownPosition())
          .thenAnswer((_) async => null);

      final result = await service.determinePosition(useFallbackOnFailure: true);

      expect(result.status, LocationStatus.error);
      expect(result.latitude, GeolocationService.defaultLatitude);
      expect(result.longitude, GeolocationService.defaultLongitude);
    });
  });
}
