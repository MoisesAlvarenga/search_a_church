import 'package:equatable/equatable.dart';
import 'claim_enums.dart';

/// Payload para contestação de propriedade de igreja já reivindicada.
class DisputeContestRequestModel extends Equatable {
  final String churchId;
  final String tosVersion;
  final bool tosAccepted;
  final String legalRepresentativeName;
  final String legalRepresentativeCpf;
  final String churchCnpj;
  final VerificationTier submittedTier;
  final String documentFileHash;
  final DateTime documentAverbationDate;
  final String? justification;

  const DisputeContestRequestModel({
    required this.churchId,
    this.tosVersion = '1.0',
    required this.tosAccepted,
    required this.legalRepresentativeName,
    required this.legalRepresentativeCpf,
    required this.churchCnpj,
    required this.submittedTier,
    required this.documentFileHash,
    required this.documentAverbationDate,
    this.justification,
  });

  Map<String, dynamic> toJson() => {
    'churchId': churchId,
    'tosVersion': tosVersion,
    'tosAccepted': tosAccepted,
    'legalRepresentativeName': legalRepresentativeName,
    'legalRepresentativeCpf': legalRepresentativeCpf,
    'churchCnpj': churchCnpj,
    'submittedTier': submittedTier.value,
    'documentFileHash': documentFileHash,
    'documentAverbationDate': documentAverbationDate.toIso8601String(),
    if (justification != null) 'justification': justification,
  };

  @override
  List<Object?> get props => [
    churchId,
    tosVersion,
    tosAccepted,
    legalRepresentativeName,
    legalRepresentativeCpf,
    churchCnpj,
    submittedTier,
    documentFileHash,
    documentAverbationDate,
    justification,
  ];
}

/// Resposta da contestação ou abertura de litígio paritário.
class DisputeCaseModel extends Equatable {
  final bool success;
  final DisputeResolutionType resolutionType;
  final String churchId;
  final String? disputeId;
  final DateTime? deadlineAt;
  final String message;
  final VerificationTier currentTier;
  final String? activeRepresentativeUserId;

  const DisputeCaseModel({
    required this.success,
    required this.resolutionType,
    required this.churchId,
    this.disputeId,
    this.deadlineAt,
    required this.message,
    required this.currentTier,
    this.activeRepresentativeUserId,
  });

  factory DisputeCaseModel.fromJson(Map<String, dynamic> json) {
    return DisputeCaseModel(
      success: json['success'] as bool? ?? false,
      resolutionType: DisputeResolutionType.fromJson(json['resolutionType']),
      churchId: json['churchId'] as String? ?? '',
      disputeId: json['disputeId'] as String?,
      deadlineAt: json['deadlineAt'] != null
          ? DateTime.tryParse(json['deadlineAt'] as String)
          : null,
      message: json['message'] as String? ?? '',
      currentTier: VerificationTier.fromJson(json['currentTier']),
      activeRepresentativeUserId: json['activeRepresentativeUserId'] as String?,
    );
  }

  Map<String, dynamic> toJson() => {
    'success': success,
    'resolutionType': resolutionType.toJson(),
    'churchId': churchId,
    'disputeId': disputeId,
    'deadlineAt': deadlineAt?.toIso8601String(),
    'message': message,
    'currentTier': currentTier.toJson(),
    'activeRepresentativeUserId': activeRepresentativeUserId,
  };

  @override
  List<Object?> get props => [
    success,
    resolutionType,
    churchId,
    disputeId,
    deadlineAt,
    message,
    currentTier,
    activeRepresentativeUserId,
  ];
}

/// Payload para submissão tempestiva de certidão cartorial em litígio paritário.
class DisputeEvidenceRequestModel extends Equatable {
  final String disputeId;
  final String documentFileHash;
  final DateTime averbationDate;
  final String? notes;

  const DisputeEvidenceRequestModel({
    required this.disputeId,
    required this.documentFileHash,
    required this.averbationDate,
    this.notes,
  });

  Map<String, dynamic> toJson() => {
    'documentFileHash': documentFileHash,
    'averbationDate': averbationDate.toIso8601String(),
    if (notes != null) 'notes': notes,
  };

  @override
  List<Object?> get props => [disputeId, documentFileHash, averbationDate, notes];
}

/// Resposta da juntada de certidão complementar em disputa paritária.
class DisputeEvidenceResponseModel extends Equatable {
  final String disputeId;
  final String userId;
  final DateTime submittedAt;
  final String documentFileHash;
  final DateTime averbationDate;
  final String message;

  const DisputeEvidenceResponseModel({
    required this.disputeId,
    required this.userId,
    required this.submittedAt,
    required this.documentFileHash,
    required this.averbationDate,
    required this.message,
  });

  factory DisputeEvidenceResponseModel.fromJson(Map<String, dynamic> json) {
    return DisputeEvidenceResponseModel(
      disputeId: json['disputeId'] as String? ?? '',
      userId: json['userId'] as String? ?? '',
      submittedAt: json['submittedAt'] != null
          ? DateTime.tryParse(json['submittedAt'] as String) ?? DateTime.now()
          : DateTime.now(),
      documentFileHash: json['documentFileHash'] as String? ?? '',
      averbationDate: json['averbationDate'] != null
          ? DateTime.tryParse(json['averbationDate'] as String) ?? DateTime.now()
          : DateTime.now(),
      message: json['message'] as String? ?? '',
    );
  }

  Map<String, dynamic> toJson() => {
    'disputeId': disputeId,
    'userId': userId,
    'submittedAt': submittedAt.toIso8601String(),
    'documentFileHash': documentFileHash,
    'averbationDate': averbationDate.toIso8601String(),
    'message': message,
  };

  @override
  List<Object?> get props => [
    disputeId,
    userId,
    submittedAt,
    documentFileHash,
    averbationDate,
    message,
  ];
}
