import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:search_a_church_app/features/claim/claim.dart';

class MockClaimRepository extends Mock implements IClaimRepository {}

class FakeClaimInitiationRequestModel extends Fake
    implements ClaimInitiationRequestModel {}

class FakeGeofenceVerificationRequestModel extends Fake
    implements GeofenceVerificationRequestModel {}

class FakeDisputeContestRequestModel extends Fake
    implements DisputeContestRequestModel {}

class FakeDisputeEvidenceRequestModel extends Fake
    implements DisputeEvidenceRequestModel {}

void main() {
  late MockClaimRepository mockRepository;
  late ClaimCubit claimCubit;
  late DisputeCubit disputeCubit;

  setUpAll(() {
    registerFallbackValue(FakeClaimInitiationRequestModel());
    registerFallbackValue(FakeGeofenceVerificationRequestModel());
    registerFallbackValue(FakeDisputeContestRequestModel());
    registerFallbackValue(FakeDisputeEvidenceRequestModel());
  });

  setUp(() {
    mockRepository = MockClaimRepository();
    claimCubit = ClaimCubit(repository: mockRepository);
    disputeCubit = DisputeCubit(repository: mockRepository);
  });

  tearDown(() {
    claimCubit.close();
    disputeCubit.close();
  });

  void configureViewport(WidgetTester tester) {
    tester.view.physicalSize = const Size(800, 1600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(() {
      tester.view.resetPhysicalSize();
      tester.view.resetDevicePixelRatio();
    });
  }

  Widget buildTestableWidget(Widget child) {
    return MultiBlocProvider(
      providers: [
        BlocProvider<ClaimCubit>.value(value: claimCubit),
        BlocProvider<DisputeCubit>.value(value: disputeCubit),
      ],
      child: MaterialApp(
        home: child,
      ),
    );
  }

  group('ClaimInitiationScreen Widget Tests', () {
    const testChurchId = 'church-123';
    const testChurchName = 'Igreja Batista Central';
    const testChurchAddress = 'Av. Paulista, 1000 - São Paulo, SP';
    const testLat = -23.5600;
    const testLng = -46.6500;

    testWidgets('renders pre-filled church arguments and initial state',
        (tester) async {
      configureViewport(tester);

      await tester.pumpWidget(
        buildTestableWidget(
          const ClaimInitiationScreen(
            churchId: testChurchId,
            churchName: testChurchName,
            churchAddress: testChurchAddress,
            latitude: testLat,
            longitude: testLng,
          ),
        ),
      );

      expect(find.text(testChurchName), findsOneWidget);
      expect(find.text(testChurchAddress), findsOneWidget);
      expect(find.textContaining(testChurchId), findsOneWidget);
      expect(find.byKey(const Key('tos_art299_checkbox')), findsOneWidget);
      expect(
          find.byKey(const Key('tos_intermediary_checkbox')), findsOneWidget);
      expect(find.byKey(const Key('initiate_claim_button')), findsOneWidget);

      // Verify that initiate button is initially disabled (requires both checkboxes)
      final button = tester.widget<ElevatedButton>(
          find.byKey(const Key('initiate_claim_button')));
      expect(button.onPressed, isNull);
    });

    testWidgets(
        'enables initiate button only when both ToS checkboxes are checked',
        (tester) async {
      configureViewport(tester);

      await tester.pumpWidget(
        buildTestableWidget(
          const ClaimInitiationScreen(
            churchId: testChurchId,
            churchName: testChurchName,
            churchAddress: testChurchAddress,
            latitude: testLat,
            longitude: testLng,
          ),
        ),
      );

      // Check first checkbox
      await tester.ensureVisible(find.byKey(const Key('tos_art299_checkbox')));
      await tester.tap(find.byKey(const Key('tos_art299_checkbox')));
      await tester.pumpAndSettle();

      var button = tester.widget<ElevatedButton>(
          find.byKey(const Key('initiate_claim_button')));
      expect(button.onPressed, isNull);

      // Check second checkbox
      await tester
          .ensureVisible(find.byKey(const Key('tos_intermediary_checkbox')));
      await tester.tap(find.byKey(const Key('tos_intermediary_checkbox')));
      await tester.pumpAndSettle();

      button = tester.widget<ElevatedButton>(
          find.byKey(const Key('initiate_claim_button')));
      expect(button.onPressed, isNotNull);
    });

    testWidgets(
        'submits claim initiation and navigates to Geofence screen on success',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.initiateClaim(any())).thenAnswer(
        (_) async => const ChurchClaimModel(
          claimId: 'claim-abc-999',
          churchId: testChurchId,
          status: ClaimRecordStatus.pending,
          targetTier: VerificationTier.tier3SocialPresencial,
          ttlHours: 48,
          message: 'Processo iniciado com sucesso.',
        ),
      );

      await tester.pumpWidget(
        buildTestableWidget(
          const ClaimInitiationScreen(
            churchId: testChurchId,
            churchName: testChurchName,
            churchAddress: testChurchAddress,
            latitude: testLat,
            longitude: testLng,
          ),
        ),
      );

      // Check both checkboxes
      await tester.ensureVisible(find.byKey(const Key('tos_art299_checkbox')));
      await tester.tap(find.byKey(const Key('tos_art299_checkbox')));
      await tester
          .ensureVisible(find.byKey(const Key('tos_intermediary_checkbox')));
      await tester.tap(find.byKey(const Key('tos_intermediary_checkbox')));
      await tester.pumpAndSettle();

      // Tap initiate button
      await tester
          .ensureVisible(find.byKey(const Key('initiate_claim_button')));
      await tester.tap(find.byKey(const Key('initiate_claim_button')));
      await tester.pumpAndSettle();

      verify(() => mockRepository.initiateClaim(any())).called(1);
      // Verify navigated to Geofence screen
      expect(find.byType(ClaimGeofenceCameraScreen), findsOneWidget);
    });

    testWidgets('shows error banner and dispute button when conflict arises',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.initiateClaim(any())).thenThrow(
        const ConflictFailure(
          'Igreja já reivindicada por outro usuário.',
          errorCode: 'IGREJA_JA_REIVINDICADA',
          statusCode: 409,
        ),
      );

      await tester.pumpWidget(
        buildTestableWidget(
          const ClaimInitiationScreen(
            churchId: testChurchId,
            churchName: testChurchName,
            churchAddress: testChurchAddress,
            latitude: testLat,
            longitude: testLng,
          ),
        ),
      );

      await tester.ensureVisible(find.byKey(const Key('tos_art299_checkbox')));
      await tester.tap(find.byKey(const Key('tos_art299_checkbox')));
      await tester
          .ensureVisible(find.byKey(const Key('tos_intermediary_checkbox')));
      await tester.tap(find.byKey(const Key('tos_intermediary_checkbox')));
      await tester.pumpAndSettle();

      await tester
          .ensureVisible(find.byKey(const Key('initiate_claim_button')));
      await tester.tap(find.byKey(const Key('initiate_claim_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('claim_error_banner')), findsOneWidget);
      expect(find.byKey(const Key('open_dispute_button')), findsOneWidget);
      expect(find.text('Igreja já reivindicada por outro usuário.'),
          findsOneWidget);

      // Tapping open dispute button navigates to ClaimDisputeScreen
      await tester.ensureVisible(find.byKey(const Key('open_dispute_button')));
      await tester.tap(find.byKey(const Key('open_dispute_button')));
      await tester.pumpAndSettle();

      expect(find.byType(ClaimDisputeScreen), findsOneWidget);
    });

    testWidgets('fromArguments factory correctly maps navigation arguments',
        (tester) async {
      final widget = ClaimInitiationScreen.fromArguments({
        'placeId': 'ChIJ_abc',
        'churchId': 'church-555',
        'name': 'Igreja Presbiteriana',
        'address': 'Rua Augusta, 500',
        'latitude': -23.55,
        'longitude': -46.66,
      });

      expect(widget.churchId, 'church-555');
      expect(widget.churchName, 'Igreja Presbiteriana');
      expect(widget.churchAddress, 'Rua Augusta, 500');
      expect(widget.latitude, -23.55);
      expect(widget.longitude, -46.66);
    });
  });

  group('ClaimMethodSelectionScreen Widget Tests', () {
    testWidgets('renders all 3 probationary tiers with seals and powers',
        (tester) async {
      ValidationMethod? selectedMethod;
      VerificationTier? selectedTier;

      await tester.pumpWidget(
        MaterialApp(
          home: ClaimMethodSelectionScreen(
            onMethodSelected: (method, tier) {
              selectedMethod = method;
              selectedTier = tier;
            },
          ),
        ),
      );

      // Verify Tier 1
      expect(
          find.byKey(const Key('method_tier1_cartorio_card')), findsOneWidget);
      expect(find.text('Selo Ouro / Pleno'), findsOneWidget);
      expect(find.byKey(const Key('select_tier1_button')), findsOneWidget);

      // Verify Tier 2
      expect(find.byKey(const Key('method_tier2_domain_card')), findsOneWidget);
      expect(find.text('Selo Prata / Institucional'), findsOneWidget);
      expect(find.byKey(const Key('select_tier2_button')), findsOneWidget);

      // Verify Tier 3
      expect(
          find.byKey(const Key('method_tier3_geofence_card')), findsOneWidget);
      expect(find.text('Selo Bronze / Probatório'), findsOneWidget);
      expect(find.byKey(const Key('select_tier3_button')), findsOneWidget);

      // Tap Tier 1 button
      await tester.tap(find.byKey(const Key('select_tier1_button')));
      await tester.pumpAndSettle();

      expect(selectedMethod, ValidationMethod.cartorioRcpj);
      expect(selectedTier, VerificationTier.tier1Cartorio);
    });
  });

  group('ClaimGeofenceCameraScreen Widget Tests', () {
    const claimId = 'claim-test-123';
    const targetLat = -23.5505;
    const targetLng = -46.6333;

    testWidgets('displays in-range feedback when within 100 meters',
        (tester) async {
      configureViewport(tester);

      await tester.pumpWidget(
        buildTestableWidget(
          const ClaimGeofenceCameraScreen(
            claimId: claimId,
            targetLatitude: targetLat,
            targetLongitude: targetLng,
            initialDeviceLatitude: targetLat, // Same position: distance ~ 0m
            initialDeviceLongitude: targetLng,
            initialAccuracyMeters: 10.0,
            initialIsMockLocation: false,
          ),
        ),
      );

      expect(
          find.byKey(const Key('geofence_distance_feedback')), findsOneWidget);
      expect(find.byKey(const Key('geofence_status_text')), findsOneWidget);
      expect(find.textContaining('Dentro do raio permitido'), findsOneWidget);
      expect(find.byKey(const Key('geofence_photo_button')), findsOneWidget);

      // Validate button is disabled until photo is taken
      var button = tester.widget<ElevatedButton>(
          find.byKey(const Key('geofence_validate_button')));
      expect(button.onPressed, isNull);
    });

    testWidgets('displays out-of-range warning when outside 100 meters',
        (tester) async {
      configureViewport(tester);

      await tester.pumpWidget(
        buildTestableWidget(
          const ClaimGeofenceCameraScreen(
            claimId: claimId,
            targetLatitude: targetLat,
            targetLongitude: targetLng,
            // 0.05 degrees away (~5.5 km)
            initialDeviceLatitude: targetLat + 0.05,
            initialDeviceLongitude: targetLng + 0.05,
            initialAccuracyMeters: 15.0,
            initialIsMockLocation: false,
          ),
        ),
      );

      expect(find.textContaining('Fora do raio permitido'), findsOneWidget);

      // Validate button remains disabled
      final button = tester.widget<ElevatedButton>(
          find.byKey(const Key('geofence_validate_button')));
      expect(button.onPressed, isNull);
    });

    testWidgets('displays mock location alert when GPS spoofing is detected',
        (tester) async {
      configureViewport(tester);

      await tester.pumpWidget(
        buildTestableWidget(
          const ClaimGeofenceCameraScreen(
            claimId: claimId,
            targetLatitude: targetLat,
            targetLongitude: targetLng,
            initialDeviceLatitude: targetLat,
            initialDeviceLongitude: targetLng,
            initialAccuracyMeters: 10.0,
            initialIsMockLocation: true, // Mock location simulated
          ),
        ),
      );

      expect(find.textContaining('Localização simulada detectada'),
          findsOneWidget);

      final button = tester.widget<ElevatedButton>(
          find.byKey(const Key('geofence_validate_button')));
      expect(button.onPressed, isNull);
    });

    testWidgets('captures live photo and validates geofence presence successfully',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.verifyGeofence(any())).thenAnswer(
        (_) async => const VerificationResultModel(
          claimId: claimId,
          churchId: 'church-123',
          isVerified: true,
          tier: VerificationTier.tier3SocialPresencial,
          message: 'Presença física validada com sucesso!',
        ),
      );

      await tester.pumpWidget(
        buildTestableWidget(
          const ClaimGeofenceCameraScreen(
            claimId: claimId,
            targetLatitude: targetLat,
            targetLongitude: targetLng,
            initialDeviceLatitude: targetLat,
            initialDeviceLongitude: targetLng,
            initialAccuracyMeters: 10.0,
            initialIsMockLocation: false,
          ),
        ),
      );

      // Tap capture live photo button
      await tester.ensureVisible(find.byKey(const Key('geofence_photo_button')));
      await tester.tap(find.byKey(const Key('geofence_photo_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('geofence_photo_preview')), findsOneWidget);
      expect(find.textContaining('Hash SHA-256'), findsOneWidget);

      // Validate button is now enabled
      final button = tester.widget<ElevatedButton>(
          find.byKey(const Key('geofence_validate_button')));
      expect(button.onPressed, isNotNull);

      // Tap validate button
      await tester
          .ensureVisible(find.byKey(const Key('geofence_validate_button')));
      await tester.tap(find.byKey(const Key('geofence_validate_button')));
      await tester.pumpAndSettle();

      verify(() => mockRepository.verifyGeofence(any())).called(1);
      expect(find.byKey(const Key('geofence_success_badge')), findsOneWidget);
    });
  });

  group('ClaimDisputeScreen Widget Tests', () {
    const churchId = 'church-789';

    testWidgets('renders contest form and submits formal dispute',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.contestDispute(any())).thenAnswer(
        (_) async => const DisputeCaseModel(
          success: true,
          resolutionType: DisputeResolutionType.parityDisputeOpened,
          churchId: churchId,
          disputeId: 'disp-001',
          deadlineAt: null,
          message: 'Litígio instaurado com sucesso.',
          currentTier: VerificationTier.tier2Institucional,
        ),
      );

      await tester.pumpWidget(
        buildTestableWidget(
          const ClaimDisputeScreen(
            churchId: churchId,
            churchName: 'Igreja Metodista',
          ),
        ),
      );

      expect(find.byKey(const Key('dispute_rep_name_input')), findsOneWidget);
      expect(find.byKey(const Key('dispute_rep_cpf_input')), findsOneWidget);
      expect(find.byKey(const Key('dispute_cnpj_input')), findsOneWidget);
      expect(find.byKey(const Key('dispute_file_hash_input')), findsOneWidget);
      expect(find.byKey(const Key('dispute_tos_checkbox')), findsOneWidget);
      expect(find.byKey(const Key('dispute_contest_button')), findsOneWidget);

      // Fill in required form fields
      await tester.enterText(
          find.byKey(const Key('dispute_rep_name_input')), 'Pastor Paulo');
      await tester.enterText(
          find.byKey(const Key('dispute_rep_cpf_input')), '12345678900');
      await tester.enterText(
          find.byKey(const Key('dispute_cnpj_input')), '12345678000199');
      await tester.enterText(
          find.byKey(const Key('dispute_file_hash_input')),
          'a1b2c3d4e5f678901234567890abcdef1234567890abcdef1234567890abcdef');

      await tester.ensureVisible(find.byKey(const Key('dispute_tos_checkbox')));
      await tester.tap(find.byKey(const Key('dispute_tos_checkbox')));
      await tester.pumpAndSettle();

      // Submit dispute
      await tester
          .ensureVisible(find.byKey(const Key('dispute_contest_button')));
      await tester.tap(find.byKey(const Key('dispute_contest_button')));
      await tester.pumpAndSettle();

      verify(() => mockRepository.contestDispute(any())).called(1);
      // Parity Dispute Opened state UI
      expect(find.byKey(const Key('dispute_frozen_notice')), findsOneWidget);
      expect(find.textContaining('Perfil Congelado (In_Dispute)'),
          findsOneWidget);
      expect(find.byKey(const Key('dispute_deadline_countdown')),
          findsOneWidget);
      expect(find.byKey(const Key('dispute_submit_cert_button')),
          findsOneWidget);
    });

    testWidgets('submits supplementary certificate in parity dispute',
        (tester) async {
      configureViewport(tester);

      when(() => mockRepository.submitDisputeCertificate(any())).thenAnswer(
        (_) async => DisputeEvidenceResponseModel(
          disputeId: 'disp-001',
          userId: 'user-1',
          submittedAt: DateTime.now(),
          documentFileHash: 'feedbeef1234567890',
          averbationDate: DateTime.now(),
          message: 'Certidão complementar averbada anexada com sucesso.',
        ),
      );

      // Emit DisputeParityOpened state
      disputeCubit.emit(
        DisputeParityOpened(
          DisputeCaseModel(
            success: true,
            resolutionType: DisputeResolutionType.parityDisputeOpened,
            churchId: churchId,
            disputeId: 'disp-001',
            deadlineAt: DateTime.now().add(const Duration(days: 5)),
            message: 'Litígio instaurado.',
            currentTier: VerificationTier.tier2Institucional,
          ),
        ),
      );

      await tester.pumpWidget(
        buildTestableWidget(
          const ClaimDisputeScreen(
            churchId: churchId,
            churchName: 'Igreja Batista Boas Novas',
            initialDisputeId: 'disp-001',
          ),
        ),
      );

      expect(find.byKey(const Key('dispute_frozen_notice')), findsOneWidget);
      expect(find.byKey(const Key('dispute_deadline_countdown')),
          findsOneWidget);
      expect(find.byKey(const Key('dispute_supplementary_hash_input')),
          findsOneWidget);

      await tester.enterText(
          find.byKey(const Key('dispute_supplementary_hash_input')),
          'feedbeef1234567890');
      await tester.pumpAndSettle();

      await tester
          .ensureVisible(find.byKey(const Key('dispute_submit_cert_button')));
      await tester.tap(find.byKey(const Key('dispute_submit_cert_button')));
      await tester.pumpAndSettle();

      verify(() => mockRepository.submitDisputeCertificate(any())).called(1);
    });
  });
}
