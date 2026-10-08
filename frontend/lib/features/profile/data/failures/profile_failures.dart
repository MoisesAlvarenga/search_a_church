import 'package:equatable/equatable.dart';

/// Falha base para operações no módulo de gestão de perfis e catálogo de tags.
class ProfileFailure extends Equatable implements Exception {
  final String message;
  final String? errorCode;
  final int? statusCode;

  const ProfileFailure(
    this.message, {
    this.errorCode,
    this.statusCode,
  });

  @override
  List<Object?> get props => [message, errorCode, statusCode];

  @override
  String toString() =>
      'ProfileFailure(message: $message, errorCode: $errorCode, statusCode: $statusCode)';
}

/// Falha de autenticação obrigatória (HTTP 401).
class UnauthorizedFailure extends ProfileFailure {
  const UnauthorizedFailure(
    super.message, {
    super.errorCode = 'UNAUTHORIZED',
    super.statusCode = 401,
  });
}

/// Falha de autorização granular (HTTP 403): violação de titularidade (ACESSO_NEGADO_PROPRIEDADE)
/// ou tentativa de alteração sem credencial de representante verificado (REPRESENTANTE_NAO_VERIFICADO).
class ForbiddenFailure extends ProfileFailure {
  const ForbiddenFailure(
    super.message, {
    super.errorCode,
    super.statusCode = 403,
  });
}

/// Falha de conflito (HTTP 409): colisão de Google Maps Place ID (PLACE_ID_JA_VINCULADO)
/// ou conflito de concorrência otimista (CONFLITO_CONCORRENCIA).
class ConflictFailure extends ProfileFailure {
  const ConflictFailure(
    super.message, {
    super.errorCode,
    super.statusCode = 409,
  });
}

/// Falha de validação: tentativa de associar tag não catalogada ou inativa (TAG_INVALIDA).
class InvalidTagFailure extends ProfileFailure {
  const InvalidTagFailure(
    super.message, {
    super.errorCode = 'TAG_INVALIDA',
    super.statusCode = 400,
  });
}

/// Falha de validação: raio informado fora dos limites operacionais 1.0 a 100.0 km (RAIO_INVALIDO).
class InvalidRadiusFailure extends ProfileFailure {
  const InvalidRadiusFailure(
    super.message, {
    super.errorCode = 'RAIO_INVALIDO',
    super.statusCode = 400,
  });
}

/// Falha de recurso não encontrado (HTTP 404): perfil de usuário ou igreja inexistente.
class ProfileNotFoundFailure extends ProfileFailure {
  const ProfileNotFoundFailure(
    super.message, {
    super.errorCode,
    super.statusCode = 404,
  });
}
