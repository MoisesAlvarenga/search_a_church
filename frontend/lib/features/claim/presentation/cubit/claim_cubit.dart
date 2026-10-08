import 'package:flutter_bloc/flutter_bloc.dart';
import '../../data/failures/claim_failures.dart';
import '../../data/models/church_claim_model.dart';
import '../../data/models/claim_enums.dart';
import '../../data/models/verification_result_model.dart';
import '../../data/repositories/claim_repository.dart';
import 'claim_state.dart';

/// Cubit responsável pelo gerenciamento de estado do fluxo de reivindicação de perfil.
class ClaimCubit extends Cubit<ClaimState> {
  final IClaimRepository _repository;

  ClaimCubit({required IClaimRepository repository})
      : _repository = repository,
        super(const ClaimInitial());

  /// Carrega o estado atual da congregação no ciclo de vida de reivindicação.
  Future<void> loadStatus(String churchId) async {
    emit(const ClaimLoading());
    try {
      final status = await _repository.getClaimStatus(churchId);
      emit(ClaimStatusLoaded(status));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Inicia o processo de reivindicação com aceite obrigatório de ToS e seleção de tier/método.
  Future<void> initiateClaim({
    required String churchId,
    required bool art299Accepted,
    required bool technicalIntermediaryAccepted,
    required ValidationMethod validationMethod,
    required VerificationTier targetTier,
    String tosVersion = '1.0',
  }) async {
    emit(const ClaimSubmitting());
    try {
      final request = ClaimInitiationRequestModel(
        churchId: churchId,
        tosVersion: tosVersion,
        art299Accepted: art299Accepted,
        technicalIntermediaryAccepted: technicalIntermediaryAccepted,
        validationMethod: validationMethod,
        targetTier: targetTier,
      );

      final claim = await _repository.initiateClaim(request);
      emit(ClaimInitiated(claim));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Executa validação de presença física via Geofencing Haversine e foto ao vivo.
  Future<void> verifyGeofence({
    required String claimId,
    required double deviceLatitude,
    required double deviceLongitude,
    required double horizontalAccuracyMeters,
    required bool isMockLocation,
    String? photoUrl,
    String? photoHashSha256,
  }) async {
    emit(const ClaimVerifying(stepDescription: 'Verificando presença física no templo...'));
    try {
      final request = GeofenceVerificationRequestModel(
        claimId: claimId,
        deviceLatitude: deviceLatitude,
        deviceLongitude: deviceLongitude,
        horizontalAccuracyMeters: horizontalAccuracyMeters,
        isMockLocation: isMockLocation,
        photoUrl: photoUrl,
        photoHashSha256: photoHashSha256,
      );

      final result = await _repository.verifyGeofence(request);
      emit(ClaimVerifiedSuccess(result));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Gera token temporário SAC-XXXX-VERIFY para bio de rede social.
  Future<void> generateSocialBioToken({
    required String claimId,
    required String socialNetwork,
    required String profileHandle,
  }) async {
    emit(const ClaimVerifying(stepDescription: 'Gerando código de verificação para redes sociais...'));
    try {
      final request = SocialTokenGenerateRequestModel(
        claimId: claimId,
        socialNetwork: socialNetwork,
        profileHandle: profileHandle,
      );

      final tokenModel = await _repository.generateSocialBioToken(request);
      emit(SocialBioTokenGenerated(tokenModel));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Confirma a presença do código temporário na bio pública da congregação.
  Future<void> confirmSocialBio({
    required String claimId,
    required String socialNetwork,
    required String profileHandle,
    required String expectedToken,
    String? bioContent,
  }) async {
    emit(const ClaimVerifying(stepDescription: 'Conferindo bio da rede social oficial...'));
    try {
      final request = SocialBioConfirmRequestModel(
        claimId: claimId,
        socialNetwork: socialNetwork,
        profileHandle: profileHandle,
        expectedToken: expectedToken,
        bioContent: bioContent,
      );

      final result = await _repository.confirmSocialBio(request);
      emit(ClaimVerifiedSuccess(result));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Envia código numérico OTP para e-mail corporativo em domínio institucional próprio.
  Future<void> sendDomainOtp({
    required String claimId,
    required String corporateEmail,
    required String expectedDomain,
  }) async {
    emit(const ClaimVerifying(stepDescription: 'Enviando código OTP para e-mail institucional...'));
    try {
      final request = DomainOtpSendRequestModel(
        claimId: claimId,
        corporateEmail: corporateEmail,
        expectedDomain: expectedDomain,
      );

      final response = await _repository.sendDomainOtp(request);
      emit(DomainOtpSent(response));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Confirma código numérico OTP de 6 dígitos recebido por e-mail institucional.
  Future<void> confirmDomainOtp({
    required String claimId,
    required String corporateEmail,
    required String otpCode,
  }) async {
    emit(const ClaimVerifying(stepDescription: 'Validando código OTP institucional...'));
    try {
      final request = DomainOtpConfirmRequestModel(
        claimId: claimId,
        corporateEmail: corporateEmail,
        otpCode: otpCode,
      );

      final result = await _repository.confirmDomainOtp(request);
      emit(ClaimVerifiedSuccess(result));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Submete documento cartorial registrado em RCPJ (Ata de Posse e Estatuto Social).
  Future<void> submitCartorioDocument({
    required String claimId,
    required String documentFileName,
    required String documentFileHashSha256,
    required DateTime averbationDate,
    String? documentUrl,
  }) async {
    emit(const ClaimVerifying(stepDescription: 'Registrando documentos do Cartório RCPJ...'));
    try {
      final request = CartorioDocumentRequestModel(
        claimId: claimId,
        documentFileName: documentFileName,
        documentFileHashSha256: documentFileHashSha256,
        averbationDate: averbationDate,
        documentUrl: documentUrl,
      );

      final result = await _repository.submitCartorioDocument(request);
      emit(ClaimVerifiedSuccess(result));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Valida qualificação de representação legal via cruzamento de CPF/CNPJ no QSA.
  Future<void> verifyQsa({
    required String claimId,
    required String churchCnpj,
    required String representativeCpf,
    required String representativeName,
  }) async {
    emit(const ClaimVerifying(stepDescription: 'Consultando Quadro de Sócios e Administradores (QSA)...'));
    try {
      final request = QsaVerificationRequestModel(
        claimId: claimId,
        churchCnpj: churchCnpj,
        representativeCpf: representativeCpf,
        representativeName: representativeName,
      );

      final result = await _repository.verifyQsa(request);
      emit(ClaimVerifiedSuccess(result));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Reinicia o estado do Cubit para o início.
  void reset() {
    emit(const ClaimInitial());
  }

  ClaimError _mapError(dynamic error) {
    if (error is MockLocationFailure) {
      return ClaimError(
        error.message,
        errorCode: error.errorCode,
        statusCode: error.statusCode,
        isMockLocation: true,
      );
    }
    if (error is GeofenceFailure) {
      return ClaimError(
        error.message,
        errorCode: error.errorCode,
        statusCode: error.statusCode,
        isOutOfRange: true,
      );
    }
    if (error is ConflictFailure) {
      return ClaimError(
        error.message,
        errorCode: error.errorCode,
        statusCode: error.statusCode,
        isConflict: true,
      );
    }
    if (error is ClaimFailure) {
      return ClaimError(
        error.message,
        errorCode: error.errorCode,
        statusCode: error.statusCode,
      );
    }
    return ClaimError('Ocorreu um erro inesperado: $error');
  }
}
