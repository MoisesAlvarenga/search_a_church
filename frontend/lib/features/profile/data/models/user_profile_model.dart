import 'package:equatable/equatable.dart';

/// Modelo de dados consolidado com preferências e perfil do usuário autenticado.
class UserProfileModel extends Equatable {
  final String userId;
  final String name;
  final String email;
  final String? denomination;
  final String? worshipStyle;
  final List<String> preferredLanguages;
  final double defaultRadiusKm;
  final List<String> selectedTags;
  final bool isConfigured;

  const UserProfileModel({
    required this.userId,
    required this.name,
    required this.email,
    this.denomination,
    this.worshipStyle,
    this.preferredLanguages = const ['pt'],
    this.defaultRadiusKm = 10.0,
    this.selectedTags = const [],
    this.isConfigured = false,
  });

  factory UserProfileModel.fromJson(Map<String, dynamic> json) {
    return UserProfileModel(
      userId: json['userId'] as String? ?? '',
      name: json['name'] as String? ?? '',
      email: json['email'] as String? ?? '',
      denomination: json['denomination'] as String?,
      worshipStyle: json['worshipStyle'] as String?,
      preferredLanguages: (json['preferredLanguages'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          const ['pt'],
      defaultRadiusKm: (json['defaultRadiusKm'] as num?)?.toDouble() ?? 10.0,
      selectedTags: (json['selectedTags'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          const [],
      isConfigured: json['isConfigured'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() => {
        'userId': userId,
        'name': name,
        'email': email,
        'denomination': denomination,
        'worshipStyle': worshipStyle,
        'preferredLanguages': preferredLanguages,
        'defaultRadiusKm': defaultRadiusKm,
        'selectedTags': selectedTags,
        'isConfigured': isConfigured,
      };

  UserProfileModel copyWith({
    String? userId,
    String? name,
    String? email,
    String? denomination,
    String? worshipStyle,
    List<String>? preferredLanguages,
    double? defaultRadiusKm,
    List<String>? selectedTags,
    bool? isConfigured,
  }) {
    return UserProfileModel(
      userId: userId ?? this.userId,
      name: name ?? this.name,
      email: email ?? this.email,
      denomination: denomination ?? this.denomination,
      worshipStyle: worshipStyle ?? this.worshipStyle,
      preferredLanguages: preferredLanguages ?? this.preferredLanguages,
      defaultRadiusKm: defaultRadiusKm ?? this.defaultRadiusKm,
      selectedTags: selectedTags ?? this.selectedTags,
      isConfigured: isConfigured ?? this.isConfigured,
    );
  }

  @override
  List<Object?> get props => [
        userId,
        name,
        email,
        denomination,
        worshipStyle,
        preferredLanguages,
        defaultRadiusKm,
        selectedTags,
        isConfigured,
      ];
}

/// Payload de requisição para atualização ou salvamento de preferências de perfil.
class UpdateUserProfileRequestModel extends Equatable {
  final String? denomination;
  final String? worshipStyle;
  final List<String>? preferredLanguages;
  final double defaultRadiusKm;
  final List<String>? tagCodes;
  final String? targetUserId;

  const UpdateUserProfileRequestModel({
    this.denomination,
    this.worshipStyle,
    this.preferredLanguages,
    this.defaultRadiusKm = 10.0,
    this.tagCodes,
    this.targetUserId,
  });

  Map<String, dynamic> toJson() {
    final map = <String, dynamic>{
      'defaultRadiusKm': defaultRadiusKm,
    };
    if (denomination != null) map['denomination'] = denomination;
    if (worshipStyle != null) map['worshipStyle'] = worshipStyle;
    if (preferredLanguages != null) map['preferredLanguages'] = preferredLanguages;
    if (tagCodes != null) map['tagCodes'] = tagCodes;
    if (targetUserId != null) map['targetUserId'] = targetUserId;
    return map;
  }

  @override
  List<Object?> get props => [
        denomination,
        worshipStyle,
        preferredLanguages,
        defaultRadiusKm,
        tagCodes,
        targetUserId,
      ];
}

/// Resposta de encerramento de conta sob conformidade da LGPD.
class DeleteAccountResponseModel extends Equatable {
  final bool success;
  final String message;

  const DeleteAccountResponseModel({
    required this.success,
    required this.message,
  });

  factory DeleteAccountResponseModel.fromJson(Map<String, dynamic> json) {
    return DeleteAccountResponseModel(
      success: json['success'] as bool? ?? false,
      message: json['message'] as String? ?? '',
    );
  }

  Map<String, dynamic> toJson() => {
        'success': success,
        'message': message,
      };

  @override
  List<Object?> get props => [success, message];
}
