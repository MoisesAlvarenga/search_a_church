import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/claim/data/failures/claim_failures.dart';
import 'package:search_a_church_app/features/claim/data/models/church_claim_model.dart';
import 'package:search_a_church_app/features/claim/data/models/claim_enums.dart';
import 'package:search_a_church_app/features/claim/data/models/claim_status_model.dart';
import 'package:search_a_church_app/features/claim/data/models/verification_result_model.dart';
import 'package:search_a_church_app/features/claim/data/repositories/claim_repository.dart';
import 'package:search_a_church_app/features/claim/presentation/cubit/claim_cubit.dart';
import 'package:search_a_church_app/features/claim/presentation/cubit/claim_state.dart';

class MockClaimRepository extends Mock implements IClaimRepository {}

class FakeClaimInitiationRequestModel extends Fake implements ClaimInitiationRequestModel {}
class FakeGeofenceVerificationRequestModel extends Fake implements GeofenceVerificationRequestModel {}
class FakeSocialTokenGenerateRequestModel extends Fake implements SocialTokenGenerateRequestModel {}
class FakeSocialBioConfirmRequestModel extends Fake implements SocialBioConfirmRequestModel {}
class FakeDomainOtpSendRequestModel extends Fake implements DomainOtpSendRequestModel {}
class FakeDomainOtpConfirmRequestModel extends Fake implements DomainOtpConfirmRequestModel {}
class FakeCartorioDocumentRequestModel extends Fake implements CartorioDocumentRequestModel {}
class FakeQsaVerificationRequestModel extends Fake implements QsaVerificationRequestModel {}

void main() {
  late MockClaimRepository mockRepository;

  setUpAll(() {
    registerFallbackValue(FakeClaimInitiationRequestModel());
    registerFallbackValue(FakeGeofenceVerificationRequestModel());
    registerFallbackValue(FakeSocialTokenGenerateRequestModel());
    registerFallbackValue(FakeSocialBioConfirmRequestModel());
    registerFallbackValue(FakeDomainOtpSendRequestModel());
    registerFallbackValue(FakeDomainOtpConfirmRequestModel());
    registerFallbackValue(FakeCartorioDocumentRequestModel());
    registerFallbackValue(FakeQsaVerificationRequestModel());
  });

  setUp(() {
    mockRepository = MockClaimRepository();
  });

  group('ClaimCubit - Status & Initiation', () {
    test('initial state is ClaimInitial', () {
      final cubit = ClaimCubit(repository: mockRepository);
      expect(cubit.state, isA<ClaimInitial>());
    });

    blocTest<ClaimCubit, ClaimState>(
      'loadStatus emits [ClaimLoading, ClaimStatusLoaded] on success',
      build: () {
        when(() => mockRepository.getClaimStatus('church-1')).thenAnswer(
          (_) async => const ClaimStatusModel(
            churchId: 'church-1',
            churchName: 'Igreja Central',
            claimStatus: ChurchClaimState.unclaimed,
            verificationTier: VerificationTier.none,
            isVerified: false,
            isCurrentUserRepresentative: false,
          ),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.loadStatus('church-1'),
      expect: () => [
        isA<ClaimLoading>(),
        isA<ClaimStatusLoaded>()
            .having((s) => s.status.churchName, 'churchName', 'Igreja Central')
            .having((s) => s.status.claimStatus, 'claimStatus', ChurchClaimState.unclaimed),
      ],
    );

    blocTest<ClaimCubit, ClaimState>(
      'loadStatus emits [ClaimLoading, ClaimError] on failure',
      build: () {
        when(() => mockRepository.getClaimStatus('church-1')).thenThrow(
          const NotFoundFailure('Igreja não encontrada', errorCode: 'IGREJA_NAO_ENCONTRADA'),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.loadStatus('church-1'),
      expect: () => [
        isA<ClaimLoading>(),
        isA<ClaimError>()
            .having((e) => e.message, 'message', 'Igreja não encontrada')
            .having((e) => e.errorCode, 'errorCode', 'IGREJA_NAO_ENCONTRADA'),
      ],
    );

    blocTest<ClaimCubit, ClaimState>(
      'initiateClaim emits [ClaimSubmitting, ClaimInitiated] on success',
      build: () {
        when(() => mockRepository.initiateClaim(any())).thenAnswer(
          (_) async => ChurchClaimModel(
            claimId: 'claim-1',
            churchId: 'church-1',
            status: ClaimRecordStatus.pending,
            targetTier: VerificationTier.tier3SocialPresencial,
            expiresAt: DateTime.now().add(const Duration(hours: 48)),
            ttlHours: 48,
            message: 'Iniciado com sucesso',
          ),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.initiateClaim(
        churchId: 'church-1',
        art299Accepted: true,
        technicalIntermediaryAccepted: true,
        validationMethod: ValidationMethod.geofence,
        targetTier: VerificationTier.tier3SocialPresencial,
      ),
      expect: () => [
        isA<ClaimSubmitting>(),
        isA<ClaimInitiated>().having((s) => s.claim.claimId, 'claimId', 'claim-1'),
      ],
    );

    blocTest<ClaimCubit, ClaimState>(
      'initiateClaim emits [ClaimSubmitting, ClaimError] with isConflict true when church already claimed',
      build: () {
        when(() => mockRepository.initiateClaim(any())).thenThrow(
          const ConflictFailure('Igreja já possui titular', errorCode: 'IGREJA_JA_REIVINDICADA'),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.initiateClaim(
        churchId: 'church-1',
        art299Accepted: true,
        technicalIntermediaryAccepted: true,
        validationMethod: ValidationMethod.geofence,
        targetTier: VerificationTier.tier3SocialPresencial,
      ),
      expect: () => [
        isA<ClaimSubmitting>(),
        isA<ClaimError>()
            .having((e) => e.isConflict, 'isConflict', true)
            .having((e) => e.errorCode, 'errorCode', 'IGREJA_JA_REIVINDICADA'),
      ],
    );
  });

  group('ClaimCubit - Evidence Verifications', () {
    blocTest<ClaimCubit, ClaimState>(
      'verifyGeofence emits [ClaimVerifying, ClaimVerifiedSuccess] on success',
      build: () {
        when(() => mockRepository.verifyGeofence(any())).thenAnswer(
          (_) async => const VerificationResultModel(
            claimId: 'claim-1',
            churchId: 'church-1',
            isVerified: true,
            tier: VerificationTier.tier3SocialPresencial,
            message: 'Presença confirmada',
          ),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.verifyGeofence(
        claimId: 'claim-1',
        deviceLatitude: -23.55,
        deviceLongitude: -46.63,
        horizontalAccuracyMeters: 5.0,
        isMockLocation: false,
      ),
      expect: () => [
        isA<ClaimVerifying>(),
        isA<ClaimVerifiedSuccess>().having((s) => s.result.isVerified, 'isVerified', true),
      ],
    );

    blocTest<ClaimCubit, ClaimState>(
      'verifyGeofence emits [ClaimVerifying, ClaimError] with isMockLocation true on mock detection',
      build: () {
        when(() => mockRepository.verifyGeofence(any())).thenThrow(
          const MockLocationFailure('Localização simulada recusada'),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.verifyGeofence(
        claimId: 'claim-1',
        deviceLatitude: -23.55,
        deviceLongitude: -46.63,
        horizontalAccuracyMeters: 5.0,
        isMockLocation: true,
      ),
      expect: () => [
        isA<ClaimVerifying>(),
        isA<ClaimError>().having((e) => e.isMockLocation, 'isMockLocation', true),
      ],
    );

    blocTest<ClaimCubit, ClaimState>(
      'verifyGeofence emits [ClaimVerifying, ClaimError] with isOutOfRange true when distance > 100m',
      build: () {
        when(() => mockRepository.verifyGeofence(any())).thenThrow(
          const GeofenceFailure('Fora do raio permitido', errorCode: 'FORA_DO_RAIO_PERMITIDO'),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.verifyGeofence(
        claimId: 'claim-1',
        deviceLatitude: -22.0,
        deviceLongitude: -43.0,
        horizontalAccuracyMeters: 5.0,
        isMockLocation: false,
      ),
      expect: () => [
        isA<ClaimVerifying>(),
        isA<ClaimError>().having((e) => e.isOutOfRange, 'isOutOfRange', true),
      ],
    );

    blocTest<ClaimCubit, ClaimState>(
      'generateSocialBioToken emits [ClaimVerifying, SocialBioTokenGenerated] on success',
      build: () {
        when(() => mockRepository.generateSocialBioToken(any())).thenAnswer(
          (_) async => const SocialBioTokenModel(
            claimId: 'claim-1',
            token: 'SAC-1234-VERIFY',
            instructions: 'Coloque na bio',
          ),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.generateSocialBioToken(
        claimId: 'claim-1',
        socialNetwork: 'Instagram',
        profileHandle: 'igreja_oficial',
      ),
      expect: () => [
        isA<ClaimVerifying>(),
        isA<SocialBioTokenGenerated>()
            .having((s) => s.tokenModel.token, 'token', 'SAC-1234-VERIFY'),
      ],
    );

    blocTest<ClaimCubit, ClaimState>(
      'confirmSocialBio emits [ClaimVerifying, ClaimVerifiedSuccess] on success',
      build: () {
        when(() => mockRepository.confirmSocialBio(any())).thenAnswer(
          (_) async => const VerificationResultModel(
            claimId: 'claim-1',
            churchId: 'church-1',
            isVerified: true,
            tier: VerificationTier.tier3SocialPresencial,
            message: 'Bio verificada',
          ),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.confirmSocialBio(
        claimId: 'claim-1',
        socialNetwork: 'Instagram',
        profileHandle: 'igreja_oficial',
        expectedToken: 'SAC-1234-VERIFY',
      ),
      expect: () => [
        isA<ClaimVerifying>(),
        isA<ClaimVerifiedSuccess>().having((s) => s.result.isVerified, 'isVerified', true),
      ],
    );

    blocTest<ClaimCubit, ClaimState>(
      'sendDomainOtp emits [ClaimVerifying, DomainOtpSent] on success',
      build: () {
        when(() => mockRepository.sendDomainOtp(any())).thenAnswer(
          (_) async => const DomainOtpSendResponseModel(
            claimId: 'claim-1',
            email: 'contato@igreja.org.br',
            message: 'OTP enviado',
          ),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.sendDomainOtp(
        claimId: 'claim-1',
        corporateEmail: 'contato@igreja.org.br',
        expectedDomain: 'igreja.org.br',
      ),
      expect: () => [
        isA<ClaimVerifying>(),
        isA<DomainOtpSent>().having((s) => s.otpResponse.email, 'email', 'contato@igreja.org.br'),
      ],
    );

    blocTest<ClaimCubit, ClaimState>(
      'confirmDomainOtp emits [ClaimVerifying, ClaimVerifiedSuccess] on success',
      build: () {
        when(() => mockRepository.confirmDomainOtp(any())).thenAnswer(
          (_) async => const VerificationResultModel(
            claimId: 'claim-1',
            churchId: 'church-1',
            isVerified: true,
            tier: VerificationTier.tier2Institucional,
            message: 'E-mail verificado',
          ),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.confirmDomainOtp(
        claimId: 'claim-1',
        corporateEmail: 'contato@igreja.org.br',
        otpCode: '123456',
      ),
      expect: () => [
        isA<ClaimVerifying>(),
        isA<ClaimVerifiedSuccess>().having((s) => s.result.isVerified, 'isVerified', true),
      ],
    );

    blocTest<ClaimCubit, ClaimState>(
      'submitCartorioDocument emits [ClaimVerifying, ClaimVerifiedSuccess] on success',
      build: () {
        when(() => mockRepository.submitCartorioDocument(any())).thenAnswer(
          (_) async => const VerificationResultModel(
            claimId: 'claim-1',
            churchId: 'church-1',
            isVerified: true,
            tier: VerificationTier.tier1Cartorio,
            message: 'Documento homologado',
          ),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.submitCartorioDocument(
        claimId: 'claim-1',
        documentFileName: 'estatuto.pdf',
        documentFileHashSha256: 'hash-rcpj',
        averbationDate: DateTime.now(),
      ),
      expect: () => [
        isA<ClaimVerifying>(),
        isA<ClaimVerifiedSuccess>()
            .having((s) => s.result.tier, 'tier', VerificationTier.tier1Cartorio),
      ],
    );

    blocTest<ClaimCubit, ClaimState>(
      'verifyQsa emits [ClaimVerifying, ClaimVerifiedSuccess] on success',
      build: () {
        when(() => mockRepository.verifyQsa(any())).thenAnswer(
          (_) async => const VerificationResultModel(
            claimId: 'claim-1',
            churchId: 'church-1',
            isVerified: true,
            tier: VerificationTier.tier2Institucional,
            message: 'QSA aprovado',
          ),
        );
        return ClaimCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.verifyQsa(
        claimId: 'claim-1',
        churchCnpj: '04.252.011/0001-10',
        representativeCpf: '111.444.777-35',
        representativeName: 'Pastor Representante',
      ),
      expect: () => [
        isA<ClaimVerifying>(),
        isA<ClaimVerifiedSuccess>().having((s) => s.result.isVerified, 'isVerified', true),
      ],
    );

    test('reset emits ClaimInitial', () {
      final cubit = ClaimCubit(repository: mockRepository);
      cubit.reset();
      expect(cubit.state, isA<ClaimInitial>());
    });
  });
}
