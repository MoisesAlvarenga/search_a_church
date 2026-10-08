import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/claim/data/failures/claim_failures.dart';
import 'package:search_a_church_app/features/claim/data/models/claim_enums.dart';
import 'package:search_a_church_app/features/claim/data/models/dispute_case_model.dart';
import 'package:search_a_church_app/features/claim/data/repositories/claim_repository.dart';
import 'package:search_a_church_app/features/claim/presentation/cubit/dispute_cubit.dart';
import 'package:search_a_church_app/features/claim/presentation/cubit/dispute_state.dart';

class MockClaimRepository extends Mock implements IClaimRepository {}

class FakeDisputeContestRequestModel extends Fake implements DisputeContestRequestModel {}
class FakeDisputeEvidenceRequestModel extends Fake implements DisputeEvidenceRequestModel {}

void main() {
  late MockClaimRepository mockRepository;

  setUpAll(() {
    registerFallbackValue(FakeDisputeContestRequestModel());
    registerFallbackValue(FakeDisputeEvidenceRequestModel());
  });

  setUp(() {
    mockRepository = MockClaimRepository();
  });

  group('DisputeCubit', () {
    test('initial state is DisputeInitial', () {
      final cubit = DisputeCubit(repository: mockRepository);
      expect(cubit.state, isA<DisputeInitial>());
    });

    blocTest<DisputeCubit, DisputeState>(
      'contestDispute with parityDisputeOpened emits [DisputeLoading, DisputeParityOpened]',
      build: () {
        when(() => mockRepository.contestDispute(any())).thenAnswer(
          (_) async => DisputeCaseModel(
            success: true,
            resolutionType: DisputeResolutionType.parityDisputeOpened,
            churchId: 'church-1',
            disputeId: 'disp-100',
            deadlineAt: DateTime.now().add(const Duration(days: 5)),
            message: 'Litígio paritário aberto por 5 dias úteis',
            currentTier: VerificationTier.tier1Cartorio,
          ),
        );
        return DisputeCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.contestDispute(
        churchId: 'church-1',
        tosAccepted: true,
        legalRepresentativeName: 'Pastor Contestante',
        legalRepresentativeCpf: '111.444.777-35',
        churchCnpj: '04.252.011/0001-10',
        submittedTier: VerificationTier.tier1Cartorio,
        documentFileHash: 'hash-rcpj',
        documentAverbationDate: DateTime.now(),
      ),
      expect: () => [
        isA<DisputeLoading>(),
        isA<DisputeParityOpened>()
            .having((s) => s.dispute.disputeId, 'disputeId', 'disp-100')
            .having((s) => s.dispute.resolutionType, 'resolutionType', DisputeResolutionType.parityDisputeOpened),
      ],
    );

    blocTest<DisputeCubit, DisputeState>(
      'contestDispute with automaticOverrideN1 emits [DisputeLoading, DisputeResolved]',
      build: () {
        when(() => mockRepository.contestDispute(any())).thenAnswer(
          (_) async => const DisputeCaseModel(
            success: true,
            resolutionType: DisputeResolutionType.automaticOverrideN1,
            churchId: 'church-1',
            message: 'Sobreposição automática de Nível 1 confirmada',
            currentTier: VerificationTier.tier1Cartorio,
            activeRepresentativeUserId: 'pastor-titular-id',
          ),
        );
        return DisputeCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.contestDispute(
        churchId: 'church-1',
        tosAccepted: true,
        legalRepresentativeName: 'Pastor Titular',
        legalRepresentativeCpf: '111.444.777-35',
        churchCnpj: '04.252.011/0001-10',
        submittedTier: VerificationTier.tier1Cartorio,
        documentFileHash: 'hash-rcpj',
        documentAverbationDate: DateTime.now(),
      ),
      expect: () => [
        isA<DisputeLoading>(),
        isA<DisputeResolved>()
            .having((s) => s.dispute.activeRepresentativeUserId, 'activeRepresentativeUserId', 'pastor-titular-id'),
      ],
    );

    blocTest<DisputeCubit, DisputeState>(
      'contestDispute emits [DisputeLoading, DisputeError] on failure',
      build: () {
        when(() => mockRepository.contestDispute(any())).thenThrow(
          const ConflictFailure('Litígio paritário já em andamento', errorCode: 'DISPUTA_EM_ANDAMENTO'),
        );
        return DisputeCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.contestDispute(
        churchId: 'church-1',
        tosAccepted: true,
        legalRepresentativeName: 'Pastor Contestante',
        legalRepresentativeCpf: '111.444.777-35',
        churchCnpj: '04.252.011/0001-10',
        submittedTier: VerificationTier.tier1Cartorio,
        documentFileHash: 'hash-rcpj',
        documentAverbationDate: DateTime.now(),
      ),
      expect: () => [
        isA<DisputeLoading>(),
        isA<DisputeError>()
            .having((e) => e.errorCode, 'errorCode', 'DISPUTA_EM_ANDAMENTO')
            .having((e) => e.message, 'message', 'Litígio paritário já em andamento'),
      ],
    );

    blocTest<DisputeCubit, DisputeState>(
      'submitCertificate emits [DisputeLoading, DisputeCertificateSubmitted] on success',
      build: () {
        when(() => mockRepository.submitDisputeCertificate(any())).thenAnswer(
          (_) async => DisputeEvidenceResponseModel(
            disputeId: 'disp-100',
            userId: 'user-1',
            submittedAt: DateTime.now(),
            documentFileHash: 'cert-hash',
            averbationDate: DateTime.now(),
            message: 'Certidão averbada protocolada',
          ),
        );
        return DisputeCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.submitCertificate(
        disputeId: 'disp-100',
        documentFileHash: 'cert-hash',
        averbationDate: DateTime.now(),
        notes: 'Certidão atualizada do RCPJ',
      ),
      expect: () => [
        isA<DisputeLoading>(),
        isA<DisputeCertificateSubmitted>()
            .having((s) => s.response.disputeId, 'disputeId', 'disp-100'),
      ],
    );

    blocTest<DisputeCubit, DisputeState>(
      'submitCertificate emits [DisputeLoading, DisputeError] on failure',
      build: () {
        when(() => mockRepository.submitDisputeCertificate(any())).thenThrow(
          const NotFoundFailure('Disputa não encontrada', errorCode: 'DISPUTA_NAO_ENCONTRADA'),
        );
        return DisputeCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.submitCertificate(
        disputeId: 'disp-invalid',
        documentFileHash: 'cert-hash',
        averbationDate: DateTime.now(),
      ),
      expect: () => [
        isA<DisputeLoading>(),
        isA<DisputeError>()
            .having((e) => e.errorCode, 'errorCode', 'DISPUTA_NAO_ENCONTRADA'),
      ],
    );

    test('reset emits DisputeInitial', () {
      final cubit = DisputeCubit(repository: mockRepository);
      cubit.reset();
      expect(cubit.state, isA<DisputeInitial>());
    });
  });
}
