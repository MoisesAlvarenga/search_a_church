import 'dart:ui';
import 'package:dio/dio.dart';
import 'package:search_a_church_app/core/storage/secure_storage_service.dart';

class AuthInterceptor extends QueuedInterceptor {
  final Dio dio;
  final ISecureStorageService storage;
  final VoidCallback onSessionExpired;
  final Dio? _refreshClient;

  AuthInterceptor({
    required this.dio,
    required this.storage,
    required this.onSessionExpired,
    this._refreshClient,
  });

  Dio get refreshClient =>
      _refreshClient ?? Dio(BaseOptions(baseUrl: dio.options.baseUrl));

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    final accessToken = await storage.getAccessToken();
    if (accessToken != null && accessToken.isNotEmpty) {
      options.headers['Authorization'] = 'Bearer $accessToken';
    }
    return handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final path = err.requestOptions.path;
    final isAuthRoute = path.contains('/auth/login') ||
        path.contains('/auth/register') ||
        path.contains('/auth/refresh') ||
        path.contains('/auth/forgot-password') ||
        path.contains('/auth/reset-password');

    if (err.response?.statusCode == 401 && !isAuthRoute) {
      final refreshToken = await storage.getRefreshToken();
      final deviceId = await storage.getDeviceId();

      if (refreshToken == null || refreshToken.isEmpty) {
        await storage.clearTokens();
        onSessionExpired();
        return handler.reject(err);
      }

      try {
        final refreshResponse = await refreshClient.post(
          '/auth/refresh',
          data: {
            'refreshToken': refreshToken,
            'deviceId': deviceId ?? 'unknown',
          },
        );

        final responseData = refreshResponse.data;
        final String newAccessToken = responseData['accessToken'];
        final String newRefreshToken = responseData['refreshToken'];

        await storage.saveTokens(
          accessToken: newAccessToken,
          refreshToken: newRefreshToken,
        );

        final retryOptions = err.requestOptions;
        retryOptions.headers['Authorization'] = 'Bearer $newAccessToken';

        final retryResponse = await dio.fetch(retryOptions);
        return handler.resolve(retryResponse);
      } on DioException catch (refreshErr) {
        await storage.clearTokens();
        onSessionExpired();
        return handler.reject(refreshErr);
      } catch (e) {
        await storage.clearTokens();
        onSessionExpired();
        return handler.reject(err);
      }
    }

    return handler.next(err);
  }
}
