import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/profile/data/failures/profile_failures.dart';
import 'package:search_a_church_app/features/profile/data/models/church_profile_model.dart';
import 'package:search_a_church_app/features/profile/data/models/meeting_schedule_model.dart';
import 'package:search_a_church_app/features/profile/data/repositories/profile_repository.dart';
import 'package:search_a_church_app/features/profile/presentation/cubit/church_profile_cubit.dart';
import 'package:search_a_church_app/features/profile/presentation/cubit/church_profile_state.dart';

class MockProfileRepository extends Mock implements IProfileRepository {}

class FakeUpdateChurchProfileRequestModel extends Fake
    implements UpdateChurchProfileRequestModel {}

class FakeUpdateChurchStatusRequestModel extends Fake
    implements UpdateChurchStatusRequestModel {}

void main() {
  late MockProfileRepository mockRepository;

  setUpAll(() {
    registerFallbackValue(FakeUpdateChurchProfileRequestModel());
    registerFallbackValue(FakeUpdateChurchStatusRequestModel());
  });

  setUp(() {
    mockRepository = MockProfileRepository();
  });

  group('ChurchProfileCubit', () {
    test('initial state is ChurchProfileInitial', () {
      final cubit = ChurchProfileCubit(repository: mockRepository);
      expect(cubit.state, isA<ChurchProfileInitial>());
    });

    group('loadProfile', () {
      const mockChurch = ChurchProfileModel(
        id: 'ch-1',
        name: 'Igreja Nova Aliança',
        address: 'Rua Principal, 200',
        latitude: -23.55,
        longitude: -46.63,
        concurrencyStamp: 'stamp-100',
        tags: ['rampa_acesso'],
        schedules: [
          MeetingScheduleModel(
            dayOfWeek: 0,
            startTime: '10:00',
            description: 'Culto Matutino',
          ),
        ],
      );

      blocTest<ChurchProfileCubit, ChurchProfileState>(
        'emits [ChurchProfileLoading, ChurchProfileLoaded] on success',
        build: () {
          when(() => mockRepository.getChurchProfile('ch-1'))
              .thenAnswer((_) async => mockChurch);
          return ChurchProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.loadProfile('ch-1'),
        expect: () => [
          isA<ChurchProfileLoading>(),
          isA<ChurchProfileLoaded>()
              .having((s) => s.profile.name, 'name', 'Igreja Nova Aliança')
              .having((s) => s.profile.schedules.length, 'schedules.length', 1),
        ],
        verify: (_) =>
            verify(() => mockRepository.getChurchProfile('ch-1')).called(1),
      );

      blocTest<ChurchProfileCubit, ChurchProfileState>(
        'emits [ChurchProfileLoading, ChurchProfileError] when church is not found',
        build: () {
          when(() => mockRepository.getChurchProfile('ch-unknown')).thenThrow(
            const ProfileNotFoundFailure(
              'Igreja não encontrada no sistema.',
              errorCode: 'IGREJA_NAO_ENCONTRADA',
            ),
          );
          return ChurchProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.loadProfile('ch-unknown'),
        expect: () => [
          isA<ChurchProfileLoading>(),
          isA<ChurchProfileError>()
              .having((s) => s.errorCode, 'errorCode', 'IGREJA_NAO_ENCONTRADA')
              .having((s) => s.statusCode, 'statusCode', 404),
        ],
      );
    });

    group('updateProfile', () {
      const request = UpdateChurchProfileRequestModel(
        name: 'Igreja Vida Plena',
        address: 'Rua Nova, 50',
        latitude: -23.56,
        longitude: -46.64,
        concurrencyStamp: 'stamp-100',
      );

      const updatedChurch = ChurchProfileModel(
        id: 'ch-1',
        name: 'Igreja Vida Plena',
        address: 'Rua Nova, 50',
        latitude: -23.56,
        longitude: -46.64,
        concurrencyStamp: 'stamp-101',
      );

      blocTest<ChurchProfileCubit, ChurchProfileState>(
        'emits [ChurchProfileSaving, ChurchProfileSaveSuccess] on success',
        build: () {
          when(() => mockRepository.updateChurchProfile(
                'ch-1',
                request,
                ifMatchHeader: 'stamp-100',
              )).thenAnswer((_) async => updatedChurch);
          return ChurchProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.updateProfile(
          'ch-1',
          request,
          ifMatchHeader: 'stamp-100',
        ),
        expect: () => [
          isA<ChurchProfileSaving>(),
          isA<ChurchProfileSaveSuccess>()
              .having((s) => s.updatedProfile.name, 'name', 'Igreja Vida Plena')
              .having((s) => s.updatedProfile.concurrencyStamp, 'concurrencyStamp', 'stamp-101'),
        ],
        verify: (_) => verify(() => mockRepository.updateChurchProfile(
              'ch-1',
              request,
              ifMatchHeader: 'stamp-100',
            )).called(1),
      );

      blocTest<ChurchProfileCubit, ChurchProfileState>(
        'emits [ChurchProfileSaving, ChurchProfileError] with isPlaceIdConflict=true on Place ID collision',
        build: () {
          when(() => mockRepository.updateChurchProfile(
                'ch-1',
                request,
                ifMatchHeader: any(named: 'ifMatchHeader'),
              )).thenThrow(
            const ConflictFailure(
              'O Google Maps Place ID já está vinculado a outra igreja.',
              errorCode: 'PLACE_ID_JA_VINCULADO',
            ),
          );
          return ChurchProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.updateProfile('ch-1', request),
        expect: () => [
          isA<ChurchProfileSaving>(),
          isA<ChurchProfileError>()
              .having((s) => s.isPlaceIdConflict, 'isPlaceIdConflict', true)
              .having((s) => s.isConcurrencyConflict, 'isConcurrencyConflict', false)
              .having((s) => s.errorCode, 'errorCode', 'PLACE_ID_JA_VINCULADO')
              .having((s) => s.statusCode, 'statusCode', 409),
        ],
      );

      blocTest<ChurchProfileCubit, ChurchProfileState>(
        'emits [ChurchProfileSaving, ChurchProfileError] with isConcurrencyConflict=true on stale concurrency stamp',
        build: () {
          when(() => mockRepository.updateChurchProfile(
                'ch-1',
                request,
                ifMatchHeader: any(named: 'ifMatchHeader'),
              )).thenThrow(
            const ConflictFailure(
              'Os dados foram atualizados por outro usuário.',
              errorCode: 'CONFLITO_CONCORRENCIA',
            ),
          );
          return ChurchProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.updateProfile('ch-1', request),
        expect: () => [
          isA<ChurchProfileSaving>(),
          isA<ChurchProfileError>()
              .having((s) => s.isConcurrencyConflict, 'isConcurrencyConflict', true)
              .having((s) => s.isPlaceIdConflict, 'isPlaceIdConflict', false)
              .having((s) => s.errorCode, 'errorCode', 'CONFLITO_CONCORRENCIA')
              .having((s) => s.statusCode, 'statusCode', 409),
        ],
      );

      blocTest<ChurchProfileCubit, ChurchProfileState>(
        'emits [ChurchProfileSaving, ChurchProfileError] when user lacks Verified representative status',
        build: () {
          when(() => mockRepository.updateChurchProfile(
                'ch-1',
                request,
                ifMatchHeader: any(named: 'ifMatchHeader'),
              )).thenThrow(
            const ForbiddenFailure(
              'Apenas representantes verificados podem atualizar o cadastro.',
              errorCode: 'REPRESENTANTE_NAO_VERIFICADO',
            ),
          );
          return ChurchProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.updateProfile('ch-1', request),
        expect: () => [
          isA<ChurchProfileSaving>(),
          isA<ChurchProfileError>()
              .having((s) => s.errorCode, 'errorCode', 'REPRESENTANTE_NAO_VERIFICADO')
              .having((s) => s.statusCode, 'statusCode', 403),
        ],
      );
    });

    group('setChurchStatus', () {
      const statusRequest = UpdateChurchStatusRequestModel(
        isActive: false,
        concurrencyStamp: 'stamp-100',
      );

      final statusResponse = ChurchStatusResponseModel(
        churchId: 'ch-1',
        isActive: false,
        message: 'Congregação inativada com sucesso.',
      );

      blocTest<ChurchProfileCubit, ChurchProfileState>(
        'emits [ChurchProfileStatusUpdating, ChurchProfileStatusUpdated] on success',
        build: () {
          when(() => mockRepository.setChurchStatus(
                'ch-1',
                statusRequest,
                ifMatchHeader: 'stamp-100',
              )).thenAnswer((_) async => statusResponse);
          return ChurchProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.setChurchStatus(
          'ch-1',
          statusRequest,
          ifMatchHeader: 'stamp-100',
        ),
        expect: () => [
          isA<ChurchProfileStatusUpdating>(),
          isA<ChurchProfileStatusUpdated>()
              .having((s) => s.response.isActive, 'isActive', false)
              .having((s) => s.response.churchId, 'churchId', 'ch-1'),
        ],
        verify: (_) => verify(() => mockRepository.setChurchStatus(
              'ch-1',
              statusRequest,
              ifMatchHeader: 'stamp-100',
            )).called(1),
      );

      blocTest<ChurchProfileCubit, ChurchProfileState>(
        'emits [ChurchProfileStatusUpdating, ChurchProfileError] on failure',
        build: () {
          when(() => mockRepository.setChurchStatus(
                'ch-1',
                statusRequest,
                ifMatchHeader: any(named: 'ifMatchHeader'),
              )).thenThrow(
            const ForbiddenFailure('Sem autorização.'),
          );
          return ChurchProfileCubit(repository: mockRepository);
        },
        act: (cubit) => cubit.setChurchStatus('ch-1', statusRequest),
        expect: () => [
          isA<ChurchProfileStatusUpdating>(),
          isA<ChurchProfileError>()
              .having((s) => s.statusCode, 'statusCode', 403),
        ],
      );
    });
  });
}
