namespace SearchAChurch.Api.Data.Entities;

public class ChurchMeetingSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChurchId { get; set; }
    public Church Church { get; set; } = null!;

    /// <summary>
    /// Dia da semana da celebração (0 = Domingo, 1 = Segunda-feira, ..., 6 = Sábado).
    /// </summary>
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>
    /// Formato "HH:mm" (ex: "10:00", "19:30").
    /// </summary>
    public string StartTime { get; set; } = string.Empty;

    /// <summary>
    /// Descrição do culto ou reunião (ex: "Culto da Família", "Escola Bíblica Dominical").
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Idioma da celebração (ex: "pt", "en"). Padrão "pt".
    /// </summary>
    public string Language { get; set; } = "pt";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
