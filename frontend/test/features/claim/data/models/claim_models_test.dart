import 'package:flutter_test/flutter_test.dart';
import 'package:search_a_church_app/features/claim/data/models/church_claim_model.dart';
import 'package:search_a_church_app/features/claim/data/models/claim_enums.dart';
import 'package:search_a_church_app/features/claim/data/models/claim_status_model.dart';
import 'package:search_a_church_app/features/claim/data/models/dispute_case_model.dart';
import 'package:search_a_church_app/features/claim/data/models/verification_result_model.dart';

void main() {
  group('Claim Enums Serialization & Deserialization', () {
    test('ChurchClaimState fromJson handles integer and string', () {
      expect(ChurchClaimState.fromJson(0), ChurchClaimState.unclaimed);
      expect(ChurchClaimState.fromJson(1), ChurchClaimState.pendingVerification);
      expect(ChurchClaimState.fromJson(2), ChurchClaimState.verified);
      expect(ChurchClaimState.fromJson(3), ChurchClaimState.inDispute);
      expect(ChurchClaimState.fromJson('Pending_Verification'), ChurchClaimState.pendingVerification);
      expect(ChurchClaimState.fromJson('verified'), ChurchClaimState.verified);
      expect(ChurchClaimState.fromJson('UNKNOWN'), ChurchClaimState.unclaimed);
      expect(ChurchClaimState.verified.toJson(), 2);
    });

    test('VerificationTier fromJson handles integer and string', () {
      expect(VerificationTier.fromJson(0), VerificationTier.none);
      expect(VerificationTier.fromJson(1), VerificationTier.tier3SocialPresencial);
      expect(VerificationTier.fromJson(2), VerificationTier.tier2Institucional);
      expect(VerificationTier.fromJson(3), VerificationTier.tier1Cartorio);
      expect(VerificationTier.fromJson('Tier1_Cartorio'), VerificationTier.tier1Cartorio);
      expect(VerificationTier.fromJson(99), VerificationTier.none);
    });

    test('ClaimRecordStatus fromJson handles integer and string', () {
      expect(ClaimRecordStatus.fromJson(0), ClaimRecordStatus.pending);
      expect(ClaimRecordStatus.fromJson(1), ClaimRecordStatus.approved);
      expect(ClaimRecordStatus.fromJson(2), ClaimRecordStatus.expired);
      expect(ClaimRecordStatus.fromJson(3), ClaimRecordStatus.rejected);
      expect(ClaimRecordStatus.fromJson(4), ClaimRecordStatus.revoked);
      expect(ClaimRecordStatus.fromJson('Approved'), ClaimRecordStatus.approved);
    });

    test('ValidationMethod fromJson handles integer and string', () {
      expect(ValidationMethod.fromJson(1), ValidationMethod.geofence);
      expect(ValidationMethod.fromJson(2), ValidationMethod.socialBio);
      expect(ValidationMethod.fromJson(4), ValidationMethod.institutionalEmail);
      expect(ValidationMethod.fromJson('CartorioRcpj'), ValidationMethod.cartorioRcpj);
    });

    test('DisputeResolutionType fromJson handles integer and string', () {
      expect(DisputeResolutionType.fromJson(1), DisputeResolutionType.automaticOverrideN1);
      expect(DisputeResolutionType.fromJson(2), DisputeResolutionType.parityDisputeOpened);
      expect(DisputeResolutionType.fromJson(3), DisputeResolutionType.resolvedByAverbationPrevalence);
      expect(DisputeResolutionType.fromJson('ResolvedByInertia'), DisputeResolutionType.resolvedByInertia);
    });
  });

  group('ChurchClaimModel and ClaimInitiationRequestModel', () {
    test('ClaimInitiationRequestModel serializes to expected JSON', () {
      const request = ClaimInitiationRequestModel(
        churchId: 'church-123',
        tosVersion: 'v1.0',
        art299Accepted: true,
        technicalIntermediaryAccepted: true,
        validationMethod: ValidationMethod.geofence,
        targetTier: VerificationTier.tier3SocialPresencial,
      );

      final json = request.toJson();
      expect(json['churchId'], 'church-123');
      expect(json['tosVersion'], 'v1.0');
      expect(json['art299Accepted'], true);
      expect(json['technicalIntermediaryAccepted'], true);
      expect(json['validationMethod'], 1);
      expect(json['targetTier'], 1);
    });

    test('ChurchClaimModel deserializes and serializes correctly', () {
      final json = {
        'claimId': 'claim-abc-123',
        'churchId': 'church-456',
        'status': 0,
        'targetTier': 1,
        'expiresAt': '2026-10-15T12:00:00.000Z',
        'ttlHours': 48,
        'message': 'Reivindicação iniciada com sucesso',
      };

      final model = ChurchClaimModel.fromJson(json);
      expect(model.claimId, 'claim-abc-123');
      expect(model.churchId, 'church-456');
      expect(model.status, ClaimRecordStatus.pending);
      expect(model.targetTier, VerificationTier.tier3SocialPresencial);
      expect(model.ttlHours, 48);
      expect(model.message, 'Reivindicação iniciada com sucesso');

      final serialized = model.toJson();
      expect(serialized['claimId'], 'claim-abc-123');
      expect(serialized['status'], 0);
      expect(serialized['targetTier'], 1);
    });
  });

  group('Verification Models', () {
    test('VerificationResultModel parses from JSON correctly', () {
      final json = {
        'claimId': 'claim-1',
        'churchId': 'church-1',
        'isVerified': true,
        'tier': 3,
        'message': 'Documento aprovado',
      };

      final model = VerificationResultModel.fromJson(json);
      expect(model.isVerified, true);
      expect(model.tier, VerificationTier.tier1Cartorio);
      expect(model.message, 'Documento aprovado');
    });

    test('GeofenceVerificationRequestModel serializes correctly', () {
      const req = GeofenceVerificationRequestModel(
        claimId: 'claim-geo',
        deviceLatitude: -23.55,
        deviceLongitude: -46.63,
        horizontalAccuracyMeters: 10.0,
        isMockLocation: false,
        photoUrl: 'https://cdn.sac.org/fachada.jpg',
        photoHashSha256: 'hash123',
      );

      final json = req.toJson();
      expect(json['claimId'], 'claim-geo');
      expect(json['deviceLatitude'], -23.55);
      expect(json['horizontalAccuracyMeters'], 10.0);
      expect(json['isMockLocation'], false);
      expect(json['photoUrl'], 'https://cdn.sac.org/fachada.jpg');
      expect(json['photoHashSha256'], 'hash123');
    });

    test('SocialBioTokenModel parses and serializes correctly', () {
      final json = {
        'claimId': 'claim-soc',
        'token': 'SAC-1234-VERIFY',
        'expiresAt': '2026-10-10T00:00:00.000Z',
        'instructions': 'Insira na bio',
      };

      final model = SocialBioTokenModel.fromJson(json);
      expect(model.token, 'SAC-1234-VERIFY');
      expect(model.instructions, 'Insira na bio');
      expect(model.toJson()['token'], 'SAC-1234-VERIFY');
    });

    test('DomainOtpSendResponseModel parses correctly', () {
      final json = {
        'claimId': 'claim-otp',
        'email': 'pastor@ad.org.br',
        'expiresAt': '2026-10-08T00:15:00.000Z',
        'message': 'OTP enviado',
      };

      final model = DomainOtpSendResponseModel.fromJson(json);
      expect(model.email, 'pastor@ad.org.br');
      expect(model.message, 'OTP enviado');
    });

    test('CartorioDocumentRequestModel serializes correctly', () {
      final req = CartorioDocumentRequestModel(
        claimId: 'claim-doc',
        documentFileName: 'estatuto.pdf',
        documentFileHashSha256: 'sha256hash',
        averbationDate: DateTime.parse('2026-01-01T00:00:00.000Z'),
        documentUrl: 'https://doc.url',
      );

      final json = req.toJson();
      expect(json['documentFileName'], 'estatuto.pdf');
      expect(json['documentFileHashSha256'], 'sha256hash');
      expect(json['documentUrl'], 'https://doc.url');
    });

    test('QsaVerificationRequestModel serializes correctly', () {
      const req = QsaVerificationRequestModel(
        claimId: 'claim-qsa',
        churchCnpj: '04.252.011/0001-10',
        representativeCpf: '111.444.777-35',
        representativeName: 'Pastor Teste',
      );

      final json = req.toJson();
      expect(json['churchCnpj'], '04.252.011/0001-10');
      expect(json['representativeCpf'], '111.444.777-35');
    });
  });

  group('Dispute Models', () {
    test('DisputeCaseModel parses correctly', () {
      final json = {
        'success': true,
        'resolutionType': 1,
        'churchId': 'church-disp',
        'disputeId': 'disp-123',
        'deadlineAt': '2026-10-15T00:00:00.000Z',
        'message': 'Sobreposição automática N1',
        'currentTier': 3,
        'activeRepresentativeUserId': 'user-win',
      };

      final model = DisputeCaseModel.fromJson(json);
      expect(model.success, true);
      expect(model.resolutionType, DisputeResolutionType.automaticOverrideN1);
      expect(model.currentTier, VerificationTier.tier1Cartorio);
      expect(model.activeRepresentativeUserId, 'user-win');
    });

    test('DisputeEvidenceResponseModel parses correctly', () {
      final json = {
        'disputeId': 'disp-99',
        'userId': 'user-1',
        'submittedAt': '2026-10-08T00:00:00.000Z',
        'documentFileHash': 'hash99',
        'averbationDate': '2026-09-01T00:00:00.000Z',
        'message': 'Certidão anexada',
      };

      final model = DisputeEvidenceResponseModel.fromJson(json);
      expect(model.disputeId, 'disp-99');
      expect(model.documentFileHash, 'hash99');
      expect(model.message, 'Certidão anexada');
    });
  });

  group('ClaimStatusModel', () {
    test('ClaimStatusModel parses complete status with TimeSpan correctly', () {
      final json = {
        'churchId': 'church-status-1',
        'churchName': 'Igreja Batista Central',
        'claimStatus': 1,
        'verificationTier': 1,
        'isVerified': false,
        'isCurrentUserRepresentative': false,
        'activeClaimId': 'claim-active-1',
        'activeClaimStatus': 0,
        'expiresAt': '2026-10-10T15:30:00.000Z',
        'timeRemaining': '1.12:30:00',
      };

      final model = ClaimStatusModel.fromJson(json);
      expect(model.churchName, 'Igreja Batista Central');
      expect(model.claimStatus, ChurchClaimState.pendingVerification);
      expect(model.verificationTier, VerificationTier.tier3SocialPresencial);
      expect(model.isVerified, false);
      expect(model.isCurrentUserRepresentative, false);
      expect(model.activeClaimStatus, ClaimRecordStatus.pending);
      expect(model.timeRemaining?.inDays, 1);
      expect(model.timeRemaining?.inHours, 36);
    });
  });
}
