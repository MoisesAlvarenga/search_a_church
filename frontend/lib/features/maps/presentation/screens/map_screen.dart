import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import '../../data/models/church_map_item_model.dart';
import '../cubit/map_cubit.dart';
import '../cubit/map_state.dart';
import '../widgets/church_map_bottom_sheet.dart';

/// Tela interativa de exploração de mapas com Google Maps.
/// Suporta marcadores diferenciados (App vs Maps), clustering dinâmico,
/// sincronização bidirecional lista-mapa e degradação graciosa.
class MapScreen extends StatelessWidget {
  final MapCubit? cubit;
  final void Function(ChurchMapItemModel church)? onClaim;

  const MapScreen({
    super.key,
    this.cubit,
    this.onClaim,
  });

  @override
  Widget build(BuildContext context) {
    if (cubit != null) {
      return BlocProvider<MapCubit>.value(
        value: cubit!,
        child: _MapScreenView(onClaim: onClaim),
      );
    }
    return _MapScreenView(onClaim: onClaim);
  }
}

class _MapScreenView extends StatefulWidget {
  final void Function(ChurchMapItemModel church)? onClaim;

  const _MapScreenView({this.onClaim});

  @override
  State<_MapScreenView> createState() => _MapScreenViewState();
}

class _MapScreenViewState extends State<_MapScreenView> {
  GoogleMapController? _mapController;
  bool _showHorizontalList = true;

  static const LatLng _defaultCenter = LatLng(-23.5505, -46.6333);
  static const ClusterManagerId _clusterId = ClusterManagerId('church_clusters');

  @override
  Widget build(BuildContext context) {
    final cubit = context.read<MapCubit>();

    return Scaffold(
      body: BlocConsumer<MapCubit, MapState>(
        listener: (context, state) {
          if (state is MapLoaded && state.selectedChurch != null) {
            _animateToLocation(
              state.selectedChurch!.latitude,
              state.selectedChurch!.longitude,
            );
          }
        },
        builder: (context, state) {
          final center = _resolveCenter(state);
          final churches = _resolveChurches(state);
          final markers = _buildMarkers(churches, cubit);
          final clusterManagers = _buildClusterManagers();

          return Stack(
            fit: StackFit.expand,
            children: [
              // 1. GoogleMap Canvas
              GoogleMap(
                key: const Key('google_map_widget'),
                initialCameraPosition: CameraPosition(
                  target: center,
                  zoom: 14.0,
                ),
                markers: markers,
                clusterManagers: clusterManagers,
                myLocationEnabled: true,
                myLocationButtonEnabled: false,
                zoomControlsEnabled: false,
                onMapCreated: (controller) {
                  _mapController = controller;
                },
                onCameraMove: (position) {
                  cubit.onCameraMoved(
                    latitude: position.target.latitude,
                    longitude: position.target.longitude,
                  );
                },
              ),

              // 2. Top Header, Degradation / Error & Loading Overlay
              SafeArea(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    _buildTopBar(context),
                    if (state is MapLoading)
                      const LinearProgressIndicator(
                        key: Key('map_loading_indicator'),
                        minHeight: 3,
                      ),
                    if (state is MapLoaded && state.isDegraded)
                      _buildDegradedBanner(context, state.degradedMessage),
                    if (state is MapErrorGraceful)
                      _buildErrorBanner(context, state.message),
                  ],
                ),
              ),

              // 3. Floating Action Controls
              Positioned(
                right: 16,
                bottom: _resolveFabBottom(state),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    FloatingActionButton.small(
                      key: const Key('toggle_list_button'),
                      heroTag: 'toggle_list_tag',
                      backgroundColor: Theme.of(context).colorScheme.surface,
                      onPressed: () {
                        setState(() {
                          _showHorizontalList = !_showHorizontalList;
                        });
                      },
                      child: Icon(
                        _showHorizontalList ? Icons.map : Icons.view_carousel,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                    ),
                    const SizedBox(height: 8),
                    FloatingActionButton.small(
                      key: const Key('my_location_fab'),
                      heroTag: 'my_location_tag',
                      backgroundColor: Theme.of(context).colorScheme.surface,
                      onPressed: () => cubit.init(),
                      child: Icon(
                        Icons.my_location,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                    ),
                  ],
                ),
              ),

              // 4. Bottom Sheets and Horizontal Cards
              _buildBottomContent(context, state, cubit),
            ],
          );
        },
      ),
    );
  }

  LatLng _resolveCenter(MapState state) {
    if (state is MapLoaded &&
        state.centerLatitude != 0.0 &&
        state.centerLongitude != 0.0) {
      return LatLng(state.centerLatitude, state.centerLongitude);
    }
    return _defaultCenter;
  }

  List<ChurchMapItemModel> _resolveChurches(MapState state) {
    if (state is MapLoaded) {
      return state.churches;
    }
    if (state is MapLoading) {
      return state.previousChurches;
    }
    if (state is MapErrorGraceful) {
      return state.cachedChurches;
    }
    return const [];
  }

  Set<ClusterManager> _buildClusterManagers() {
    return {
      ClusterManager(
        clusterManagerId: _clusterId,
        onClusterTap: (cluster) => _onClusterTap(cluster),
      ),
    };
  }

  Set<Marker> _buildMarkers(List<ChurchMapItemModel> churches, MapCubit cubit) {
    return churches.map((church) {
      final isAppSource = church.source == ChurchSource.app;
      final hue = isAppSource
          ? BitmapDescriptor.hueAzure
          : BitmapDescriptor.hueOrange;

      return Marker(
        markerId: MarkerId(church.id),
        position: LatLng(church.latitude, church.longitude),
        clusterManagerId: _clusterId,
        icon: BitmapDescriptor.defaultMarkerWithHue(hue),
        infoWindow: InfoWindow(
          title: church.name,
          snippet: church.address,
          onTap: () => cubit.selectChurch(church),
        ),
        onTap: () => cubit.selectChurch(church),
      );
    }).toSet();
  }

  Widget _buildTopBar(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Card(
        elevation: 4,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
          child: Row(
            children: [
              Icon(Icons.search, color: theme.colorScheme.primary),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  'Buscar igrejas próximas...',
                  key: const Key('map_search_header'),
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
              ),
              Icon(Icons.tune, color: theme.colorScheme.outline),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildDegradedBanner(BuildContext context, String? message) {
    return Container(
      key: const Key('degraded_mode_banner'),
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: Colors.amber.shade100,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: Colors.amber.shade400),
      ),
      child: Row(
        children: [
          Icon(Icons.cloud_off, size: 18, color: Colors.amber.shade900),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              message ??
                  'Google Maps temporariamente indisponível. Exibindo igrejas cadastradas.',
              style: TextStyle(
                fontSize: 12,
                color: Colors.amber.shade900,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildErrorBanner(BuildContext context, String message) {
    return Container(
      key: const Key('map_error_banner'),
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: Colors.red.shade100,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: Colors.red.shade300),
      ),
      child: Row(
        children: [
          Icon(Icons.error_outline, size: 18, color: Colors.red.shade900),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              message,
              style: TextStyle(
                fontSize: 12,
                color: Colors.red.shade900,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBottomContent(
    BuildContext context,
    MapState state,
    MapCubit cubit,
  ) {
    if (state is MapLoaded && state.selectedChurch != null) {
      return Positioned(
        left: 0,
        right: 0,
        bottom: 0,
        child: ChurchMapBottomSheet(
          church: state.selectedChurch!,
          onClose: () => cubit.clearSelection(),
          onClaim: widget.onClaim,
        ),
      );
    }

    if (state is MapLoaded && state.churches.isEmpty) {
      return Positioned(
        left: 16,
        right: 16,
        bottom: 24,
        child: Card(
          key: const Key('empty_map_message'),
          elevation: 4,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
          ),
          child: const Padding(
            padding: EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(Icons.info_outline, size: 20, color: Colors.grey),
                SizedBox(width: 8),
                Text(
                  'Nenhuma igreja encontrada nesta região.',
                  style: TextStyle(color: Colors.grey, fontWeight: FontWeight.w500),
                ),
              ],
            ),
          ),
        ),
      );
    }

    if (state is MapLoaded && _showHorizontalList && state.churches.isNotEmpty) {
      return Positioned(
        left: 0,
        right: 0,
        bottom: 16,
        height: 120,
        child: ListView.separated(
          key: const Key('churches_horizontal_list'),
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: 16),
          itemCount: state.churches.length,
          separatorBuilder: (_, _) => const SizedBox(width: 10),
          itemBuilder: (context, index) {
            final church = state.churches[index];
            return _buildChurchCard(context, church, cubit);
          },
        ),
      );
    }

    return const SizedBox.shrink();
  }

  Widget _buildChurchCard(
    BuildContext context,
    ChurchMapItemModel church,
    MapCubit cubit,
  ) {
    final theme = Theme.of(context);
    final isAppSource = church.source == ChurchSource.app;

    return GestureDetector(
      key: Key('church_card_${church.id}'),
      onTap: () {
        cubit.selectChurch(church);
      },
      child: Container(
        width: 250,
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: theme.colorScheme.surface,
          borderRadius: BorderRadius.circular(16),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.08),
              blurRadius: 8,
              offset: const Offset(0, 2),
            ),
          ],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    church.name,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                const SizedBox(width: 4),
                Icon(
                  isAppSource ? Icons.verified : Icons.place_outlined,
                  size: 16,
                  color: isAppSource
                      ? theme.colorScheme.primary
                      : Colors.orange.shade800,
                ),
              ],
            ),
            Text(
              church.address,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: theme.textTheme.bodySmall?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  '${church.distanceKm.toStringAsFixed(1)} km',
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: theme.colorScheme.secondary,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                Text(
                  isAppSource ? 'Oficial' : 'Maps',
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.bold,
                    color: isAppSource
                        ? theme.colorScheme.primary
                        : Colors.orange.shade800,
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  double _resolveFabBottom(MapState state) {
    if (state is MapLoaded && state.selectedChurch != null) {
      return 260;
    }
    if (state is MapLoaded && _showHorizontalList && state.churches.isNotEmpty) {
      return 145;
    }
    return 24;
  }

  Future<void> _animateToLocation(double lat, double lng) async {
    if (_mapController != null) {
      await _mapController!.animateCamera(
        CameraUpdate.newLatLng(LatLng(lat, lng)),
      );
    }
  }

  Future<void> _onClusterTap(Cluster cluster) async {
    if (_mapController != null) {
      final currentZoom = await _mapController!.getZoomLevel();
      await _mapController!.animateCamera(
        CameraUpdate.newLatLngZoom(cluster.position, currentZoom + 2.0),
      );
    }
  }
}
