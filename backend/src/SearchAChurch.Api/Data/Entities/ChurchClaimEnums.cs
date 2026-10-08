namespace SearchAChurch.Api.Data.Entities;

/// <summary>
/// Estados do ciclo de vida da congregação no processo de reivindicação (AD-005, AD-015, AD-016).
/// </summary>
public enum ChurchClaimState
{
    Unclaimed = 0,
    Pending_Verification = 1,
    Verified = 2,
    In_Dispute = 3
}

/// <summary>
/// Hierarquia Probatória Estrita em 3 Níveis (AD-012, AD-013).
/// </summary>
public enum VerificationTier
{
    None = 0,
    Tier3_SocialPresencial = 1,
    Tier2_Institucional = 2,
    Tier1_Cartorio = 3
}

/// <summary>
/// Status do registro de reivindicação individual.
/// </summary>
public enum ClaimRecordStatus
{
    Pending = 0,
    Approved = 1,
    Expired = 2,
    Rejected = 3,
    Revoked = 4
}

/// <summary>
/// Métodos de validação suportados no processo de claim.
/// </summary>
public enum ValidationMethod
{
    Geofence = 1,
    SocialBio = 2,
    DomainDns = 3,
    InstitutionalEmail = 4,
    CartorioRcpj = 5,
    ReceitaQsa = 6
}

/// <summary>
/// Tipos de evidências comprobatórias anexadas à reivindicação.
/// </summary>
public enum EvidenceType
{
    PhotoGps = 1,
    BioToken = 2,
    DnsRecord = 3,
    EmailOtp = 4,
    DocumentPdf = 5,
    QsaCrossCheck = 6
}

/// <summary>
/// Status do processo de contestação / litígio paritário (AD-016).
/// </summary>
public enum DisputeStatus
{
    Open = 1,
    InReview = 2,
    ResolvedIncumbent = 3,
    ResolvedChallenger = 4,
    CanceledJudicial = 5
}
