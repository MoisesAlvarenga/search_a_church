namespace SearchAChurch.Api.Features.Claim.Services;

/// <summary>
/// Resultado da validação de presença física geodésica.
/// </summary>
public record GeofenceValidationResult(
    bool IsValid,
    double DistanceMeters,
    string? ErrorCode = null,
    string? ErrorMessage = null
)
{
    public static GeofenceValidationResult Success(double distanceMeters) =>
        new(true, Math.Round(distanceMeters, 2));

    public static GeofenceValidationResult Failure(string errorCode, string errorMessage, double distanceMeters = 0.0) =>
        new(false, Math.Round(distanceMeters, 2), errorCode, errorMessage);
}

/// <summary>
/// Contrato do motor de validação de presença física e geofencing server-side (AD-017, CLAIM-02).
/// </summary>
public interface IGeofencingService
{
    /// <summary>
    /// Calcula a distância geodésica em metros entre duas coordenadas utilizando a fórmula de Haversine.
    /// </summary>
    double CalculateHaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2);

    /// <summary>
    /// Valida se o dispositivo móvel está fisicamente presente no templo (raio <= 100m, precisão <= 50m, anti-mock).
    /// </summary>
    GeofenceValidationResult ValidatePresence(
        double churchLat,
        double churchLng,
        double deviceLat,
        double deviceLng,
        double horizontalAccuracyMeters,
        bool isMockLocation);
}

/// <summary>
/// Implementação do serviço de geofencing com Haversine server-side e validação anti-fraude.
/// </summary>
public class GeofencingService : IGeofencingService
{
    public const double EarthRadiusMeters = 6371000.0;
    public const double MaxToleranceRadiusMeters = 100.0;
    public const double MaxAllowedAccuracyMeters = 50.0;

    public const string ErrorMockLocationDetected = "LOCALIZACAO_SIMULADA_DETECTADA";
    public const string ErrorInsufficientGpsAccuracy = "PRECISAO_GPS_INSUFICIENTE";
    public const string ErrorOutOfAllowedRadius = "FORA_DO_RAIO_PERMITIDO";

    public double CalculateHaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        ValidateCoordinates(lat1, lon1, nameof(lat1), nameof(lon1));
        ValidateCoordinates(lat2, lon2, nameof(lat2), nameof(lon2));

        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double radLat1 = ToRadians(lat1);
        double radLat2 = ToRadians(lat2);

        double sinDLat2 = Math.Sin(dLat / 2.0);
        double sinDLon2 = Math.Sin(dLon / 2.0);

        double a = (sinDLat2 * sinDLat2) +
                   (Math.Cos(radLat1) * Math.Cos(radLat2) * sinDLon2 * sinDLon2);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

        return EarthRadiusMeters * c;
    }

    public GeofenceValidationResult ValidatePresence(
        double churchLat,
        double churchLng,
        double deviceLat,
        double deviceLng,
        double horizontalAccuracyMeters,
        bool isMockLocation)
    {
        if (horizontalAccuracyMeters < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(horizontalAccuracyMeters),
                "Horizontal accuracy cannot be negative.");
        }

        // 1. Verificação Anti-Mock Location (AD-017)
        if (isMockLocation)
        {
            return GeofenceValidationResult.Failure(
                ErrorMockLocationDetected,
                "A localização informada foi identificada como simulada ou manipulada pelo dispositivo.");
        }

        // 2. Verificação de Precisão do Sinal de Satélite (accuracy <= 50m)
        if (horizontalAccuracyMeters > MaxAllowedAccuracyMeters)
        {
            return GeofenceValidationResult.Failure(
                ErrorInsufficientGpsAccuracy,
                $"A precisão do sinal de GPS ({horizontalAccuracyMeters:F1}m) é insuficiente para comprovar presença física. É exigida dispersão horizontal ≤ 50 metros.");
        }

        // 3. Cálculo Geodésico Server-Side (Haversine <= 100m)
        double distanceMeters = CalculateHaversineDistanceMeters(churchLat, churchLng, deviceLat, deviceLng);

        if (distanceMeters > MaxToleranceRadiusMeters)
        {
            return GeofenceValidationResult.Failure(
                ErrorOutOfAllowedRadius,
                $"O dispositivo está a {distanceMeters:F1} metros da igreja, fora do raio máximo permitido de 100 metros.",
                distanceMeters);
        }

        return GeofenceValidationResult.Success(distanceMeters);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static void ValidateCoordinates(double lat, double lon, string latParamName, string lonParamName)
    {
        if (lat is < -90.0 or > 90.0)
        {
            throw new ArgumentOutOfRangeException(latParamName, $"Latitude must be between -90 and 90 degrees. Received: {lat}");
        }

        if (lon is < -180.0 or > 180.0)
        {
            throw new ArgumentOutOfRangeException(lonParamName, $"Longitude must be between -180 and 180 degrees. Received: {lon}");
        }
    }
}
