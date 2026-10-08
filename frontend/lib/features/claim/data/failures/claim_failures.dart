import 'package:equatable/equatable.dart';

/// Falha base para operações de reivindicação de perfil e disputas.
class ClaimFailure extends Equatable implements Exception {
  final String message;
  final String? errorCode;
  final int? statusCode;

  const ClaimFailure(
    this.message, {
    this.errorCode,
    this.statusCode,
  });

  @override
  List<Object?> get props => [message, errorCode, statusCode];

  @override
  String toString() => 'ClaimFailure(message: $message, errorCode: $errorCode, statusCode: $statusCode)';
}

/// Falha na validação presencial / geofencing (raio > 100m, precisão > 50m).
class GeofenceFailure extends ClaimFailure {
  final double? distanceMeters;
  final double? accuracyMeters;

  const GeofenceFailure(
    super.message, {
    super.errorCode,
    super.statusCode,
    this.distanceMeters,
    this.accuracyMeters,
  });

  @override
  List<Object?> get props => [message, errorCode, statusCode, distanceMeters, accuracyMeters];
}

/// Falha por detecção de localização simulada (mock location / fake GPS).
class MockLocationFailure extends GeofenceFailure {
  const MockLocationFailure(
    super.message, {
    super.errorCode = 'LOCALIZACAO_SIMULADA_DETECTADA',
    super.statusCode = 400,
    super.distanceMeters,
    super.accuracyMeters,
  });
}

/// Falha em processos de contestação ou litígio paritário.
class DisputeFailure extends ClaimFailure {
  final String? disputeId;

  const DisputeFailure(
    super.message, {
    super.errorCode,
    super.statusCode,
    this.disputeId,
  });

  @override
  List<Object?> get props => [message, errorCode, statusCode, disputeId];
}

/// Conflito de regras de negócio (HTTP 409 - igreja já verificada ou em disputa).
class ConflictFailure extends ClaimFailure {
  const ConflictFailure(
    super.message, {
    super.errorCode,
    super.statusCode = 409,
  });
}

/// Recurso não encontrado (HTTP 404 - igreja ou disputa inexistente).
class NotFoundFailure extends ClaimFailure {
  const NotFoundFailure(
    super.message, {
    super.errorCode,
    super.statusCode = 404,
  });
}

/// Falha de autenticação (HTTP 401 - token JWT ausente ou expirado).
class UnauthorizedFailure extends ClaimFailure {
  const UnauthorizedFailure(
    super.message, {
    super.errorCode = 'UNAUTHORIZED',
    super.statusCode = 401,
  });
}
