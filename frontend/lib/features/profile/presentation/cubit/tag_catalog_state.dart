import 'package:equatable/equatable.dart';
import '../../data/models/tag_catalog_model.dart';

/// Estado base para o catálogo oficial de tags do sistema.
abstract class TagCatalogState extends Equatable {
  const TagCatalogState();

  @override
  List<Object?> get props => [];
}

/// Estado inicial antes de carregar o catálogo.
class TagCatalogInitial extends TagCatalogState {
  const TagCatalogInitial();
}

/// Estado durante a consulta ao catálogo de tags.
class TagCatalogLoading extends TagCatalogState {
  const TagCatalogLoading();
}

/// Estado com o catálogo oficial carregado em memória.
class TagCatalogLoaded extends TagCatalogState {
  final TagCatalogModel catalog;

  const TagCatalogLoaded(this.catalog);

  @override
  List<Object?> get props => [catalog];
}

/// Estado de falha ao recuperar o catálogo oficial.
class TagCatalogError extends TagCatalogState {
  final String message;
  final String? errorCode;
  final int? statusCode;

  const TagCatalogError(
    this.message, {
    this.errorCode,
    this.statusCode,
  });

  @override
  List<Object?> get props => [message, errorCode, statusCode];
}
