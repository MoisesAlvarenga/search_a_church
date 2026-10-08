import 'package:dio/dio.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../../core/storage/secure_storage_service.dart';
import '../../data/auth_repository.dart';
import 'auth_state.dart';

class AuthCubit extends Cubit<AuthState> {
  final IAuthRepository _authRepository;
  final ISecureStorageService _storage;

  AuthCubit({
    required this._authRepository,
    required this._storage,
  })  : super(AuthInitial());

  Future<void> checkAuthStatus() async {
    final token = await _storage.getAccessToken();
    if (token == null || token.isEmpty) {
      emit(Unauthenticated());
      return;
    }

    try {
      final user = await _authRepository.getCurrentUser();
      emit(Authenticated(user));
    } catch (_) {
      emit(Unauthenticated());
    }
  }

  Future<void> login({
    required String email,
    required String password,
    required String deviceId,
  }) async {
    emit(Authenticating());
    try {
      final result = await _authRepository.login(
        email: email,
        password: password,
        deviceId: deviceId,
      );
      emit(Authenticated(result.user));
    } on DioException catch (e) {
      final data = e.response?.data;
      String message = 'Falha ao autenticar. Verifique suas credenciais.';
      String? errorCode;
      int? retryAfter;

      if (data is Map<String, dynamic>) {
        message = data['message'] as String? ?? message;
        errorCode = data['error'] as String?;
        retryAfter = data['retryAfterSeconds'] as int?;
      }

      emit(AuthError(message, errorCode: errorCode, retryAfterSeconds: retryAfter));
    } catch (e) {
      emit(AuthError('Ocorreu um erro inesperado: $e'));
    }
  }

  Future<void> register({
    required String name,
    required String email,
    required String password,
    required String deviceId,
  }) async {
    emit(Authenticating());
    try {
      final result = await _authRepository.register(
        name: name,
        email: email,
        password: password,
        deviceId: deviceId,
      );
      emit(Authenticated(result.user));
    } on DioException catch (e) {
      final data = e.response?.data;
      String message = 'Falha ao criar conta.';
      String? errorCode;
      int? retryAfter;

      if (data is Map<String, dynamic>) {
        message = data['message'] as String? ?? message;
        errorCode = data['error'] as String?;
        retryAfter = data['retryAfterSeconds'] as int?;
      }

      emit(AuthError(message, errorCode: errorCode, retryAfterSeconds: retryAfter));
    } catch (e) {
      emit(AuthError('Ocorreu um erro inesperado: $e'));
    }
  }

  Future<void> logout() async {
    await _authRepository.logout();
    emit(Unauthenticated());
  }

  void onSessionExpired() {
    emit(Unauthenticated());
  }
}
