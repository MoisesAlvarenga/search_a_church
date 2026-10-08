/// Estados do ciclo de vida da congregação no processo de reivindicação (AD-005, AD-015, AD-016).
enum ChurchClaimState {
  unclaimed(0, 'Unclaimed'),
  pendingVerification(1, 'Pending_Verification'),
  verified(2, 'Verified'),
  inDispute(3, 'In_Dispute');

  final int value;
  final String wireName;

  const ChurchClaimState(this.value, this.wireName);

  static ChurchClaimState fromJson(dynamic json) {
    if (json is int) {
      return ChurchClaimState.values.firstWhere(
        (e) => e.value == json,
        orElse: () => ChurchClaimState.unclaimed,
      );
    }
    if (json is String) {
      final normalized = json.trim().toLowerCase();
      return ChurchClaimState.values.firstWhere(
        (e) => e.wireName.toLowerCase() == normalized || e.name.toLowerCase() == normalized,
        orElse: () => ChurchClaimState.unclaimed,
      );
    }
    return ChurchClaimState.unclaimed;
  }

  dynamic toJson() => value;
}

/// Hierarquia Probatória Estrita em 3 Níveis (AD-012, AD-013).
enum VerificationTier {
  none(0, 'None'),
  tier3SocialPresencial(1, 'Tier3_SocialPresencial'),
  tier2Institucional(2, 'Tier2_Institucional'),
  tier1Cartorio(3, 'Tier1_Cartorio');

  final int value;
  final String wireName;

  const VerificationTier(this.value, this.wireName);

  static VerificationTier fromJson(dynamic json) {
    if (json is int) {
      return VerificationTier.values.firstWhere(
        (e) => e.value == json,
        orElse: () => VerificationTier.none,
      );
    }
    if (json is String) {
      final normalized = json.trim().toLowerCase();
      return VerificationTier.values.firstWhere(
        (e) => e.wireName.toLowerCase() == normalized || e.name.toLowerCase() == normalized,
        orElse: () => VerificationTier.none,
      );
    }
    return VerificationTier.none;
  }

  dynamic toJson() => value;
}

/// Status do registro de reivindicação individual.
enum ClaimRecordStatus {
  pending(0, 'Pending'),
  approved(1, 'Approved'),
  expired(2, 'Expired'),
  rejected(3, 'Rejected'),
  revoked(4, 'Revoked');

  final int value;
  final String wireName;

  const ClaimRecordStatus(this.value, this.wireName);

  static ClaimRecordStatus fromJson(dynamic json) {
    if (json is int) {
      return ClaimRecordStatus.values.firstWhere(
        (e) => e.value == json,
        orElse: () => ClaimRecordStatus.pending,
      );
    }
    if (json is String) {
      final normalized = json.trim().toLowerCase();
      return ClaimRecordStatus.values.firstWhere(
        (e) => e.wireName.toLowerCase() == normalized || e.name.toLowerCase() == normalized,
        orElse: () => ClaimRecordStatus.pending,
      );
    }
    return ClaimRecordStatus.pending;
  }

  dynamic toJson() => value;
}

/// Métodos de validação suportados no processo de claim.
enum ValidationMethod {
  geofence(1, 'Geofence'),
  socialBio(2, 'SocialBio'),
  domainDns(3, 'DomainDns'),
  institutionalEmail(4, 'InstitutionalEmail'),
  cartorioRcpj(5, 'CartorioRcpj'),
  receitaQsa(6, 'ReceitaQsa');

  final int value;
  final String wireName;

  const ValidationMethod(this.value, this.wireName);

  static ValidationMethod fromJson(dynamic json) {
    if (json is int) {
      return ValidationMethod.values.firstWhere(
        (e) => e.value == json,
        orElse: () => ValidationMethod.geofence,
      );
    }
    if (json is String) {
      final normalized = json.trim().toLowerCase();
      return ValidationMethod.values.firstWhere(
        (e) => e.wireName.toLowerCase() == normalized || e.name.toLowerCase() == normalized,
        orElse: () => ValidationMethod.geofence,
      );
    }
    return ValidationMethod.geofence;
  }

  dynamic toJson() => value;
}

/// Tipos de desfecho do processo de contestação / disputa (AD-012, AD-016).
enum DisputeResolutionType {
  automaticOverrideN1(1, 'AutomaticOverrideN1'),
  parityDisputeOpened(2, 'ParityDisputeOpened'),
  resolvedByAverbationPrevalence(3, 'ResolvedByAverbationPrevalence'),
  resolvedByInertia(4, 'ResolvedByInertia'),
  canceledJudicialFallback(5, 'CanceledJudicialFallback');

  final int value;
  final String wireName;

  const DisputeResolutionType(this.value, this.wireName);

  static DisputeResolutionType fromJson(dynamic json) {
    if (json is int) {
      return DisputeResolutionType.values.firstWhere(
        (e) => e.value == json,
        orElse: () => DisputeResolutionType.automaticOverrideN1,
      );
    }
    if (json is String) {
      final normalized = json.trim().toLowerCase();
      return DisputeResolutionType.values.firstWhere(
        (e) => e.wireName.toLowerCase() == normalized || e.name.toLowerCase() == normalized,
        orElse: () => DisputeResolutionType.automaticOverrideN1,
      );
    }
    return DisputeResolutionType.automaticOverrideN1;
  }

  dynamic toJson() => value;
}
