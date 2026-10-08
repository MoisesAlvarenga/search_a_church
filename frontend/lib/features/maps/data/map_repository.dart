import 'package:dio/dio.dart';
import 'models/church_map_item_model.dart';
import 'models/map_search_response_model.dart';

/// Contrato de repositório de dados de mapa.
abstract class IMapRepository {
  Future<MapSearchResponseModel> searchNearby({
    required double latitude,
    required double longitude,
    double radiusKm = 5.0,
    String? query,
  });

  Future<ChurchMapItemModel?> getPlaceDetails(String placeId);
}

/// Implementação do repositório de mapa consumindo a API protegida por JWT.
class MapRepository implements IMapRepository {
  final Dio _dio;

  MapRepository({required this._dio});

  @override
  Future<MapSearchResponseModel> searchNearby({
    required double latitude,
    required double longitude,
    double radiusKm = 5.0,
    String? query,
  }) async {
    final queryParams = <String, dynamic>{
      'lat': latitude,
      'lng': longitude,
      'radiusKm': radiusKm,
    };

    if (query != null && query.trim().isNotEmpty) {
      queryParams['query'] = query.trim();
    }

    final response = await _dio.get(
      '/map/search',
      queryParameters: queryParams,
    );

    return MapSearchResponseModel.fromJson(
      response.data as Map<String, dynamic>,
    );
  }

  @override
  Future<ChurchMapItemModel?> getPlaceDetails(String placeId) async {
    final response = await _dio.get('/map/places/$placeId');
    if (response.statusCode == 200 && response.data != null) {
      return ChurchMapItemModel.fromJson(response.data as Map<String, dynamic>);
    }
    return null;
  }
}
