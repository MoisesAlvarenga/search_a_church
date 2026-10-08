using Microsoft.AspNetCore.Authorization;

namespace SearchAChurch.Api.Features.Profile.Authorization;

/// <summary>
/// Requisito de autorização estrita baseada em titularidade (ownership) do perfil (AD-008, PROFILE-02).
/// Garante que apenas o próprio titular autenticado (sub claim) possa consultar ou alterar seu perfil.
/// </summary>
public class ProfileOwnershipRequirement : IAuthorizationRequirement
{
}
