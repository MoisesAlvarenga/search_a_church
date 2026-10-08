namespace SearchAChurch.Api.Features.Profile.Authorization;

/// <summary>
/// Constantes de políticas de autorização para o módulo de Gestão de Perfis (AD-008, AD-009, AD-026).
/// </summary>
public static class ProfileAuthorizationPolicies
{
    /// <summary>
    /// Exige que o usuário autenticado seja o titular do perfil acessado/modificado (sub == userId).
    /// </summary>
    public const string RequireProfileOwnership = "RequireProfileOwnership";

    /// <summary>
    /// Exige que o usuário autenticado seja o representante verificado homologado da congregação (claim Verified).
    /// </summary>
    public const string RequireVerifiedRepresentative = "RequireVerifiedRepresentative";
}
