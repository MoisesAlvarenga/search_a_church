import 'dart:async';
import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';
import 'package:google_maps_flutter_platform_interface/google_maps_flutter_platform_interface.dart';

/// Implementação Fake completa do GoogleMapsFlutterPlatform para testes de widget headless.
class TestFakeMapsPlatform extends GoogleMapsFlutterPlatform {
  final StreamController<MapEvent<dynamic>> _streamController =
      StreamController<MapEvent<dynamic>>.broadcast();

  final List<MarkerUpdates> markerUpdates = [];
  final List<ClusterManagerUpdates> clusterUpdates = [];
  final List<CameraUpdate> cameraUpdates = [];

  @override
  Future<void> init(int mapId) async {}

  @override
  Future<void> updateMapConfiguration(MapConfiguration update,
      {required int mapId}) async {}

  @override
  Future<void> updateMarkers(MarkerUpdates update,
      {required int mapId}) async {
    markerUpdates.add(update);
  }

  @override
  Future<void> updateClusterManagers(ClusterManagerUpdates update,
      {required int mapId}) async {
    clusterUpdates.add(update);
  }

  @override
  Future<void> updatePolygons(PolygonUpdates update,
      {required int mapId}) async {}

  @override
  Future<void> updatePolylines(PolylineUpdates update,
      {required int mapId}) async {}

  @override
  Future<void> updateCircles(CircleUpdates update,
      {required int mapId}) async {}

  @override
  Future<void> updateHeatmaps(HeatmapUpdates update,
      {required int mapId}) async {}

  @override
  Future<void> updateTileOverlays(
      {required Set<TileOverlay> newTileOverlays,
      required int mapId}) async {}

  @override
  Future<void> updateGroundOverlays(GroundOverlayUpdates update,
      {required int mapId}) async {}

  @override
  Future<void> clearTileCache(TileOverlayId tileOverlayId,
      {required int mapId}) async {}

  @override
  Future<void> animateCamera(CameraUpdate cameraUpdate,
      {required int mapId}) async {
    cameraUpdates.add(cameraUpdate);
  }

  @override
  Future<void> animateCameraWithConfiguration(
    CameraUpdate cameraUpdate,
    CameraUpdateAnimationConfiguration configuration, {
    required int mapId,
  }) async {
    cameraUpdates.add(cameraUpdate);
  }

  @override
  Future<void> moveCamera(CameraUpdate cameraUpdate,
      {required int mapId}) async {}

  @override
  Future<void> setMapStyle(String? mapStyle, {required int mapId}) async {}

  @override
  Future<LatLngBounds> getVisibleRegion({required int mapId}) async =>
      LatLngBounds(
        southwest: const LatLng(-23.56, -46.66),
        northeast: const LatLng(-23.54, -46.62),
      );

  @override
  Future<ScreenCoordinate> getScreenCoordinate(LatLng latLng,
          {required int mapId}) async =>
      const ScreenCoordinate(x: 0, y: 0);

  @override
  Future<LatLng> getLatLng(ScreenCoordinate screenCoordinate,
          {required int mapId}) async =>
      const LatLng(0, 0);

  @override
  Future<void> showMarkerInfoWindow(MarkerId markerId,
      {required int mapId}) async {}

  @override
  Future<void> hideMarkerInfoWindow(MarkerId markerId,
      {required int mapId}) async {}

  @override
  Future<bool> isMarkerInfoWindowShown(MarkerId markerId,
      {required int mapId}) async =>
      false;

  @override
  Future<double> getZoomLevel({required int mapId}) async => 14.0;

  @override
  Future<Uint8List?> takeSnapshot({required int mapId}) async => null;

  @override
  Stream<CameraMoveStartedEvent> onCameraMoveStarted({required int mapId}) =>
      _streamController.stream
          .where((e) => e is CameraMoveStartedEvent)
          .cast<CameraMoveStartedEvent>();

  @override
  Stream<CameraMoveEvent> onCameraMove({required int mapId}) =>
      _streamController.stream
          .where((e) => e is CameraMoveEvent)
          .cast<CameraMoveEvent>();

  @override
  Stream<CameraIdleEvent> onCameraIdle({required int mapId}) =>
      _streamController.stream
          .where((e) => e is CameraIdleEvent)
          .cast<CameraIdleEvent>();

  @override
  Stream<MarkerTapEvent> onMarkerTap({required int mapId}) =>
      _streamController.stream
          .where((e) => e is MarkerTapEvent)
          .cast<MarkerTapEvent>();

  @override
  Stream<MarkerDragStartEvent> onMarkerDragStart({required int mapId}) =>
      _streamController.stream
          .where((e) => e is MarkerDragStartEvent)
          .cast<MarkerDragStartEvent>();

  @override
  Stream<MarkerDragEvent> onMarkerDrag({required int mapId}) =>
      _streamController.stream
          .where((e) => e is MarkerDragEvent)
          .cast<MarkerDragEvent>();

  @override
  Stream<MarkerDragEndEvent> onMarkerDragEnd({required int mapId}) =>
      _streamController.stream
          .where((e) => e is MarkerDragEndEvent)
          .cast<MarkerDragEndEvent>();

  @override
  Stream<InfoWindowTapEvent> onInfoWindowTap({required int mapId}) =>
      _streamController.stream
          .where((e) => e is InfoWindowTapEvent)
          .cast<InfoWindowTapEvent>();

  @override
  Stream<PolylineTapEvent> onPolylineTap({required int mapId}) =>
      _streamController.stream
          .where((e) => e is PolylineTapEvent)
          .cast<PolylineTapEvent>();

  @override
  Stream<PolygonTapEvent> onPolygonTap({required int mapId}) =>
      _streamController.stream
          .where((e) => e is PolygonTapEvent)
          .cast<PolygonTapEvent>();

  @override
  Stream<CircleTapEvent> onCircleTap({required int mapId}) =>
      _streamController.stream
          .where((e) => e is CircleTapEvent)
          .cast<CircleTapEvent>();

  @override
  Stream<MapTapEvent> onTap({required int mapId}) =>
      _streamController.stream
          .where((e) => e is MapTapEvent)
          .cast<MapTapEvent>();

  @override
  Stream<MapLongPressEvent> onLongPress({required int mapId}) =>
      _streamController.stream
          .where((e) => e is MapLongPressEvent)
          .cast<MapLongPressEvent>();

  @override
  Stream<ClusterTapEvent> onClusterTap({required int mapId}) =>
      _streamController.stream
          .where((e) => e is ClusterTapEvent)
          .cast<ClusterTapEvent>();

  final Set<int> _createdMapIds = <int>{};

  @override
  Widget buildViewWithConfiguration(
    int creationId,
    PlatformViewCreatedCallback onPlatformViewCreated, {
    required MapWidgetConfiguration widgetConfiguration,
    MapObjects mapObjects = const MapObjects(),
    MapConfiguration mapConfiguration = const MapConfiguration(),
  }) {
    if (_createdMapIds.add(creationId)) {
      onPlatformViewCreated(creationId);
    }
    return const SizedBox.expand(
      key: Key('fake_google_map_canvas'),
    );
  }

  @override
  void dispose({required int mapId}) {}
}
