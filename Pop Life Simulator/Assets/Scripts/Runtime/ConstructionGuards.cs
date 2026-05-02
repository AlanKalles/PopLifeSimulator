namespace PopLife.Runtime
{
    public static class ConstructionGuards
    {
        public static bool IsTileReadOnlyForPlayer(FloorTileInstance tile)
        {
            if (tile == null) return false;

            var registry = ShopZoneRegistry.Instance;
            if (registry == null)
            {
                return tile.StoreId != StoreIds.PlayerStore;
            }

            var zone = registry.GetByStoreId(tile.StoreId);
            if (zone == null)
            {
                return tile.StoreId != StoreIds.PlayerStore;
            }

            return zone.IsReadOnlyForPlayer;
        }

        public static bool IsStoreOwnedByPlayer(string storeId)
        {
            var registry = ShopZoneRegistry.Instance;
            if (registry == null)
            {
                return storeId == StoreIds.PlayerStore;
            }

            var zone = registry.GetByStoreId(storeId);
            if (zone == null)
            {
                return storeId == StoreIds.PlayerStore;
            }

            return zone.IsOwnedByPlayer;
        }
    }
}
