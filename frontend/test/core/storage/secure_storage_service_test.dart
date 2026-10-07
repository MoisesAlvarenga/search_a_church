import 'package:flutter/services.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/core/storage/secure_storage_service.dart';

class MockFlutterSecureStorage extends Mock implements FlutterSecureStorage {}

void main() {
  late MockFlutterSecureStorage mockStorage;
  late SecureStorageService storageService;

  setUp(() {
    mockStorage = MockFlutterSecureStorage();
    storageService = SecureStorageService(storage: mockStorage);
  });

  group('SecureStorageService - Normal Operations', () {
    test('saveAccessToken writes to storage with correct key', () async {
      when(() => mockStorage.write(
            key: SecureStorageService.keyAccessToken,
            value: 'test_access_token',
          )).thenAnswer((_) async {});

      await storageService.saveAccessToken('test_access_token');

      verify(() => mockStorage.write(
            key: SecureStorageService.keyAccessToken,
            value: 'test_access_token',
          )).called(1);
    });

    test('getAccessToken reads from storage with correct key', () async {
      when(() => mockStorage.read(key: SecureStorageService.keyAccessToken))
          .thenAnswer((_) async => 'stored_access_token');

      final result = await storageService.getAccessToken();

      expect(result, 'stored_access_token');
      verify(() => mockStorage.read(key: SecureStorageService.keyAccessToken))
          .called(1);
    });

    test('saveRefreshToken writes to storage with correct key', () async {
      when(() => mockStorage.write(
            key: SecureStorageService.keyRefreshToken,
            value: 'test_refresh_token',
          )).thenAnswer((_) async {});

      await storageService.saveRefreshToken('test_refresh_token');

      verify(() => mockStorage.write(
            key: SecureStorageService.keyRefreshToken,
            value: 'test_refresh_token',
          )).called(1);
    });

    test('getRefreshToken reads from storage with correct key', () async {
      when(() => mockStorage.read(key: SecureStorageService.keyRefreshToken))
          .thenAnswer((_) async => 'stored_refresh_token');

      final result = await storageService.getRefreshToken();

      expect(result, 'stored_refresh_token');
      verify(() => mockStorage.read(key: SecureStorageService.keyRefreshToken))
          .called(1);
    });

    test('saveDeviceId writes to storage with correct key', () async {
      when(() => mockStorage.write(
            key: SecureStorageService.keyDeviceId,
            value: 'device_uuid_123',
          )).thenAnswer((_) async {});

      await storageService.saveDeviceId('device_uuid_123');

      verify(() => mockStorage.write(
            key: SecureStorageService.keyDeviceId,
            value: 'device_uuid_123',
          )).called(1);
    });

    test('getDeviceId reads from storage with correct key', () async {
      when(() => mockStorage.read(key: SecureStorageService.keyDeviceId))
          .thenAnswer((_) async => 'device_uuid_123');

      final result = await storageService.getDeviceId();

      expect(result, 'device_uuid_123');
      verify(() => mockStorage.read(key: SecureStorageService.keyDeviceId))
          .called(1);
    });

    test('saveTokens saves both access and refresh tokens', () async {
      when(() => mockStorage.write(
            key: any(named: 'key'),
            value: any(named: 'value'),
          )).thenAnswer((_) async {});

      await storageService.saveTokens(
        accessToken: 'access_123',
        refreshToken: 'refresh_456',
      );

      verify(() => mockStorage.write(
            key: SecureStorageService.keyAccessToken,
            value: 'access_123',
          )).called(1);
      verify(() => mockStorage.write(
            key: SecureStorageService.keyRefreshToken,
            value: 'refresh_456',
          )).called(1);
    });

    test('clearTokens deletes access and refresh tokens but retains other keys', () async {
      when(() => mockStorage.delete(key: any(named: 'key')))
          .thenAnswer((_) async {});

      await storageService.clearTokens();

      verify(() => mockStorage.delete(key: SecureStorageService.keyAccessToken))
          .called(1);
      verify(() => mockStorage.delete(key: SecureStorageService.keyRefreshToken))
          .called(1);
      verifyNever(() => mockStorage.delete(key: SecureStorageService.keyDeviceId));
    });

    test('clearAll calls deleteAll on storage', () async {
      when(() => mockStorage.deleteAll()).thenAnswer((_) async {});

      await storageService.clearAll();

      verify(() => mockStorage.deleteAll()).called(1);
    });
  });

  group('SecureStorageService - Defensive Keystore Error Handling', () {
    test('getAccessToken returns null and cleans up on hardware PlatformException', () async {
      when(() => mockStorage.read(key: SecureStorageService.keyAccessToken))
          .thenThrow(PlatformException(code: 'KEYSTORE_ERROR', message: 'Keystore corrupted'));
      when(() => mockStorage.delete(key: SecureStorageService.keyAccessToken))
          .thenAnswer((_) async {});

      final result = await storageService.getAccessToken();

      expect(result, isNull);
      verify(() => mockStorage.delete(key: SecureStorageService.keyAccessToken))
          .called(1);
    });

    test('saveAccessToken catches exception and cleans up on hardware PlatformException', () async {
      when(() => mockStorage.write(
            key: SecureStorageService.keyAccessToken,
            value: 'new_token',
          )).thenThrow(PlatformException(code: 'KEYSTORE_ERROR', message: 'Encryption failed'));
      when(() => mockStorage.delete(key: SecureStorageService.keyAccessToken))
          .thenAnswer((_) async {});

      // Should complete without throwing
      await expectLater(
        storageService.saveAccessToken('new_token'),
        completes,
      );

      verify(() => mockStorage.delete(key: SecureStorageService.keyAccessToken))
          .called(1);
    });

    test('clearAll catches PlatformException gracefully without crashing', () async {
      when(() => mockStorage.deleteAll())
          .thenThrow(PlatformException(code: 'KEYSTORE_ERROR', message: 'Wipe failed'));

      await expectLater(
        storageService.clearAll(),
        completes,
      );

      verify(() => mockStorage.deleteAll()).called(1);
    });
  });
}
