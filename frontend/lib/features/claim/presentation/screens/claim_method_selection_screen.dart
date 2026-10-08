import 'package:flutter/material.dart';
import '../../data/models/claim_enums.dart';

/// Tela de seleção e explicação transparente dos 3 níveis probatórios de verificação (AD-012, AD-013).
class ClaimMethodSelectionScreen extends StatelessWidget {
  final void Function(ValidationMethod method, VerificationTier tier)? onMethodSelected;
  final String? churchId;
  final String? churchName;
  final double? latitude;
  final double? longitude;

  const ClaimMethodSelectionScreen({
    super.key,
    this.onMethodSelected,
    this.churchId,
    this.churchName,
    this.latitude,
    this.longitude,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Níveis de Verificação'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Escolha o método probatório para sua congregação',
              style: theme.textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'A plataforma adota uma hierarquia estrita de fé pública. Níveis superiores possuem prevalência jurídica e poderes administrativos ampliados.',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
            const SizedBox(height: 20),

            // Tier 1: Cartório RCPJ
            _buildTierCard(
              context: context,
              cardKey: const Key('method_tier1_cartorio_card'),
              buttonKey: const Key('select_tier1_button'),
              title: 'Nível 1 - Cartório RCPJ (Ata de Posse e Estatuto)',
              sealLabel: 'Selo Ouro / Pleno',
              sealColor: Colors.amber.shade800,
              badgeIcon: Icons.verified_user,
              description:
                  'Ata de eleição/posse e Estatuto Social averbados em Cartório de Registro Civil de Pessoas Jurídicas.',
              powers: [
                'Homologação definitiva de representação legal.',
                'Prevalência jurídica absoluta sobre Tiers 2 e 3.',
                'Poder irrecorrível de resolução de disputas e litígios.',
                'Gestão corporativa e financeira integral no app.',
              ],
              onSelect: () {
                onMethodSelected?.call(
                  ValidationMethod.cartorioRcpj,
                  VerificationTier.tier1Cartorio,
                );
              },
            ),
            const SizedBox(height: 16),

            // Tier 2: Institucional / QSA
            _buildTierCard(
              context: context,
              cardKey: const Key('method_tier2_domain_card'),
              buttonKey: const Key('select_tier2_button'),
              title: 'Nível 2 - E-mail Corporativo e QSA Receita',
              sealLabel: 'Selo Prata / Institucional',
              sealColor: Colors.blueGrey.shade700,
              badgeIcon: Icons.business,
              description:
                  'Código OTP em e-mail institucional (@dominio.org) ou cruzamento cadastral de CPF com o CNPJ no QSA.',
              powers: [
                'Gestão administrativa e publicação oficial de eventos.',
                'Módulo de dízimos, ofertas e tesouraria.',
                'Aviso de pendência até eventual validação de Nível 1.',
              ],
              onSelect: () {
                onMethodSelected?.call(
                  ValidationMethod.institutionalEmail,
                  VerificationTier.tier2Institucional,
                );
              },
            ),
            const SizedBox(height: 16),

            // Tier 3: Presencial Geofence e Redes Sociais
            _buildTierCard(
              context: context,
              cardKey: const Key('method_tier3_geofence_card'),
              buttonKey: const Key('select_tier3_button'),
              title: 'Nível 3 - Presencial Geofence e Bio Social',
              sealLabel: 'Selo Bronze / Probatório',
              sealColor: Colors.brown.shade600,
              badgeIcon: Icons.pin_drop,
              description:
                  'Validação presencial por GPS (raio ≤ 100m) e foto ao vivo ou token SAC na biografia de redes sociais oficiais.',
              powers: [
                'Edição comunitária de horários de cultos e endereço.',
                'Carimbo de verificação física provisória (sujeito a sobreposição por instâncias superiores).',
              ],
              onSelect: () {
                onMethodSelected?.call(
                  ValidationMethod.geofence,
                  VerificationTier.tier3SocialPresencial,
                );
              },
            ),
            const SizedBox(height: 24),
          ],
        ),
      ),
    );
  }

  Widget _buildTierCard({
    required BuildContext context,
    required Key cardKey,
    required Key buttonKey,
    required String title,
    required String sealLabel,
    required Color sealColor,
    required IconData badgeIcon,
    required String description,
    required List<String> powers,
    required VoidCallback onSelect,
  }) {
    final theme = Theme.of(context);

    return Card(
      key: cardKey,
      elevation: 2,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(color: sealColor.withValues(alpha: 0.3)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(badgeIcon, color: sealColor, size: 28),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    title,
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: sealColor.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Text(
                sealLabel,
                style: TextStyle(
                  color: sealColor,
                  fontWeight: FontWeight.bold,
                  fontSize: 12,
                ),
              ),
            ),
            const SizedBox(height: 10),
            Text(
              description,
              style: theme.textTheme.bodySmall?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
            const Divider(height: 20),
            Text(
              'Poderes Concedidos:',
              style: theme.textTheme.labelMedium?.copyWith(
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 6),
            ...powers.map(
              (p) => Padding(
                padding: const EdgeInsets.only(bottom: 4.0),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(
                      Icons.check_circle_outline,
                      size: 16,
                      color: sealColor,
                    ),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(
                        p,
                        style: theme.textTheme.bodySmall,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: OutlinedButton(
                key: buttonKey,
                style: OutlinedButton.styleFrom(
                  foregroundColor: sealColor,
                  side: BorderSide(color: sealColor),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10),
                  ),
                ),
                onPressed: onSelect,
                child: const Text('Selecionar Este Método'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
