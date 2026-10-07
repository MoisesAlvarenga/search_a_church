import 'dart:developer' as developer;
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

abstract class ISecureStorageService {
  Future<void> saveAccessToken(String token);
  Future<String?> getAccessToken();
  Future<void> saveRefreshToken(String token);
  Future<String?> getRefreshToken();
  Future<void> saveDeviceId(String deviceId);
  Future<String?> getDeviceId();
  Future<void> saveTokens({required String accessToken, required String refreshToken});
  Future<void> clearTokens();
  Future<void> clearAll();
}

class SecureStorageService implements ISecureStorageService {
  static const String keyAccessToken = 'auth_access_token';
  static const String keyRefreshToken = 'auth_refresh_token';
  static const String keyDeviceId = 'auth_device_id';

  final FlutterSecureStorage _storage;

  SecureStorageService({FlutterSecureStorage? storage})
      : _storage = storage ??
            const FlutterSecureStorage(
              iOptions: IOSOptions(
                accessibility: KeychainAccessibility.first_unlock_this_device,
              ),
              aOptions: AndroidOptions(
                encryptedSharedPreferences: true,
                resetOnError: true,
              ),
            );

  @override
  Future<void> saveAccessToken(String token) async {
    await _safeWrite(keyAccessToken, token);
  }

  @override
  Future<String?> getAccessToken() async {
    return _safeRead(keyAccessToken);
  }

  @override
  Future<void> saveRefreshToken(String token) async {
    await _safeWrite(keyRefreshToken, token);
  }

  @override
  Future<String?> getRefreshToken() async {
    return _safeRead(keyRefreshToken);
  }

  @override
  Future<void> saveDeviceId(String deviceId) async {
    await _safeWrite(keyDeviceId, deviceId);
  }

  @override
  Future<String?> getDeviceId() async {
    return _safeRead(keyDeviceId);
  }

  @override
  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
  }) async {
    await saveAccessToken(accessToken);
    await saveRefreshToken(refreshToken);
  }

  @override
  Future<void> clearTokens() async {
    await _safeDelete(keyAccessToken);
    await _safeDelete(keyRefreshToken);
  }

  @override
  Future<void> clearAll() async {
    try {
      await _storage.deleteAll();
    } catch (e, stack) {
      developer.log(
        'Defensive handling: Failed to delete all secure storage keys: $e',
        error: e,
        stackTrace: stack,
      );
    }
  }

  Future<void> _safeWrite(String key, String value) async {
    try {
      await _storage.write(key: key, value: value);
    } catch (e, stack) {
      developer.log(
        'Defensive handling: Failed to write key "$key" to secure storage: $e',
        error: e,
        stackTrace: stack,
      );
      try {
        await _storage.delete(key: key);
      } catch (_) {}
    }
  }

  Future<String?> _safeRead(String key) async {
    try {
      return await _storage.read(key: key);
    } catch (e, stack) {
      developer.log(
        'Defensive handling: Failed to read key "$key" from secure storage: $e',
        error: e,
        stackTrace: stack,
      );
      try {
        await _storage.delete(key: key);
      } catch (_) {}
      return null;
    }
  }

  Future<void> _safeDelete(String key) async {
    try {
      await _storage.delete(key: key);
    } catch (e, stack) {
      developer.log(
        'Defensive handling: Failed to delete key "$key" from secure storage: $e',
        error: e,
        stackTrace: stack,
      );
    }
  }
}
