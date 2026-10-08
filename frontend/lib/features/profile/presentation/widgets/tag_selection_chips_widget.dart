import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../cubit/tag_catalog_cubit.dart';
import '../cubit/tag_catalog_state.dart';

/// Componente interativo reutilizável para seleção de tags organizadas pelo catálogo oficial.
/// Agrupa as tags por categorias (Acessibilidade, Infraestrutura, Ministérios),
/// exibindo ícones contextuais e tooltips descritivos.
class TagSelectionChipsWidget extends StatelessWidget {
  final List<String> selectedTagCodes;
  final ValueChanged<List<String>>? onSelectionChanged;
  final bool readOnly;

  const TagSelectionChipsWidget({
    super.key,
    required this.selectedTagCodes,
    this.onSelectionChanged,
    this.readOnly = false,
  });

  IconData _getIconForName(String iconName) {
    switch (iconName.toLowerCase()) {
      case 'accessible':
        return Icons.accessible;
      case 'sign_language':
        return Icons.sign_language;
      case 'local_parking':
        return Icons.local_parking;
      case 'child_care':
      case 'culto_infantil':
        return Icons.child_care;
      case 'ac_unit':
        return Icons.ac_unit;
      case 'wc':
      case 'restroom':
        return Icons.wc;
      case 'people':
      case 'group':
        return Icons.people;
      case 'menu_book':
        return Icons.menu_book;
      default:
        return Icons.label_outline;
    }
  }

  void _toggleTag(String code) {
    if (readOnly || onSelectionChanged == null) return;

    final updated = List<String>.from(selectedTagCodes);
    if (updated.contains(code)) {
      updated.remove(code);
    } else {
      updated.add(code);
    }
    onSelectionChanged!(updated);
  }

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<TagCatalogCubit, TagCatalogState>(
      builder: (context, state) {
        if (state is TagCatalogLoading) {
          return const Center(
            child: Padding(
              padding: EdgeInsets.all(16.0),
              child: CircularProgressIndicator(),
            ),
          );
        }

        if (state is TagCatalogError) {
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 8.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Não foi possível carregar as tags do catálogo.',
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
                TextButton.icon(
                  onPressed: () => context
                      .read<TagCatalogCubit>()
                      .loadCatalog(forceRefresh: true),
                  icon: const Icon(Icons.refresh),
                  label: const Text('Tentar novamente'),
                ),
              ],
            ),
          );
        }

        if (state is TagCatalogLoaded) {
          final categories = state.catalog.categories;

          if (categories.isEmpty) {
            return const Text('Nenhuma tag disponível no momento.');
          }

          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: categories.map((cat) {
              return Padding(
                padding: const EdgeInsets.only(bottom: 16.0),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      cat.name,
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.bold,
                            color: Theme.of(context).colorScheme.primary,
                          ),
                    ),
                    const SizedBox(height: 8.0),
                    Wrap(
                      spacing: 8.0,
                      runSpacing: 4.0,
                      children: cat.tags.map((tag) {
                        final isSelected = selectedTagCodes.contains(tag.code);
                        return Tooltip(
                          message: tag.description,
                          child: FilterChip(
                            key: ValueKey('chip_${tag.code}'),
                            avatar: Icon(
                              _getIconForName(tag.iconName),
                              size: 18.0,
                              color: isSelected
                                  ? Theme.of(context).colorScheme.onSecondaryContainer
                                  : Theme.of(context).colorScheme.outline,
                            ),
                            label: Text(tag.name),
                            selected: isSelected,
                            onSelected: readOnly
                                ? null
                                : (_) => _toggleTag(tag.code),
                          ),
                        );
                      }).toList(),
                    ),
                  ],
                ),
              );
            }).toList(),
          );
        }

        return const SizedBox.shrink();
      },
    );
  }
}
