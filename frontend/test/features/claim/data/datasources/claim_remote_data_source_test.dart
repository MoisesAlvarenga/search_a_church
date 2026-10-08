import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/claim/data/datasources/claim_remote_data_source.dart';
import 'package:search_a_church_app/features/claim/data/models/church_claim_model.dart';
import 'package:search_a_church_app/features/claim/data/models/claim_enums.dart';
import 'package:search_a_church_app/features/claim/data/models/dispute_case_model.dart';
import 'package:search_a_church_app/features/claim/data/models/verification_result_model.dart';

class MockDio extends Mock implements Dio {}

void main() {
  late MockDio mockDio;
  late ClaimRemoteDataSource dataSource;

  setUp(() {
    mockDio = MockDio();
    dataSource = ClaimRemoteDataSource(dio: mockDio);
  });

  group('ClaimRemoteDataSource Endpoints', () {
    test('initiateClaim calls POST /claim/initiate and returns ChurchClaimModel', () async {
      const request = ClaimInitiationRequestModel(
        churchId: 'church-1',
        tosVersion: '1.0',
        art299Accepted: true,
        technicalIntermediaryAccepted: true,
        validationMethod: ValidationMethod.geofence,
        targetTier: VerificationTier.tier3SocialPresencial,
      );

      final mockResponseData = {
        'claimId': 'claim-1',
        'churchId': 'church-1',
        'status': 0,
        'targetTier': 1,
        'expiresAt': '2026-10-15T00:00:00.000Z',
        'ttlHours': 48,
        'message': 'Sucesso',
      };

      when(() => mockDio.post(
            '/claim/initiate',
            data: any(named: 'data'),
          )).thenAnswer((_) async => Response(
            data: mockResponseData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/claim/initiate'),
          ));

      final result = await dataSource.initiateClaim(request);

      expect(result.claimId, 'claim-1');
      expect(result.status, ClaimRecordStatus.pending);
      verify(() => mockDio.post('/claim/initiate', data: request.toJson())).called(1);
    });

    test('verifyGeofence calls POST /claim/verify/geofence and returns VerificationResultModel', () async {
      const request = GeofenceVerificationRequestModel(
        claimId: 'claim-1',
        deviceLatitude: -23.55,
        deviceLongitude: -46.63,
        horizontalAccuracyMeters: 5.0,
        isMockLocation: false,
      );

      final mockResponseData = {
        'claimId': 'claim-1',
        'churchId': 'church-1',
        'isVerified': true,
        'tier': 1,
        'message': 'Presença física confirmada',
      };

      when(() => mockDio.post(
            '/claim/verify/geofence',
            data: any(named: 'data'),
          )).thenAnswer((_) async => Response(
            data: mockResponseData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/claim/verify/geofence'),
          ));

      final result = await dataSource.verifyGeofence(request);

      expect(result.isVerified, true);
      expect(result.tier, VerificationTier.tier3SocialPresencial);
      verify(() => mockDio.post('/claim/verify/geofence', data: request.toJson())).called(1);
    });

    test('generateSocialBioToken calls POST /claim/verify/social-bio/generate', () async {
      const request = SocialTokenGenerateRequestModel(
        claimId: 'claim-1',
        socialNetwork: 'Instagram',
        profileHandle: 'igrejasp',
      );

      final mockResponseData = {
        'claimId': 'claim-1',
        'token': 'SAC-ABCD-VERIFY',
        'expiresAt': '2026-10-10T00:00:00.000Z',
        'instructions': 'Insira na bio',
      };

      when(() => mockDio.post(
            '/claim/verify/social-bio/generate',
            data: any(named: 'data'),
          )).thenAnswer((_) async => Response(
            data: mockResponseData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/claim/verify/social-bio/generate'),
          ));

      final result = await dataSource.generateSocialBioToken(request);

      expect(result.token, 'SAC-ABCD-VERIFY');
      verify(() => mockDio.post('/claim/verify/social-bio/generate', data: request.toJson())).called(1);
    });

    test('confirmSocialBio calls POST /claim/verify/social-bio/confirm', () async {
      const request = SocialBioConfirmRequestModel(
        claimId: 'claim-1',
        socialNetwork: 'Instagram',
        profileHandle: 'igrejasp',
        expectedToken: 'SAC-ABCD-VERIFY',
      );

      final mockResponseData = {
        'claimId': 'claim-1',
        'churchId': 'church-1',
        'isVerified': true,
        'tier': 1,
        'message': 'Bio confirmada',
      };

      when(() => mockDio.post(
            '/claim/verify/social-bio/confirm',
            data: any(named: 'data'),
          )).thenAnswer((_) async => Response(
            data: mockResponseData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/claim/verify/social-bio/confirm'),
          ));

      final result = await dataSource.confirmSocialBio(request);

      expect(result.isVerified, true);
      verify(() => mockDio.post('/claim/verify/social-bio/confirm', data: request.toJson())).called(1);
    });

    test('sendDomainOtp calls POST /claim/verify/domain/send-otp', () async {
      const request = DomainOtpSendRequestModel(
        claimId: 'claim-1',
        corporateEmail: 'contato@igreja.org.br',
        expectedDomain: 'igreja.org.br',
      );

      final mockResponseData = {
        'claimId': 'claim-1',
        'email': 'contato@igreja.org.br',
        'expiresAt': '2026-10-08T00:15:00.000Z',
        'message': 'OTP enviado',
      };

      when(() => mockDio.post(
            '/claim/verify/domain/send-otp',
            data: any(named: 'data'),
          )).thenAnswer((_) async => Response(
            data: mockResponseData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/claim/verify/domain/send-otp'),
          ));

      final result = await dataSource.sendDomainOtp(request);

      expect(result.email, 'contato@igreja.org.br');
      verify(() => mockDio.post('/claim/verify/domain/send-otp', data: request.toJson())).called(1);
    });

    test('confirmDomainOtp calls POST /claim/verify/domain/confirm-otp', () async {
      const request = DomainOtpConfirmRequestModel(
        claimId: 'claim-1',
        corporateEmail: 'contato@igreja.org.br',
        otpCode: '123456',
      );

      final mockResponseData = {
        'claimId': 'claim-1',
        'churchId': 'church-1',
        'isVerified': true,
        'tier': 2,
        'message': 'E-mail validado',
      };

      when(() => mockDio.post(
            '/claim/verify/domain/confirm-otp',
            data: any(named: 'data'),
          )).thenAnswer((_) async => Response(
            data: mockResponseData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/claim/verify/domain/confirm-otp'),
          ));

      final result = await dataSource.confirmDomainOtp(request);

      expect(result.isVerified, true);
      expect(result.tier, VerificationTier.tier2Institucional);
      verify(() => mockDio.post('/claim/verify/domain/confirm-otp', data: request.toJson())).called(1);
    });

    test('submitCartorioDocument calls POST /claim/verify/document/rcpj', () async {
      final request = CartorioDocumentRequestModel(
        claimId: 'claim-1',
        documentFileName: 'estatuto.pdf',
        documentFileHashSha256: 'hash-rcpj',
        averbationDate: DateTime.parse('2026-01-01T00:00:00.000Z'),
      );

      final mockResponseData = {
        'claimId': 'claim-1',
        'churchId': 'church-1',
        'isVerified': true,
        'tier': 3,
        'message': 'Ata aprovada',
      };

      when(() => mockDio.post(
            '/claim/verify/document/rcpj',
            data: any(named: 'data'),
          )).thenAnswer((_) async => Response(
            data: mockResponseData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/claim/verify/document/rcpj'),
          ));

      final result = await dataSource.submitCartorioDocument(request);

      expect(result.isVerified, true);
      expect(result.tier, VerificationTier.tier1Cartorio);
      verify(() => mockDio.post('/claim/verify/document/rcpj', data: request.toJson())).called(1);
    });

    test('verifyQsa calls POST /claim/verify/document/qsa', () async {
      const request = QsaVerificationRequestModel(
        claimId: 'claim-1',
        churchCnpj: '04.252.011/0001-10',
        representativeCpf: '111.444.777-35',
        representativeName: 'Representante',
      );

      final mockResponseData = {
        'claimId': 'claim-1',
        'churchId': 'church-1',
        'isVerified': true,
        'tier': 2,
        'message': 'QSA aprovado',
      };

      when(() => mockDio.post(
            '/claim/verify/document/qsa',
            data: any(named: 'data'),
          )).thenAnswer((_) async => Response(
            data: mockResponseData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/claim/verify/document/qsa'),
          ));

      final result = await dataSource.verifyQsa(request);

      expect(result.isVerified, true);
      verify(() => mockDio.post('/claim/verify/document/qsa', data: request.toJson())).called(1);
    });

    test('contestDispute calls POST /claim/dispute/contest', () async {
      final request = DisputeContestRequestModel(
        churchId: 'church-1',
        tosAccepted: true,
        legalRepresentativeName: 'Novo Pastor',
        legalRepresentativeCpf: '111.444.777-35',
        churchCnpj: '04.252.011/0001-10',
        submittedTier: VerificationTier.tier1Cartorio,
        documentFileHash: 'hash-disp',
        documentAverbationDate: DateTime.parse('2026-02-01T00:00:00.000Z'),
      );

      final mockResponseData = {
        'success': true,
        'resolutionType': 1,
        'churchId': 'church-1',
        'message': 'Sobreposição realizada',
        'currentTier': 3,
      };

      when(() => mockDio.post(
            '/claim/dispute/contest',
            data: any(named: 'data'),
          )).thenAnswer((_) async => Response(
            data: mockResponseData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/claim/dispute/contest'),
          ));

      final result = await dataSource.contestDispute(request);

      expect(result.success, true);
      expect(result.resolutionType, DisputeResolutionType.automaticOverrideN1);
      verify(() => mockDio.post('/claim/dispute/contest', data: request.toJson())).called(1);
    });

    test('submitDisputeCertificate calls POST /claim/dispute/{id}/submit-certificate', () async {
      final request = DisputeEvidenceRequestModel(
        disputeId: 'disp-456',
        documentFileHash: 'cert-hash',
        averbationDate: DateTime.parse('2026-03-01T00:00:00.000Z'),
      );

      final mockResponseData = {
        'disputeId': 'disp-456',
        'userId': 'user-1',
        'submittedAt': '2026-03-02T00:00:00.000Z',
        'documentFileHash': 'cert-hash',
        'averbationDate': '2026-03-01T00:00:00.000Z',
        'message': 'Certidão anexada',
      };

      when(() => mockDio.post(
            '/claim/dispute/disp-456/submit-certificate',
            data: any(named: 'data'),
          )).thenAnswer((_) async => Response(
            data: mockResponseData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/claim/dispute/disp-456/submit-certificate'),
          ));

      final result = await dataSource.submitDisputeCertificate(request);

      expect(result.disputeId, 'disp-456');
      verify(() => mockDio.post('/claim/dispute/disp-456/submit-certificate', data: request.toJson())).called(1);
    });

    test('getClaimStatus calls GET /claim/status/{churchId}', () async {
      final mockResponseData = {
        'churchId': 'church-1',
        'churchName': 'Igreja Central',
        'claimStatus': 2,
        'verificationTier': 3,
        'isVerified': true,
        'isCurrentUserRepresentative': true,
      };

      when(() => mockDio.get('/claim/status/church-1')).thenAnswer((_) async => Response(
            data: mockResponseData,
            statusCode: 200,
            requestOptions: RequestOptions(path: '/claim/status/church-1'),
          ));

      final result = await dataSource.getClaimStatus('church-1');

      expect(result.churchId, 'church-1');
      expect(result.isVerified, true);
      expect(result.isCurrentUserRepresentative, true);
      verify(() => mockDio.get('/claim/status/church-1')).called(1);
    });
  });
}
