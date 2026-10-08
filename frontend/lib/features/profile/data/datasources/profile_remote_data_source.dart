import 'package:dio/dio.dart';
import '../models/church_profile_model.dart';
import '../models/tag_catalog_model.dart';
import '../models/user_profile_model.dart';

/// Contrato da fonte de dados remota para consumo das rotas /profile/* e /tags/catalog.
abstract class IProfileRemoteDataSource {
  /// Consulta dados e preferências do perfil do usuário autenticado.
  Future<UserProfileModel> getUserProfile();

  /// Salva ou atualiza as preferências do usuário no backend.
  Future<UserProfileModel> updateUserProfile(
      UpdateUserProfileRequestModel request);

  /// Executa encerramento e anonimização de conta sob a LGPD.
  Future<DeleteAccountResponseModel> deleteUserAccount();

  /// Consulta os dados completos ou públicos de uma congregação.
  Future<ChurchProfileModel> getChurchProfile(String churchId);

  /// Atualiza os dados da igreja, cultos e tags por representante verificado.
  Future<ChurchProfileModel> updateChurchProfile(
    String churchId,
    UpdateChurchProfileRequestModel request, {
    String? ifMatchHeader,
  });

  /// Altera o status de atividade da igreja (IsActive) por representante verificado.
  Future<ChurchStatusResponseModel> setChurchStatus(
    String churchId,
    UpdateChurchStatusRequestModel request, {
    String? ifMatchHeader,
  });

  /// Consulta o catálogo oficial de tags ativas agrupadas por categoria.
  Future<TagCatalogModel> getTagCatalog();
}

/// Implementação da fonte de dados remota utilizando Dio.
class ProfileRemoteDataSource implements IProfileRemoteDataSource {
  final Dio _dio;

  ProfileRemoteDataSource({required Dio dio}) : _dio = dio;

  @override
  Future<UserProfileModel> getUserProfile() async {
    final response = await _dio.get('/profile/user');
    return UserProfileModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<UserProfileModel> updateUserProfile(
      UpdateUserProfileRequestModel request) async {
    final response = await _dio.put(
      '/profile/user',
      data: request.toJson(),
    );
    return UserProfileModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<DeleteAccountResponseModel> deleteUserAccount() async {
    final response = await _dio.delete('/profile/user');
    return DeleteAccountResponseModel.fromJson(
        response.data as Map<String, dynamic>);
  }

  @override
  Future<ChurchProfileModel> getChurchProfile(String churchId) async {
    final response = await _dio.get('/profile/church/$churchId');
    return ChurchProfileModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<ChurchProfileModel> updateChurchProfile(
    String churchId,
    UpdateChurchProfileRequestModel request, {
    String? ifMatchHeader,
  }) async {
    final options = ifMatchHeader != null
        ? Options(headers: {'If-Match': ifMatchHeader})
        : null;

    final response = await _dio.put(
      '/profile/church/$churchId',
      data: request.toJson(),
      options: options,
    );
    return ChurchProfileModel.fromJson(response.data as Map<String, dynamic>);
  }

  @override
  Future<ChurchStatusResponseModel> setChurchStatus(
    String churchId,
    UpdateChurchStatusRequestModel request, {
    String? ifMatchHeader,
  }) async {
    final options = ifMatchHeader != null
        ? Options(headers: {'If-Match': ifMatchHeader})
        : null;

    final response = await _dio.patch(
      '/profile/church/$churchId/status',
      data: request.toJson(),
      options: options,
    );
    return ChurchStatusResponseModel.fromJson(
        response.data as Map<String, dynamic>);
  }

  @override
  Future<TagCatalogModel> getTagCatalog() async {
    final response = await _dio.get('/tags/catalog');
    return TagCatalogModel.fromJson(response.data as Map<String, dynamic>);
  }
}
