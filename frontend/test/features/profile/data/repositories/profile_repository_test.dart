import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/profile/data/datasources/profile_remote_data_source.dart';
import 'package:search_a_church_app/features/profile/data/failures/profile_failures.dart';
import 'package:search_a_church_app/features/profile/data/models/church_profile_model.dart';
import 'package:search_a_church_app/features/profile/data/models/tag_catalog_model.dart';
import 'package:search_a_church_app/features/profile/data/models/user_profile_model.dart';
import 'package:search_a_church_app/features/profile/data/repositories/profile_repository.dart';

class MockProfileRemoteDataSource extends Mock
    implements IProfileRemoteDataSource {}

void main() {
  late MockProfileRemoteDataSource mockRemoteDataSource;
  late ProfileRepository repository;

  setUp(() {
    mockRemoteDataSource = MockProfileRemoteDataSource();
    repository = ProfileRepository(remoteDataSource: mockRemoteDataSource);
  });

  group('ProfileRepository - Success Delegation', () {
    test('getUserProfile delegates to remoteDataSource', () async {
      const expected = UserProfileModel(
        userId: 'u-1',
        name: 'John Doe',
        email: 'test@example.com',
        defaultRadiusKm: 10.0,
        selectedTags: ['rampa_acesso'],
      );

      when(() => mockRemoteDataSource.getUserProfile())
          .thenAnswer((_) async => expected);

      final result = await repository.getUserProfile();

      expect(result, expected);
      verify(() => mockRemoteDataSource.getUserProfile()).called(1);
    });

    test('updateUserProfile delegates to remoteDataSource', () async {
      const request = UpdateUserProfileRequestModel(
        denomination: 'Batista',
        worshipStyle: 'Contemporâneo',
        defaultRadiusKm: 20.0,
      );

      const expected = UserProfileModel(
        userId: 'u-1',
        name: 'Jane Doe',
        email: 'test@example.com',
        denomination: 'Batista',
        worshipStyle: 'Contemporâneo',
        defaultRadiusKm: 20.0,
      );

      when(() => mockRemoteDataSource.updateUserProfile(request))
          .thenAnswer((_) async => expected);

      final result = await repository.updateUserProfile(request);

      expect(result, expected);
      verify(() => mockRemoteDataSource.updateUserProfile(request)).called(1);
    });

    test('deleteUserAccount delegates to remoteDataSource', () async {
      const expected = DeleteAccountResponseModel(
        success: true,
        message: 'Conta anonimizada sob a LGPD',
      );

      when(() => mockRemoteDataSource.deleteUserAccount())
          .thenAnswer((_) async => expected);

      final result = await repository.deleteUserAccount();

      expect(result, expected);
      verify(() => mockRemoteDataSource.deleteUserAccount()).called(1);
    });

    test('getChurchProfile delegates to remoteDataSource', () async {
      const expected = ChurchProfileModel(
        id: 'c-1',
        name: 'Igreja Central',
        address: 'Rua Principal, 100',
        latitude: -25.4284,
        longitude: -49.2733,
        isActive: true,
        concurrencyStamp: 'ct-1',
      );

      when(() => mockRemoteDataSource.getChurchProfile('c-1'))
          .thenAnswer((_) async => expected);

      final result = await repository.getChurchProfile('c-1');

      expect(result, expected);
      verify(() => mockRemoteDataSource.getChurchProfile('c-1')).called(1);
    });

    test('updateChurchProfile delegates to remoteDataSource with concurrency token', () async {
      const request = UpdateChurchProfileRequestModel(
        name: 'Igreja Renovada',
        address: 'Rua Principal, 100',
        latitude: -25.4284,
        longitude: -49.2733,
      );

      const expected = ChurchProfileModel(
        id: 'c-1',
        name: 'Igreja Renovada',
        address: 'Rua Principal, 100',
        latitude: -25.4284,
        longitude: -49.2733,
        isActive: true,
        concurrencyStamp: 'ct-2',
      );

      when(() => mockRemoteDataSource.updateChurchProfile(
            'c-1',
            request,
            ifMatchHeader: 'ct-1',
          )).thenAnswer((_) async => expected);

      final result = await repository.updateChurchProfile(
        'c-1',
        request,
        ifMatchHeader: 'ct-1',
      );

      expect(result, expected);
      verify(() => mockRemoteDataSource.updateChurchProfile(
            'c-1',
            request,
            ifMatchHeader: 'ct-1',
          )).called(1);
    });

    test('setChurchStatus delegates to remoteDataSource', () async {
      const request = UpdateChurchStatusRequestModel(
        isActive: false,
        concurrencyStamp: 'ct-1',
      );

      const expected = ChurchStatusResponseModel(
        churchId: 'c-1',
        isActive: false,
        message: 'Desativada temporariamente',
      );

      when(() => mockRemoteDataSource.setChurchStatus(
            'c-1',
            request,
            ifMatchHeader: 'ct-1',
          )).thenAnswer((_) async => expected);

      final result = await repository.setChurchStatus(
        'c-1',
        request,
        ifMatchHeader: 'ct-1',
      );

      expect(result, expected);
      verify(() => mockRemoteDataSource.setChurchStatus(
            'c-1',
            request,
            ifMatchHeader: 'ct-1',
          )).called(1);
    });

    test('getTagCatalog delegates to remoteDataSource', () async {
      const expected = TagCatalogModel(
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
            ],
          ),
        ],
      );

      when(() => mockRemoteDataSource.getTagCatalog())
          .thenAnswer((_) async => expected);

      final result = await repository.getTagCatalog();

      expect(result, expected);
      verify(() => mockRemoteDataSource.getTagCatalog()).called(1);
    });
  });

  group('ProfileRepository - Failure Mapping', () {
    test('maps 401 Unauthorized to UnauthorizedFailure', () async {
      final dioException = DioException(
        requestOptions: RequestOptions(path: '/profile/user'),
        response: Response(
          requestOptions: RequestOptions(path: '/profile/user'),
          statusCode: 401,
          data: {'error': 'UNAUTHORIZED', 'message': 'Token expirado ou inválido.'},
        ),
      );

      when(() => mockRemoteDataSource.getUserProfile()).thenThrow(dioException);

      expect(
        () => repository.getUserProfile(),
        throwsA(isA<UnauthorizedFailure>()
            .having((e) => e.statusCode, 'statusCode', 401)
            .having((e) => e.message, 'message', 'Token expirado ou inválido.')),
      );
    });

    test('maps 403 Forbidden with ACESSO_NEGADO_PROPRIEDADE to ForbiddenFailure', () async {
      final dioException = DioException(
        requestOptions: RequestOptions(path: '/profile/church/c-1'),
        response: Response(
          requestOptions: RequestOptions(path: '/profile/church/c-1'),
          statusCode: 403,
          data: {
            'error': 'ACESSO_NEGADO_PROPRIEDADE',
            'message': 'Você não tem permissão para editar esta congregação.',
          },
        ),
      );

      const request = UpdateChurchProfileRequestModel(
        name: 'Hack',
        address: 'Rua Hack, 1',
        latitude: -23.5,
        longitude: -46.6,
      );
      when(() => mockRemoteDataSource.updateChurchProfile('c-1', request))
          .thenThrow(dioException);

      expect(
        () => repository.updateChurchProfile('c-1', request),
        throwsA(isA<ForbiddenFailure>()
            .having((e) => e.statusCode, 'statusCode', 403)
            .having((e) => e.errorCode, 'errorCode', 'ACESSO_NEGADO_PROPRIEDADE')),
      );
    });

    test('maps 403 Forbidden with REPRESENTANTE_NAO_VERIFICADO to ForbiddenFailure', () async {
      final dioException = DioException(
        requestOptions: RequestOptions(path: '/profile/church/c-1/status'),
        response: Response(
          requestOptions: RequestOptions(path: '/profile/church/c-1/status'),
          statusCode: 403,
          data: {
            'error': 'REPRESENTANTE_NAO_VERIFICADO',
            'message': 'Apenas representantes verificados podem alterar status.',
          },
        ),
      );

      const request = UpdateChurchStatusRequestModel(isActive: false);
      when(() => mockRemoteDataSource.setChurchStatus('c-1', request))
          .thenThrow(dioException);

      expect(
        () => repository.setChurchStatus('c-1', request),
        throwsA(isA<ForbiddenFailure>()
            .having((e) => e.statusCode, 'statusCode', 403)
            .having((e) => e.errorCode, 'errorCode', 'REPRESENTANTE_NAO_VERIFICADO')),
      );
    });

    test('maps 404 Not Found with IGREJA_NAO_ENCONTRADA to ProfileNotFoundFailure', () async {
      final dioException = DioException(
        requestOptions: RequestOptions(path: '/profile/church/c-not-found'),
        response: Response(
          requestOptions: RequestOptions(path: '/profile/church/c-not-found'),
          statusCode: 404,
          data: {
            'error': 'IGREJA_NAO_ENCONTRADA',
            'message': 'Igreja solicitada não existe.',
          },
        ),
      );

      when(() => mockRemoteDataSource.getChurchProfile('c-not-found'))
          .thenThrow(dioException);

      expect(
        () => repository.getChurchProfile('c-not-found'),
        throwsA(isA<ProfileNotFoundFailure>()
            .having((e) => e.statusCode, 'statusCode', 404)
            .having((e) => e.errorCode, 'errorCode', 'IGREJA_NAO_ENCONTRADA')),
      );
    });

    test('maps 409 Conflict with PLACE_ID_JA_VINCULADO to ConflictFailure', () async {
      final dioException = DioException(
        requestOptions: RequestOptions(path: '/profile/church/c-1'),
        response: Response(
          requestOptions: RequestOptions(path: '/profile/church/c-1'),
          statusCode: 409,
          data: {
            'error': 'PLACE_ID_JA_VINCULADO',
            'message': 'O Place ID informado já pertence a outra congregação.',
          },
        ),
      );

      const request = UpdateChurchProfileRequestModel(
        name: 'Igreja',
        address: 'Rua A',
        latitude: -23.5,
        longitude: -46.6,
        placeId: 'place-dup',
      );
      when(() => mockRemoteDataSource.updateChurchProfile('c-1', request))
          .thenThrow(dioException);

      expect(
        () => repository.updateChurchProfile('c-1', request),
        throwsA(isA<ConflictFailure>()
            .having((e) => e.statusCode, 'statusCode', 409)
            .having((e) => e.errorCode, 'errorCode', 'PLACE_ID_JA_VINCULADO')),
      );
    });

    test('maps 409 Conflict with CONFLITO_CONCORRENCIA to ConflictFailure', () async {
      final dioException = DioException(
        requestOptions: RequestOptions(path: '/profile/church/c-1'),
        response: Response(
          requestOptions: RequestOptions(path: '/profile/church/c-1'),
          statusCode: 409,
          data: {
            'error': 'CONFLITO_CONCORRENCIA',
            'message': 'O cadastro foi modificado por outro usuário.',
          },
        ),
      );

      const request = UpdateChurchProfileRequestModel(
        name: 'Outro Nome',
        address: 'Rua A',
        latitude: -23.5,
        longitude: -46.6,
      );
      when(() => mockRemoteDataSource.updateChurchProfile('c-1', request))
          .thenThrow(dioException);

      expect(
        () => repository.updateChurchProfile('c-1', request),
        throwsA(isA<ConflictFailure>()
            .having((e) => e.statusCode, 'statusCode', 409)
            .having((e) => e.errorCode, 'errorCode', 'CONFLITO_CONCORRENCIA')),
      );
    });

    test('maps 400 Bad Request with TAG_INVALIDA to InvalidTagFailure', () async {
      final dioException = DioException(
        requestOptions: RequestOptions(path: '/profile/church/c-1'),
        response: Response(
          requestOptions: RequestOptions(path: '/profile/church/c-1'),
          statusCode: 400,
          data: {
            'error': 'TAG_INVALIDA',
            'message': 'Tag desconhecida.',
          },
        ),
      );

      const request = UpdateChurchProfileRequestModel(
        name: 'Igreja',
        address: 'Rua A',
        latitude: -23.5,
        longitude: -46.6,
        tagCodes: ['tag_fake'],
      );
      when(() => mockRemoteDataSource.updateChurchProfile('c-1', request))
          .thenThrow(dioException);

      expect(
        () => repository.updateChurchProfile('c-1', request),
        throwsA(isA<InvalidTagFailure>()
            .having((e) => e.statusCode, 'statusCode', 400)
            .having((e) => e.errorCode, 'errorCode', 'TAG_INVALIDA')),
      );
    });

    test('maps 400 Bad Request with RAIO_INVALIDO to InvalidRadiusFailure', () async {
      final dioException = DioException(
        requestOptions: RequestOptions(path: '/profile/user'),
        response: Response(
          requestOptions: RequestOptions(path: '/profile/user'),
          statusCode: 400,
          data: {
            'error': 'RAIO_INVALIDO',
            'message': 'Raio fora do limite.',
          },
        ),
      );

      const request = UpdateUserProfileRequestModel(defaultRadiusKm: 500.0);
      when(() => mockRemoteDataSource.updateUserProfile(request))
          .thenThrow(dioException);

      expect(
        () => repository.updateUserProfile(request),
        throwsA(isA<InvalidRadiusFailure>()
            .having((e) => e.statusCode, 'statusCode', 400)
            .having((e) => e.errorCode, 'errorCode', 'RAIO_INVALIDO')),
      );
    });

    test('maps generic DioException with status 500 to generic ProfileFailure', () async {
      final dioException = DioException(
        requestOptions: RequestOptions(path: '/tags/catalog'),
        response: Response(
          requestOptions: RequestOptions(path: '/tags/catalog'),
          statusCode: 500,
          data: 'Internal error',
        ),
      );

      when(() => mockRemoteDataSource.getTagCatalog()).thenThrow(dioException);

      expect(
        () => repository.getTagCatalog(),
        throwsA(isA<ProfileFailure>()
            .having((e) => e.statusCode, 'statusCode', 500)),
      );
    });

    test('rethrows ProfileFailure directly if already thrown', () async {
      const existingFailure = UnauthorizedFailure('Já não autenticado');
      when(() => mockRemoteDataSource.getUserProfile()).thenThrow(existingFailure);

      expect(
        () => repository.getUserProfile(),
        throwsA(isA<UnauthorizedFailure>()
            .having((e) => e.message, 'message', 'Já não autenticado')),
      );
    });

    test('maps unknown exception to generic ProfileFailure', () async {
      when(() => mockRemoteDataSource.deleteUserAccount())
          .thenThrow(Exception('Falha de IO imprevista'));

      expect(
        () => repository.deleteUserAccount(),
        throwsA(isA<ProfileFailure>()
            .having((e) => e.message, 'message', contains('Erro inesperado'))),
      );
    });
  });
}
