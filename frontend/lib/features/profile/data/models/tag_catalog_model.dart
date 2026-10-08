import 'package:equatable/equatable.dart';

/// Item individual de tag oficial do catálogo (acessibilidade, infraestrutura ou ministérios).
class TagItemModel extends Equatable {
  final String code;
  final String name;
  final String description;
  final String iconName;

  const TagItemModel({
    required this.code,
    required this.name,
    required this.description,
    required this.iconName,
  });

  factory TagItemModel.fromJson(Map<String, dynamic> json) {
    return TagItemModel(
      code: json['code'] as String? ?? '',
      name: json['name'] as String? ?? '',
      description: json['description'] as String? ?? '',
      iconName: json['iconName'] as String? ?? '',
    );
  }

  Map<String, dynamic> toJson() => {
        'code': code,
        'name': name,
        'description': description,
        'iconName': iconName,
      };

  @override
  List<Object?> get props => [code, name, description, iconName];
}

/// Categoria agregadora de tags do catálogo oficial.
class TagCategoryModel extends Equatable {
  final int id;
  final String name;
  final List<TagItemModel> tags;

  const TagCategoryModel({
    required this.id,
    required this.name,
    required this.tags,
  });

  factory TagCategoryModel.fromJson(Map<String, dynamic> json) {
    return TagCategoryModel(
      id: (json['id'] as num?)?.toInt() ?? 0,
      name: json['name'] as String? ?? '',
      tags: (json['tags'] as List<dynamic>?)
              ?.map((e) => TagItemModel.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'name': name,
        'tags': tags.map((t) => t.toJson()).toList(),
      };

  @override
  List<Object?> get props => [id, name, tags];
}

/// Resposta completa com o catálogo oficial agrupado por categorias.
class TagCatalogModel extends Equatable {
  final List<TagCategoryModel> categories;

  const TagCatalogModel({
    required this.categories,
  });

  factory TagCatalogModel.fromJson(Map<String, dynamic> json) {
    return TagCatalogModel(
      categories: (json['categories'] as List<dynamic>?)
              ?.map((e) => TagCategoryModel.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
    );
  }

  Map<String, dynamic> toJson() => {
        'categories': categories.map((c) => c.toJson()).toList(),
      };

  /// Lista plana de todas as tags ativas no catálogo.
  List<TagItemModel> get allTags =>
      categories.expand((c) => c.tags).toList(growable: false);

  @override
  List<Object?> get props => [categories];
}
