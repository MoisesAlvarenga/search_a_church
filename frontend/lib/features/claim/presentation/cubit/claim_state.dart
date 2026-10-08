import 'package:equatable/equatable.dart';
import '../../data/models/church_claim_model.dart';
import '../../data/models/claim_status_model.dart';
import '../../data/models/verification_result_model.dart';

/// Estado base do ClaimCubit.
abstract class ClaimState extends Equatable {
  const ClaimState();

  @override
  List<Object?> get props => [];
}

/// Estado inicial sem operações de reivindicação em andamento.
class ClaimInitial extends ClaimState {
  const ClaimInitial();
}

/// Estado de carregamento geral (ex: carregando status).
class ClaimLoading extends ClaimState {
  const ClaimLoading();
}

/// Estado com status atual da congregação carregado com sucesso.
class ClaimStatusLoaded extends ClaimState {
  final ClaimStatusModel status;

  const ClaimStatusLoaded(this.status);

  @override
  List<Object?> get props => [status];
}

/// Estado durante submissão inicial de reivindicação com aceite de ToS.
class ClaimSubmitting extends ClaimState {
  const ClaimSubmitting();
}

/// Estado quando o processo de reivindicação é iniciado com sucesso (Under_Review).
class ClaimInitiated extends ClaimState {
  final ChurchClaimModel claim;

  const ClaimInitiated(this.claim);

  bool get hasExpiringWarning {
    if (claim.expiresAt == null) return false;
    final diff = claim.expiresAt!.difference(DateTime.now());
    return diff.inHours <= 24 && !diff.isNegative;
  }

  @override
  List<Object?> get props => [claim];
}

/// Estado durante submissão ou conferência de provas (Geofencing, Redes, OTP, Documento, QSA).
class ClaimVerifying extends ClaimState {
  final String stepDescription;

  const ClaimVerifying({this.stepDescription = 'Validando evidência...'});

  @override
  List<Object?> get props => [stepDescription];
}

/// Estado quando um token temporário SAC-XXXX-VERIFY para bio é gerado.
class SocialBioTokenGenerated extends ClaimState {
  final SocialBioTokenModel tokenModel;

  const SocialBioTokenGenerated(this.tokenModel);

  @override
  List<Object?> get props => [tokenModel];
}

/// Estado quando o OTP para e-mail com domínio institucional é enviado.
class DomainOtpSent extends ClaimState {
  final DomainOtpSendResponseModel otpResponse;

  const DomainOtpSent(this.otpResponse);

  @override
  List<Object?> get props => [otpResponse];
}

/// Estado de homologação com sucesso e selo de verificação concedido.
class ClaimVerifiedSuccess extends ClaimState {
  final VerificationResultModel result;

  const ClaimVerifiedSuccess(this.result);

  @override
  List<Object?> get props => [result];
}

/// Estado de erro com mensagens formatadas e flags de contexto.
class ClaimError extends ClaimState {
  final String message;
  final String? errorCode;
  final int? statusCode;
  final bool isMockLocation;
  final bool isOutOfRange;
  final bool isConflict;

  const ClaimError(
    this.message, {
    this.errorCode,
    this.statusCode,
    this.isMockLocation = false,
    this.isOutOfRange = false,
    this.isConflict = false,
  });

  @override
  List<Object?> get props => [
    message,
    errorCode,
    statusCode,
    isMockLocation,
    isOutOfRange,
    isConflict,
  ];
}
