import 'package:equatable/equatable.dart';
import 'claim_enums.dart';

/// Payload de requisição para iniciar um processo de reivindicação.
class ClaimInitiationRequestModel extends Equatable {
  final String churchId;
  final String tosVersion;
  final bool art299Accepted;
  final bool technicalIntermediaryAccepted;
  final ValidationMethod validationMethod;
  final VerificationTier targetTier;

  const ClaimInitiationRequestModel({
    required this.churchId,
    this.tosVersion = '1.0',
    required this.art299Accepted,
    required this.technicalIntermediaryAccepted,
    required this.validationMethod,
    required this.targetTier,
  });

  Map<String, dynamic> toJson() => {
    'churchId': churchId,
    'tosVersion': tosVersion,
    'art299Accepted': art299Accepted,
    'technicalIntermediaryAccepted': technicalIntermediaryAccepted,
    'validationMethod': validationMethod.value,
    'targetTier': targetTier.value,
  };

  @override
  List<Object?> get props => [
    churchId,
    tosVersion,
    art299Accepted,
    technicalIntermediaryAccepted,
    validationMethod,
    targetTier,
  ];
}

/// Modelo de dados de retorno para início de reivindicação de perfil.
class ChurchClaimModel extends Equatable {
  final String claimId;
  final String churchId;
  final ClaimRecordStatus status;
  final VerificationTier targetTier;
  final DateTime? expiresAt;
  final int ttlHours;
  final String message;

  const ChurchClaimModel({
    required this.claimId,
    required this.churchId,
    required this.status,
    required this.targetTier,
    this.expiresAt,
    required this.ttlHours,
    required this.message,
  });

  factory ChurchClaimModel.fromJson(Map<String, dynamic> json) {
    return ChurchClaimModel(
      claimId: json['claimId'] as String? ?? json['id'] as String? ?? '',
      churchId: json['churchId'] as String? ?? '',
      status: ClaimRecordStatus.fromJson(json['status']),
      targetTier: VerificationTier.fromJson(json['targetTier']),
      expiresAt: json['expiresAt'] != null
          ? DateTime.tryParse(json['expiresAt'] as String)
          : null,
      ttlHours: (json['ttlHours'] as num?)?.toInt() ?? 0,
      message: json['message'] as String? ?? '',
    );
  }

  Map<String, dynamic> toJson() => {
    'claimId': claimId,
    'churchId': churchId,
    'status': status.toJson(),
    'targetTier': targetTier.toJson(),
    'expiresAt': expiresAt?.toIso8601String(),
    'ttlHours': ttlHours,
    'message': message,
  };

  @override
  List<Object?> get props => [
    claimId,
    churchId,
    status,
    targetTier,
    expiresAt,
    ttlHours,
    message,
  ];
}
