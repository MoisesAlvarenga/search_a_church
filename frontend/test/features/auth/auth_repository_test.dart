import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/core/storage/secure_storage_service.dart';
import 'package:search_a_church_app/features/auth/data/auth_repository.dart';
import 'package:search_a_church_app/features/auth/data/models/user_model.dart';

class MockDio extends Mock implements Dio {}
class MockSecureStorageService extends Mock implements ISecureStorageService {}

void main() {
  late MockDio mockDio;
  late MockSecureStorageService mockStorage;
  late AuthRepository repository;

  final testUser = const UserModel(
    id: 'user-uuid-1',
    email: 'user@church.org',
    name: 'Brother Joseph',
    role: 'User',
    isVerifiedRepresentative: false,
  );

  final testAuthJson = {
    'accessToken': 'jwt_access_token',
    'refreshToken': 'refresh_token_abc',
    'user': testUser.toJson(),
  };

  setUp(() {
    mockDio = MockDio();
    mockStorage = MockSecureStorageService();
    repository = AuthRepository(dio: mockDio, storage: mockStorage);
  });

  group('AuthRepository', () {
    test('register calls /auth/register, saves tokens and deviceId, returns AuthResponseModel', () async {
      when(() => mockDio.post('/auth/register', data: any(named: 'data')))
          .thenAnswer((_) async => Response(
                requestOptions: RequestOptions(path: '/auth/register'),
                statusCode: 201,
                data: testAuthJson,
              ));

      when(() => mockStorage.saveTokens(
            accessToken: 'jwt_access_token',
            refreshToken: 'refresh_token_abc',
          )).thenAnswer((_) async {});

      when(() => mockStorage.saveDeviceId('device-uuid-1'))
          .thenAnswer((_) async {});

      final result = await repository.register(
        name: 'Brother Joseph',
        email: 'user@church.org',
        password: 'Password123!',
        deviceId: 'device-uuid-1',
      );

      expect(result.accessToken, 'jwt_access_token');
      expect(result.refreshToken, 'refresh_token_abc');
      expect(result.user, testUser);

      verify(() => mockStorage.saveTokens(
            accessToken: 'jwt_access_token',
            refreshToken: 'refresh_token_abc',
          )).called(1);
      verify(() => mockStorage.saveDeviceId('device-uuid-1')).called(1);
    });

    test('login calls /auth/login, saves tokens and deviceId, returns AuthResponseModel', () async {
      when(() => mockDio.post('/auth/login', data: any(named: 'data')))
          .thenAnswer((_) async => Response(
                requestOptions: RequestOptions(path: '/auth/login'),
                statusCode: 200,
                data: testAuthJson,
              ));

      when(() => mockStorage.saveTokens(
            accessToken: 'jwt_access_token',
            refreshToken: 'refresh_token_abc',
          )).thenAnswer((_) async {});

      when(() => mockStorage.saveDeviceId('device-uuid-1'))
          .thenAnswer((_) async {});

      final result = await repository.login(
        email: 'user@church.org',
        password: 'Password123!',
        deviceId: 'device-uuid-1',
      );

      expect(result.user.email, 'user@church.org');
      verify(() => mockStorage.saveTokens(
            accessToken: 'jwt_access_token',
            refreshToken: 'refresh_token_abc',
          )).called(1);
    });

    test('logout calls /auth/logout and clears tokens from storage', () async {
      when(() => mockStorage.getRefreshToken())
          .thenAnswer((_) async => 'active_refresh_token');

      when(() => mockDio.post('/auth/logout', data: any(named: 'data')))
          .thenAnswer((_) async => Response(
                requestOptions: RequestOptions(path: '/auth/logout'),
                statusCode: 204,
              ));

      when(() => mockStorage.clearTokens()).thenAnswer((_) async {});

      await repository.logout();

      verify(() => mockDio.post(
            '/auth/logout',
            data: {'refreshToken': 'active_refresh_token'},
          )).called(1);
      verify(() => mockStorage.clearTokens()).called(1);
    });

    test('forgotPassword posts to /auth/forgot-password', () async {
      when(() => mockDio.post('/auth/forgot-password', data: any(named: 'data')))
          .thenAnswer((_) async => Response(
                requestOptions: RequestOptions(path: '/auth/forgot-password'),
                statusCode: 200,
              ));

      await repository.forgotPassword(email: 'user@church.org');

      verify(() => mockDio.post(
            '/auth/forgot-password',
            data: {'email': 'user@church.org'},
          )).called(1);
    });

    test('resetPassword posts to /auth/reset-password', () async {
      when(() => mockDio.post('/auth/reset-password', data: any(named: 'data')))
          .thenAnswer((_) async => Response(
                requestOptions: RequestOptions(path: '/auth/reset-password'),
                statusCode: 200,
              ));

      await repository.resetPassword(
        email: 'user@church.org',
        otpCode: '123456',
        newPassword: 'NewPassword123!',
      );

      verify(() => mockDio.post(
            '/auth/reset-password',
            data: {
              'email': 'user@church.org',
              'otpCode': '123456',
              'newPassword': 'NewPassword123!',
            },
          )).called(1);
    });

    test('getCurrentUser calls /auth/me and parses UserModel', () async {
      when(() => mockDio.get('/auth/me'))
          .thenAnswer((_) async => Response(
                requestOptions: RequestOptions(path: '/auth/me'),
                statusCode: 200,
                data: testUser.toJson(),
              ));

      final user = await repository.getCurrentUser();

      expect(user, testUser);
      verify(() => mockDio.get('/auth/me')).called(1);
    });
  });
}
