import 'package:flutter_bloc/flutter_bloc.dart';
import '../../data/failures/profile_failures.dart';
import '../../data/models/user_profile_model.dart';
import '../../data/repositories/profile_repository.dart';
import 'user_profile_state.dart';

/// Cubit para orquestração do ciclo de vida e preferências do perfil do usuário autenticado.
class UserProfileCubit extends Cubit<UserProfileState> {
  final IProfileRepository _repository;

  UserProfileCubit({required IProfileRepository repository})
      : _repository = repository,
        super(const UserProfileInitial());

  /// Consulta os dados e preferências do perfil do usuário autenticado.
  Future<void> loadProfile() async {
    emit(const UserProfileLoading());
    try {
      final profile = await _repository.getUserProfile();
      emit(UserProfileLoaded(profile));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Atualiza as preferências do usuário no backend (baseline para buscas futuras).
  Future<void> updateProfile(UpdateUserProfileRequestModel request) async {
    emit(const UserProfileSaving());
    try {
      final updated = await _repository.updateUserProfile(request);
      emit(UserProfileSaveSuccess(updated));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Executa o encerramento voluntário da conta com anonimização sob a LGPD.
  Future<void> deleteAccount() async {
    emit(const UserProfileDeleting());
    try {
      final response = await _repository.deleteUserAccount();
      emit(UserProfileDeleted(response.message));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  UserProfileError _mapError(Object error) {
    if (error is ProfileFailure) {
      return UserProfileError(
        error.message,
        errorCode: error.errorCode,
        statusCode: error.statusCode,
      );
    }
    return UserProfileError('Erro inesperado: $error');
  }
}
