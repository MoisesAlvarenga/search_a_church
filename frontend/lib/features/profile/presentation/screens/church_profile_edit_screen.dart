import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../data/models/church_profile_model.dart';
import '../../data/models/meeting_schedule_model.dart';
import '../cubit/church_profile_cubit.dart';
import '../cubit/church_profile_state.dart';
import '../cubit/tag_catalog_cubit.dart';
import '../widgets/tag_selection_chips_widget.dart';

/// Tela para edição dos dados cadastrais, cultos e tags da congregação por representantes verificados.
class ChurchProfileEditScreen extends StatefulWidget {
  final String churchId;

  const ChurchProfileEditScreen({
    super.key,
    required this.churchId,
  });

  @override
  State<ChurchProfileEditScreen> createState() =>
      _ChurchProfileEditScreenState();
}

class _ChurchProfileEditScreenState extends State<ChurchProfileEditScreen> {
  final _formKey = GlobalKey<FormState>();

  late TextEditingController _nameController;
  late TextEditingController _addressController;
  late TextEditingController _latitudeController;
  late TextEditingController _longitudeController;
  late TextEditingController _denominationController;
  late TextEditingController _worshipStyleController;
  late TextEditingController _phoneController;
  late TextEditingController _emailController;
  late TextEditingController _websiteController;
  late TextEditingController _instagramController;
  late TextEditingController _facebookController;

  bool _isActive = true;
  String _concurrencyStamp = '';
  List<String> _selectedTags = [];
  List<MeetingScheduleModel> _schedules = [];
  bool _isInitialized = false;

  @override
  void initState() {
    super.initState();
    _nameController = TextEditingController();
    _addressController = TextEditingController();
    _latitudeController = TextEditingController();
    _longitudeController = TextEditingController();
    _denominationController = TextEditingController();
    _worshipStyleController = TextEditingController();
    _phoneController = TextEditingController();
    _emailController = TextEditingController();
    _websiteController = TextEditingController();
    _instagramController = TextEditingController();
    _facebookController = TextEditingController();

    context.read<ChurchProfileCubit>().loadProfile(widget.churchId);
    context.read<TagCatalogCubit>().loadCatalog();
  }

  @override
  void dispose() {
    _nameController.dispose();
    _addressController.dispose();
    _latitudeController.dispose();
    _longitudeController.dispose();
    _denominationController.dispose();
    _worshipStyleController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    _websiteController.dispose();
    _instagramController.dispose();
    _facebookController.dispose();
    super.dispose();
  }

  void _populateForm(ChurchProfileModel profile) {
    if (_isInitialized) return;
    _isInitialized = true;
    _nameController.text = profile.name;
    _addressController.text = profile.address;
    _latitudeController.text = profile.latitude.toString();
    _longitudeController.text = profile.longitude.toString();
    _denominationController.text = profile.denomination ?? '';
    _worshipStyleController.text = profile.worshipStyle ?? '';
    _phoneController.text = profile.phone ?? '';
    _emailController.text = profile.email ?? '';
    _websiteController.text = profile.website ?? '';
    _instagramController.text = profile.socialInstagram ?? '';
    _facebookController.text = profile.socialFacebook ?? '';
    _isActive = profile.isActive;
    _concurrencyStamp = profile.concurrencyStamp;
    _selectedTags = List<String>.from(profile.tags);
    _schedules = List<MeetingScheduleModel>.from(profile.schedules);
  }

  void _onSave() {
    if (!_formKey.currentState!.validate()) return;

    final lat = double.tryParse(_latitudeController.text.trim()) ?? 0.0;
    final lng = double.tryParse(_longitudeController.text.trim()) ?? 0.0;

    final request = UpdateChurchProfileRequestModel(
      name: _nameController.text.trim(),
      address: _addressController.text.trim(),
      latitude: lat,
      longitude: lng,
      denomination: _denominationController.text.trim().isEmpty
          ? null
          : _denominationController.text.trim(),
      worshipStyle: _worshipStyleController.text.trim().isEmpty
          ? null
          : _worshipStyleController.text.trim(),
      phone: _phoneController.text.trim().isEmpty
          ? null
          : _phoneController.text.trim(),
      email: _emailController.text.trim().isEmpty
          ? null
          : _emailController.text.trim(),
      website: _websiteController.text.trim().isEmpty
          ? null
          : _websiteController.text.trim(),
      socialInstagram: _instagramController.text.trim().isEmpty
          ? null
          : _instagramController.text.trim(),
      socialFacebook: _facebookController.text.trim().isEmpty
          ? null
          : _facebookController.text.trim(),
      concurrencyStamp: _concurrencyStamp.isEmpty ? null : _concurrencyStamp,
      tagCodes: _selectedTags,
      schedules: _schedules,
    );

    context.read<ChurchProfileCubit>().updateProfile(
          widget.churchId,
          request,
          ifMatchHeader: _concurrencyStamp.isEmpty ? null : _concurrencyStamp,
        );
  }

  void _onToggleStatus(bool value) {
    final request = UpdateChurchStatusRequestModel(
      isActive: value,
      concurrencyStamp: _concurrencyStamp.isEmpty ? null : _concurrencyStamp,
    );

    context.read<ChurchProfileCubit>().setChurchStatus(
          widget.churchId,
          request,
          ifMatchHeader: _concurrencyStamp.isEmpty ? null : _concurrencyStamp,
        );
  }

  void _showAddScheduleDialog() {
    int dayOfWeek = 0;
    final timeController = TextEditingController(text: '19:00');
    final descController = TextEditingController(text: 'Culto de Celebração');

    showDialog<void>(
      context: context,
      builder: (dialogContext) {
        return StatefulBuilder(
          builder: (context, setDialogState) {
            return AlertDialog(
              title: const Text('Adicionar Horário de Culto'),
              content: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  DropdownButtonFormField<int>(
                    value: dayOfWeek,
                    decoration: const InputDecoration(labelText: 'Dia da Semana'),
                    items: const [
                      DropdownMenuItem(value: 0, child: Text('Domingo')),
                      DropdownMenuItem(value: 1, child: Text('Segunda-feira')),
                      DropdownMenuItem(value: 2, child: Text('Terça-feira')),
                      DropdownMenuItem(value: 3, child: Text('Quarta-feira')),
                      DropdownMenuItem(value: 4, child: Text('Quinta-feira')),
                      DropdownMenuItem(value: 5, child: Text('Sexta-feira')),
                      DropdownMenuItem(value: 6, child: Text('Sábado')),
                    ],
                    onChanged: (val) {
                      if (val != null) setDialogState(() => dayOfWeek = val);
                    },
                  ),
                  const SizedBox(height: 8.0),
                  TextFormField(
                    controller: timeController,
                    decoration: const InputDecoration(
                      labelText: 'Horário de Início (HH:mm)',
                      hintText: '19:00',
                    ),
                  ),
                  const SizedBox(height: 8.0),
                  TextFormField(
                    controller: descController,
                    decoration: const InputDecoration(
                      labelText: 'Descrição / Título do Culto',
                    ),
                  ),
                ],
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(dialogContext).pop(),
                  child: const Text('Cancelar'),
                ),
                ElevatedButton(
                  key: const ValueKey('confirm_add_schedule_button'),
                  onPressed: () {
                    final newSchedule = MeetingScheduleModel(
                      dayOfWeek: dayOfWeek,
                      startTime: timeController.text.trim(),
                      description: descController.text.trim(),
                    );
                    setState(() {
                      _schedules.add(newSchedule);
                    });
                    Navigator.of(dialogContext).pop();
                  },
                  child: const Text('Adicionar'),
                ),
              ],
            );
          },
        );
      },
    );
  }

  String _formatDayOfWeek(int day) {
    switch (day) {
      case 0:
        return 'Domingo';
      case 1:
        return 'Segunda';
      case 2:
        return 'Terça';
      case 3:
        return 'Quarta';
      case 4:
        return 'Quinta';
      case 5:
        return 'Sexta';
      case 6:
        return 'Sábado';
      default:
        return 'Dia $day';
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Editar Congregação'),
      ),
      body: BlocConsumer<ChurchProfileCubit, ChurchProfileState>(
        listener: (context, state) {
          if (state is ChurchProfileSaveSuccess) {
            _concurrencyStamp = state.updatedProfile.concurrencyStamp;
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                key: ValueKey('church_save_success_snackbar'),
                content: Text('Congregação atualizada com sucesso!'),
                backgroundColor: Colors.green,
              ),
            );
          } else if (state is ChurchProfileStatusUpdated) {
            setState(() {
              _isActive = state.response.isActive;
            });
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                key: const ValueKey('church_status_snackbar'),
                content: Text(state.response.message),
                backgroundColor: Colors.blueGrey,
              ),
            );
          } else if (state is ChurchProfileError) {
            if (state.isPlaceIdConflict) {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(
                  key: ValueKey('church_place_id_conflict_snackbar'),
                  content: Text(
                      'Conflito: Este Place ID do Google Maps já pertence a outra igreja.'),
                  backgroundColor: Colors.deepOrange,
                ),
              );
            } else if (state.isConcurrencyConflict) {
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                  key: const ValueKey('church_concurrency_conflict_snackbar'),
                  content: const Text(
                      'Conflito de concorrência: os dados foram modificados por outro usuário.'),
                  backgroundColor: Colors.orange,
                  action: SnackBarAction(
                    label: 'Recarregar',
                    textColor: Colors.white,
                    onPressed: () {
                      _isInitialized = false;
                      context
                          .read<ChurchProfileCubit>()
                          .loadProfile(widget.churchId);
                    },
                  ),
                ),
              );
            } else {
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                  key: const ValueKey('church_error_snackbar'),
                  content: Text(state.message),
                  backgroundColor: Colors.red,
                ),
              );
            }
          }
        },
        builder: (context, state) {
          if (state is ChurchProfileLoading && !_isInitialized) {
            return const Center(
              child: CircularProgressIndicator(
                key: ValueKey('church_loading_indicator'),
              ),
            );
          }

          if (state is ChurchProfileLoaded) {
            _populateForm(state.profile);
          } else if (state is ChurchProfileSaveSuccess) {
            _populateForm(state.updatedProfile);
          }

          final isSaving = state is ChurchProfileSaving;
          final isStatusUpdating = state is ChurchProfileStatusUpdating;

          return Form(
            key: _formKey,
            child: ListView(
              padding: const EdgeInsets.all(16.0),
              children: [
                SwitchListTile(
                  key: const ValueKey('church_active_switch'),
                  title: const Text('Congregação Ativa'),
                  subtitle: Text(_isActive
                      ? 'Visível para todos os fiéis na busca e mapa'
                      : 'Oculta temporariamente para manutenção'),
                  value: _isActive,
                  onChanged: isStatusUpdating ? null : _onToggleStatus,
                ),
                const Divider(),
                const SizedBox(height: 8.0),

                Text(
                  'Dados Principais',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
                const SizedBox(height: 8.0),
                TextFormField(
                  key: const ValueKey('church_name_field'),
                  controller: _nameController,
                  decoration: const InputDecoration(
                    labelText: 'Nome da Congregação *',
                    border: OutlineInputBorder(),
                  ),
                  validator: (v) =>
                      v == null || v.trim().isEmpty ? 'Nome obrigatório' : null,
                ),
                const SizedBox(height: 12.0),
                TextFormField(
                  key: const ValueKey('church_address_field'),
                  controller: _addressController,
                  decoration: const InputDecoration(
                    labelText: 'Endereço Completo *',
                    border: OutlineInputBorder(),
                  ),
                  validator: (v) => v == null || v.trim().isEmpty
                      ? 'Endereço obrigatório'
                      : null,
                ),
                const SizedBox(height: 12.0),
                Row(
                  children: [
                    Expanded(
                      child: TextFormField(
                        key: const ValueKey('church_latitude_field'),
                        controller: _latitudeController,
                        decoration: const InputDecoration(
                          labelText: 'Latitude *',
                          border: OutlineInputBorder(),
                        ),
                        keyboardType: TextInputType.number,
                        validator: (v) => double.tryParse(v ?? '') == null
                            ? 'Latitude inválida'
                            : null,
                      ),
                    ),
                    const SizedBox(width: 8.0),
                    Expanded(
                      child: TextFormField(
                        key: const ValueKey('church_longitude_field'),
                        controller: _longitudeController,
                        decoration: const InputDecoration(
                          labelText: 'Longitude *',
                          border: OutlineInputBorder(),
                        ),
                        keyboardType: TextInputType.number,
                        validator: (v) => double.tryParse(v ?? '') == null
                            ? 'Longitude inválida'
                            : null,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 24.0),

                Text(
                  'Identidade e Liturgia',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
                const SizedBox(height: 8.0),
                TextFormField(
                  key: const ValueKey('church_denomination_field'),
                  controller: _denominationController,
                  decoration: const InputDecoration(
                    labelText: 'Denominação',
                    hintText: 'Ex: Batista, Presbiteriana',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12.0),
                TextFormField(
                  key: const ValueKey('church_worship_style_field'),
                  controller: _worshipStyleController,
                  decoration: const InputDecoration(
                    labelText: 'Estilo de Liturgia',
                    hintText: 'Ex: Contemporâneo, Tradicional',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 24.0),

                Text(
                  'Contatos e Canais Oficiais',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
                const SizedBox(height: 8.0),
                TextFormField(
                  key: const ValueKey('church_phone_field'),
                  controller: _phoneController,
                  decoration: const InputDecoration(
                    labelText: 'Telefone / WhatsApp',
                    prefixIcon: Icon(Icons.phone),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12.0),
                TextFormField(
                  key: const ValueKey('church_email_field'),
                  controller: _emailController,
                  decoration: const InputDecoration(
                    labelText: 'E-mail de Contato',
                    prefixIcon: Icon(Icons.email),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12.0),
                TextFormField(
                  key: const ValueKey('church_website_field'),
                  controller: _websiteController,
                  decoration: const InputDecoration(
                    labelText: 'Website Oficial',
                    prefixIcon: Icon(Icons.language),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 24.0),

                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'Cultos e Horários Semanais',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.bold,
                          ),
                    ),
                    TextButton.icon(
                      key: const ValueKey('add_schedule_button'),
                      onPressed: _showAddScheduleDialog,
                      icon: const Icon(Icons.add),
                      label: const Text('Adicionar'),
                    ),
                  ],
                ),
                const SizedBox(height: 8.0),
                if (_schedules.isEmpty)
                  const Text('Nenhum horário de culto cadastrado.')
                else
                  ..._schedules.asMap().entries.map((entry) {
                    final idx = entry.key;
                    final sch = entry.value;
                    return Card(
                      key: ValueKey('schedule_card_$idx'),
                      child: ListTile(
                        leading: const Icon(Icons.access_time),
                        title: Text(
                            '${_formatDayOfWeek(sch.dayOfWeek)} às ${sch.startTime}'),
                        subtitle: Text(sch.description ?? 'Culto Regular'),
                        trailing: IconButton(
                          icon: const Icon(Icons.delete, color: Colors.red),
                          onPressed: () {
                            setState(() {
                              _schedules.removeAt(idx);
                            });
                          },
                        ),
                      ),
                    );
                  }),
                const SizedBox(height: 24.0),

                Text(
                  'Recursos e Acessibilidade (Tags)',
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
                  key: const ValueKey('save_church_button'),
                  onPressed: isSaving ? null : _onSave,
                  icon: isSaving
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.save),
                  label:
                      Text(isSaving ? 'Salvando...' : 'Salvar Alterações'),
                  style: ElevatedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 14.0),
                  ),
                ),
                const SizedBox(height: 32.0),
              ],
            ),
          );
        },
      ),
    );
  }
}
