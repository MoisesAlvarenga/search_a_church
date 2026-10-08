using SearchAChurch.Api.Common;
using SearchAChurch.Api.Features.Profile.Models;

namespace SearchAChurch.Api.Features.Profile.Services;

/// <summary>
/// Contrato do serviço de domínio para gerenciamento de preferências de perfil de usuário e conformidade LGPD.
/// </summary>
public interface IUserProfileService
{
    /// <summary>
    /// Consulta o perfil e preferências do usuário autenticado. Retorna defaults se ainda não configurado.
    /// </summary>
    Task<Result<UserProfileResponse>> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria ou atualiza as preferências persistidas do usuário (baseline de busca teológica e raio padrão).
    /// </summary>
    Task<Result<UserProfileResponse>> UpsertUserProfileAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa o soft delete da conta em conformidade com o art. 18 da LGPD, anonimizando dados cadastrais e revogando sessões.
    /// </summary>
    Task<Result<DeleteAccountResponse>> DeleteUserAccountAsync(Guid userId, CancellationToken cancellationToken = default);
}
