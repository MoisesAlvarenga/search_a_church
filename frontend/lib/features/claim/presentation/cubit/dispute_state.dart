import 'package:equatable/equatable.dart';
import '../../data/models/dispute_case_model.dart';

/// Estado base do DisputeCubit.
abstract class DisputeState extends Equatable {
  const DisputeState();

  @override
  List<Object?> get props => [];
}

/// Estado inicial sem contestações em andamento.
class DisputeInitial extends DisputeState {
  const DisputeInitial();
}

/// Estado de carregamento de processamento de disputa.
class DisputeLoading extends DisputeState {
  final String actionDescription;

  const DisputeLoading({this.actionDescription = 'Processando contestação...'});

  @override
  List<Object?> get props => [actionDescription];
}

/// Estado de litígio paritário instaurado (congelamento em In_Dispute e prazo de 5 dias úteis).
class DisputeParityOpened extends DisputeState {
  final DisputeCaseModel dispute;

  const DisputeParityOpened(this.dispute);

  Duration? get timeRemaining {
    if (dispute.deadlineAt == null) return null;
    return dispute.deadlineAt!.difference(DateTime.now());
  }

  @override
  List<Object?> get props => [dispute];
}

/// Estado de resolução e encerramento de disputa (ex: sobreposição automática de Nível 1).
class DisputeResolved extends DisputeState {
  final DisputeCaseModel dispute;

  const DisputeResolved(this.dispute);

  @override
  List<Object?> get props => [dispute];
}

/// Estado quando certidão cartorial complementar é anexada com sucesso.
class DisputeCertificateSubmitted extends DisputeState {
  final DisputeEvidenceResponseModel response;

  const DisputeCertificateSubmitted(this.response);

  @override
  List<Object?> get props => [response];
}

/// Estado de erro em operações de disputa e contestação.
class DisputeError extends DisputeState {
  final String message;
  final String? errorCode;
  final int? statusCode;

  const DisputeError(
    this.message, {
    this.errorCode,
    this.statusCode,
  });

  @override
  List<Object?> get props => [message, errorCode, statusCode];
}
