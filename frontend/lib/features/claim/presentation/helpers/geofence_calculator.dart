import 'dart:math';

/// Utilitário matemático para cálculo de Geofencing com fórmula Haversine no cliente.
/// Parâmetros alinhados com GeofencingService.cs no backend:
/// - Raio da Terra: R = 6.371.000m
/// - Raio máximo permitido: 100m
/// - Precisão horizontal máxima exigida: 50m
class GeofenceCalculator {
  static const double earthRadiusMeters = 6371000.0;
  static const double maxAllowedRadiusMeters = 100.0;
  static const double maxAllowedAccuracyMeters = 50.0;

  /// Calcula a distância ortodrômica em metros entre dois pontos geográficos.
  static double calculateDistanceMeters({
    required double lat1,
    required double lon1,
    required double lat2,
    required double lon2,
  }) {
    final dLat = _toRadians(lat2 - lat1);
    final dLon = _toRadians(lon2 - lon1);

    final a = sin(dLat / 2) * sin(dLat / 2) +
        cos(_toRadians(lat1)) *
            cos(_toRadians(lat2)) *
            sin(dLon / 2) *
            sin(dLon / 2);

    final c = 2 * atan2(sqrt(a), sqrt(1 - a));
    return earthRadiusMeters * c;
  }

  /// Verifica se o dispositivo está dentro do raio permitido de 100 metros.
  static bool isWithinRadius({
    required double deviceLat,
    required double deviceLon,
    required double churchLat,
    required double churchLon,
    double maxRadius = maxAllowedRadiusMeters,
  }) {
    final distance = calculateDistanceMeters(
      lat1: deviceLat,
      lon1: deviceLon,
      lat2: churchLat,
      lon2: churchLon,
    );
    return distance <= maxRadius;
  }

  /// Verifica se a precisão do GPS é aceitável (menor ou igual a 50 metros).
  static bool isValidAccuracy(double horizontalAccuracyMeters) {
    return horizontalAccuracyMeters >= 0 &&
        horizontalAccuracyMeters <= maxAllowedAccuracyMeters;
  }

  static double _toRadians(double degrees) => degrees * (pi / 180.0);
}
