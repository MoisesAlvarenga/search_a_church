import 'package:equatable/equatable.dart';
import 'package:geolocator/geolocator.dart';

/// Status do resultado da obtenção de geolocalização.
enum LocationStatus {
  success,
  serviceDisabled,
  permissionDenied,
  permissionDeniedForever,
  error,
}

/// Resultado padronizado da consulta de geolocalização do usuário.
class UserLocationResult extends Equatable {
  final double? latitude;
  final double? longitude;
  final LocationStatus status;
  final String? message;

  const UserLocationResult.success({
    required double this.latitude,
    required double this.longitude,
  })  : status = LocationStatus.success,
        message = null;

  const UserLocationResult.serviceDisabled({
    this.message =
        'O serviço de localização (GPS) está desativado no seu aparelho.',
    this.latitude,
    this.longitude,
  }) : status = LocationStatus.serviceDisabled;

  const UserLocationResult.permissionDenied({
    this.message = 'Permissão de localização foi recusada.',
    this.latitude,
    this.longitude,
  }) : status = LocationStatus.permissionDenied;

  const UserLocationResult.permissionDeniedForever({
    this.message =
        'Permissão de localização negada permanentemente. Ative nas configurações do aparelho.',
    this.latitude,
    this.longitude,
  }) : status = LocationStatus.permissionDeniedForever;

  const UserLocationResult.error({
    required this.message,
    this.latitude,
    this.longitude,
  }) : status = LocationStatus.error;

  bool get isSuccess =>
      status == LocationStatus.success &&
      latitude != null &&
      longitude != null;

  @override
  List<Object?> get props => [latitude, longitude, status, message];
}

/// Serviço de obtenção e controle defensivo da posição geográfica do dispositivo.
class GeolocationService {
  /// Ponto de referência padrão (Praça da Sé, São Paulo) para fallback gracioso.
  static const double defaultLatitude = -23.5505;
  static const double defaultLongitude = -46.6333;

  final GeolocatorPlatform _geolocator;

  GeolocationService({GeolocatorPlatform? geolocator})
      : _geolocator = geolocator ?? GeolocatorPlatform.instance;

  /// Determina a posição do usuário tratando permissões e GPS desligado.
  /// Se [useFallbackOnFailure] for true, retorna coordenadas padrão em caso de indisponibilidade.
  Future<UserLocationResult> determinePosition({
    bool useFallbackOnFailure = true,
  }) async {
    try {
      // 1. Verifica se os serviços de GPS do sistema operacional estão ativos
      final isServiceEnabled = await _geolocator.isLocationServiceEnabled();
      if (!isServiceEnabled) {
        return UserLocationResult.serviceDisabled(
          latitude: useFallbackOnFailure ? defaultLatitude : null,
          longitude: useFallbackOnFailure ? defaultLongitude : null,
        );
      }

      // 2. Verifica e requisita permissões defensivamente
      LocationPermission permission = await _geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await _geolocator.requestPermission();
        if (permission == LocationPermission.denied) {
          return UserLocationResult.permissionDenied(
            latitude: useFallbackOnFailure ? defaultLatitude : null,
            longitude: useFallbackOnFailure ? defaultLongitude : null,
          );
        }
      }

      if (permission == LocationPermission.deniedForever) {
        return UserLocationResult.permissionDeniedForever(
          latitude: useFallbackOnFailure ? defaultLatitude : null,
          longitude: useFallbackOnFailure ? defaultLongitude : null,
        );
      }

      // 3. Tenta obter coordenadas atuais com limite de tempo defensivo
      final position = await _geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.medium,
          timeLimit: Duration(seconds: 5),
        ),
      );

      return UserLocationResult.success(
        latitude: position.latitude,
        longitude: position.longitude,
      );
    } catch (e) {
      // 4. Fallback para última posição conhecida se disponível
      try {
        final lastKnown = await _geolocator.getLastKnownPosition();
        if (lastKnown != null) {
          return UserLocationResult.success(
            latitude: lastKnown.latitude,
            longitude: lastKnown.longitude,
          );
        }
      } catch (_) {
        // Ignora falha de lastKnown
      }

      return UserLocationResult.error(
        message: 'Não foi possível capturar a localização atual.',
        latitude: useFallbackOnFailure ? defaultLatitude : null,
        longitude: useFallbackOnFailure ? defaultLongitude : null,
      );
    }
  }
}
