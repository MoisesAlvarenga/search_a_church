using SearchAChurch.Api.Common;
using SearchAChurch.Api.Features.Profile.Models;

namespace SearchAChurch.Api.Features.Profile.Services;

/// <summary>
/// Contrato do serviço de domínio para gestão eclesiástica de congregações (AD-004, AD-009, AD-026).
/// </summary>
public interface IChurchProfileService
{
    /// <summary>
    /// Consulta as informações completas do perfil da igreja, incluindo contatos, cultos e tags ofertadas.
    /// </summary>
    Task<Result<ChurchProfileResponse>> GetChurchProfileAsync(Guid churchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza dados cadastrais, cultos e tags com validação estrita de PlaceId e concorrência otimista.
    /// </summary>
    Task<Result<ChurchProfileResponse>> UpdateChurchProfileAsync(
        Guid churchId,
        UpdateChurchProfileRequest request,
        string? ifMatchHeader = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Altera o status de atividade da igreja (IsActive), preservando seu histórico sem exclusão física.
    /// </summary>
    Task<Result<ChurchStatusResponse>> SetChurchStatusAsync(
        Guid churchId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
