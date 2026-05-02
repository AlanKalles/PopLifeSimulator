using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PopLife.Runtime
{
    [DefaultExecutionOrder(-100)]
    public class ShopZoneRegistry : MonoBehaviour
    {
        public static ShopZoneRegistry Instance { get; private set; }

        private readonly Dictionary<string, ShopZone> zonesByStoreId = new();
        private readonly List<ShopZone> zones = new();

        public IReadOnlyList<ShopZone> All => zones;
        public ShopZone DefaultPlayerZone => zones.FirstOrDefault(z => z != null && z.Kind == ZoneKind.Shop && z.IsOwnedByPlayer);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Refresh();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Refresh()
        {
            zones.Clear();
            zonesByStoreId.Clear();

            foreach (var zone in FindObjectsByType<ShopZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (zone == null || string.IsNullOrWhiteSpace(zone.StoreId)) continue;
                RegisterZone(zone);
            }

            EnsureRuntimeZonesForDiscoveredStores();

            Debug.Log($"[ShopZoneRegistry] Discovered {zones.Count} zones");
        }

        private void RegisterZone(ShopZone zone)
        {
            if (zonesByStoreId.ContainsKey(zone.StoreId))
            {
                Debug.LogError($"[ShopZoneRegistry] Duplicate storeId '{zone.StoreId}' found.", zone);
                return;
            }

            zone.RefreshRuntimeReferences();
            zones.Add(zone);
            zonesByStoreId.Add(zone.StoreId, zone);
        }

        private void EnsureRuntimeZonesForDiscoveredStores()
        {
            var discoveredStoreIds = new HashSet<string>();

            foreach (var tile in FindObjectsByType<FloorTileInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (tile != null && !string.IsNullOrWhiteSpace(tile.StoreId))
                    discoveredStoreIds.Add(tile.StoreId);

            foreach (var entrance in FindObjectsByType<StoreEntrancePoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (entrance != null && !string.IsNullOrWhiteSpace(entrance.StoreId))
                    discoveredStoreIds.Add(entrance.StoreId);

            foreach (var exit in FindObjectsByType<ExitPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (exit != null && !string.IsNullOrWhiteSpace(exit.StoreId))
                    discoveredStoreIds.Add(exit.StoreId);

            foreach (var stayPoint in FindObjectsByType<ClubStayPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (stayPoint != null && !string.IsNullOrWhiteSpace(stayPoint.StoreId))
                    discoveredStoreIds.Add(stayPoint.StoreId);

            foreach (var storeId in discoveredStoreIds)
            {
                if (zonesByStoreId.ContainsKey(storeId)) continue;

                var go = new GameObject($"RuntimeShopZone_{storeId}");
                go.transform.SetParent(transform, false);
                go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

                var zone = go.AddComponent<ShopZone>();
                bool isClub = storeId == StoreIds.Club;
                zone.ConfigureRuntime(
                    storeId,
                    isClub ? ZoneKind.Club : ZoneKind.Shop,
                    storeId == StoreIds.PlayerStore,
                    isClub);
                RegisterZone(zone);
            }
        }

        public ShopZone GetByStoreId(string storeId)
        {
            if (string.IsNullOrEmpty(storeId)) return null;
            zonesByStoreId.TryGetValue(storeId, out var zone);
            return zone;
        }

        public ShopZone GetByFloorTile(FloorTileInstance tile)
        {
            return tile == null ? null : GetByStoreId(tile.StoreId);
        }

        public IReadOnlyList<ShopZone> GetOwnedZones()
        {
            return zones.Where(z => z != null && z.IsOwnedByPlayer).ToList();
        }
    }
}
