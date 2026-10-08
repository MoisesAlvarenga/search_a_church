import 'package:equatable/equatable.dart';
import '../../data/models/church_map_item_model.dart';

/// Estado base do MapCubit.
abstract class MapState extends Equatable {
  const MapState();

  @override
  List<Object?> get props => [];
}

/// Estado inicial do mapa antes da determinação de coordenadas.
class MapInitial extends MapState {
  const MapInitial();
}

/// Estado de carregamento de congregações no raio da câmera.
class MapLoading extends MapState {
  final List<ChurchMapItemModel> previousChurches;

  const MapLoading({this.previousChurches = const []});

  @override
  List<Object?> get props => [previousChurches];
}

/// Estado de sucesso com templos carregados e controle de seleção/degradação.
class MapLoaded extends MapState {
  final List<ChurchMapItemModel> churches;
  final double centerLatitude;
  final double centerLongitude;
  final double radiusKm;
  final ChurchMapItemModel? selectedChurch;
  final bool isDegraded;
  final String? degradedMessage;

  const MapLoaded({
    required this.churches,
    required this.centerLatitude,
    required this.centerLongitude,
    required this.radiusKm,
    this.selectedChurch,
    this.isDegraded = false,
    this.degradedMessage,
  });

  MapLoaded copyWith({
    List<ChurchMapItemModel>? churches,
    double? centerLatitude,
    double? centerLongitude,
    double? radiusKm,
    ChurchMapItemModel? selectedChurch,
    bool clearSelectedChurch = false,
    bool? isDegraded,
    String? degradedMessage,
  }) {
    return MapLoaded(
      churches: churches ?? this.churches,
      centerLatitude: centerLatitude ?? this.centerLatitude,
      centerLongitude: centerLongitude ?? this.centerLongitude,
      radiusKm: radiusKm ?? this.radiusKm,
      selectedChurch: clearSelectedChurch
          ? null
          : (selectedChurch ?? this.selectedChurch),
      isDegraded: isDegraded ?? this.isDegraded,
      degradedMessage: degradedMessage ?? this.degradedMessage,
    );
  }

  @override
  List<Object?> get props => [
        churches,
        centerLatitude,
        centerLongitude,
        radiusKm,
        selectedChurch,
        isDegraded,
        degradedMessage,
      ];
}

/// Estado de falha de conexão/servidor com degradação graciosa na UI.
class MapErrorGraceful extends MapState {
  final String message;
  final List<ChurchMapItemModel> cachedChurches;

  const MapErrorGraceful({
    required this.message,
    this.cachedChurches = const [],
  });

  @override
  List<Object?> get props => [message, cachedChurches];
}
