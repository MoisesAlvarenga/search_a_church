import 'package:dio/dio.dart';
import '../datasources/claim_remote_data_source.dart';
import '../failures/claim_failures.dart';
import '../models/church_claim_model.dart';
import '../models/claim_status_model.dart';
import '../models/dispute_case_model.dart';
import '../models/verification_result_model.dart';

/// Contrato do repositório da funcionalidade de reivindicação de perfil e disputas.
abstract class IClaimRepository {
  Future<ChurchClaimModel> initiateClaim(ClaimInitiationRequestModel request);

  Future<VerificationResultModel> verifyGeofence(GeofenceVerificationRequestModel request);

  Future<SocialBioTokenModel> generateSocialBioToken(SocialTokenGenerateRequestModel request);

  Future<VerificationResultModel> confirmSocialBio(SocialBioConfirmRequestModel request);

  Future<DomainOtpSendResponseModel> sendDomainOtp(DomainOtpSendRequestModel request);

  Future<VerificationResultModel> confirmDomainOtp(DomainOtpConfirmRequestModel request);

  Future<VerificationResultModel> submitCartorioDocument(CartorioDocumentRequestModel request);

  Future<VerificationResultModel> verifyQsa(QsaVerificationRequestModel request);

  Future<DisputeCaseModel> contestDispute(DisputeContestRequestModel request);

  Future<DisputeEvidenceResponseModel> submitDisputeCertificate(DisputeEvidenceRequestModel request);

  Future<ClaimStatusModel> getClaimStatus(String churchId);
}

/// Implementação do repositório convertendo exceções HTTP Dio em falhas tipadas de domínio.
class ClaimRepository implements IClaimRepository {
  final IClaimRemoteDataSource _remoteDataSource;

  ClaimRepository({required this._remoteDataSource});

  @override
  Future<ChurchClaimModel> initiateClaim(ClaimInitiationRequestModel request) =>
      _guard(() => _remoteDataSource.initiateClaim(request));

  @override
  Future<VerificationResultModel> verifyGeofence(GeofenceVerificationRequestModel request) =>
      _guard(() => _remoteDataSource.verifyGeofence(request));

  @override
  Future<SocialBioTokenModel> generateSocialBioToken(SocialTokenGenerateRequestModel request) =>
      _guard(() => _remoteDataSource.generateSocialBioToken(request));

  @override
  Future<VerificationResultModel> confirmSocialBio(SocialBioConfirmRequestModel request) =>
      _guard(() => _remoteDataSource.confirmSocialBio(request));

  @override
  Future<DomainOtpSendResponseModel> sendDomainOtp(DomainOtpSendRequestModel request) =>
      _guard(() => _remoteDataSource.sendDomainOtp(request));

  @override
  Future<VerificationResultModel> confirmDomainOtp(DomainOtpConfirmRequestModel request) =>
      _guard(() => _remoteDataSource.confirmDomainOtp(request));

  @override
  Future<VerificationResultModel> submitCartorioDocument(CartorioDocumentRequestModel request) =>
      _guard(() => _remoteDataSource.submitCartorioDocument(request));

  @override
  Future<VerificationResultModel> verifyQsa(QsaVerificationRequestModel request) =>
      _guard(() => _remoteDataSource.verifyQsa(request));

  @override
  Future<DisputeCaseModel> contestDispute(DisputeContestRequestModel request) =>
      _guard(() => _remoteDataSource.contestDispute(request));

  @override
  Future<DisputeEvidenceResponseModel> submitDisputeCertificate(DisputeEvidenceRequestModel request) =>
      _guard(() => _remoteDataSource.submitDisputeCertificate(request));

  @override
  Future<ClaimStatusModel> getClaimStatus(String churchId) =>
      _guard(() => _remoteDataSource.getClaimStatus(churchId));

  Future<T> _guard<T>(Future<T> Function() call) async {
    try {
      return await call();
    } on DioException catch (e) {
      throw _mapDioException(e);
    } catch (e) {
      if (e is ClaimFailure) rethrow;
      throw ClaimFailure('Erro inesperado durante a operação: $e');
    }
  }

  ClaimFailure _mapDioException(DioException e) {
    final statusCode = e.response?.statusCode;
    final data = e.response?.data;

    String message = e.message ?? 'Falha de comunicação com o servidor';
    String? errorCode;

    if (data is Map<String, dynamic>) {
      message = data['message'] as String? ?? message;
      errorCode = data['error'] as String?;
    }

    if (statusCode == 401) {
      return UnauthorizedFailure(
        message,
        errorCode: errorCode ?? 'UNAUTHORIZED',
        statusCode: statusCode,
      );
    }

    if (statusCode == 404) {
      return NotFoundFailure(
        message,
        errorCode: errorCode ?? 'NOT_FOUND',
        statusCode: statusCode,
      );
    }

    if (statusCode == 409 ||
        errorCode == 'IGREJA_JA_REIVINDICADA' ||
        errorCode == 'DISPUTA_EM_ANDAMENTO' ||
        errorCode == 'CLAIM_JA_EM_ANDAMENTO' ||
        errorCode == 'CLAIM_PENDENTE_OUTRO_USUARIO') {
      return ConflictFailure(
        message,
        errorCode: errorCode ?? 'CONFLICT',
        statusCode: statusCode ?? 409,
      );
    }

    if (errorCode == 'LOCALIZACAO_SIMULADA_DETECTADA') {
      return MockLocationFailure(
        message,
        errorCode: errorCode,
        statusCode: statusCode ?? 400,
      );
    }

    if (errorCode == 'FORA_DO_RAIO_PERMITIDO' || errorCode == 'PRECISAO_GPS_INSUFICIENTE') {
      return GeofenceFailure(
        message,
        errorCode: errorCode,
        statusCode: statusCode ?? 400,
      );
    }

    if (errorCode != null && errorCode.contains('DISPUTA')) {
      return DisputeFailure(
        message,
        errorCode: errorCode,
        statusCode: statusCode ?? 400,
      );
    }

    return ClaimFailure(
      message,
      errorCode: errorCode,
      statusCode: statusCode,
    );
  }
}
