import 'package:flutter_bloc/flutter_bloc.dart';
import '../../data/failures/profile_failures.dart';
import '../../data/models/tag_catalog_model.dart';
import '../../data/repositories/profile_repository.dart';
import 'tag_catalog_state.dart';

/// Cubit responsável por carregar e disponibilizar o catálogo oficial de tags com cache em memória.
class TagCatalogCubit extends Cubit<TagCatalogState> {
  final IProfileRepository _repository;

  TagCatalogCubit({required IProfileRepository repository})
      : _repository = repository,
        super(const TagCatalogInitial());

  /// Retorna o catálogo atualmente carregado em memória, se disponível.
  TagCatalogModel? get currentCatalog =>
      state is TagCatalogLoaded ? (state as TagCatalogLoaded).catalog : null;

  /// Carrega as tags oficiais ativas agrupadas por categoria.
  /// Se o catálogo já estiver carregado e [forceRefresh] for false, reutiliza os dados em memória.
  Future<void> loadCatalog({bool forceRefresh = false}) async {
    if (!forceRefresh && state is TagCatalogLoaded) {
      return;
    }

    emit(const TagCatalogLoading());
    try {
      final catalog = await _repository.getTagCatalog();
      emit(TagCatalogLoaded(catalog));
    } catch (e) {
      emit(_mapError(e));
    }
  }

  TagCatalogError _mapError(Object error) {
    if (error is ProfileFailure) {
      return TagCatalogError(
        error.message,
        errorCode: error.errorCode,
        statusCode: error.statusCode,
      );
    }
    return TagCatalogError('Erro inesperado: $error');
  }
}
