import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/profile/data/models/tag_catalog_model.dart';
import 'package:search_a_church_app/features/profile/data/repositories/profile_repository.dart';
import 'package:search_a_church_app/features/profile/presentation/cubit/tag_catalog_cubit.dart';
import 'package:search_a_church_app/features/profile/presentation/widgets/tag_selection_chips_widget.dart';

class MockProfileRepository extends Mock implements IProfileRepository {}

void main() {
  late MockProfileRepository mockRepository;
  late TagCatalogCubit tagCatalogCubit;

  const mockCatalog = TagCatalogModel(
    categories: [
      TagCategoryModel(
        id: 1,
        name: 'Acessibilidade',
        tags: [
          TagItemModel(
            code: 'rampa_acesso',
            name: 'Rampa de Acesso',
            description: 'Acesso a cadeirantes',
            iconName: 'accessible',
          ),
          TagItemModel(
            code: 'interprete_libras',
            name: 'LIBRAS',
            description: 'Intérprete de sinais',
            iconName: 'sign_language',
          ),
        ],
      ),
      TagCategoryModel(
        id: 2,
        name: 'Infraestrutura',
        tags: [
          TagItemModel(
            code: 'estacionamento_proprio',
            name: 'Estacionamento Próprio',
            description: 'Vagas exclusivas',
            iconName: 'local_parking',
          ),
        ],
      ),
    ],
  );

  setUp(() {
    mockRepository = MockProfileRepository();
    tagCatalogCubit = TagCatalogCubit(repository: mockRepository);
  });

  tearDown(() {
    tagCatalogCubit.close();
  });

  Widget buildWidget({
    List<String> selectedTags = const [],
    ValueChanged<List<String>>? onSelectionChanged,
    bool readOnly = false,
  }) {
    return BlocProvider<TagCatalogCubit>.value(
      value: tagCatalogCubit,
      child: MaterialApp(
        home: Scaffold(
          body: TagSelectionChipsWidget(
            selectedTagCodes: selectedTags,
            onSelectionChanged: onSelectionChanged,
            readOnly: readOnly,
          ),
        ),
      ),
    );
  }

  group('TagSelectionChipsWidget', () {
    testWidgets('renders categories and chips when catalog is loaded',
        (tester) async {
      when(() => mockRepository.getTagCatalog())
          .thenAnswer((_) async => mockCatalog);

      await tester.pumpWidget(
        buildWidget(selectedTags: ['rampa_acesso']),
      );

      await tagCatalogCubit.loadCatalog();
      await tester.pumpAndSettle();

      expect(find.text('Acessibilidade'), findsOneWidget);
      expect(find.text('Infraestrutura'), findsOneWidget);
      expect(find.text('Rampa de Acesso'), findsOneWidget);
      expect(find.text('LIBRAS'), findsOneWidget);
      expect(find.text('Estacionamento Próprio'), findsOneWidget);

      final rampaChip = tester.widget<FilterChip>(
          find.byKey(const ValueKey('chip_rampa_acesso')));
      expect(rampaChip.selected, isTrue);

      final librasChip = tester.widget<FilterChip>(
          find.byKey(const ValueKey('chip_interprete_libras')));
      expect(librasChip.selected, isFalse);
    });

    testWidgets('toggling chip calls onSelectionChanged', (tester) async {
      when(() => mockRepository.getTagCatalog())
          .thenAnswer((_) async => mockCatalog);

      List<String>? updatedSelection;

      await tester.pumpWidget(
        buildWidget(
          selectedTags: ['rampa_acesso'],
          onSelectionChanged: (tags) => updatedSelection = tags,
        ),
      );

      await tagCatalogCubit.loadCatalog();
      await tester.pumpAndSettle();

      // Tap unselected chip (LIBRAS) -> adds to list
      await tester.tap(find.byKey(const ValueKey('chip_interprete_libras')));
      await tester.pump();

      expect(updatedSelection, ['rampa_acesso', 'interprete_libras']);

      // Tap selected chip (Rampa) -> removes from list
      await tester.tap(find.byKey(const ValueKey('chip_rampa_acesso')));
      await tester.pump();

      expect(updatedSelection, []);
    });

    testWidgets('readOnly chip cannot be toggled', (tester) async {
      when(() => mockRepository.getTagCatalog())
          .thenAnswer((_) async => mockCatalog);

      List<String>? updatedSelection;

      await tester.pumpWidget(
        buildWidget(
          selectedTags: ['rampa_acesso'],
          onSelectionChanged: (tags) => updatedSelection = tags,
          readOnly: true,
        ),
      );

      await tagCatalogCubit.loadCatalog();
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('chip_interprete_libras')));
      await tester.pump();

      expect(updatedSelection, isNull);
    });
  });
}
