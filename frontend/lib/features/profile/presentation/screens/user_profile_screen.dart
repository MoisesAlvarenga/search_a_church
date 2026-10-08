import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../data/models/user_profile_model.dart';
import '../cubit/tag_catalog_cubit.dart';
import '../cubit/user_profile_cubit.dart';
import '../cubit/user_profile_state.dart';
import '../widgets/tag_selection_chips_widget.dart';

/// Tela de preferências de perfil de usuário e conformidade LGPD.
/// Permite configurar denominação, liturgia, idiomas, raio operacional (1-100 km),
/// selecionar tags de preferência e solicitar encerramento definitivo de conta.
class UserProfileScreen extends StatefulWidget {
  const UserProfileScreen({super.key});

  @override
  State<UserProfileScreen> createState() => _UserProfileScreenState();
}

class _UserProfileScreenState extends State<UserProfileScreen> {
  final _formKey = GlobalKey<FormState>();

  late TextEditingController _denominationController;
  late TextEditingController _worshipStyleController;

  double _radiusKm = 10.0;
  List<String> _selectedTags = [];
  bool _isInitialized = false;

  @override
  void initState() {
    super.initState();
    _denominationController = TextEditingController();
    _worshipStyleController = TextEditingController();

    context.read<UserProfileCubit>().loadProfile();
    context.read<TagCatalogCubit>().loadCatalog();
  }

  @override
  void dispose() {
    _denominationController.dispose();
    _worshipStyleController.dispose();
    super.dispose();
  }

  void _populateForm(UserProfileModel profile) {
    if (_isInitialized) return;
    _isInitialized = true;
    _denominationController.text = profile.denomination ?? '';
    _worshipStyleController.text = profile.worshipStyle ?? '';
    _radiusKm = profile.defaultRadiusKm.clamp(1.0, 100.0);
    _selectedTags = List<String>.from(profile.selectedTags);
  }

  void _onSave() {
    if (!_formKey.currentState!.validate()) return;

    final request = UpdateUserProfileRequestModel(
      denomination: _denominationController.text.trim().isEmpty
          ? null
          : _denominationController.text.trim(),
      worshipStyle: _worshipStyleController.text.trim().isEmpty
          ? null
          : _worshipStyleController.text.trim(),
      defaultRadiusKm: _radiusKm,
      tagCodes: _selectedTags,
    );

    context.read<UserProfileCubit>().updateProfile(request);
  }

  void _showDeleteAccountDialog() {
    showDialog<void>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text('Excluir e Anonimizar Conta'),
          content: const Text(
            'Tem certeza de que deseja encerrar sua conta? '
            'Sob a Lei Geral de Proteção de Dados (LGPD), todos os seus dados '
            'pessoais identificáveis serão removidos ou irreversivelmente anonimizados. '
            'Esta ação não pode ser desfeita.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Cancelar'),
            ),
            ElevatedButton(
              key: const ValueKey('confirm_delete_button'),
              style: ElevatedButton.styleFrom(
                backgroundColor: Theme.of(context).colorScheme.error,
                foregroundColor: Theme.of(context).colorScheme.onError,
              ),
              onPressed: () {
                Navigator.of(dialogContext).pop();
                context.read<UserProfileCubit>().deleteAccount();
              },
              child: const Text('Sim, Excluir'),
            ),
          ],
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Meu Perfil e Preferências'),
      ),
      body: BlocConsumer<UserProfileCubit, UserProfileState>(
        listener: (context, state) {
          if (state is UserProfileSaveSuccess) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                key: ValueKey('save_success_snackbar'),
                content: Text('Preferências atualizadas com sucesso!'),
                backgroundColor: Colors.green,
              ),
            );
          } else if (state is UserProfileDeleted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                key: const ValueKey('delete_success_snackbar'),
                content: Text(state.message),
                backgroundColor: Colors.blueGrey,
              ),
            );
          } else if (state is UserProfileError) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                key: const ValueKey('profile_error_snackbar'),
                content: Text(state.message),
                backgroundColor: Colors.red,
              ),
            );
          }
        },
        builder: (context, state) {
          if (state is UserProfileLoading && !_isInitialized) {
            return const Center(
              child: CircularProgressIndicator(
                key: ValueKey('profile_loading_indicator'),
              ),
            );
          }

          if (state is UserProfileLoaded) {
            _populateForm(state.profile);
          } else if (state is UserProfileSaveSuccess) {
            _populateForm(state.updatedProfile);
          }

          final isSaving = state is UserProfileSaving;
          final isDeleting = state is UserProfileDeleting;

          return Form(
            key: _formKey,
            child: ListView(
              padding: const EdgeInsets.all(16.0),
              children: [
                if (state is UserProfileLoaded) ...[
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            state.profile.name,
                            style: Theme.of(context).textTheme.titleLarge,
                          ),
                          const SizedBox(height: 4.0),
                          Text(
                            state.profile.email,
                            style: Theme.of(context)
                                .textTheme
                                .bodyMedium
                                ?.copyWith(color: Colors.grey[600]),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 16.0),
                ],

                Text(
                  'Preferências Espirituais e Litúrgicas',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
                const SizedBox(height: 8.0),
                TextFormField(
                  key: const ValueKey('denomination_field'),
                  controller: _denominationController,
                  decoration: const InputDecoration(
                    labelText: 'Denominação de Preferência',
                    hintText: 'Ex: Batista, Presbiteriana, Assembleia de Deus',
                    prefixIcon: Icon(Icons.church),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12.0),
                TextFormField(
                  key: const ValueKey('worship_style_field'),
                  controller: _worshipStyleController,
                  decoration: const InputDecoration(
                    labelText: 'Estilo de Liturgia',
                    hintText: 'Ex: Contemporâneo, Tradicional, Avivado',
                    prefixIcon: Icon(Icons.music_note),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 24.0),

                Text(
                  'Raio Padrão de Busca',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
                const SizedBox(height: 4.0),
                Text(
                  'Define o alcance operacional inicial ao buscar congregações no mapa: ${_radiusKm.toStringAsFixed(1)} km',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
                Slider(
                  key: const ValueKey('radius_slider'),
                  value: _radiusKm,
                  min: 1.0,
                  max: 100.0,
                  divisions: 99,
                  label: '${_radiusKm.toStringAsFixed(1)} km',
                  onChanged: (val) {
                    setState(() {
                      _radiusKm = val;
                    });
                  },
                ),
                const SizedBox(height: 24.0),

                Text(
                  'Recursos e Acessibilidade Desejados',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
                const SizedBox(height: 8.0),
                TagSelectionChipsWidget(
                  selectedTagCodes: _selectedTags,
                  onSelectionChanged: (tags) {
                    setState(() {
                      _selectedTags = tags;
                    });
                  },
                ),
                const SizedBox(height: 32.0),

                ElevatedButton.icon(
                  key: const ValueKey('save_preferences_button'),
                  onPressed: isSaving || isDeleting ? null : _onSave,
                  icon: isSaving
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.save),
                  label: Text(isSaving ? 'Salvando...' : 'Salvar Preferências'),
                  style: ElevatedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 14.0),
                  ),
                ),
                const SizedBox(height: 16.0),

                OutlinedButton.icon(
                  key: const ValueKey('delete_account_button'),
                  onPressed: isSaving || isDeleting
                      ? null
                      : _showDeleteAccountDialog,
                  icon: const Icon(Icons.delete_forever),
                  label: Text(isDeleting
                      ? 'Anonimizando...'
                      : 'Excluir Minha Conta (LGPD)'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: Theme.of(context).colorScheme.error,
                    side: BorderSide(
                        color: Theme.of(context).colorScheme.error),
                    padding: const EdgeInsets.symmetric(vertical: 14.0),
                  ),
                ),
                const SizedBox(height: 24.0),
              ],
            ),
          );
        },
      ),
    );
  }
}
