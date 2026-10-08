import 'package:dio/dio.dart';
import 'package:search_a_church_app/core/storage/secure_storage_service.dart';
import 'models/auth_response_model.dart';
import 'models/user_model.dart';

abstract class IAuthRepository {
  Future<AuthResponseModel> register({
    required String name,
    required String email,
    required String password,
    required String deviceId,
  });

  Future<AuthResponseModel> login({
    required String email,
    required String password,
    required String deviceId,
  });

  Future<void> logout();

  Future<void> forgotPassword({required String email});

  Future<void> resetPassword({
    required String email,
    required String otpCode,
    required String newPassword,
  });

  Future<UserModel> getCurrentUser();
}

class AuthRepository implements IAuthRepository {
  final Dio _dio;
  final ISecureStorageService _storage;

  AuthRepository({
    required this._dio,
    required this._storage,
  });

  @override
  Future<AuthResponseModel> register({
    required String name,
    required String email,
    required String password,
    required String deviceId,
  }) async {
    final response = await _dio.post(
      '/auth/register',
      data: {
        'name': name,
        'email': email,
        'password': password,
        'deviceId': deviceId,
      },
    );

    final authResponse = AuthResponseModel.fromJson(response.data as Map<String, dynamic>);
    await _storage.saveTokens(
      accessToken: authResponse.accessToken,
      refreshToken: authResponse.refreshToken,
    );
    await _storage.saveDeviceId(deviceId);

    return authResponse;
  }

  @override
  Future<AuthResponseModel> login({
    required String email,
    required String password,
    required String deviceId,
  }) async {
    final response = await _dio.post(
      '/auth/login',
      data: {
        'email': email,
        'password': password,
        'deviceId': deviceId,
      },
    );

    final authResponse = AuthResponseModel.fromJson(response.data as Map<String, dynamic>);
    await _storage.saveTokens(
      accessToken: authResponse.accessToken,
      refreshToken: authResponse.refreshToken,
    );
    await _storage.saveDeviceId(deviceId);

    return authResponse;
  }

  @override
  Future<void> logout() async {
    try {
      final refreshToken = await _storage.getRefreshToken();
      await _dio.post(
        '/auth/logout',
        data: {'refreshToken': refreshToken},
      );
    } catch (_) {
      // Best-effort remote revocation; local storage must be cleared regardless
    } finally {
      await _storage.clearTokens();
    }
  }

  @override
  Future<void> forgotPassword({required String email}) async {
    await _dio.post(
      '/auth/forgot-password',
      data: {'email': email},
    );
  }

  @override
  Future<void> resetPassword({
    required String email,
    required String otpCode,
    required String newPassword,
  }) async {
    await _dio.post(
      '/auth/reset-password',
      data: {
        'email': email,
        'otpCode': otpCode,
        'newPassword': newPassword,
      },
    );
  }

  @override
  Future<UserModel> getCurrentUser() async {
    final response = await _dio.get('/auth/me');
    return UserModel.fromJson(response.data as Map<String, dynamic>);
  }
}
