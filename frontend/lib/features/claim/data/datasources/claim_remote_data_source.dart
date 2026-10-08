import 'package:dio/dio.dart';
import '../models/church_claim_model.dart';
import '../models/claim_status_model.dart';
import '../models/dispute_case_model.dart';
import '../models/verification_result_model.dart';

/// Contrato da fonte de dados remota para consumo dos endpoints de reivindicação e disputa (/claim/*).
abstract class IClaimRemoteDataSource {
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

/// Implementação da fonte de dados remota utilizando Dio com Bearer JWT.
class ClaimRemoteDataSource implements IClaimRemoteDataSource {
  final Dio _dio;

  ClaimRemoteDataSource({required this._dio});

  @override
  Future<ChurchClaimModel> initiateClaim(ClaimInitiationRequestModel request) async {
    final response = await _dio.post(
      '/claim/initiate',
      data: request.toJson(),
    );
    return ChurchClaimModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<VerificationResultModel> verifyGeofence(GeofenceVerificationRequestModel request) async {
    final response = await _dio.post(
      '/claim/verify/geofence',
      data: request.toJson(),
    );
    return VerificationResultModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<SocialBioTokenModel> generateSocialBioToken(SocialTokenGenerateRequestModel request) async {
    final response = await _dio.post(
      '/claim/verify/social-bio/generate',
      data: request.toJson(),
    );
    return SocialBioTokenModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<VerificationResultModel> confirmSocialBio(SocialBioConfirmRequestModel request) async {
    final response = await _dio.post(
      '/claim/verify/social-bio/confirm',
      data: request.toJson(),
    );
    return VerificationResultModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<DomainOtpSendResponseModel> sendDomainOtp(DomainOtpSendRequestModel request) async {
    final response = await _dio.post(
      '/claim/verify/domain/send-otp',
      data: request.toJson(),
    );
    return DomainOtpSendResponseModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<VerificationResultModel> confirmDomainOtp(DomainOtpConfirmRequestModel request) async {
    final response = await _dio.post(
      '/claim/verify/domain/confirm-otp',
      data: request.toJson(),
    );
    return VerificationResultModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<VerificationResultModel> submitCartorioDocument(CartorioDocumentRequestModel request) async {
    final response = await _dio.post(
      '/claim/verify/document/rcpj',
      data: request.toJson(),
    );
    return VerificationResultModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<VerificationResultModel> verifyQsa(QsaVerificationRequestModel request) async {
    final response = await _dio.post(
      '/claim/verify/document/qsa',
      data: request.toJson(),
    );
    return VerificationResultModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<DisputeCaseModel> contestDispute(DisputeContestRequestModel request) async {
    final response = await _dio.post(
      '/claim/dispute/contest',
      data: request.toJson(),
    );
    return DisputeCaseModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<DisputeEvidenceResponseModel> submitDisputeCertificate(DisputeEvidenceRequestModel request) async {
    final response = await _dio.post(
      '/claim/dispute/${request.disputeId}/submit-certificate',
      data: request.toJson(),
    );
    return DisputeEvidenceResponseModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<ClaimStatusModel> getClaimStatus(String churchId) async {
    final response = await _dio.get('/claim/status/$churchId');
    return ClaimStatusModel.fromJson(response.data as Map<String, dynamic>);
  }
}
