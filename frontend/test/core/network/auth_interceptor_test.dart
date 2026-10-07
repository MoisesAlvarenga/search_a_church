import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/core/network/auth_interceptor.dart';
import 'package:search_a_church_app/core/storage/secure_storage_service.dart';

class MockDio extends Mock implements Dio {}
class MockSecureStorageService extends Mock implements ISecureStorageService {}
class MockRequestInterceptorHandler extends Mock implements RequestInterceptorHandler {}
class MockErrorInterceptorHandler extends Mock implements ErrorInterceptorHandler {}

void main() {
  late MockDio mockDio;
  late MockDio mockRefreshClient;
  late MockSecureStorageService mockStorage;
  late bool sessionExpiredCalled;
  late AuthInterceptor interceptor;

  setUpAll(() {
    registerFallbackValue(RequestOptions(path: '/'));
    registerFallbackValue(DioException(requestOptions: RequestOptions(path: '/')));
    registerFallbackValue(Response(requestOptions: RequestOptions(path: '/')));
  });

  setUp(() {
    mockDio = MockDio();
    mockRefreshClient = MockDio();
    mockStorage = MockSecureStorageService();
    sessionExpiredCalled = false;

    when(() => mockDio.options).thenReturn(BaseOptions(baseUrl: 'https://api.church.org'));

    interceptor = AuthInterceptor(
      dio: mockDio,
      storage: mockStorage,
      onSessionExpired: () => sessionExpiredCalled = true,
      refreshClient: mockRefreshClient,
    );
  });

  group('AuthInterceptor - onRequest', () {
    test('injects Bearer token into headers when access token is stored', () async {
      when(() => mockStorage.getAccessToken())
          .thenAnswer((_) async => 'valid_jwt_token_123');

      final options = RequestOptions(path: '/map/search');
      final handler = MockRequestInterceptorHandler();
      when(() => handler.next(options)).thenReturn(null);

      await interceptor.onRequest(options, handler);

      expect(options.headers['Authorization'], 'Bearer valid_jwt_token_123');
      verify(() => handler.next(options)).called(1);
    });

    test('does not inject Authorization header when access token is null', () async {
      when(() => mockStorage.getAccessToken()).thenAnswer((_) async => null);

      final options = RequestOptions(path: '/map/search');
      final handler = MockRequestInterceptorHandler();
      when(() => handler.next(options)).thenReturn(null);

      await interceptor.onRequest(options, handler);

      expect(options.headers['Authorization'], isNull);
      verify(() => handler.next(options)).called(1);
    });
  });

  group('AuthInterceptor - onError', () {
    test('passes through non-401 errors without attempting refresh', () async {
      final requestOptions = RequestOptions(path: '/map/search');
      final error = DioException(
        requestOptions: requestOptions,
        response: Response(requestOptions: requestOptions, statusCode: 500),
      );
      final handler = MockErrorInterceptorHandler();
      when(() => handler.next(error)).thenReturn(null);

      await interceptor.onError(error, handler);

      verify(() => handler.next(error)).called(1);
      verifyZeroInteractions(mockRefreshClient);
      expect(sessionExpiredCalled, isFalse);
    });

    test('passes through 401 errors on auth routes without recursive refresh', () async {
      final requestOptions = RequestOptions(path: '/auth/login');
      final error = DioException(
        requestOptions: requestOptions,
        response: Response(requestOptions: requestOptions, statusCode: 401),
      );
      final handler = MockErrorInterceptorHandler();
      when(() => handler.next(error)).thenReturn(null);

      await interceptor.onError(error, handler);

      verify(() => handler.next(error)).called(1);
      verifyZeroInteractions(mockRefreshClient);
      expect(sessionExpiredCalled, isFalse);
    });

    test('clears tokens and notifies session expired when refresh token is missing', () async {
      final requestOptions = RequestOptions(path: '/map/search');
      final error = DioException(
        requestOptions: requestOptions,
        response: Response(requestOptions: requestOptions, statusCode: 401),
      );
      final handler = MockErrorInterceptorHandler();
      when(() => mockStorage.getRefreshToken()).thenAnswer((_) async => null);
      when(() => mockStorage.getDeviceId()).thenAnswer((_) async => 'device-1');
      when(() => mockStorage.clearTokens()).thenAnswer((_) async {});
      when(() => handler.reject(error)).thenReturn(null);

      await interceptor.onError(error, handler);

      verify(() => mockStorage.clearTokens()).called(1);
      expect(sessionExpiredCalled, isTrue);
      verify(() => handler.reject(error)).called(1);
    });

    test('executes atomic silent refresh on 401 and retries original request', () async {
      final requestOptions = RequestOptions(path: '/map/search', headers: {});
      final error = DioException(
        requestOptions: requestOptions,
        response: Response(requestOptions: requestOptions, statusCode: 401),
      );
      final handler = MockErrorInterceptorHandler();

      when(() => mockStorage.getRefreshToken()).thenAnswer((_) async => 'old_refresh_token');
      when(() => mockStorage.getDeviceId()).thenAnswer((_) async => 'device-uuid-1');

      when(() => mockRefreshClient.post(
            '/auth/refresh',
            data: {
              'refreshToken': 'old_refresh_token',
              'deviceId': 'device-uuid-1',
            },
          )).thenAnswer((_) async => Response(
            requestOptions: RequestOptions(path: '/auth/refresh'),
            statusCode: 200,
            data: {
              'accessToken': 'new_access_token_789',
              'refreshToken': 'new_refresh_token_999',
            },
          ));

      when(() => mockStorage.saveTokens(
            accessToken: 'new_access_token_789',
            refreshToken: 'new_refresh_token_999',
          )).thenAnswer((_) async {});

      final retriedResponse = Response(
        requestOptions: requestOptions,
        statusCode: 200,
        data: {'churches': []},
      );

      when(() => mockDio.fetch(any())).thenAnswer((_) async => retriedResponse);
      when(() => handler.resolve(retriedResponse)).thenReturn(null);

      await interceptor.onError(error, handler);

      verify(() => mockStorage.saveTokens(
            accessToken: 'new_access_token_789',
            refreshToken: 'new_refresh_token_999',
          )).called(1);

      expect(requestOptions.headers['Authorization'], 'Bearer new_access_token_789');
      verify(() => mockDio.fetch(requestOptions)).called(1);
      verify(() => handler.resolve(retriedResponse)).called(1);
      expect(sessionExpiredCalled, isFalse);
    });

    test('under refresh breach/failure, clears tokens, calls callback and rejects', () async {
      final requestOptions = RequestOptions(path: '/map/search');
      final error = DioException(
        requestOptions: requestOptions,
        response: Response(requestOptions: requestOptions, statusCode: 401),
      );
      final handler = MockErrorInterceptorHandler();

      when(() => mockStorage.getRefreshToken()).thenAnswer((_) async => 'compromised_refresh_token');
      when(() => mockStorage.getDeviceId()).thenAnswer((_) async => 'device-uuid-1');

      final breachException = DioException(
        requestOptions: RequestOptions(path: '/auth/refresh'),
        response: Response(
          requestOptions: RequestOptions(path: '/auth/refresh'),
          statusCode: 401,
          data: {'error': 'TOKEN_BREACH_DETECTED'},
        ),
      );

      when(() => mockRefreshClient.post(
            '/auth/refresh',
            data: any(named: 'data'),
          )).thenThrow(breachException);

      when(() => mockStorage.clearTokens()).thenAnswer((_) async {});
      when(() => handler.reject(breachException)).thenReturn(null);

      await interceptor.onError(error, handler);

      verify(() => mockStorage.clearTokens()).called(1);
      expect(sessionExpiredCalled, isTrue);
      verify(() => handler.reject(breachException)).called(1);
    });
  });
}
