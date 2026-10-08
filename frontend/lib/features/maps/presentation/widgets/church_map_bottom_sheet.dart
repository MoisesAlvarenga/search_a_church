import 'package:flutter/material.dart';
import '../../data/models/church_map_item_model.dart';

/// BottomSheet exibido ao selecionar uma congregação no mapa ou na lista.
/// Apresenta dados identificadores, badges de origem (Cadastrada vs Não cadastrada)
/// e o botão destacado para reivindicar igrejas externas da Google Maps Platform.
class ChurchMapBottomSheet extends StatelessWidget {
  final ChurchMapItemModel church;
  final VoidCallback? onClose;
  final void Function(ChurchMapItemModel church)? onClaim;

  const ChurchMapBottomSheet({
    super.key,
    required this.church,
    this.onClose,
    this.onClaim,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isAppSource = church.source == ChurchSource.app;

    return Container(
      key: const Key('church_map_bottom_sheet'),
      decoration: BoxDecoration(
        color: theme.colorScheme.surface,
        borderRadius: const BorderRadius.vertical(top: Radius.circular(20)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.12),
            blurRadius: 10,
            offset: const Offset(0, -3),
          ),
        ],
      ),
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _buildDragHandle(theme),
          _buildHeader(theme),
          const SizedBox(height: 8),
          _buildBadgesRow(theme, isAppSource),
          const SizedBox(height: 12),
          _buildAddressRow(theme),
          if (church.canClaim) ...[
            const SizedBox(height: 16),
            _buildClaimButton(context, theme),
          ],
        ],
      ),
    );
  }

  Widget _buildDragHandle(ThemeData theme) {
    return Center(
      child: Container(
        width: 40,
        height: 4,
        margin: const EdgeInsets.only(bottom: 12),
        decoration: BoxDecoration(
          color: theme.colorScheme.onSurfaceVariant.withValues(alpha: 0.4),
          borderRadius: BorderRadius.circular(2),
        ),
      ),
    );
  }

  Widget _buildHeader(ThemeData theme) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Text(
            church.name,
            key: const Key('church_bottom_sheet_name'),
            style: theme.textTheme.titleLarge?.copyWith(
              fontWeight: FontWeight.bold,
            ),
          ),
        ),
        IconButton(
          key: const Key('church_bottom_sheet_close_button'),
          icon: const Icon(Icons.close),
          onPressed: onClose,
          tooltip: 'Fechar',
          padding: EdgeInsets.zero,
          constraints: const BoxConstraints(),
        ),
      ],
    );
  }

  Widget _buildBadgesRow(ThemeData theme, bool isAppSource) {
    return Wrap(
      spacing: 8,
      runSpacing: 6,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        _buildOriginBadge(theme, isAppSource),
        if (church.isVerifiedRepresentative) _buildVerifiedBadge(),
        _buildDistanceBadge(theme),
        if (church.ratingAverage != null) _buildRatingBadge(theme),
      ],
    );
  }

  Widget _buildOriginBadge(ThemeData theme, bool isAppSource) {
    if (isAppSource) {
      return Container(
        key: const Key('church_badge_app'),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        decoration: BoxDecoration(
          color: theme.colorScheme.primaryContainer,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.verified,
              size: 14,
              color: theme.colorScheme.onPrimaryContainer,
            ),
            const SizedBox(width: 4),
            Text(
              'Cadastrada',
              style: theme.textTheme.labelSmall?.copyWith(
                color: theme.colorScheme.onPrimaryContainer,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      );
    }

    return Container(
      key: const Key('church_badge_maps'),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: Colors.orange.shade100,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            Icons.place_outlined,
            size: 14,
            color: Colors.orange.shade900,
          ),
          const SizedBox(width: 4),
          Text(
            'Não cadastrada',
            style: theme.textTheme.labelSmall?.copyWith(
              color: Colors.orange.shade900,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildVerifiedBadge() {
    return Container(
      key: const Key('church_badge_verified'),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: Colors.green.shade100,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            Icons.check_circle_outline,
            size: 14,
            color: Colors.green.shade900,
          ),
          const SizedBox(width: 4),
          Text(
            'Representante Verificado',
            style: TextStyle(
              fontSize: 11,
              color: Colors.green.shade900,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDistanceBadge(ThemeData theme) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(
          Icons.near_me_outlined,
          size: 14,
          color: theme.colorScheme.secondary,
        ),
        const SizedBox(width: 4),
        Text(
          '${church.distanceKm.toStringAsFixed(1)} km',
          key: const Key('church_distance_text'),
          style: theme.textTheme.bodySmall?.copyWith(
            color: theme.colorScheme.secondary,
            fontWeight: FontWeight.w500,
          ),
        ),
      ],
    );
  }

  Widget _buildRatingBadge(ThemeData theme) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(Icons.star, size: 14, color: Colors.amber),
        const SizedBox(width: 2),
        Text(
          '${church.ratingAverage!.toStringAsFixed(1)} (${church.reviewCount ?? 0})',
          key: const Key('church_rating_text'),
          style: theme.textTheme.bodySmall?.copyWith(
            fontWeight: FontWeight.w500,
          ),
        ),
      ],
    );
  }

  Widget _buildAddressRow(ThemeData theme) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(
          Icons.location_on_outlined,
          size: 18,
          color: theme.colorScheme.onSurfaceVariant,
        ),
        const SizedBox(width: 6),
        Expanded(
          child: Text(
            church.address,
            key: const Key('church_address_text'),
            style: theme.textTheme.bodyMedium?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildClaimButton(BuildContext context, ThemeData theme) {
    return SizedBox(
      width: double.infinity,
      child: FilledButton.icon(
        key: const Key('claim_church_button'),
        style: FilledButton.styleFrom(
          backgroundColor: theme.colorScheme.primary,
          padding: const EdgeInsets.symmetric(vertical: 12),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
        ),
        icon: const Icon(Icons.app_registration),
        label: const Text(
          'Reivindicar esta igreja',
          style: TextStyle(fontWeight: FontWeight.bold),
        ),
        onPressed: () {
          if (onClaim != null) {
            onClaim!(church);
          } else {
            Navigator.of(context).pushNamed(
              '/claim',
              arguments: {
                'placeId': church.placeId,
                'name': church.name,
                'address': church.address,
                'latitude': church.latitude,
                'longitude': church.longitude,
              },
            );
          }
        },
      ),
    );
  }
}
