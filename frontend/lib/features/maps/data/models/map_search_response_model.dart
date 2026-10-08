import 'package:equatable/equatable.dart';
import 'church_map_item_model.dart';

/// Resposta deserializada do endpoint /map/search.
class MapSearchResponseModel extends Equatable {
  final List<ChurchMapItemModel> results;
  final double centerLatitude;
  final double centerLongitude;
  final double appliedRadiusKm;
  final bool isDegraded;
  final String? degradedMessage;

  const MapSearchResponseModel({
    required this.results,
    required this.centerLatitude,
    required this.centerLongitude,
    required this.appliedRadiusKm,
    required this.isDegraded,
    this.degradedMessage,
  });

  factory MapSearchResponseModel.fromJson(Map<String, dynamic> json) {
    final rawList = json['results'] as List<dynamic>? ?? [];
    final results = rawList
        .map((item) =>
            ChurchMapItemModel.fromJson(item as Map<String, dynamic>))
        .toList();

    return MapSearchResponseModel(
      results: results,
      centerLatitude: (json['centerLatitude'] as num?)?.toDouble() ?? 0.0,
      centerLongitude: (json['centerLongitude'] as num?)?.toDouble() ?? 0.0,
      appliedRadiusKm: (json['appliedRadiusKm'] as num?)?.toDouble() ?? 5.0,
      isDegraded: json['isDegraded'] as bool? ?? false,
      degradedMessage: json['degradedMessage'] as String?,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'results': results.map((r) => r.toJson()).toList(),
      'centerLatitude': centerLatitude,
      'centerLongitude': centerLongitude,
      'appliedRadiusKm': appliedRadiusKm,
      'isDegraded': isDegraded,
      'degradedMessage': degradedMessage,
    };
  }

  @override
  List<Object?> get props => [
        results,
        centerLatitude,
        centerLongitude,
        appliedRadiusKm,
        isDegraded,
        degradedMessage,
      ];
}
