using Microsoft.AspNetCore.Authorization;

namespace SearchAChurch.Api.Features.Profile.Authorization;

/// <summary>
/// Requisito de autorização para representantes verificados de congregações (AD-009, AD-026, PROFILE-03).
/// Garante que alterações em perfis de igrejas só ocorram se o usuário autenticado for o
/// VerifiedRepresentativeUserId com status Verified via church-profile-claim.
/// </summary>
public class VerifiedRepresentativeRequirement : IAuthorizationRequirement
{
}
