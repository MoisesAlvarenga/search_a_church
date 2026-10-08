import 'package:dio/dio.dart';
import '../datasources/profile_remote_data_source.dart';
import '../failures/profile_failures.dart';
import '../models/church_profile_model.dart';
import '../models/tag_catalog_model.dart';
import '../models/user_profile_model.dart';

/// Contrato do repositório da funcionalidade de gestão de perfis e tags.
abstract class IProfileRepository {
  Future<UserProfileModel> getUserProfile();

  Future<UserProfileModel> updateUserProfile(
      UpdateUserProfileRequestModel request);

  Future<DeleteAccountResponseModel> deleteUserAccount();

  Future<ChurchProfileModel> getChurchProfile(String churchId);

  Future<ChurchProfileModel> updateChurchProfile(
    String churchId,
    UpdateChurchProfileRequestModel request, {
    String? ifMatchHeader,
  });

  Future<ChurchStatusResponseModel> setChurchStatus(
    String churchId,
    UpdateChurchStatusRequestModel request, {
    String? ifMatchHeader,
  });

  Future<TagCatalogModel> getTagCatalog();
}

/// Implementação do repositório interceptando e convertendo exceções HTTP em falhas de domínio.
class ProfileRepository implements IProfileRepository {
  final IProfileRemoteDataSource _remoteDataSource;

  ProfileRepository({required IProfileRemoteDataSource remoteDataSource})
      : _remoteDataSource = remoteDataSource;

  @override
  Future<UserProfileModel> getUserProfile() =>
      _guard(() => _remoteDataSource.getUserProfile());

  @override
  Future<UserProfileModel> updateUserProfile(
          UpdateUserProfileRequestModel request) =>
      _guard(() => _remoteDataSource.updateUserProfile(request));

  @override
  Future<DeleteAccountResponseModel> deleteUserAccount() =>
      _guard(() => _remoteDataSource.deleteUserAccount());

  @override
  Future<ChurchProfileModel> getChurchProfile(String churchId) =>
      _guard(() => _remoteDataSource.getChurchProfile(churchId));

  @override
  Future<ChurchProfileModel> updateChurchProfile(
    String churchId,
    UpdateChurchProfileRequestModel request, {
    String? ifMatchHeader,
  }) =>
      _guard(() => _remoteDataSource.updateChurchProfile(
            churchId,
            request,
            ifMatchHeader: ifMatchHeader,
          ));

  @override
  Future<ChurchStatusResponseModel> setChurchStatus(
    String churchId,
    UpdateChurchStatusRequestModel request, {
    String? ifMatchHeader,
  }) =>
      _guard(() => _remoteDataSource.setChurchStatus(
            churchId,
            request,
            ifMatchHeader: ifMatchHeader,
          ));

  @override
  Future<TagCatalogModel> getTagCatalog() =>
      _guard(() => _remoteDataSource.getTagCatalog());

  Future<T> _guard<T>(Future<T> Function() call) async {
    try {
      return await call();
    } on DioException catch (e) {
      throw _mapDioException(e);
    } catch (e) {
      if (e is ProfileFailure) rethrow;
      throw ProfileFailure('Erro inesperado durante a operação: $e');
    }
  }

  ProfileFailure _mapDioException(DioException e) {
    final statusCode = e.response?.statusCode;
    final data = e.response?.data;

    String message = e.message ?? 'Falha de comunicação com o servidor';
    String? errorCode;

    if (data is Map<String, dynamic>) {
      message = data['message'] as String? ?? message;
      errorCode = data['error'] as String?;
    }

    if (statusCode == 401) {
      return UnauthorizedFailure(
        message,
        errorCode: errorCode ?? 'UNAUTHORIZED',
        statusCode: statusCode,
      );
    }

    if (statusCode == 403 ||
        errorCode == 'ACESSO_NEGADO_PROPRIEDADE' ||
        errorCode == 'REPRESENTANTE_NAO_VERIFICADO') {
      return ForbiddenFailure(
        message,
        errorCode: errorCode ?? 'FORBIDDEN',
        statusCode: statusCode ?? 403,
      );
    }

    if (statusCode == 404 ||
        errorCode == 'IGREJA_NAO_ENCONTRADA' ||
        errorCode == 'USUARIO_NAO_ENCONTRADO') {
      return ProfileNotFoundFailure(
        message,
        errorCode: errorCode ?? 'NOT_FOUND',
        statusCode: statusCode ?? 404,
      );
    }

    if (statusCode == 409 ||
        errorCode == 'PLACE_ID_JA_VINCULADO' ||
        errorCode == 'CONFLITO_CONCORRENCIA') {
      return ConflictFailure(
        message,
        errorCode: errorCode ?? 'CONFLICT',
        statusCode: statusCode ?? 409,
      );
    }

    if (errorCode == 'TAG_INVALIDA') {
      return InvalidTagFailure(
        message,
        errorCode: errorCode,
        statusCode: statusCode ?? 400,
      );
    }

    if (errorCode == 'RAIO_INVALIDO') {
      return InvalidRadiusFailure(
        message,
        errorCode: errorCode,
        statusCode: statusCode ?? 400,
      );
    }

    return ProfileFailure(
      message,
      errorCode: errorCode,
      statusCode: statusCode,
    );
  }
}
