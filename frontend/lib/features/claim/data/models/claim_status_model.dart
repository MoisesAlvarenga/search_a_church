import 'package:equatable/equatable.dart';
import 'claim_enums.dart';

/// Modelo de resposta com estado completo do ciclo de vida da congregação no processo de reivindicação.
class ClaimStatusModel extends Equatable {
  final String churchId;
  final String churchName;
  final ChurchClaimState claimStatus;
  final VerificationTier verificationTier;
  final bool isVerified;
  final bool isCurrentUserRepresentative;
  final String? activeClaimId;
  final ClaimRecordStatus? activeClaimStatus;
  final DateTime? expiresAt;
  final Duration? timeRemaining;

  const ClaimStatusModel({
    required this.churchId,
    required this.churchName,
    required this.claimStatus,
    required this.verificationTier,
    required this.isVerified,
    required this.isCurrentUserRepresentative,
    this.activeClaimId,
    this.activeClaimStatus,
    this.expiresAt,
    this.timeRemaining,
  });

  factory ClaimStatusModel.fromJson(Map<String, dynamic> json) {
    Duration? parseDuration(dynamic val) {
      if (val == null) return null;
      if (val is String) {
        // Formato TimeSpan padrão .NET ex: "1.02:03:04" ou "02:03:04"
        final parts = val.split(':');
        if (parts.length >= 3) {
          int days = 0;
          int hours = 0;
          if (parts[0].contains('.')) {
            final dayHour = parts[0].split('.');
            days = int.tryParse(dayHour[0]) ?? 0;
            hours = int.tryParse(dayHour[1]) ?? 0;
          } else {
            hours = int.tryParse(parts[0]) ?? 0;
          }
          final minutes = int.tryParse(parts[1]) ?? 0;
          final seconds = double.tryParse(parts[2])?.toInt() ?? 0;
          return Duration(days: days, hours: hours, minutes: minutes, seconds: seconds);
        }
      }
      return null;
    }

    return ClaimStatusModel(
      churchId: json['churchId'] as String? ?? '',
      churchName: json['churchName'] as String? ?? '',
      claimStatus: ChurchClaimState.fromJson(json['claimStatus']),
      verificationTier: VerificationTier.fromJson(json['verificationTier']),
      isVerified: json['isVerified'] as bool? ?? false,
      isCurrentUserRepresentative: json['isCurrentUserRepresentative'] as bool? ?? false,
      activeClaimId: json['activeClaimId'] as String?,
      activeClaimStatus: json['activeClaimStatus'] != null
          ? ClaimRecordStatus.fromJson(json['activeClaimStatus'])
          : null,
      expiresAt: json['expiresAt'] != null
          ? DateTime.tryParse(json['expiresAt'] as String)
          : null,
      timeRemaining: parseDuration(json['timeRemaining']),
    );
  }

  Map<String, dynamic> toJson() => {
    'churchId': churchId,
    'churchName': churchName,
    'claimStatus': claimStatus.toJson(),
    'verificationTier': verificationTier.toJson(),
    'isVerified': isVerified,
    'isCurrentUserRepresentative': isCurrentUserRepresentative,
    'activeClaimId': activeClaimId,
    'activeClaimStatus': activeClaimStatus?.toJson(),
    'expiresAt': expiresAt?.toIso8601String(),
    'timeRemaining': timeRemaining?.toString(),
  };

  @override
  List<Object?> get props => [
    churchId,
    churchName,
    claimStatus,
    verificationTier,
    isVerified,
    isCurrentUserRepresentative,
    activeClaimId,
    activeClaimStatus,
    expiresAt,
    timeRemaining,
  ];
}
