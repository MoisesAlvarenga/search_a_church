import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../data/models/claim_enums.dart';
import '../cubit/dispute_cubit.dart';
import '../cubit/dispute_state.dart';

/// Tela de contestação de congregação, litígio paritário e juntada de certidões averbadas em RCPJ.
class ClaimDisputeScreen extends StatefulWidget {
  final String churchId;
  final String churchName;
  final String? initialDisputeId;

  const ClaimDisputeScreen({
    super.key,
    required this.churchId,
    this.churchName = 'Congregação',
    this.initialDisputeId,
  });

  @override
  State<ClaimDisputeScreen> createState() => _ClaimDisputeScreenState();
}

class _ClaimDisputeScreenState extends State<ClaimDisputeScreen> {
  final _formKey = GlobalKey<FormState>();

  final _repNameController = TextEditingController();
  final _repCpfController = TextEditingController();
  final _cnpjController = TextEditingController();
  final _fileHashController = TextEditingController();
  final _justificationController = TextEditingController();
  final _supplementaryHashController = TextEditingController();

  DateTime _averbationDate = DateTime.now().subtract(const Duration(days: 30));
  bool _tosAccepted = false;

  @override
  void dispose() {
    _repNameController.dispose();
    _repCpfController.dispose();
    _cnpjController.dispose();
    _fileHashController.dispose();
    _justificationController.dispose();
    _supplementaryHashController.dispose();
    super.dispose();
  }

  void _onContestSubmit() {
    if (_formKey.currentState?.validate() ?? false) {
      if (!_tosAccepted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('É obrigatório aceitar o termo de fé pública.')),
        );
        return;
      }

      context.read<DisputeCubit>().contestDispute(
            churchId: widget.churchId,
            tosAccepted: _tosAccepted,
            legalRepresentativeName: _repNameController.text.trim(),
            legalRepresentativeCpf: _repCpfController.text.trim(),
            churchCnpj: _cnpjController.text.trim(),
            submittedTier: VerificationTier.tier1Cartorio,
            documentFileHash: _fileHashController.text.trim(),
            documentAverbationDate: _averbationDate,
            justification: _justificationController.text.trim().isNotEmpty
                ? _justificationController.text.trim()
                : null,
          );
    }
  }

  void _onSupplementarySubmit(String disputeId) {
    final hash = _supplementaryHashController.text.trim();
    if (hash.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Informe o hash SHA-256 da certidão complementar.')),
      );
      return;
    }

    context.read<DisputeCubit>().submitCertificate(
          disputeId: disputeId,
          documentFileHash: hash,
          averbationDate: _averbationDate,
          notes: 'Certidão averbada complementar tempestiva juntada aos autos.',
        );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Contestação e Litígio'),
      ),
      body: BlocConsumer<DisputeCubit, DisputeState>(
        listener: (context, state) {
          if (state is DisputeParityOpened) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('Litígio instaurado. Prazo de 5 dias úteis aberto para contraditório.'),
                backgroundColor: Colors.orange,
              ),
            );
          } else if (state is DisputeResolved) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text('Disputa resolvida: ${state.dispute.message}'),
                backgroundColor: Colors.green,
              ),
            );
          } else if (state is DisputeCertificateSubmitted) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('Certidão complementar juntada aos autos com sucesso!'),
                backgroundColor: Colors.green,
              ),
            );
          }
        },
        builder: (context, state) {
          final isParity = state is DisputeParityOpened;
          final isResolved = state is DisputeResolved;

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
                        Icon(Icons.gavel, color: theme.colorScheme.primary, size: 28),
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
                                'ID da Igreja: ${widget.churchId}',
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

                // 2. Banner de Congelamento e Litígio Paritário (5 Dias Úteis)
                if (isParity) ...[
                  Container(
                    key: const Key('dispute_frozen_notice'),
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: Colors.amber.shade100,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: Colors.amber.shade500),
                    ),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(Icons.lock_clock, color: Colors.amber.shade900, size: 26),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'Perfil Congelado (In_Dispute)',
                                style: TextStyle(
                                  color: Colors.amber.shade900,
                                  fontWeight: FontWeight.bold,
                                  fontSize: 15,
                                ),
                              ),
                              const SizedBox(height: 4),
                              Text(
                                'Litígio instaurado por equivalência probatória. A congregação permanece congelada para edição até a apuração da certidão mais antiga averbada em cartório.',
                                style: TextStyle(
                                  color: Colors.amber.shade900,
                                  fontSize: 13,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  Card(
                    elevation: 2,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    child: Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              const Icon(Icons.timer, color: Colors.deepOrange),
                              const SizedBox(width: 8),
                              Text(
                                'Prazo Operacional de Contraditório',
                                style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold),
                              ),
                            ],
                          ),
                          const SizedBox(height: 8),
                          Text(
                            _formatRemainingTime(state.dispute.deadlineAt),
                            key: const Key('dispute_deadline_countdown'),
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.bold,
                              color: Colors.deepOrange.shade800,
                            ),
                          ),
                          const Divider(height: 24),
                          Text(
                            'Juntada de Certidão Averbada Complementar:',
                            style: theme.textTheme.bodySmall?.copyWith(fontWeight: FontWeight.bold),
                          ),
                          const SizedBox(height: 8),
                          TextFormField(
                            controller: _supplementaryHashController,
                            key: const Key('dispute_supplementary_hash_input'),
                            decoration: const InputDecoration(
                              labelText: 'Hash SHA-256 da Certidão Adicional',
                              border: OutlineInputBorder(),
                              isDense: true,
                            ),
                          ),
                          const SizedBox(height: 12),
                          SizedBox(
                            width: double.infinity,
                            child: ElevatedButton.icon(
                              key: const Key('dispute_submit_cert_button'),
                              icon: const Icon(Icons.upload_file),
                              label: const Text('Submeter Certidão Tempestiva'),
                              onPressed: () {
                                final dId = state.dispute.disputeId ?? widget.initialDisputeId ?? 'dispute-1';
                                _onSupplementarySubmit(dId);
                              },
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                ],

                // 3. Banner de Resolução de Disputa
                if (isResolved) ...[
                  Container(
                    key: const Key('dispute_resolved_notice'),
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
                            'Disputa resolvida! Desfecho: ${state.dispute.resolutionType.wireName}. ${state.dispute.message}',
                            style: TextStyle(
                              color: Colors.green.shade900,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),
                ],

                // 4. Banner de Erro
                if (state is DisputeError) ...[
                  Container(
                    key: const Key('dispute_error_banner'),
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
                  const SizedBox(height: 16),
                ],

                // 5. Formulário de Abertura de Contestação (apenas se não estiver resolvido nem em litígio paritário ativo)
                if (!isParity && !isResolved) ...[
                  Form(
                    key: _formKey,
                    child: Card(
                      elevation: 2,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                      child: Padding(
                        padding: const EdgeInsets.all(16.0),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Formulário de Contestação e Provas',
                              style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              key: const Key('dispute_rep_name_input'),
                              controller: _repNameController,
                              decoration: const InputDecoration(
                                labelText: 'Nome do Representante Legal',
                                border: OutlineInputBorder(),
                                prefixIcon: Icon(Icons.person),
                              ),
                              validator: (val) =>
                                  val == null || val.trim().isEmpty ? 'Nome é obrigatório' : null,
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              key: const Key('dispute_rep_cpf_input'),
                              controller: _repCpfController,
                              decoration: const InputDecoration(
                                labelText: 'CPF do Representante',
                                border: OutlineInputBorder(),
                                prefixIcon: Icon(Icons.badge),
                              ),
                              validator: (val) =>
                                  val == null || val.trim().isEmpty ? 'CPF é obrigatório' : null,
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              key: const Key('dispute_cnpj_input'),
                              controller: _cnpjController,
                              decoration: const InputDecoration(
                                labelText: 'CNPJ da Igreja',
                                border: OutlineInputBorder(),
                                prefixIcon: Icon(Icons.business),
                              ),
                              validator: (val) =>
                                  val == null || val.trim().isEmpty ? 'CNPJ é obrigatório' : null,
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              key: const Key('dispute_file_hash_input'),
                              controller: _fileHashController,
                              decoration: const InputDecoration(
                                labelText: 'Hash SHA-256 da Ata/Estatuto RCPJ',
                                border: OutlineInputBorder(),
                                prefixIcon: Icon(Icons.fingerprint),
                              ),
                              validator: (val) =>
                                  val == null || val.trim().isEmpty ? 'Hash é obrigatório' : null,
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              key: const Key('dispute_averbation_date_input'),
                              readOnly: true,
                              controller: TextEditingController(
                                text: '${_averbationDate.day.toString().padLeft(2, '0')}/${_averbationDate.month.toString().padLeft(2, '0')}/${_averbationDate.year}',
                              ),
                              decoration: const InputDecoration(
                                labelText: 'Data de Averbação no Cartório RCPJ',
                                border: OutlineInputBorder(),
                                prefixIcon: Icon(Icons.calendar_today),
                              ),
                              onTap: () async {
                                final picked = await showDatePicker(
                                  context: context,
                                  initialDate: _averbationDate,
                                  firstDate: DateTime(1900),
                                  lastDate: DateTime.now(),
                                );
                                if (picked != null) {
                                  setState(() {
                                    _averbationDate = picked;
                                  });
                                }
                              },
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              key: const Key('dispute_justification_input'),
                              controller: _justificationController,
                              maxLines: 3,
                              decoration: const InputDecoration(
                                labelText: 'Justificativa Jurídica da Contestação',
                                border: OutlineInputBorder(),
                              ),
                            ),
                            const SizedBox(height: 14),
                            CheckboxListTile(
                              key: const Key('dispute_tos_checkbox'),
                              value: _tosAccepted,
                              onChanged: (val) {
                                setState(() {
                                  _tosAccepted = val ?? false;
                                });
                              },
                              contentPadding: EdgeInsets.zero,
                              controlAffinity: ListTileControlAffinity.leading,
                              title: const Text(
                                'Declaro sob as penas do art. 299 do Código Penal (Falsidade Ideológica) a veracidade e autenticidade da certidão averbada.',
                                style: TextStyle(fontSize: 12),
                              ),
                            ),
                            const SizedBox(height: 16),
                            SizedBox(
                              width: double.infinity,
                              height: 48,
                              child: ElevatedButton(
                                key: const Key('dispute_contest_button'),
                                onPressed: state is DisputeLoading ? null : _onContestSubmit,
                                child: state is DisputeLoading
                                    ? const SizedBox(
                                        width: 20,
                                        height: 20,
                                        child: CircularProgressIndicator(
                                          strokeWidth: 2,
                                          color: Colors.white,
                                        ),
                                      )
                                    : const Text(
                                        'Instaurar Contestação Formal',
                                        style: TextStyle(fontWeight: FontWeight.bold),
                                      ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ],
              ],
            ),
          );
        },
      ),
    );
  }

  String _formatRemainingTime(DateTime? deadlineAt) {
    if (deadlineAt == null) {
      return 'Prazo de 5 dias úteis em andamento';
    }
    final diff = deadlineAt.difference(DateTime.now());
    if (diff.isNegative) {
      return 'Prazo expirado. Aguardando apuração de inércia.';
    }
    final days = diff.inDays;
    final hours = diff.inHours % 24;
    return '$days dias úteis e $hours horas restantes para manifestação';
  }
}
