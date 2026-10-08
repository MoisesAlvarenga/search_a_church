import 'package:flutter_test/flutter_test.dart';
import 'package:search_a_church_app/features/profile/data/models/tag_catalog_model.dart';

void main() {
  group('TagCatalogModel', () {
    const rawJson = {
      'categories': [
        {
          'id': 1,
          'name': 'Acessibilidade',
          'tags': [
            {
              'code': 'rampa_acesso',
              'name': 'Rampa de Acesso',
              'description': 'Acesso a cadeirantes',
              'iconName': 'accessible',
            },
            {
              'code': 'interprete_libras',
              'name': 'Intérprete de LIBRAS',
              'description': 'LIBRAS durante os cultos',
              'iconName': 'sign_language',
            }
          ]
        },
        {
          'id': 2,
          'name': 'Infraestrutura',
          'tags': [
            {
              'code': 'estacionamento_proprio',
              'name': 'Estacionamento Próprio',
              'description': 'Vagas privativas',
              'iconName': 'local_parking',
            }
          ]
        }
      ]
    };

    test('fromJson and toJson map correctly', () {
      final catalog = TagCatalogModel.fromJson(rawJson);

      expect(catalog.categories, hasLength(2));
      expect(catalog.categories[0].id, 1);
      expect(catalog.categories[0].name, 'Acessibilidade');
      expect(catalog.categories[0].tags, hasLength(2));
      expect(catalog.categories[0].tags[0].code, 'rampa_acesso');
      expect(catalog.categories[1].tags[0].code, 'estacionamento_proprio');

      final json = catalog.toJson();
      expect(json['categories'], hasLength(2));
    });

    test('allTags returns flattened list of all categories tags', () {
      final catalog = TagCatalogModel.fromJson(rawJson);
      final all = catalog.allTags;

      expect(all, hasLength(3));
      expect(all.map((t) => t.code).toList(), [
        'rampa_acesso',
        'interprete_libras',
        'estacionamento_proprio',
      ]);
    });
  });
}
