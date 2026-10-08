import 'dart:async';
import 'package:dio/dio.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../core/geolocation_service.dart';
import '../../data/map_repository.dart';
import '../../data/models/church_map_item_model.dart';
import 'map_state.dart';

/// Cubit responsável pelo gerenciamento de estado do mapa,
/// debounce de 500ms nos eventos de câmera e sincronização bidirecional.
class MapCubit extends Cubit<MapState> {
  final IMapRepository _repository;
  final GeolocationService _geolocationService;

  Timer? _debounceTimer;

  MapCubit({
    required IMapRepository repository,
    required GeolocationService geolocationService,
  })  : _repository = repository,
        _geolocationService = geolocationService,
        super(const MapInitial());

  /// Inicializa o mapa determinando a posição GPS inicial do usuário.
  Future<void> init({double? initialLat, double? initialLng}) async {
    if (initialLat != null && initialLng != null) {
      await searchChurches(latitude: initialLat, longitude: initialLng);
      return;
    }

    final locationResult = await _geolocationService.determinePosition();
    final lat = locationResult.latitude ?? GeolocationService.defaultLatitude;
    final lng = locationResult.longitude ?? GeolocationService.defaultLongitude;

    await searchChurches(latitude: lat, longitude: lng);
  }

  /// Manipula eventos de movimento/arraste de câmera aplicando debounce de 500ms
  /// antes de disparar uma nova requisição à API.
  void onCameraMoved({
    required double latitude,
    required double longitude,
    double radiusKm = 5.0,
    String? query,
    Duration debounceDuration = const Duration(milliseconds: 500),
  }) {
    _debounceTimer?.cancel();
    _debounceTimer = Timer(debounceDuration, () {
      searchChurches(
        latitude: latitude,
        longitude: longitude,
        radiusKm: radiusKm,
        query: query,
      );
    });
  }

  /// Executa a busca híbrida de igrejas no raio informado.
  Future<void> searchChurches({
    required double latitude,
    required double longitude,
    double radiusKm = 5.0,
    String? query,
  }) async {
    final currentChurches = state is MapLoaded
        ? (state as MapLoaded).churches
        : const <ChurchMapItemModel>[];

    emit(MapLoading(previousChurches: currentChurches));

    try {
      final response = await _repository.searchNearby(
        latitude: latitude,
        longitude: longitude,
        radiusKm: radiusKm,
        query: query,
      );

      final currentSelected = state is MapLoaded
          ? (state as MapLoaded).selectedChurch
          : null;

      emit(MapLoaded(
        churches: response.results,
        centerLatitude: response.centerLatitude,
        centerLongitude: response.centerLongitude,
        radiusKm: response.appliedRadiusKm,
        isDegraded: response.isDegraded,
        degradedMessage: response.degradedMessage,
        selectedChurch: currentSelected,
      ));
    } on DioException catch (dioErr) {
      final message = dioErr.response?.data is Map &&
              (dioErr.response?.data as Map).containsKey('message')
          ? dioErr.response?.data['message'] as String
          : 'Falha na comunicação com o servidor de mapas.';

      emit(MapErrorGraceful(
        message: message,
        cachedChurches: currentChurches,
      ));
    } catch (e) {
      emit(MapErrorGraceful(
        message: 'Ocorreu um erro inesperado ao pesquisar locais.',
        cachedChurches: currentChurches,
      ));
    }
  }

  /// Seleciona uma igreja para destacar no mapa e abrir o card inferior.
  void selectChurch(ChurchMapItemModel? church) {
    if (state is MapLoaded) {
      final loaded = state as MapLoaded;
      emit(loaded.copyWith(selectedChurch: church));
    }
  }

  /// Limpa a congregação atualmente selecionada.
  void clearSelection() {
    if (state is MapLoaded) {
      final loaded = state as MapLoaded;
      emit(loaded.copyWith(clearSelectedChurch: true));
    }
  }

  @override
  Future<void> close() {
    _debounceTimer?.cancel();
    return super.close();
  }
}
