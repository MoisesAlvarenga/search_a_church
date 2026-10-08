import 'package:equatable/equatable.dart';
import 'meeting_schedule_model.dart';

/// Modelo de dados representativo do perfil completo da congregação.
class ChurchProfileModel extends Equatable {
  final String id;
  final String? placeId;
  final String name;
  final String address;
  final double latitude;
  final double longitude;
  final String? denomination;
  final String? worshipStyle;
  final List<String> languages;
  final String? phone;
  final String? email;
  final String? website;
  final String? socialInstagram;
  final String? socialFacebook;
  final String claimState;
  final String? verifiedRepresentativeUserId;
  final bool isActive;
  final String concurrencyStamp;
  final List<String> tags;
  final List<MeetingScheduleModel> schedules;

  const ChurchProfileModel({
    required this.id,
    this.placeId,
    required this.name,
    required this.address,
    required this.latitude,
    required this.longitude,
    this.denomination,
    this.worshipStyle,
    this.languages = const ['pt'],
    this.phone,
    this.email,
    this.website,
    this.socialInstagram,
    this.socialFacebook,
    this.claimState = 'Unclaimed',
    this.verifiedRepresentativeUserId,
    this.isActive = true,
    required this.concurrencyStamp,
    this.tags = const [],
    this.schedules = const [],
  });

  factory ChurchProfileModel.fromJson(Map<String, dynamic> json) {
    return ChurchProfileModel(
      id: json['id'] as String? ?? '',
      placeId: json['placeId'] as String?,
      name: json['name'] as String? ?? '',
      address: json['address'] as String? ?? '',
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0.0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0.0,
      denomination: json['denomination'] as String?,
      worshipStyle: json['worshipStyle'] as String?,
      languages: (json['languages'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          const ['pt'],
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      website: json['website'] as String?,
      socialInstagram: json['socialInstagram'] as String?,
      socialFacebook: json['socialFacebook'] as String?,
      claimState: json['claimState'] as String? ?? 'Unclaimed',
      verifiedRepresentativeUserId:
          json['verifiedRepresentativeUserId'] as String?,
      isActive: json['isActive'] as bool? ?? true,
      concurrencyStamp: json['concurrencyStamp'] as String? ?? '',
      tags: (json['tags'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          const [],
      schedules: (json['schedules'] as List<dynamic>?)
              ?.map((e) =>
                  MeetingScheduleModel.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'placeId': placeId,
        'name': name,
        'address': address,
        'latitude': latitude,
        'longitude': longitude,
        'denomination': denomination,
        'worshipStyle': worshipStyle,
        'languages': languages,
        'phone': phone,
        'email': email,
        'website': website,
        'socialInstagram': socialInstagram,
        'socialFacebook': socialFacebook,
        'claimState': claimState,
        'verifiedRepresentativeUserId': verifiedRepresentativeUserId,
        'isActive': isActive,
        'concurrencyStamp': concurrencyStamp,
        'tags': tags,
        'schedules': schedules.map((s) => s.toJson()).toList(),
      };

  ChurchProfileModel copyWith({
    String? id,
    String? placeId,
    String? name,
    String? address,
    double? latitude,
    double? longitude,
    String? denomination,
    String? worshipStyle,
    List<String>? languages,
    String? phone,
    String? email,
    String? website,
    String? socialInstagram,
    String? socialFacebook,
    String? claimState,
    String? verifiedRepresentativeUserId,
    bool? isActive,
    String? concurrencyStamp,
    List<String>? tags,
    List<MeetingScheduleModel>? schedules,
  }) {
    return ChurchProfileModel(
      id: id ?? this.id,
      placeId: placeId ?? this.placeId,
      name: name ?? this.name,
      address: address ?? this.address,
      latitude: latitude ?? this.latitude,
      longitude: longitude ?? this.longitude,
      denomination: denomination ?? this.denomination,
      worshipStyle: worshipStyle ?? this.worshipStyle,
      languages: languages ?? this.languages,
      phone: phone ?? this.phone,
      email: email ?? this.email,
      website: website ?? this.website,
      socialInstagram: socialInstagram ?? this.socialInstagram,
      socialFacebook: socialFacebook ?? this.socialFacebook,
      claimState: claimState ?? this.claimState,
      verifiedRepresentativeUserId:
          verifiedRepresentativeUserId ?? this.verifiedRepresentativeUserId,
      isActive: isActive ?? this.isActive,
      concurrencyStamp: concurrencyStamp ?? this.concurrencyStamp,
      tags: tags ?? this.tags,
      schedules: schedules ?? this.schedules,
    );
  }

  @override
  List<Object?> get props => [
        id,
        placeId,
        name,
        address,
        latitude,
        longitude,
        denomination,
        worshipStyle,
        languages,
        phone,
        email,
        website,
        socialInstagram,
        socialFacebook,
        claimState,
        verifiedRepresentativeUserId,
        isActive,
        concurrencyStamp,
        tags,
        schedules,
      ];
}

/// Payload para atualização do perfil da congregação.
class UpdateChurchProfileRequestModel extends Equatable {
  final String name;
  final String address;
  final double latitude;
  final double longitude;
  final String? placeId;
  final String? denomination;
  final String? worshipStyle;
  final List<String>? languages;
  final String? phone;
  final String? email;
  final String? website;
  final String? socialInstagram;
  final String? socialFacebook;
  final String? concurrencyStamp;
  final List<String>? tagCodes;
  final List<MeetingScheduleModel>? schedules;

  const UpdateChurchProfileRequestModel({
    required this.name,
    required this.address,
    required this.latitude,
    required this.longitude,
    this.placeId,
    this.denomination,
    this.worshipStyle,
    this.languages,
    this.phone,
    this.email,
    this.website,
    this.socialInstagram,
    this.socialFacebook,
    this.concurrencyStamp,
    this.tagCodes,
    this.schedules,
  });

  Map<String, dynamic> toJson() {
    final map = <String, dynamic>{
      'name': name,
      'address': address,
      'latitude': latitude,
      'longitude': longitude,
    };
    if (placeId != null) map['placeId'] = placeId;
    if (denomination != null) map['denomination'] = denomination;
    if (worshipStyle != null) map['worshipStyle'] = worshipStyle;
    if (languages != null) map['languages'] = languages;
    if (phone != null) map['phone'] = phone;
    if (email != null) map['email'] = email;
    if (website != null) map['website'] = website;
    if (socialInstagram != null) map['socialInstagram'] = socialInstagram;
    if (socialFacebook != null) map['socialFacebook'] = socialFacebook;
    if (concurrencyStamp != null) map['concurrencyStamp'] = concurrencyStamp;
    if (tagCodes != null) map['tagCodes'] = tagCodes;
    if (schedules != null) {
      map['schedules'] = schedules!.map((s) => s.toJson()).toList();
    }
    return map;
  }

  @override
  List<Object?> get props => [
        name,
        address,
        latitude,
        longitude,
        placeId,
        denomination,
        worshipStyle,
        languages,
        phone,
        email,
        website,
        socialInstagram,
        socialFacebook,
        concurrencyStamp,
        tagCodes,
        schedules,
      ];
}

/// Payload para alternância de status de atividade.
class UpdateChurchStatusRequestModel extends Equatable {
  final bool isActive;
  final String? concurrencyStamp;

  const UpdateChurchStatusRequestModel({
    required this.isActive,
    this.concurrencyStamp,
  });

  Map<String, dynamic> toJson() {
    final map = <String, dynamic>{'isActive': isActive};
    if (concurrencyStamp != null) {
      map['concurrencyStamp'] = concurrencyStamp;
    }
    return map;
  }

  @override
  List<Object?> get props => [isActive, concurrencyStamp];
}

/// Resposta de atualização de status de atividade.
class ChurchStatusResponseModel extends Equatable {
  final String churchId;
  final bool isActive;
  final String message;

  const ChurchStatusResponseModel({
    required this.churchId,
    required this.isActive,
    required this.message,
  });

  factory ChurchStatusResponseModel.fromJson(Map<String, dynamic> json) {
    return ChurchStatusResponseModel(
      churchId: json['churchId'] as String? ?? '',
      isActive: json['isActive'] as bool? ?? true,
      message: json['message'] as String? ?? '',
    );
  }

  Map<String, dynamic> toJson() => {
        'churchId': churchId,
        'isActive': isActive,
        'message': message,
      };

  @override
  List<Object?> get props => [churchId, isActive, message];
}
