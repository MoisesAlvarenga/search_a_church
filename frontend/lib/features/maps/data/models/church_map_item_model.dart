import 'package:equatable/equatable.dart';

/// Origem do cadastro da congregação.
enum ChurchSource {
  app,
  maps;

  static ChurchSource fromJson(dynamic json) {
    if (json is int) {
      return json == 1 ? ChurchSource.maps : ChurchSource.app;
    }
    if (json is String) {
      final lower = json.toLowerCase();
      if (lower == 'maps') return ChurchSource.maps;
      return ChurchSource.app;
    }
    return ChurchSource.app;
  }

  String toJson() => name;
}

/// Modelo de item individual de igreja no mapa.
/// Representa tanto congregações cadastradas (App) quanto locais externos (Maps).
class ChurchMapItemModel extends Equatable {
  final String id;
  final String? placeId;
  final String name;
  final String address;
  final double latitude;
  final double longitude;
  final double distanceKm;
  final ChurchSource source;
  final bool isRegistered;
  final bool isVerifiedRepresentative;
  final double? ratingAverage;
  final int? reviewCount;
  final bool canClaim;

  const ChurchMapItemModel({
    required this.id,
    this.placeId,
    required this.name,
    required this.address,
    required this.latitude,
    required this.longitude,
    required this.distanceKm,
    required this.source,
    required this.isRegistered,
    required this.isVerifiedRepresentative,
    this.ratingAverage,
    this.reviewCount,
    required this.canClaim,
  });

  factory ChurchMapItemModel.fromJson(Map<String, dynamic> json) {
    return ChurchMapItemModel(
      id: json['id'] as String? ?? '',
      placeId: json['placeId'] as String?,
      name: json['name'] as String? ?? '',
      address: json['address'] as String? ?? '',
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0.0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0.0,
      distanceKm: (json['distanceKm'] as num?)?.toDouble() ?? 0.0,
      source: ChurchSource.fromJson(json['source']),
      isRegistered: json['isRegistered'] as bool? ?? false,
      isVerifiedRepresentative:
          json['isVerifiedRepresentative'] as bool? ?? false,
      ratingAverage: (json['ratingAverage'] as num?)?.toDouble(),
      reviewCount: (json['reviewCount'] as num?)?.toInt(),
      canClaim: json['canClaim'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'placeId': placeId,
      'name': name,
      'address': address,
      'latitude': latitude,
      'longitude': longitude,
      'distanceKm': distanceKm,
      'source': source.toJson(),
      'isRegistered': isRegistered,
      'isVerifiedRepresentative': isVerifiedRepresentative,
      'ratingAverage': ratingAverage,
      'reviewCount': reviewCount,
      'canClaim': canClaim,
    };
  }

  @override
  List<Object?> get props => [
        id,
        placeId,
        name,
        address,
        latitude,
        longitude,
        distanceKm,
        source,
        isRegistered,
        isVerifiedRepresentative,
        ratingAverage,
        reviewCount,
        canClaim,
      ];
}
