import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/profile/data/datasources/profile_remote_data_source.dart';
import 'package:search_a_church_app/features/profile/data/models/church_profile_model.dart';
import 'package:search_a_church_app/features/profile/data/models/meeting_schedule_model.dart';
import 'package:search_a_church_app/features/profile/data/models/user_profile_model.dart';

class MockDio extends Mock implements Dio {}

void main() {
  late MockDio mockDio;
  late ProfileRemoteDataSource dataSource;

  setUp(() {
    mockDio = MockDio();
    dataSource = ProfileRemoteDataSource(dio: mockDio);
  });

  group('ProfileRemoteDataSource', () {
    test('getUserProfile calls GET /profile/user and returns UserProfileModel', () async {
      final mockData = {
        'userId': 'usr-123',
        'name': 'João da Silva',
        'email': 'joao@email.com',
        'denomination': 'Batista',
        'worshipStyle': 'Contemporâneo',
        'preferredLanguages': ['pt', 'en'],
        'defaultRadiusKm': 15.0,
        'selectedTags': ['rampa_acesso', 'estacionamento_proprio'],
        'isConfigured': true,
      };

      when(() => mockDio.get('/profile/user')).thenAnswer(
        (_) async => Response(
          data: mockData,
          statusCode: 200,
          requestOptions: RequestOptions(path: '/profile/user'),
        ),
      );

      final result = await dataSource.getUserProfile();

      expect(result.userId, 'usr-123');
      expect(result.name, 'João da Silva');
      expect(result.email, 'joao@email.com');
      expect(result.defaultRadiusKm, 15.0);
      expect(result.selectedTags, ['rampa_acesso', 'estacionamento_proprio']);
      expect(result.isConfigured, true);
      verify(() => mockDio.get('/profile/user')).called(1);
    });

    test('updateUserProfile calls PUT /profile/user and returns updated UserProfileModel', () async {
      const request = UpdateUserProfileRequestModel(
        denomination: 'Metodista',
        worshipStyle: 'Tradicional',
        preferredLanguages: ['pt'],
        defaultRadiusKm: 20.0,
        tagCodes: ['rampa_acesso'],
      );

      final mockData = {
        'userId': 'usr-123',
        'name': 'João da Silva',
        'email': 'joao@email.com',
        'denomination': 'Metodista',
        'worshipStyle': 'Tradicional',
        'preferredLanguages': ['pt'],
        'defaultRadiusKm': 20.0,
        'selectedTags': ['rampa_acesso'],
        'isConfigured': true,
      };

      when(() => mockDio.put(
            '/profile/user',
            data: any(named: 'data'),
          )).thenAnswer(
        (_) async => Response(
          data: mockData,
          statusCode: 200,
          requestOptions: RequestOptions(path: '/profile/user'),
        ),
      );

      final result = await dataSource.updateUserProfile(request);

      expect(result.denomination, 'Metodista');
      expect(result.defaultRadiusKm, 20.0);
      expect(result.selectedTags, ['rampa_acesso']);
      verify(() => mockDio.put('/profile/user', data: request.toJson())).called(1);
    });

    test('deleteUserAccount calls DELETE /profile/user and returns DeleteAccountResponseModel', () async {
      final mockData = {
        'success': true,
        'message': 'Conta encerrada e anonimizada com sucesso sob a LGPD.',
      };

      when(() => mockDio.delete('/profile/user')).thenAnswer(
        (_) async => Response(
          data: mockData,
          statusCode: 200,
          requestOptions: RequestOptions(path: '/profile/user'),
        ),
      );

      final result = await dataSource.deleteUserAccount();

      expect(result.success, true);
      expect(result.message, 'Conta encerrada e anonimizada com sucesso sob a LGPD.');
      verify(() => mockDio.delete('/profile/user')).called(1);
    });

    test('getChurchProfile calls GET /profile/church/{id} and returns ChurchProfileModel', () async {
      final mockData = {
        'id': 'ch-1',
        'placeId': 'place-xyz',
        'name': 'Igreja Central',
        'address': 'Av. Paulista, 1000',
        'latitude': -23.561,
        'longitude': -46.655,
        'denomination': 'Batista',
        'worshipStyle': 'Contemporâneo',
        'languages': ['pt'],
        'phone': '11999998888',
        'email': 'contato@igreja.org',
        'website': 'https://igreja.org',
        'claimState': 'Verified',
        'isActive': true,
        'concurrencyStamp': 'stamp-xyz',
        'tags': ['rampa_acesso'],
        'schedules': [
          {
            'id': 'sch-1',
            'dayOfWeek': 0,
            'startTime': '10:00',
            'description': 'Culto',
            'language': 'pt',
          }
        ],
      };

      when(() => mockDio.get('/profile/church/ch-1')).thenAnswer(
        (_) async => Response(
          data: mockData,
          statusCode: 200,
          requestOptions: RequestOptions(path: '/profile/church/ch-1'),
        ),
      );

      final result = await dataSource.getChurchProfile('ch-1');

      expect(result.id, 'ch-1');
      expect(result.name, 'Igreja Central');
      expect(result.schedules.length, 1);
      expect(result.tags, ['rampa_acesso']);
      expect(result.concurrencyStamp, 'stamp-xyz');
      verify(() => mockDio.get('/profile/church/ch-1')).called(1);
    });

    test('updateChurchProfile calls PUT /profile/church/{id} with If-Match header', () async {
      const request = UpdateChurchProfileRequestModel(
        name: 'Igreja Renovada',
        address: 'Rua das Flores, 123',
        latitude: -23.561,
        longitude: -46.655,
        concurrencyStamp: 'stamp-old',
        tagCodes: ['rampa_acesso'],
        schedules: [
          MeetingScheduleModel(
            dayOfWeek: 6,
            startTime: '19:30',
            description: 'Culto de Jovens',
          ),
        ],
      );

      final mockData = {
        'id': 'ch-1',
        'name': 'Igreja Renovada',
        'address': 'Rua das Flores, 123',
        'latitude': -23.561,
        'longitude': -46.655,
        'isActive': true,
        'concurrencyStamp': 'stamp-new',
        'tags': ['rampa_acesso'],
        'schedules': [
          {
            'dayOfWeek': 6,
            'startTime': '19:30',
            'description': 'Culto de Jovens',
            'language': 'pt',
          }
        ],
      };

      when(() => mockDio.put(
            '/profile/church/ch-1',
            data: any(named: 'data'),
            options: any(named: 'options'),
          )).thenAnswer(
        (_) async => Response(
          data: mockData,
          statusCode: 200,
          requestOptions: RequestOptions(path: '/profile/church/ch-1'),
        ),
      );

      final result = await dataSource.updateChurchProfile(
        'ch-1',
        request,
        ifMatchHeader: 'stamp-old',
      );

      expect(result.name, 'Igreja Renovada');
      expect(result.concurrencyStamp, 'stamp-new');

      final captured = verify(() => mockDio.put(
            '/profile/church/ch-1',
            data: request.toJson(),
            options: captureAny(named: 'options'),
          )).captured;

      final options = captured.first as Options;
      expect(options.headers?['If-Match'], 'stamp-old');
    });

    test('setChurchStatus calls PATCH /profile/church/{id}/status with If-Match header', () async {
      const request = UpdateChurchStatusRequestModel(
        isActive: false,
        concurrencyStamp: 'stamp-old',
      );

      final mockData = {
        'churchId': 'ch-1',
        'isActive': false,
        'message': 'Status da congregação alterado com sucesso.',
      };

      when(() => mockDio.patch(
            '/profile/church/ch-1/status',
            data: any(named: 'data'),
            options: any(named: 'options'),
          )).thenAnswer(
        (_) async => Response(
          data: mockData,
          statusCode: 200,
          requestOptions: RequestOptions(path: '/profile/church/ch-1/status'),
        ),
      );

      final result = await dataSource.setChurchStatus(
        'ch-1',
        request,
        ifMatchHeader: 'stamp-old',
      );

      expect(result.churchId, 'ch-1');
      expect(result.isActive, false);

      final captured = verify(() => mockDio.patch(
            '/profile/church/ch-1/status',
            data: request.toJson(),
            options: captureAny(named: 'options'),
          )).captured;

      final options = captured.first as Options;
      expect(options.headers?['If-Match'], 'stamp-old');
    });

    test('getTagCatalog calls GET /tags/catalog and returns TagCatalogModel', () async {
      final mockData = {
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
              }
            ],
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
            ],
          }
        ]
      };

      when(() => mockDio.get('/tags/catalog')).thenAnswer(
        (_) async => Response(
          data: mockData,
          statusCode: 200,
          requestOptions: RequestOptions(path: '/tags/catalog'),
        ),
      );

      final result = await dataSource.getTagCatalog();

      expect(result.categories, hasLength(2));
      expect(result.allTags, hasLength(2));
      expect(result.allTags.first.code, 'rampa_acesso');
      verify(() => mockDio.get('/tags/catalog')).called(1);
    });
  });
}
