import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/profile/profile.dart';

class MockProfileRepository extends Mock implements IProfileRepository {}

class FakeUpdateUserProfileRequestModel extends Fake
    implements UpdateUserProfileRequestModel {}

class FakeUpdateChurchProfileRequestModel extends Fake
    implements UpdateChurchProfileRequestModel {}

class FakeUpdateChurchStatusRequestModel extends Fake
    implements UpdateChurchStatusRequestModel {}

void main() {
  late MockProfileRepository mockRepository;
  late UserProfileCubit userProfileCubit;
  late ChurchProfileCubit churchProfileCubit;
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
            description: 'Acessibilidade física',
            iconName: 'accessible',
          ),
        ],
      ),
    ],
  );

  const mockUserProfile = UserProfileModel(
    userId: 'u-100',
    name: 'Carlos Alberto',
    email: 'carlos@exemplo.org',
    denomination: 'Batista',
    worshipStyle: 'Contemporâneo',
    defaultRadiusKm: 15.0,
    selectedTags: ['rampa_acesso'],
    isConfigured: true,
  );

  const mockChurchProfile = ChurchProfileModel(
    id: 'ch-200',
    name: 'Igreja Vida Eterna',
    address: 'Av. Brasil, 500',
    latitude: -23.5505,
    longitude: -46.6333,
    denomination: 'Presbiteriana',
    worshipStyle: 'Tradicional',
    phone: '11988887777',
    email: 'contato@vidaeterna.org',
    isActive: true,
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

  setUpAll(() {
    registerFallbackValue(FakeUpdateUserProfileRequestModel());
    registerFallbackValue(FakeUpdateChurchProfileRequestModel());
    registerFallbackValue(FakeUpdateChurchStatusRequestModel());
  });

  setUp(() {
    mockRepository = MockProfileRepository();
    userProfileCubit = UserProfileCubit(repository: mockRepository);
    churchProfileCubit = ChurchProfileCubit(repository: mockRepository);
    tagCatalogCubit = TagCatalogCubit(repository: mockRepository);

    when(() => mockRepository.getTagCatalog())
        .thenAnswer((_) async => mockCatalog);
  });

  tearDown(() {
    userProfileCubit.close();
    churchProfileCubit.close();
    tagCatalogCubit.close();
  });

  void configureViewport(WidgetTester tester) {
    tester.view.physicalSize = const Size(800, 1800);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(() {
      tester.view.resetPhysicalSize();
      tester.view.resetDevicePixelRatio();
    });
  }

  Widget buildTestableWidget(Widget child) {
    return MultiBlocProvider(
      providers: [
        BlocProvider<UserProfileCubit>.value(value: userProfileCubit),
        BlocProvider<ChurchProfileCubit>.value(value: churchProfileCubit),
        BlocProvider<TagCatalogCubit>.value(value: tagCatalogCubit),
      ],
      child: MaterialApp(
        home: child,
      ),
    );
  }

  group('UserProfileScreen Widget Tests', () {
    testWidgets('renders profile form, slider and tags correctly',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.getUserProfile())
          .thenAnswer((_) async => mockUserProfile);

      await tester.pumpWidget(
        buildTestableWidget(const UserProfileScreen()),
      );

      await tester.pumpAndSettle();

      expect(find.text('Meu Perfil e Preferências'), findsOneWidget);
      expect(find.text('Carlos Alberto'), findsOneWidget);
      expect(find.text('carlos@exemplo.org'), findsOneWidget);
      expect(find.text('Batista'), findsOneWidget);
      expect(find.text('Contemporâneo'), findsOneWidget);
      expect(find.byKey(const ValueKey('radius_slider')), findsOneWidget);
      expect(find.byKey(const ValueKey('save_preferences_button')),
          findsOneWidget);
      expect(find.byKey(const ValueKey('delete_account_button')),
          findsOneWidget);
    });

    testWidgets('saving preferences triggers cubit updateProfile',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.getUserProfile())
          .thenAnswer((_) async => mockUserProfile);
      when(() => mockRepository.updateUserProfile(any()))
          .thenAnswer((_) async => mockUserProfile);

      await tester.pumpWidget(
        buildTestableWidget(const UserProfileScreen()),
      );
      await tester.pumpAndSettle();

      // Enter new denomination
      await tester.enterText(
          find.byKey(const ValueKey('denomination_field')), 'Metodista');
      await tester.pump();

      // Tap save
      await tester.tap(find.byKey(const ValueKey('save_preferences_button')));
      await tester.pumpAndSettle();

      verify(() => mockRepository.updateUserProfile(any())).called(1);
      expect(find.byKey(const ValueKey('save_success_snackbar')),
          findsOneWidget);
    });

    testWidgets('LGPD delete account button opens confirmation dialog',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.getUserProfile())
          .thenAnswer((_) async => mockUserProfile);
      when(() => mockRepository.deleteUserAccount()).thenAnswer(
        (_) async => const DeleteAccountResponseModel(
          success: true,
          message: 'Conta encerrada e anonimizada com sucesso sob a LGPD.',
        ),
      );

      await tester.pumpWidget(
        buildTestableWidget(const UserProfileScreen()),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('delete_account_button')));
      await tester.pumpAndSettle();

      // Verify dialog is visible
      expect(find.text('Excluir e Anonimizar Conta'), findsOneWidget);
      expect(find.textContaining('Lei Geral de Proteção de Dados'),
          findsOneWidget);

      // Confirm deletion
      await tester.tap(find.byKey(const ValueKey('confirm_delete_button')));
      await tester.pumpAndSettle();

      verify(() => mockRepository.deleteUserAccount()).called(1);
      expect(find.byKey(const ValueKey('delete_success_snackbar')),
          findsOneWidget);
    });
  });

  group('ChurchProfileEditScreen Widget Tests', () {
    testWidgets('renders church edit form, switch and schedules',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.getChurchProfile('ch-200'))
          .thenAnswer((_) async => mockChurchProfile);

      await tester.pumpWidget(
        buildTestableWidget(
          const ChurchProfileEditScreen(churchId: 'ch-200'),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Editar Congregação'), findsOneWidget);
      expect(find.text('Igreja Vida Eterna'), findsOneWidget);
      expect(find.text('Av. Brasil, 500'), findsOneWidget);
      expect(find.byKey(const ValueKey('church_active_switch')),
          findsOneWidget);
      expect(find.byKey(const ValueKey('add_schedule_button')),
          findsOneWidget);
      expect(find.byKey(const ValueKey('schedule_card_0')), findsOneWidget);
      expect(find.text('Domingo às 10:00'), findsOneWidget);
    });

    testWidgets('toggling active switch calls setChurchStatus', (tester) async {
      configureViewport(tester);

      when(() => mockRepository.getChurchProfile('ch-200'))
          .thenAnswer((_) async => mockChurchProfile);
      when(() => mockRepository.setChurchStatus(
            'ch-200',
            any(),
            ifMatchHeader: any(named: 'ifMatchHeader'),
          )).thenAnswer(
        (_) async => const ChurchStatusResponseModel(
          churchId: 'ch-200',
          isActive: false,
          message: 'Status atualizado',
        ),
      );

      await tester.pumpWidget(
        buildTestableWidget(
          const ChurchProfileEditScreen(churchId: 'ch-200'),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('church_active_switch')));
      await tester.pumpAndSettle();

      verify(() => mockRepository.setChurchStatus(
            'ch-200',
            any(),
            ifMatchHeader: any(named: 'ifMatchHeader'),
          )).called(1);
      expect(find.byKey(const ValueKey('church_status_snackbar')),
          findsOneWidget);
    });

    testWidgets('adding schedule opens dialog and appends item to list',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.getChurchProfile('ch-200'))
          .thenAnswer((_) async => mockChurchProfile);

      await tester.pumpWidget(
        buildTestableWidget(
          const ChurchProfileEditScreen(churchId: 'ch-200'),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('add_schedule_button')));
      await tester.pumpAndSettle();

      expect(find.text('Adicionar Horário de Culto'), findsOneWidget);

      await tester.tap(find.byKey(const ValueKey('confirm_add_schedule_button')));
      await tester.pumpAndSettle();

      // Now we should have 2 schedule cards (0 and 1)
      expect(find.byKey(const ValueKey('schedule_card_0')), findsOneWidget);
      expect(find.byKey(const ValueKey('schedule_card_1')), findsOneWidget);
    });

    testWidgets('shows Place ID conflict snackbar when ConflictFailure is emitted',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.getChurchProfile('ch-200'))
          .thenAnswer((_) async => mockChurchProfile);
      when(() => mockRepository.updateChurchProfile(
            'ch-200',
            any(),
            ifMatchHeader: any(named: 'ifMatchHeader'),
          )).thenThrow(
        const ConflictFailure(
          'Place ID já cadastrado',
          errorCode: 'PLACE_ID_JA_VINCULADO',
        ),
      );

      await tester.pumpWidget(
        buildTestableWidget(
          const ChurchProfileEditScreen(churchId: 'ch-200'),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('save_church_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const ValueKey('church_place_id_conflict_snackbar')),
          findsOneWidget);
    });

    testWidgets('shows Concurrency conflict snackbar when concurrency stamp is stale',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.getChurchProfile('ch-200'))
          .thenAnswer((_) async => mockChurchProfile);
      when(() => mockRepository.updateChurchProfile(
            'ch-200',
            any(),
            ifMatchHeader: any(named: 'ifMatchHeader'),
          )).thenThrow(
        const ConflictFailure(
          'Dados modificados por outro usuário',
          errorCode: 'CONFLITO_CONCORRENCIA',
        ),
      );

      await tester.pumpWidget(
        buildTestableWidget(
          const ChurchProfileEditScreen(churchId: 'ch-200'),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('save_church_button')));
      await tester.pumpAndSettle();

      expect(
          find.byKey(const ValueKey('church_concurrency_conflict_snackbar')),
          findsOneWidget);
    });
  });
}
