import 'package:bloc_test/bloc_test.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/core/storage/secure_storage_service.dart';
import 'package:search_a_church_app/features/auth/data/auth_repository.dart';
import 'package:search_a_church_app/features/auth/data/models/auth_response_model.dart';
import 'package:search_a_church_app/features/auth/data/models/user_model.dart';
import 'package:search_a_church_app/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:search_a_church_app/features/auth/presentation/cubit/auth_state.dart';

class MockAuthRepository extends Mock implements IAuthRepository {}
class MockSecureStorageService extends Mock implements ISecureStorageService {}

void main() {
  late MockAuthRepository mockRepository;
  late MockSecureStorageService mockStorage;

  final testUser = const UserModel(
    id: 'user-id-123',
    email: 'member@church.org',
    name: 'Sister Mary',
    role: 'User',
    isVerifiedRepresentative: false,
  );

  final testAuthResponse = AuthResponseModel(
    accessToken: 'valid_access_token',
    refreshToken: 'valid_refresh_token',
    user: testUser,
  );

  setUp(() {
    mockRepository = MockAuthRepository();
    mockStorage = MockSecureStorageService();
  });

  group('AuthCubit - checkAuthStatus', () {
    test('initial state is AuthInitial', () {
      final cubit = AuthCubit(authRepository: mockRepository, storage: mockStorage);
      expect(cubit.state, isA<AuthInitial>());
    });

    blocTest<AuthCubit, AuthState>(
      'emits [Unauthenticated] when no access token is stored',
      build: () {
        when(() => mockStorage.getAccessToken()).thenAnswer((_) async => null);
        return AuthCubit(authRepository: mockRepository, storage: mockStorage);
      },
      act: (cubit) => cubit.checkAuthStatus(),
      expect: () => [isA<Unauthenticated>()],
    );

    blocTest<AuthCubit, AuthState>(
      'emits [Authenticated] when token exists and getCurrentUser succeeds',
      build: () {
        when(() => mockStorage.getAccessToken()).thenAnswer((_) async => 'stored_token');
        when(() => mockRepository.getCurrentUser()).thenAnswer((_) async => testUser);
        return AuthCubit(authRepository: mockRepository, storage: mockStorage);
      },
      act: (cubit) => cubit.checkAuthStatus(),
      expect: () => [Authenticated(testUser)],
    );

    blocTest<AuthCubit, AuthState>(
      'emits [Unauthenticated] when token exists but getCurrentUser fails',
      build: () {
        when(() => mockStorage.getAccessToken()).thenAnswer((_) async => 'stored_token');
        when(() => mockRepository.getCurrentUser()).thenThrow(Exception('Expired'));
        return AuthCubit(authRepository: mockRepository, storage: mockStorage);
      },
      act: (cubit) => cubit.checkAuthStatus(),
      expect: () => [isA<Unauthenticated>()],
    );
  });

  group('AuthCubit - login', () {
    blocTest<AuthCubit, AuthState>(
      'emits [Authenticating, Authenticated] on successful login',
      build: () {
        when(() => mockRepository.login(
              email: 'member@church.org',
              password: 'Password123!',
              deviceId: 'device-1',
            )).thenAnswer((_) async => testAuthResponse);
        return AuthCubit(authRepository: mockRepository, storage: mockStorage);
      },
      act: (cubit) => cubit.login(
        email: 'member@church.org',
        password: 'Password123!',
        deviceId: 'device-1',
      ),
      expect: () => [
        isA<Authenticating>(),
        Authenticated(testUser),
      ],
    );

    blocTest<AuthCubit, AuthState>(
      'emits [Authenticating, AuthError] when login fails with DioException',
      build: () {
        when(() => mockRepository.login(
              email: any(named: 'email'),
              password: any(named: 'password'),
              deviceId: any(named: 'deviceId'),
            )).thenThrow(DioException(
          requestOptions: RequestOptions(path: '/auth/login'),
          response: Response(
            requestOptions: RequestOptions(path: '/auth/login'),
            statusCode: 401,
            data: {
              'error': 'CREDENCIAIS_INVALIDAS',
              'message': 'E-mail ou senha incorretos.',
            },
          ),
        ));
        return AuthCubit(authRepository: mockRepository, storage: mockStorage);
      },
      act: (cubit) => cubit.login(
        email: 'member@church.org',
        password: 'WrongPassword!',
        deviceId: 'device-1',
      ),
      expect: () => [
        isA<Authenticating>(),
        const AuthError(
          'E-mail ou senha incorretos.',
          errorCode: 'CREDENCIAIS_INVALIDAS',
        ),
      ],
    );
  });

  group('AuthCubit - register', () {
    blocTest<AuthCubit, AuthState>(
      'emits [Authenticating, Authenticated] on successful registration',
      build: () {
        when(() => mockRepository.register(
              name: 'Sister Mary',
              email: 'member@church.org',
              password: 'Password123!',
              deviceId: 'device-1',
            )).thenAnswer((_) async => testAuthResponse);
        return AuthCubit(authRepository: mockRepository, storage: mockStorage);
      },
      act: (cubit) => cubit.register(
        name: 'Sister Mary',
        email: 'member@church.org',
        password: 'Password123!',
        deviceId: 'device-1',
      ),
      expect: () => [
        isA<Authenticating>(),
        Authenticated(testUser),
      ],
    );

    blocTest<AuthCubit, AuthState>(
      'emits [Authenticating, AuthError] when registration fails with rate limit',
      build: () {
        when(() => mockRepository.register(
              name: any(named: 'name'),
              email: any(named: 'email'),
              password: any(named: 'password'),
              deviceId: any(named: 'deviceId'),
            )).thenThrow(DioException(
          requestOptions: RequestOptions(path: '/auth/register'),
          response: Response(
            requestOptions: RequestOptions(path: '/auth/register'),
            statusCode: 429,
            data: {
              'error': 'RATE_LIMIT_EXCEEDED',
              'message': 'Limite de tentativas excedido.',
              'retryAfterSeconds': 3600,
            },
          ),
        ));
        return AuthCubit(authRepository: mockRepository, storage: mockStorage);
      },
      act: (cubit) => cubit.register(
        name: 'Sister Mary',
        email: 'member@church.org',
        password: 'Password123!',
        deviceId: 'device-1',
      ),
      expect: () => [
        isA<Authenticating>(),
        const AuthError(
          'Limite de tentativas excedido.',
          errorCode: 'RATE_LIMIT_EXCEEDED',
          retryAfterSeconds: 3600,
        ),
      ],
    );
  });

  group('AuthCubit - logout and session expired', () {
    blocTest<AuthCubit, AuthState>(
      'emits [Unauthenticated] when logout is called',
      build: () {
        when(() => mockRepository.logout()).thenAnswer((_) async {});
        return AuthCubit(authRepository: mockRepository, storage: mockStorage);
      },
      act: (cubit) => cubit.logout(),
      expect: () => [isA<Unauthenticated>()],
    );

    blocTest<AuthCubit, AuthState>(
      'emits [Unauthenticated] when onSessionExpired is triggered',
      build: () => AuthCubit(authRepository: mockRepository, storage: mockStorage),
      act: (cubit) => cubit.onSessionExpired(),
      expect: () => [isA<Unauthenticated>()],
    );
  });
}
