import 'package:equatable/equatable.dart';
import 'claim_enums.dart';

/// Resposta genérica de homologação e concessão de selo de verificação.
class VerificationResultModel extends Equatable {
  final String claimId;
  final String churchId;
  final bool isVerified;
  final VerificationTier tier;
  final String message;

  const VerificationResultModel({
    required this.claimId,
    required this.churchId,
    required this.isVerified,
    required this.tier,
    required this.message,
  });

  factory VerificationResultModel.fromJson(Map<String, dynamic> json) {
    return VerificationResultModel(
      claimId: json['claimId'] as String? ?? '',
      churchId: json['churchId'] as String? ?? '',
      isVerified: json['isVerified'] as bool? ?? false,
      tier: VerificationTier.fromJson(json['tier'] ?? json['grantedTier']),
      message: json['message'] as String? ?? '',
    );
  }

  Map<String, dynamic> toJson() => {
    'claimId': claimId,
    'churchId': churchId,
    'isVerified': isVerified,
    'tier': tier.toJson(),
    'message': message,
  };

  @override
  List<Object?> get props => [claimId, churchId, isVerified, tier, message];
}

/// Payload para verificação por Geofencing (Nível 3).
class GeofenceVerificationRequestModel extends Equatable {
  final String claimId;
  final double deviceLatitude;
  final double deviceLongitude;
  final double horizontalAccuracyMeters;
  final bool isMockLocation;
  final String? photoUrl;
  final String? photoHashSha256;

  const GeofenceVerificationRequestModel({
    required this.claimId,
    required this.deviceLatitude,
    required this.deviceLongitude,
    required this.horizontalAccuracyMeters,
    required this.isMockLocation,
    this.photoUrl,
    this.photoHashSha256,
  });

  Map<String, dynamic> toJson() => {
    'claimId': claimId,
    'deviceLatitude': deviceLatitude,
    'deviceLongitude': deviceLongitude,
    'horizontalAccuracyMeters': horizontalAccuracyMeters,
    'isMockLocation': isMockLocation,
    if (photoUrl != null) 'photoUrl': photoUrl,
    if (photoHashSha256 != null) 'photoHashSha256': photoHashSha256,
  };

  @override
  List<Object?> get props => [
    claimId,
    deviceLatitude,
    deviceLongitude,
    horizontalAccuracyMeters,
    isMockLocation,
    photoUrl,
    photoHashSha256,
  ];
}

/// Payload para geração de token de bio social (Nível 3).
class SocialTokenGenerateRequestModel extends Equatable {
  final String claimId;
  final String socialNetwork;
  final String profileHandle;

  const SocialTokenGenerateRequestModel({
    required this.claimId,
    required this.socialNetwork,
    required this.profileHandle,
  });

  Map<String, dynamic> toJson() => {
    'claimId': claimId,
    'socialNetwork': socialNetwork,
    'profileHandle': profileHandle,
  };

  @override
  List<Object?> get props => [claimId, socialNetwork, profileHandle];
}

/// Resposta com token temporário gerado para bio de rede social.
class SocialBioTokenModel extends Equatable {
  final String claimId;
  final String token;
  final DateTime? expiresAt;
  final String instructions;

  const SocialBioTokenModel({
    required this.claimId,
    required this.token,
    this.expiresAt,
    required this.instructions,
  });

  factory SocialBioTokenModel.fromJson(Map<String, dynamic> json) {
    return SocialBioTokenModel(
      claimId: json['claimId'] as String? ?? '',
      token: json['token'] as String? ?? '',
      expiresAt: json['expiresAt'] != null
          ? DateTime.tryParse(json['expiresAt'] as String)
          : null,
      instructions: json['instructions'] as String? ?? '',
    );
  }

  Map<String, dynamic> toJson() => {
    'claimId': claimId,
    'token': token,
    'expiresAt': expiresAt?.toIso8601String(),
    'instructions': instructions,
  };

  @override
  List<Object?> get props => [claimId, token, expiresAt, instructions];
}

/// Payload para confirmação de presença de token na bio de rede social.
class SocialBioConfirmRequestModel extends Equatable {
  final String claimId;
  final String socialNetwork;
  final String profileHandle;
  final String expectedToken;
  final String? bioContent;

  const SocialBioConfirmRequestModel({
    required this.claimId,
    required this.socialNetwork,
    required this.profileHandle,
    required this.expectedToken,
    this.bioContent,
  });

  Map<String, dynamic> toJson() => {
    'claimId': claimId,
    'socialNetwork': socialNetwork,
    'profileHandle': profileHandle,
    'expectedToken': expectedToken,
    if (bioContent != null) 'bioContent': bioContent,
  };

  @override
  List<Object?> get props => [
    claimId,
    socialNetwork,
    profileHandle,
    expectedToken,
    bioContent,
  ];
}

/// Payload para envio de OTP a e-mail institucional (Nível 2).
class DomainOtpSendRequestModel extends Equatable {
  final String claimId;
  final String corporateEmail;
  final String expectedDomain;

  const DomainOtpSendRequestModel({
    required this.claimId,
    required this.corporateEmail,
    required this.expectedDomain,
  });

  Map<String, dynamic> toJson() => {
    'claimId': claimId,
    'corporateEmail': corporateEmail,
    'expectedDomain': expectedDomain,
  };

  @override
  List<Object?> get props => [claimId, corporateEmail, expectedDomain];
}

/// Resposta do envio de OTP para e-mail institucional.
class DomainOtpSendResponseModel extends Equatable {
  final String claimId;
  final String email;
  final DateTime? expiresAt;
  final String message;

  const DomainOtpSendResponseModel({
    required this.claimId,
    required this.email,
    this.expiresAt,
    required this.message,
  });

  factory DomainOtpSendResponseModel.fromJson(Map<String, dynamic> json) {
    return DomainOtpSendResponseModel(
      claimId: json['claimId'] as String? ?? '',
      email: json['email'] as String? ?? '',
      expiresAt: json['expiresAt'] != null
          ? DateTime.tryParse(json['expiresAt'] as String)
          : null,
      message: json['message'] as String? ?? '',
    );
  }

  Map<String, dynamic> toJson() => {
    'claimId': claimId,
    'email': email,
    'expiresAt': expiresAt?.toIso8601String(),
    'message': message,
  };

  @override
  List<Object?> get props => [claimId, email, expiresAt, message];
}

/// Payload para validação do código OTP de e-mail institucional.
class DomainOtpConfirmRequestModel extends Equatable {
  final String claimId;
  final String corporateEmail;
  final String otpCode;

  const DomainOtpConfirmRequestModel({
    required this.claimId,
    required this.corporateEmail,
    required this.otpCode,
  });

  Map<String, dynamic> toJson() => {
    'claimId': claimId,
    'corporateEmail': corporateEmail,
    'otpCode': otpCode,
  };

  @override
  List<Object?> get props => [claimId, corporateEmail, otpCode];
}

/// Payload para submissão de documento em cartório RCPJ (Nível 1).
class CartorioDocumentRequestModel extends Equatable {
  final String claimId;
  final String documentFileName;
  final String documentFileHashSha256;
  final DateTime averbationDate;
  final String? documentUrl;

  const CartorioDocumentRequestModel({
    required this.claimId,
    required this.documentFileName,
    required this.documentFileHashSha256,
    required this.averbationDate,
    this.documentUrl,
  });

  Map<String, dynamic> toJson() => {
    'claimId': claimId,
    'documentFileName': documentFileName,
    'documentFileHashSha256': documentFileHashSha256,
    'averbationDate': averbationDate.toIso8601String(),
    if (documentUrl != null) 'documentUrl': documentUrl,
  };

  @override
  List<Object?> get props => [
    claimId,
    documentFileName,
    documentFileHashSha256,
    averbationDate,
    documentUrl,
  ];
}

/// Payload para validação cadastral junto ao QSA da Receita Federal (Nível 2).
class QsaVerificationRequestModel extends Equatable {
  final String claimId;
  final String churchCnpj;
  final String representativeCpf;
  final String representativeName;

  const QsaVerificationRequestModel({
    required this.claimId,
    required this.churchCnpj,
    required this.representativeCpf,
    required this.representativeName,
  });

  Map<String, dynamic> toJson() => {
    'claimId': claimId,
    'churchCnpj': churchCnpj,
    'representativeCpf': representativeCpf,
    'representativeName': representativeName,
  };

  @override
  List<Object?> get props => [
    claimId,
    churchCnpj,
    representativeCpf,
    representativeName,
  ];
}
