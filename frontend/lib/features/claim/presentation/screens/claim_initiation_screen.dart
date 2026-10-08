import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../data/models/claim_enums.dart';
import '../cubit/claim_cubit.dart';
import '../cubit/claim_state.dart';
import 'claim_dispute_screen.dart';
import 'claim_geofence_camera_screen.dart';
import 'claim_method_selection_screen.dart';

/// Tela inicial do fluxo de reivindicação de perfil de congregação.
/// Recebe argumentos pré-preenchidos a partir da navegação do mapa,
/// valida os checkboxes mandatórios de Fé Pública (art. 299 CP) e Marco Civil (art. 15),
/// e dispara a submissão inicial junto ao ClaimCubit.
class ClaimInitiationScreen extends StatefulWidget {
  final String churchId;
  final String churchName;
  final String churchAddress;
  final double latitude;
  final double longitude;
  final String? placeId;

  const ClaimInitiationScreen({
    super.key,
    required this.churchId,
    required this.churchName,
    required this.churchAddress,
    required this.latitude,
    required this.longitude,
    this.placeId,
  });

  /// Constrói a tela a partir do mapa de argumentos da rota `/claim`.
  factory ClaimInitiationScreen.fromArguments(Map<String, dynamic> arguments) {
    final placeId = arguments['placeId'] as String?;
    final id = arguments['churchId'] as String? ?? arguments['id'] as String? ?? placeId ?? '';
    final name = arguments['name'] as String? ?? 'Congregação';
    final address = arguments['address'] as String? ?? '';
    final lat = (arguments['latitude'] as num?)?.toDouble() ?? 0.0;
    final lng = (arguments['longitude'] as num?)?.toDouble() ?? 0.0;

    return ClaimInitiationScreen(
      churchId: id,
      churchName: name,
      churchAddress: address,
      latitude: lat,
      longitude: lng,
      placeId: placeId,
    );
  }

  @override
  State<ClaimInitiationScreen> createState() => _ClaimInitiationScreenState();
}

class _ClaimInitiationScreenState extends State<ClaimInitiationScreen> {
  bool _art299Accepted = false;
  bool _technicalIntermediaryAccepted = false;

  ValidationMethod _selectedMethod = ValidationMethod.geofence;
  VerificationTier _selectedTier = VerificationTier.tier3SocialPresencial;

  bool get _isFormValid => _art299Accepted && _technicalIntermediaryAccepted;

  void _onSelectMethodTier(ValidationMethod method, VerificationTier tier) {
    setState(() {
      _selectedMethod = method;
      _selectedTier = tier;
    });
  }

  void _onInitiateClaim() {
    if (!_isFormValid) return;

    context.read<ClaimCubit>().initiateClaim(
          churchId: widget.churchId,
          art299Accepted: _art299Accepted,
          technicalIntermediaryAccepted: _technicalIntermediaryAccepted,
          validationMethod: _selectedMethod,
          targetTier: _selectedTier,
        );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Reivindicar Congregação'),
      ),
      body: BlocConsumer<ClaimCubit, ClaimState>(
        listener: (context, state) {
          if (state is ClaimInitiated) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(
                  'Reivindicação iniciada! Prazo de ${state.claim.ttlHours}h para envio de provas.',
                ),
                backgroundColor: Colors.green.shade700,
              ),
            );

            // Se o método escolhido for geofence, navega automaticamente para a câmera/geofence
            if (_selectedMethod == ValidationMethod.geofence) {
              Navigator.of(context).push(
                MaterialPageRoute(
                  builder: (_) => BlocProvider.value(
                    value: context.read<ClaimCubit>(),
                    child: ClaimGeofenceCameraScreen(
                      claimId: state.claim.claimId,
                      targetLatitude: widget.latitude,
                      targetLongitude: widget.longitude,
                      churchName: widget.churchName,
                    ),
                  ),
                ),
              );
            }
          }
        },
        builder: (context, state) {
          final isSubmitting = state is ClaimSubmitting;

          return SingleChildScrollView(
            padding: const EdgeInsets.all(16.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // 1. Cartão com Informações da Igreja
                Card(
                  elevation: 2,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                  child: Padding(
                    padding: const EdgeInsets.all(16.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            CircleAvatar(
                              backgroundColor: theme.colorScheme.primaryContainer,
                              child: Icon(Icons.church, color: theme.colorScheme.primary),
                            ),
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
                                  const SizedBox(height: 2),
                                  Text(
                                    widget.churchAddress,
                                    style: theme.textTheme.bodySmall?.copyWith(
                                      color: theme.colorScheme.onSurfaceVariant,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                        const Divider(height: 20),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text(
                              'ID: ${widget.churchId.isNotEmpty ? widget.churchId : (widget.placeId ?? 'Pendente')}',
                              style: theme.textTheme.labelSmall?.copyWith(
                                color: theme.colorScheme.outline,
                              ),
                            ),
                            Text(
                              'GPS: ${widget.latitude.toStringAsFixed(4)}, ${widget.longitude.toStringAsFixed(4)}',
                              style: theme.textTheme.labelSmall?.copyWith(
                                color: theme.colorScheme.outline,
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // 2. Seletor de Nível Probatório / Método
                Card(
                  elevation: 1,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                  child: Padding(
                    padding: const EdgeInsets.all(16.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text(
                              'Método de Verificação',
                              style: theme.textTheme.titleSmall?.copyWith(
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                            TextButton.icon(
                              key: const Key('view_tiers_button'),
                              icon: const Icon(Icons.info_outline, size: 16),
                              label: const Text('Ver Níveis'),
                              onPressed: () {
                                Navigator.of(context).push(
                                  MaterialPageRoute(
                                    builder: (_) => ClaimMethodSelectionScreen(
                                      churchId: widget.churchId,
                                      churchName: widget.churchName,
                                      onMethodSelected: (m, t) {
                                        _onSelectMethodTier(m, t);
                                        Navigator.of(context).pop();
                                      },
                                    ),
                                  ),
                                );
                              },
                            ),
                          ],
                        ),
                        const SizedBox(height: 8),
                        _buildMethodOptionTile(
                          key: const Key('method_radio_geofence'),
                          method: ValidationMethod.geofence,
                          tier: VerificationTier.tier3SocialPresencial,
                          title: 'Presencial Geofence (GPS ≤ 100m + Foto ao Vivo)',
                          subtitle: 'Nível 3 (Selo Bronze / Probatório)',
                          icon: Icons.pin_drop,
                        ),
                        _buildMethodOptionTile(
                          key: const Key('method_radio_domain'),
                          method: ValidationMethod.institutionalEmail,
                          tier: VerificationTier.tier2Institucional,
                          title: 'E-mail Institucional (@dominio.org) ou QSA',
                          subtitle: 'Nível 2 (Selo Prata / Institucional)',
                          icon: Icons.business,
                        ),
                        _buildMethodOptionTile(
                          key: const Key('method_radio_cartorio'),
                          method: ValidationMethod.cartorioRcpj,
                          tier: VerificationTier.tier1Cartorio,
                          title: 'Ata e Estatuto no Cartório RCPJ',
                          subtitle: 'Nível 1 (Selo Ouro / Prevalência Máxima)',
                          icon: Icons.verified_user,
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // 3. Termos Legais Mandatórios (Art. 299 CP e Marco Civil Art. 15)
                Card(
                  elevation: 1,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                  child: Padding(
                    padding: const EdgeInsets.all(16.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Declaração de Fé Pública e Termos Legais',
                          style: theme.textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(height: 10),
                        CheckboxListTile(
                          key: const Key('tos_art299_checkbox'),
                          value: _art299Accepted,
                          contentPadding: EdgeInsets.zero,
                          controlAffinity: ListTileControlAffinity.leading,
                          title: const Text(
                            'Declaro, sob as penas do art. 299 do Código Penal (Falsidade Ideológica), que possuo legitimidade e representação sobre esta congregação.',
                            style: TextStyle(fontSize: 13),
                          ),
                          onChanged: (val) {
                            setState(() {
                              _art299Accepted = val ?? false;
                            });
                          },
                        ),
                        const Divider(height: 12),
                        CheckboxListTile(
                          key: const Key('tos_intermediary_checkbox'),
                          value: _technicalIntermediaryAccepted,
                          contentPadding: EdgeInsets.zero,
                          controlAffinity: ListTileControlAffinity.leading,
                          title: const Text(
                            'Reconheço o Search A Church como provedor de aplicação neutro (Marco Civil da Internet, art. 15), autorizando a coleta e guarda de registros por 180 dias para fins de auditoria e segurança jurídica.',
                            style: TextStyle(fontSize: 13),
                          ),
                          onChanged: (val) {
                            setState(() {
                              _technicalIntermediaryAccepted = val ?? false;
                            });
                          },
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // 4. Banner de Erro com CTA de Disputa se Conflito
                if (state is ClaimError) ...[
                  Container(
                    key: const Key('claim_error_banner'),
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: Colors.red.shade100,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: Colors.red.shade300),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Icon(Icons.error_outline, color: Colors.red.shade900),
                            const SizedBox(width: 8),
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
                        if (state.isConflict) ...[
                          const SizedBox(height: 10),
                          SizedBox(
                            width: double.infinity,
                            child: ElevatedButton.icon(
                              key: const Key('open_dispute_button'),
                              icon: const Icon(Icons.gavel),
                              label: const Text('Abrir Contestação Formal'),
                              style: ElevatedButton.styleFrom(
                                backgroundColor: Colors.red.shade800,
                                foregroundColor: Colors.white,
                              ),
                              onPressed: () {
                                Navigator.of(context).push(
                                  MaterialPageRoute(
                                    builder: (_) => ClaimDisputeScreen(
                                      churchId: widget.churchId,
                                      churchName: widget.churchName,
                                    ),
                                  ),
                                );
                              },
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),
                ],

                // 5. Botão de Iniciar Reivindicação
                SizedBox(
                  width: double.infinity,
                  height: 50,
                  child: ElevatedButton(
                    key: const Key('initiate_claim_button'),
                    style: ElevatedButton.styleFrom(
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                    onPressed: _isFormValid && !isSubmitting ? _onInitiateClaim : null,
                    child: isSubmitting
                        ? const SizedBox(
                            width: 22,
                            height: 22,
                            child: CircularProgressIndicator(
                              key: Key('claim_submitting_indicator'),
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Text(
                            'Iniciar Reivindicação',
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.bold,
                            ),
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

  Widget _buildMethodOptionTile({
    required Key key,
    required ValidationMethod method,
    required VerificationTier tier,
    required String title,
    required String subtitle,
    required IconData icon,
  }) {
    final isSelected = _selectedMethod == method;
    final theme = Theme.of(context);
    return InkWell(
      key: key,
      borderRadius: BorderRadius.circular(10),
      onTap: () => _onSelectMethodTier(method, tier),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        margin: const EdgeInsets.symmetric(vertical: 4),
        decoration: BoxDecoration(
          color: isSelected
              ? theme.colorScheme.primaryContainer.withValues(alpha: 0.3)
              : null,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(
            color: isSelected
                ? theme.colorScheme.primary
                : theme.colorScheme.outlineVariant,
          ),
        ),
        child: Row(
          children: [
            Icon(
              isSelected ? Icons.radio_button_checked : Icons.radio_button_off,
              color: isSelected
                  ? theme.colorScheme.primary
                  : theme.colorScheme.outline,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: theme.textTheme.bodyMedium?.copyWith(
                      fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                    ),
                  ),
                  Text(
                    subtitle,
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
    );
  }
}

