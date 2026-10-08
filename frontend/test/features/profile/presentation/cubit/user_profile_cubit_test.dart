import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/profile/data/failures/profile_failures.dart';
import 'package:search_a_church_app/features/profile/data/models/user_profile_model.dart';
import 'package:search_a_church_app/features/profile/data/repositories/profile_repository.dart';
import 'package:search_a_church_app/features/profile/presentation/cubit/user_profile_cubit.dart';
import 'package:search_a_church_app/features/profile/presentation/cubit/user_profile_state.dart';

class MockProfileRepository extends Mock implements IProfileRepository {}

class FakeUpdateUserProfileRequestModel extends Fake
    implements UpdateUserProfileRequestModel {}

void main() {
  late MockProfileRepository mockRepository;

  setUpAll(() {
    registerFallbackValue(FakeUpdateUserProfileRequestModel());
  });

  setUp(() {
    mockRepository = MockProfileRepository();
  });

  group('UserProfileCubit', () {
    test('initial state is UserProfileInitial', () {
      final cubit = UserProfileCubit(repository: mockRepository);
      expect(cubit.state, isA<UserProfileInitial>());
    });

    group('loadProfile', () {
      const mockProfile = UserProfileModel(
        userId: 'u-1',
        name: 'Maria Oliveira',
        email: 'maria@email.com',
        denomination: 'Batista',
        worshipStyle: 'Contemporâneo',
        preferredLanguages: ['pt'],
        defaultRadiusKm: 15.0,
        selectedTags: ['rampa_acesso'],
        isConfigured: true,
      );

      blocTest<UserProfileCubit, UserProfileState>(
        'emits [UserProfileLoading, UserProfileLoaded] on successful fetch',
        build: () {
          when(() => mockRepository.getUserProfile())
              .thenAnswer((_) async => mockProfile);
          return UserProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.loadProfile(),
        expect: () => [
          isA<UserProfileLoading>(),
          isA<UserProfileLoaded>()
              .having((s) => s.profile.name, 'name', 'Maria Oliveira')
              .having((s) => s.profile.defaultRadiusKm, 'defaultRadiusKm', 15.0)
              .having((s) => s.profile.isConfigured, 'isConfigured', true),
        ],
        verify: (_) => verify(() => mockRepository.getUserProfile()).called(1),
      );

      blocTest<UserProfileCubit, UserProfileState>(
        'emits [UserProfileLoading, UserProfileError] on failure',
        build: () {
          when(() => mockRepository.getUserProfile()).thenThrow(
            const UnauthorizedFailure('Token inválido ou expirado.'),
          );
          return UserProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.loadProfile(),
        expect: () => [
          isA<UserProfileLoading>(),
          isA<UserProfileError>()
              .having((s) => s.message, 'message', 'Token inválido ou expirado.')
              .having((s) => s.statusCode, 'statusCode', 401),
        ],
      );
    });

    group('updateProfile', () {
      const request = UpdateUserProfileRequestModel(
        denomination: 'Presbiteriana',
        worshipStyle: 'Tradicional',
        defaultRadiusKm: 25.0,
        tagCodes: ['rampa_acesso', 'estacionamento_proprio'],
      );

      const updatedProfile = UserProfileModel(
        userId: 'u-1',
        name: 'Maria Oliveira',
        email: 'maria@email.com',
        denomination: 'Presbiteriana',
        worshipStyle: 'Tradicional',
        preferredLanguages: ['pt'],
        defaultRadiusKm: 25.0,
        selectedTags: ['rampa_acesso', 'estacionamento_proprio'],
        isConfigured: true,
      );

      blocTest<UserProfileCubit, UserProfileState>(
        'emits [UserProfileSaving, UserProfileSaveSuccess] on success',
        build: () {
          when(() => mockRepository.updateUserProfile(request))
              .thenAnswer((_) async => updatedProfile);
          return UserProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.updateProfile(request),
        expect: () => [
          isA<UserProfileSaving>(),
          isA<UserProfileSaveSuccess>()
              .having((s) => s.updatedProfile.denomination, 'denomination', 'Presbiteriana')
              .having((s) => s.updatedProfile.defaultRadiusKm, 'defaultRadiusKm', 25.0),
        ],
        verify: (_) =>
            verify(() => mockRepository.updateUserProfile(request)).called(1),
      );

      blocTest<UserProfileCubit, UserProfileState>(
        'emits [UserProfileSaving, UserProfileError] when radius is invalid',
        build: () {
          when(() => mockRepository.updateUserProfile(request)).thenThrow(
            const InvalidRadiusFailure('O raio informado deve estar entre 1.0 e 100.0 km.'),
          );
          return UserProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.updateProfile(request),
        expect: () => [
          isA<UserProfileSaving>(),
          isA<UserProfileError>()
              .having((s) => s.errorCode, 'errorCode', 'RAIO_INVALIDO')
              .having((s) => s.statusCode, 'statusCode', 400),
        ],
      );

      blocTest<UserProfileCubit, UserProfileState>(
        'emits [UserProfileSaving, UserProfileError] on unexpected exception',
        build: () {
          when(() => mockRepository.updateUserProfile(request))
              .thenThrow(Exception('Falha de conexão com a rede'));
          return UserProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.updateProfile(request),
        expect: () => [
          isA<UserProfileSaving>(),
          isA<UserProfileError>()
              .having((s) => s.message, 'message', contains('Erro inesperado')),
        ],
      );
    });

    group('deleteAccount', () {
      final deleteResponse = DeleteAccountResponseModel(
        success: true,
        message: 'Conta encerrada e anonimizada com sucesso sob a LGPD.',
      );

      blocTest<UserProfileCubit, UserProfileState>(
        'emits [UserProfileDeleting, UserProfileDeleted] on successful deletion',
        build: () {
          when(() => mockRepository.deleteUserAccount())
              .thenAnswer((_) async => deleteResponse);
          return UserProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.deleteAccount(),
        expect: () => [
          isA<UserProfileDeleting>(),
          isA<UserProfileDeleted>()
              .having((s) => s.message, 'message', contains('LGPD')),
        ],
        verify: (_) => verify(() => mockRepository.deleteUserAccount()).called(1),
      );

      blocTest<UserProfileCubit, UserProfileState>(
        'emits [UserProfileDeleting, UserProfileError] on failure',
        build: () {
          when(() => mockRepository.deleteUserAccount()).thenThrow(
            const ForbiddenFailure(
              'Acesso negado: titularidade não confirmada.',
              errorCode: 'ACESSO_NEGADO_PROPRIEDADE',
            ),
          );
          return UserProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.deleteAccount(),
        expect: () => [
          isA<UserProfileDeleting>(),
          isA<UserProfileError>()
              .having((s) => s.errorCode, 'errorCode', 'ACESSO_NEGADO_PROPRIEDADE')
              .having((s) => s.statusCode, 'statusCode', 403),
        ],
      );
    });
  });
}
