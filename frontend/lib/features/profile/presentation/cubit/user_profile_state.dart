import 'package:equatable/equatable.dart';
import '../../data/models/user_profile_model.dart';

/// Estado base para o gerenciamento de preferências do perfil de usuário.
abstract class UserProfileState extends Equatable {
  const UserProfileState();

  @override
  List<Object?> get props => [];
}

/// Estado inicial antes de qualquer carregamento.
class UserProfileInitial extends UserProfileState {
  const UserProfileInitial();
}

/// Estado emitido durante a recuperação das preferências do usuário.
class UserProfileLoading extends UserProfileState {
  const UserProfileLoading();
}

/// Estado com o perfil e preferências do usuário carregados.
class UserProfileLoaded extends UserProfileState {
  final UserProfileModel profile;

  const UserProfileLoaded(this.profile);

  @override
  List<Object?> get props => [profile];
}

/// Estado durante a gravação/atualização de preferências no backend.
class UserProfileSaving extends UserProfileState {
  const UserProfileSaving();
}

/// Estado emitido após salvamento bem-sucedido das preferências.
class UserProfileSaveSuccess extends UserProfileState {
  final UserProfileModel updatedProfile;

  const UserProfileSaveSuccess(this.updatedProfile);

  @override
  List<Object?> get props => [updatedProfile];
}

/// Estado durante o processo de soft delete e anonimização LGPD.
class UserProfileDeleting extends UserProfileState {
  const UserProfileDeleting();
}

/// Estado emitido quando a conta é anonimizada e encerrada com sucesso.
class UserProfileDeleted extends UserProfileState {
  final String message;

  const UserProfileDeleted(this.message);

  @override
  List<Object?> get props => [message];
}

/// Estado de falha na operação sobre o perfil do usuário.
class UserProfileError extends UserProfileState {
  final String message;
  final String? errorCode;
  final int? statusCode;

  const UserProfileError(
    this.message, {
    this.errorCode,
    this.statusCode,
  });

  @override
  List<Object?> get props => [message, errorCode, statusCode];
}
