import 'package:equatable/equatable.dart';
import '../../data/models/church_profile_model.dart';

/// Estado base para o gerenciamento do perfil e dados cadastrais da congregação.
abstract class ChurchProfileState extends Equatable {
  const ChurchProfileState();

  @override
  List<Object?> get props => [];
}

/// Estado inicial antes de qualquer consulta de igreja.
class ChurchProfileInitial extends ChurchProfileState {
  const ChurchProfileInitial();
}

/// Estado emitido durante o carregamento dos dados da congregação.
class ChurchProfileLoading extends ChurchProfileState {
  const ChurchProfileLoading();
}

/// Estado com o perfil e cultos da igreja carregados com sucesso.
class ChurchProfileLoaded extends ChurchProfileState {
  final ChurchProfileModel profile;

  const ChurchProfileLoaded(this.profile);

  @override
  List<Object?> get props => [profile];
}

/// Estado durante o salvamento das alterações cadastrais ou cultos da congregação.
class ChurchProfileSaving extends ChurchProfileState {
  const ChurchProfileSaving();
}

/// Estado emitido após alteração bem-sucedida dos dados da igreja.
class ChurchProfileSaveSuccess extends ChurchProfileState {
  final ChurchProfileModel updatedProfile;

  const ChurchProfileSaveSuccess(this.updatedProfile);

  @override
  List<Object?> get props => [updatedProfile];
}

/// Estado durante a alteração de status ativo/inativo da congregação.
class ChurchProfileStatusUpdating extends ChurchProfileState {
  const ChurchProfileStatusUpdating();
}

/// Estado emitido após a alteração de status de atividade da igreja.
class ChurchProfileStatusUpdated extends ChurchProfileState {
  final ChurchStatusResponseModel response;

  const ChurchProfileStatusUpdated(this.response);

  @override
  List<Object?> get props => [response];
}

/// Estado de erro em operações sobre a congregação, com flags especializadas para conflitos.
class ChurchProfileError extends ChurchProfileState {
  final String message;
  final String? errorCode;
  final int? statusCode;
  final bool isPlaceIdConflict;
  final bool isConcurrencyConflict;

  const ChurchProfileError(
    this.message, {
    this.errorCode,
    this.statusCode,
    this.isPlaceIdConflict = false,
    this.isConcurrencyConflict = false,
  });

  @override
  List<Object?> get props => [
        message,
        errorCode,
        statusCode,
        isPlaceIdConflict,
        isConcurrencyConflict,
      ];
}
