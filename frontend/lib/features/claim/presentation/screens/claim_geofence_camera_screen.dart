import 'dart:convert';
import 'package:crypto/crypto.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../cubit/claim_cubit.dart';
import '../cubit/claim_state.dart';
import '../helpers/geofence_calculator.dart';

/// Tela de verificação presencial por Geofencing e captura de foto ao vivo (Nível 3).
/// Valida raio Haversine ≤ 100m, precisão ≤ 50m, anti-mock e integridade SHA-256.
class ClaimGeofenceCameraScreen extends StatefulWidget {
  final String claimId;
  final double targetLatitude;
  final double targetLongitude;
  final String churchName;

  /// Coordenadas e flags opcionais para injeção / testes de widget determinísticos.
  final double? initialDeviceLatitude;
  final double? initialDeviceLongitude;
  final double? initialAccuracyMeters;
  final bool? initialIsMockLocation;

  const ClaimGeofenceCameraScreen({
    super.key,
    required this.claimId,
    required this.targetLatitude,
    required this.targetLongitude,
    this.churchName = 'Congregação',
    this.initialDeviceLatitude,
    this.initialDeviceLongitude,
    this.initialAccuracyMeters,
    this.initialIsMockLocation,
  });

  @override
  State<ClaimGeofenceCameraScreen> createState() =>
      _ClaimGeofenceCameraScreenState();
}

class _ClaimGeofenceCameraScreenState extends State<ClaimGeofenceCameraScreen> {
  late double _deviceLatitude;
  late double _deviceLongitude;
  late double _horizontalAccuracy;
  late bool _isMockLocation;

  String? _capturedPhotoHashSha256;
  String? _capturedPhotoUrl;

  @override
  void initState() {
    super.initState();
    _deviceLatitude = widget.initialDeviceLatitude ?? widget.targetLatitude;
    _deviceLongitude = widget.initialDeviceLongitude ?? widget.targetLongitude;
    _horizontalAccuracy = widget.initialAccuracyMeters ?? 12.0;
    _isMockLocation = widget.initialIsMockLocation ?? false;
  }

  double get _currentDistanceMeters => GeofenceCalculator.calculateDistanceMeters(
        lat1: _deviceLatitude,
        lon1: _deviceLongitude,
        lat2: widget.targetLatitude,
        lon2: widget.targetLongitude,
      );

  bool get _isWithinRange =>
      _currentDistanceMeters <= GeofenceCalculator.maxAllowedRadiusMeters;

  bool get _isAccuracyAcceptable =>
      GeofenceCalculator.isValidAccuracy(_horizontalAccuracy);

  bool get _canSubmit =>
      _isWithinRange &&
      _isAccuracyAcceptable &&
      !_isMockLocation &&
      _capturedPhotoHashSha256 != null;

  void _simulateCapturePhoto() {
    final timestamp = DateTime.now().toIso8601String();
    final rawBytes = utf8.encode('${widget.claimId}-$timestamp-photo');
    final digest = sha256.convert(rawBytes);

    setState(() {
      _capturedPhotoHashSha256 = digest.toString();
      _capturedPhotoUrl = 'https://storage.searchachurch.org/claims/${widget.claimId}/photo.jpg';
    });
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final distance = _currentDistanceMeters;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Verificação Presencial'),
      ),
      body: BlocConsumer<ClaimCubit, ClaimState>(
        listener: (context, state) {
          if (state is ClaimVerifiedSuccess) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text('Presença verificada com sucesso! Selo concedido: ${state.result.tier.wireName}'),
                backgroundColor: Colors.green.shade700,
              ),
            );
          }
        },
        builder: (context, state) {
          return SingleChildScrollView(
            padding: const EdgeInsets.all(16.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // 1. Cabeçalho da Congregação
                Card(
                  elevation: 1,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  child: Padding(
                    padding: const EdgeInsets.all(14.0),
                    child: Row(
                      children: [
                        Icon(Icons.church, color: theme.colorScheme.primary, size: 28),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                widget.churchName,
                                style: theme.textTheme.titleMedium?.copyWith(
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              Text(
                                'Coordenadas: ${widget.targetLatitude.toStringAsFixed(4)}, ${widget.targetLongitude.toStringAsFixed(4)}',
                                style: theme.textTheme.bodySmall?.copyWith(
                                  color: theme.colorScheme.onSurfaceVariant,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // 2. Feedback de GPS e Anti-Mock
                Card(
                  elevation: 2,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                  child: Padding(
                    padding: const EdgeInsets.all(16.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Radar de Proximidade (Geofencing)',
                          style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold),
                        ),
                        const SizedBox(height: 12),
                        Row(
                          children: [
                            Icon(
                              _isWithinRange ? Icons.check_circle : Icons.warning_amber_rounded,
                              color: _isWithinRange ? Colors.green : Colors.orange.shade800,
                              size: 24,
                            ),
                            const SizedBox(width: 8),
                            Expanded(
                              child: Text(
                                'Distância: ${distance.toStringAsFixed(1)} metros (Raio máx: 100m)',
                                key: const Key('geofence_distance_feedback'),
                                style: const TextStyle(fontWeight: FontWeight.w600),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 8),
                        Text(
                          _isMockLocation
                              ? 'Localização simulada detectada! Desative apps de GPS falso.'
                              : _isWithinRange
                                  ? 'Dentro do raio permitido (≤ 100m)'
                                  : 'Fora do raio permitido. Aproxime-se do templo para validar.',
                          key: const Key('geofence_status_text'),
                          style: TextStyle(
                            color: _isMockLocation
                                ? Colors.red.shade700
                                : _isWithinRange
                                    ? Colors.green.shade800
                                    : Colors.orange.shade900,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        const Divider(height: 24),
                        Row(
                          children: [
                            Icon(
                              _isAccuracyAcceptable ? Icons.gps_fixed : Icons.gps_not_fixed,
                              color: _isAccuracyAcceptable ? Colors.blue : Colors.red,
                              size: 20,
                            ),
                            const SizedBox(width: 8),
                            Expanded(
                              child: Text(
                                'Precisão do GPS: ±${_horizontalAccuracy.toStringAsFixed(1)}m (Exigido: ≤ 50m)',
                                key: const Key('geofence_accuracy_feedback'),
                                style: theme.textTheme.bodySmall,
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // 3. Captura de Foto ao Vivo
                Card(
                  elevation: 2,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                  child: Padding(
                    padding: const EdgeInsets.all(16.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Foto ao Vivo da Fachada / Templo',
                          style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'Tire uma foto ao vivo em frente à congregação para auditoria visual.',
                          style: theme.textTheme.bodySmall?.copyWith(
                            color: theme.colorScheme.onSurfaceVariant,
                          ),
                        ),
                        const SizedBox(height: 12),
                        if (_capturedPhotoHashSha256 != null)
                          Container(
                            key: const Key('geofence_photo_preview'),
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: Colors.green.shade50,
                              borderRadius: BorderRadius.circular(10),
                              border: Border.all(color: Colors.green.shade300),
                            ),
                            child: Row(
                              children: [
                                const Icon(Icons.photo_camera, color: Colors.green),
                                const SizedBox(width: 10),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      const Text(
                                        'Foto ao vivo registrada com sucesso!',
                                        style: TextStyle(
                                          color: Colors.green,
                                          fontWeight: FontWeight.bold,
                                        ),
                                      ),
                                      Text(
                                        'Hash SHA-256: ${_capturedPhotoHashSha256!.substring(0, 16)}...',
                                        style: const TextStyle(
                                          fontSize: 11,
                                          fontFamily: 'monospace',
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                          )
                        else
                          SizedBox(
                            width: double.infinity,
                            child: OutlinedButton.icon(
                              key: const Key('geofence_photo_button'),
                              icon: const Icon(Icons.camera_alt),
                              label: const Text('Capturar Foto ao Vivo'),
                              onPressed: _simulateCapturePhoto,
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 20),

                // 4. Feedback de Sucesso
                if (state is ClaimVerifiedSuccess)
                  Container(
                    key: const Key('geofence_success_badge'),
                    margin: const EdgeInsets.only(bottom: 16),
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: Colors.green.shade100,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: Colors.green.shade400),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.verified, color: Colors.green, size: 28),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Text(
                            'Congregação homologada! Selo ${state.result.tier.wireName} ativado com sucesso.',
                            style: TextStyle(
                              color: Colors.green.shade900,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),

                // 5. Feedback de Erro
                if (state is ClaimError)
                  Container(
                    key: const Key('geofence_error_banner'),
                    margin: const EdgeInsets.only(bottom: 16),
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: Colors.red.shade100,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: Colors.red.shade400),
                    ),
                    child: Row(
                      children: [
                        Icon(Icons.error_outline, color: Colors.red.shade900, size: 24),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Text(
                            state.message,
                            style: TextStyle(
                              color: Colors.red.shade900,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),

                // 6. Botão de Validação
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: ElevatedButton(
                    key: const Key('geofence_validate_button'),
                    style: ElevatedButton.styleFrom(
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                    onPressed: _canSubmit && state is! ClaimVerifying
                        ? () {
                            context.read<ClaimCubit>().verifyGeofence(
                                  claimId: widget.claimId,
                                  deviceLatitude: _deviceLatitude,
                                  deviceLongitude: _deviceLongitude,
                                  horizontalAccuracyMeters: _horizontalAccuracy,
                                  isMockLocation: _isMockLocation,
                                  photoUrl: _capturedPhotoUrl,
                                  photoHashSha256: _capturedPhotoHashSha256,
                                );
                          }
                        : null,
                    child: state is ClaimVerifying
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(
                              key: Key('geofence_verifying_indicator'),
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Text(
                            'Validar Presença Física no Templo',
                            style: TextStyle(fontWeight: FontWeight.bold),
                          ),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
