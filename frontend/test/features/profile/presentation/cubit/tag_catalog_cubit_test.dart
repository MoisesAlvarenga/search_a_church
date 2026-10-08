import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/profile/data/failures/profile_failures.dart';
import 'package:search_a_church_app/features/profile/data/models/tag_catalog_model.dart';
import 'package:search_a_church_app/features/profile/data/repositories/profile_repository.dart';
import 'package:search_a_church_app/features/profile/presentation/cubit/tag_catalog_cubit.dart';
import 'package:search_a_church_app/features/profile/presentation/cubit/tag_catalog_state.dart';

class MockProfileRepository extends Mock implements IProfileRepository {}

void main() {
  late MockProfileRepository mockRepository;

  setUp(() {
    mockRepository = MockProfileRepository();
  });

  group('TagCatalogCubit', () {
    const mockCatalog = TagCatalogModel(
      categories: [
        TagCategoryModel(
          id: 1,
          name: 'Acessibilidade',
          tags: [
            TagItemModel(
              code: 'rampa_acesso',
              name: 'Rampa de Acesso',
              description: 'Acessibilidade para cadeirantes',
              iconName: 'accessible',
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
              description: 'Vagas no local',
              iconName: 'local_parking',
            ),
          ],
        ),
      ],
    );

    test('initial state is TagCatalogInitial and currentCatalog is null', () {
      final cubit = TagCatalogCubit(repository: mockRepository);
      expect(cubit.state, isA<TagCatalogInitial>());
      expect(cubit.currentCatalog, isNull);
    });

    blocTest<TagCatalogCubit, TagCatalogState>(
      'loadCatalog emits [TagCatalogLoading, TagCatalogLoaded] on success',
      build: () {
        when(() => mockRepository.getTagCatalog())
            .thenAnswer((_) async => mockCatalog);
        return TagCatalogCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.loadCatalog(),
      expect: () => [
        isA<TagCatalogLoading>(),
        isA<TagCatalogLoaded>()
            .having((s) => s.catalog.categories.length, 'categories.length', 2)
            .having((s) => s.catalog.allTags.length, 'allTags.length', 2),
      ],
      verify: (cubit) {
        verify(() => mockRepository.getTagCatalog()).called(1);
        expect(cubit.currentCatalog, mockCatalog);
      },
    );

    blocTest<TagCatalogCubit, TagCatalogState>(
      'loadCatalog uses in-memory cache and does not re-fetch when already loaded and forceRefresh is false',
      build: () => TagCatalogCubit(repository: mockRepository),
      seed: () => const TagCatalogLoaded(mockCatalog),
      act: (cubit) => cubit.loadCatalog(forceRefresh: false),
      expect: () => [],
      verify: (_) {
        verifyNever(() => mockRepository.getTagCatalog());
      },
    );

    blocTest<TagCatalogCubit, TagCatalogState>(
      'loadCatalog re-fetches from repository when forceRefresh is true even if already loaded',
      build: () {
        when(() => mockRepository.getTagCatalog())
            .thenAnswer((_) async => mockCatalog);
        return TagCatalogCubit(repository: mockRepository);
      },
      seed: () => const TagCatalogLoaded(mockCatalog),
      act: (cubit) => cubit.loadCatalog(forceRefresh: true),
      expect: () => [
        isA<TagCatalogLoading>(),
        isA<TagCatalogLoaded>(),
      ],
      verify: (_) {
        verify(() => mockRepository.getTagCatalog()).called(1);
      },
    );

    blocTest<TagCatalogCubit, TagCatalogState>(
      'loadCatalog emits [TagCatalogLoading, TagCatalogError] on failure',
      build: () {
        when(() => mockRepository.getTagCatalog()).thenThrow(
          const ProfileFailure(
            'Falha de rede ao consultar o catálogo de tags.',
            statusCode: 503,
          ),
        );
        return TagCatalogCubit(repository: mockRepository);
      },
      act: (cubit) => cubit.loadCatalog(),
      expect: () => [
        isA<TagCatalogLoading>(),
        isA<TagCatalogError>()
            .having((s) => s.message, 'message', contains('Falha de rede'))
            .having((s) => s.statusCode, 'statusCode', 503),
      ],
    );
  });
}
