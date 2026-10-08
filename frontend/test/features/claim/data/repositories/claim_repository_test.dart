import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/claim/data/datasources/claim_remote_data_source.dart';
import 'package:search_a_church_app/features/claim/data/failures/claim_failures.dart';
import 'package:search_a_church_app/features/claim/data/models/church_claim_model.dart';
import 'package:search_a_church_app/features/claim/data/models/claim_enums.dart';
import 'package:search_a_church_app/features/claim/data/models/claim_status_model.dart';
import 'package:search_a_church_app/features/claim/data/models/dispute_case_model.dart';
import 'package:search_a_church_app/features/claim/data/models/verification_result_model.dart';
import 'package:search_a_church_app/features/claim/data/repositories/claim_repository.dart';

class MockClaimRemoteDataSource extends Mock implements IClaimRemoteDataSource {}

void main() {
  late MockClaimRemoteDataSource mockDataSource;
  late ClaimRepository repository;

  setUp(() {
    mockDataSource = MockClaimRemoteDataSource();
    repository = ClaimRepository(remoteDataSource: mockDataSource);
  });

  group('ClaimRepository Success Flows', () {
    test('initiateClaim delegates to remoteDataSource', () async {
      const request = ClaimInitiationRequestModel(
        churchId: 'c-1',
        art299Accepted: true,
        technicalIntermediaryAccepted: true,
        validationMethod: ValidationMethod.geofence,
        targetTier: VerificationTier.tier3SocialPresencial,
      );

      const expected = ChurchClaimModel(
        claimId: 'cl-1',
        churchId: 'c-1',
        status: ClaimRecordStatus.pending,
        targetTier: VerificationTier.tier3SocialPresencial,
        ttlHours: 48,
        message: 'Iniciado',
      );

      when(() => mockDataSource.initiateClaim(request)).thenAnswer((_) async => expected);

      final result = await repository.initiateClaim(request);

      expect(result, expected);
      verify(() => mockDataSource.initiateClaim(request)).called(1);
    });

    test('verifyGeofence delegates to remoteDataSource', () async {
      const request = GeofenceVerificationRequestModel(
        claimId: 'cl-1',
        deviceLatitude: -23.5,
        deviceLongitude: -46.6,
        horizontalAccuracyMeters: 10,
        isMockLocation: false,
      );

      const expected = VerificationResultModel(
        claimId: 'cl-1',
        churchId: 'c-1',
        isVerified: true,
        tier: VerificationTier.tier3SocialPresencial,
        message: 'Confirmado',
      );

      when(() => mockDataSource.verifyGeofence(request)).thenAnswer((_) async => expected);

      final result = await repository.verifyGeofence(request);

      expect(result, expected);
      verify(() => mockDataSource.verifyGeofence(request)).called(1);
    });

    test('getClaimStatus delegates to remoteDataSource', () async {
      const expected = ClaimStatusModel(
        churchId: 'c-1',
        churchName: 'Igreja Central',
        claimStatus: ChurchClaimState.verified,
        verificationTier: VerificationTier.tier1Cartorio,
        isVerified: true,
        isCurrentUserRepresentative: true,
      );

      when(() => mockDataSource.getClaimStatus('c-1')).thenAnswer((_) async => expected);

      final result = await repository.getClaimStatus('c-1');

      expect(result, expected);
      verify(() => mockDataSource.getClaimStatus('c-1')).called(1);
    });
  });

  group('ClaimRepository Typed Failure Mapping', () {
    test('maps 401 Unauthorized to UnauthorizedFailure', () async {
      final dioError = DioException(
        requestOptions: RequestOptions(path: '/claim/status/c-1'),
        response: Response(
          requestOptions: RequestOptions(path: '/claim/status/c-1'),
          statusCode: 401,
          data: {'error': 'UNAUTHORIZED', 'message': 'Token expirado'},
        ),
      );

      when(() => mockDataSource.getClaimStatus('c-1')).thenThrow(dioError);

      expect(
        () => repository.getClaimStatus('c-1'),
        throwsA(isA<UnauthorizedFailure>()
            .having((e) => e.statusCode, 'statusCode', 401)
            .having((e) => e.message, 'message', 'Token expirado')),
      );
    });

    test('maps 404 Not Found to NotFoundFailure', () async {
      final dioError = DioException(
        requestOptions: RequestOptions(path: '/claim/status/c-1'),
        response: Response(
          requestOptions: RequestOptions(path: '/claim/status/c-1'),
          statusCode: 404,
          data: {'error': 'IGREJA_NAO_ENCONTRADA', 'message': 'Igreja não encontrada'},
        ),
      );

      when(() => mockDataSource.getClaimStatus('c-1')).thenThrow(dioError);

      expect(
        () => repository.getClaimStatus('c-1'),
        throwsA(isA<NotFoundFailure>()
            .having((e) => e.statusCode, 'statusCode', 404)
            .having((e) => e.errorCode, 'errorCode', 'IGREJA_NAO_ENCONTRADA')),
      );
    });

    test('maps 409 Conflict to ConflictFailure', () async {
      final dioError = DioException(
        requestOptions: RequestOptions(path: '/claim/initiate'),
        response: Response(
          requestOptions: RequestOptions(path: '/claim/initiate'),
          statusCode: 409,
          data: {'error': 'IGREJA_JA_REIVINDICADA', 'message': 'Já existe titular'},
        ),
      );

      const request = ClaimInitiationRequestModel(
        churchId: 'c-1',
        art299Accepted: true,
        technicalIntermediaryAccepted: true,
        validationMethod: ValidationMethod.geofence,
        targetTier: VerificationTier.tier3SocialPresencial,
      );

      when(() => mockDataSource.initiateClaim(request)).thenThrow(dioError);

      expect(
        () => repository.initiateClaim(request),
        throwsA(isA<ConflictFailure>()
            .having((e) => e.statusCode, 'statusCode', 409)
            .having((e) => e.errorCode, 'errorCode', 'IGREJA_JA_REIVINDICADA')),
      );
    });

    test('maps LOCALIZACAO_SIMULADA_DETECTADA to MockLocationFailure', () async {
      final dioError = DioException(
        requestOptions: RequestOptions(path: '/claim/verify/geofence'),
        response: Response(
          requestOptions: RequestOptions(path: '/claim/verify/geofence'),
          statusCode: 400,
          data: {
            'error': 'LOCALIZACAO_SIMULADA_DETECTADA',
            'message': 'Mock location recusado',
          },
        ),
      );

      const request = GeofenceVerificationRequestModel(
        claimId: 'cl-1',
        deviceLatitude: -23.5,
        deviceLongitude: -46.6,
        horizontalAccuracyMeters: 5,
        isMockLocation: true,
      );

      when(() => mockDataSource.verifyGeofence(request)).thenThrow(dioError);

      expect(
        () => repository.verifyGeofence(request),
        throwsA(isA<MockLocationFailure>()
            .having((e) => e.errorCode, 'errorCode', 'LOCALIZACAO_SIMULADA_DETECTADA')),
      );
    });

    test('maps FORA_DO_RAIO_PERMITIDO to GeofenceFailure', () async {
      final dioError = DioException(
        requestOptions: RequestOptions(path: '/claim/verify/geofence'),
        response: Response(
          requestOptions: RequestOptions(path: '/claim/verify/geofence'),
          statusCode: 400,
          data: {
            'error': 'FORA_DO_RAIO_PERMITIDO',
            'message': 'Distância superior a 100 metros',
          },
        ),
      );

      const request = GeofenceVerificationRequestModel(
        claimId: 'cl-1',
        deviceLatitude: -22.0,
        deviceLongitude: -43.0,
        horizontalAccuracyMeters: 5,
        isMockLocation: false,
      );

      when(() => mockDataSource.verifyGeofence(request)).thenThrow(dioError);

      expect(
        () => repository.verifyGeofence(request),
        throwsA(isA<GeofenceFailure>()
            .having((e) => e.errorCode, 'errorCode', 'FORA_DO_RAIO_PERMITIDO')),
      );
    });

    test('maps dispute error to DisputeFailure', () async {
      final dioError = DioException(
        requestOptions: RequestOptions(path: '/claim/dispute/contest'),
        response: Response(
          requestOptions: RequestOptions(path: '/claim/dispute/contest'),
          statusCode: 400,
          data: {
            'error': 'DISPUTA_INVALIDA',
            'message': 'Prazo expirado',
          },
        ),
      );

      final request = DisputeContestRequestModel(
        churchId: 'c-1',
        tosAccepted: true,
        legalRepresentativeName: 'Nome',
        legalRepresentativeCpf: '111',
        churchCnpj: '222',
        submittedTier: VerificationTier.tier1Cartorio,
        documentFileHash: 'hash',
        documentAverbationDate: DateTime.now(),
      );

      when(() => mockDataSource.contestDispute(request)).thenThrow(dioError);

      expect(
        () => repository.contestDispute(request),
        throwsA(isA<DisputeFailure>()
            .having((e) => e.errorCode, 'errorCode', 'DISPUTA_INVALIDA')),
      );
    });
  });
}
