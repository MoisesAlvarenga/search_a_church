import 'package:flutter_bloc/flutter_bloc.dart';
import '../../data/failures/claim_failures.dart';
import '../../data/models/claim_enums.dart';
import '../../data/models/dispute_case_model.dart';
import '../../data/repositories/claim_repository.dart';
import 'dispute_state.dart';

/// Cubit responsável pelo gerenciamento de contestações, paridade de 5 dias úteis e litígios.
class DisputeCubit extends Cubit<DisputeState> {
  final IClaimRepository _repository;

  DisputeCubit({required this._repository})
      : super(const DisputeInitial());

  /// Abre contestação de propriedade contra congregação já homologada.
  Future<void> contestDispute({
    required String churchId,
    required bool tosAccepted,
    required String legalRepresentativeName,
    required String legalRepresentativeCpf,
    required String churchCnpj,
    required VerificationTier submittedTier,
    required String documentFileHash,
    required DateTime documentAverbationDate,
    String? justification,
    String tosVersion = '1.0',
  }) async {
    emit(const DisputeLoading(actionDescription: 'Processando abertura de contestação...'));
    try {
      final request = DisputeContestRequestModel(
        churchId: churchId,
        tosVersion: tosVersion,
        tosAccepted: tosAccepted,
        legalRepresentativeName: legalRepresentativeName,
        legalRepresentativeCpf: legalRepresentativeCpf,
        churchCnpj: churchCnpj,
        submittedTier: submittedTier,
        documentFileHash: documentFileHash,
        documentAverbationDate: documentAverbationDate,
        justification: justification,
      );

      final result = await _repository.contestDispute(request);

      if (result.resolutionType == DisputeResolutionType.parityDisputeOpened) {
        emit(DisputeParityOpened(result));
      } else {
        emit(DisputeResolved(result));
      }
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Submete certidão cartorial adicional tempestiva durante o prazo operacional de 5 dias úteis.
  Future<void> submitCertificate({
    required String disputeId,
    required String documentFileHash,
    required DateTime averbationDate,
    String? notes,
  }) async {
    emit(const DisputeLoading(actionDescription: 'Submetendo certidão comprobatória...'));
    try {
      final request = DisputeEvidenceRequestModel(
        disputeId: disputeId,
        documentFileHash: documentFileHash,
        averbationDate: averbationDate,
        notes: notes,
      );

      final response = await _repository.submitDisputeCertificate(request);
      emit(DisputeCertificateSubmitted(response));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  /// Reinicia o estado da disputa.
  void reset() {
    emit(const DisputeInitial());
  }

  DisputeError _mapError(dynamic error) {
    if (error is ClaimFailure) {
      return DisputeError(
        error.message,
        errorCode: error.errorCode,
        statusCode: error.statusCode,
      );
    }
    return DisputeError('Ocorreu um erro inesperado: $error');
  }
}
