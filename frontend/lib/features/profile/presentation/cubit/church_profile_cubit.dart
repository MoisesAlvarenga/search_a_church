import 'package:flutter_bloc/flutter_bloc.dart';
import '../../data/failures/profile_failures.dart';
import '../../data/models/church_profile_model.dart';
import '../../data/repositories/profile_repository.dart';
import 'church_profile_state.dart';

/// Cubit responsável pelo gerenciamento de estado do perfil de uma congregação.
class ChurchProfileCubit extends Cubit<ChurchProfileState> {
  final IProfileRepository _repository;

  ChurchProfileCubit({required IProfileRepository repository})
      : _repository = repository,
        super(const ChurchProfileInitial());

  /// Carrega os dados cadastrais, cultos e tags da congregação pelo identificador.
  Future<void> loadProfile(String churchId) async {
    emit(const ChurchProfileLoading());
    try {
      final profile = await _repository.getChurchProfile(churchId);
      emit(ChurchProfileLoaded(profile));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Atualiza os dados, endereço, cultos e tags da igreja por representante verificado.
  Future<void> updateProfile(
    String churchId,
    UpdateChurchProfileRequestModel request, {
    String? ifMatchHeader,
  }) async {
    emit(const ChurchProfileSaving());
    try {
      final updated = await _repository.updateChurchProfile(
        churchId,
        request,
        ifMatchHeader: ifMatchHeader,
      );
      emit(ChurchProfileSaveSuccess(updated));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Altera o status de atividade da congregação (ativo/inativo) no sistema.
  Future<void> setChurchStatus(
    String churchId,
    UpdateChurchStatusRequestModel request, {
    String? ifMatchHeader,
  }) async {
    emit(const ChurchProfileStatusUpdating());
    try {
      final result = await _repository.setChurchStatus(
        churchId,
        request,
        ifMatchHeader: ifMatchHeader,
      );
      emit(ChurchProfileStatusUpdated(result));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  ChurchProfileError _mapError(Object error) {
    if (error is ProfileFailure) {
      final isPlaceIdConflict = error is ConflictFailure &&
          error.errorCode == 'PLACE_ID_JA_VINCULADO';
      final isConcurrencyConflict = error is ConflictFailure &&
          error.errorCode == 'CONFLITO_CONCORRENCIA';

      return ChurchProfileError(
        error.message,
        errorCode: error.errorCode,
        statusCode: error.statusCode,
        isPlaceIdConflict: isPlaceIdConflict,
        isConcurrencyConflict: isConcurrencyConflict,
      );
    }
    return ChurchProfileError('Erro inesperado: $error');
  }
}
