using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Runtime
{
    public class ShopZone : MonoBehaviour
    {
        [SerializeField] private StoreIdKind storeId = StoreIdKind.PlayerStore;
        [SerializeField] private ZoneKind kind = ZoneKind.Shop;
        [SerializeField] private bool isOwnedByPlayer = true;
        [SerializeField] private List<FloorTileInstance> floorTiles = new();
        [SerializeField] private StoreEntrancePoint entrance;
        [SerializeField] private ExitPoint exit;
        [SerializeField] private ClubStayPoint trackingPoint;
        [SerializeField] private ClubGlassController[] glassControllers;
        [SerializeField] private bool useGlassOverlayWhenUnowned;

        private readonly List<FloorTileInstance> discoveredFloorTiles = new();
        private StoreEntrancePoint discoveredEntrance;
        private ExitPoint discoveredExit;
        private ClubStayPoint discoveredTrackingPoint;
        private ClubGlassController[] discoveredGlassControllers = Array.Empty<ClubGlassController>();

        public event Action<ShopZone, bool> OnOwnershipChanged;

        public string StoreId => StoreIds.ToId(storeId);
        public ZoneKind Kind => kind;
        public bool IsOwnedByPlayer => isOwnedByPlayer;
        public bool UseGlassOverlayWhenUnowned => useGlassOverlayWhenUnowned;
        public IReadOnlyList<FloorTileInstance> FloorTiles => discoveredFloorTiles.Count > 0 ? discoveredFloorTiles : floorTiles;
        public StoreEntrancePoint Entrance => entrance != null ? entrance : discoveredEntrance;
        public ExitPoint Exit => exit != null ? exit : discoveredExit;
        public ClubStayPoint TrackingPoint => trackingPoint != null ? trackingPoint : discoveredTrackingPoint;
        public bool IsReadOnlyForPlayer => !isOwnedByPlayer;
        public bool IsClub => kind == ZoneKind.Club;

        public void ConfigureRuntime(string newStoreId, ZoneKind newKind, bool ownedByPlayer, bool glassOverlayWhenUnowned)
        {
            storeId = newStoreId == StoreIds.Club ? StoreIdKind.Club : StoreIdKind.PlayerStore;
            kind = newKind;
            isOwnedByPlayer = ownedByPlayer;
            useGlassOverlayWhenUnowned = glassOverlayWhenUnowned;
            RefreshRuntimeReferences();
            RefreshGlassState();
        }

        private void OnEnable()
        {
            RefreshRuntimeReferences();
            ValidateConfiguredTiles();
            RefreshGlassState();
        }

        public void RefreshRuntimeReferences()
        {
            discoveredFloorTiles.Clear();
            foreach (var tile in FindObjectsByType<FloorTileInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tile != null && string.Equals(tile.StoreId, StoreId, StringComparison.Ordinal))
                {
                    discoveredFloorTiles.Add(tile);
                }
            }

            discoveredEntrance = FindObjectsByType<StoreEntrancePoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(e => e != null && string.Equals(e.StoreId, StoreId, StringComparison.Ordinal));
            discoveredExit = FindObjectsByType<ExitPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(e => e != null && string.Equals(e.StoreId, StoreId, StringComparison.Ordinal));
            discoveredTrackingPoint = FindObjectsByType<ClubStayPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(p => p != null && string.Equals(p.StoreId, StoreId, StringComparison.Ordinal));

            var discoveredGlass = new List<ClubGlassController>();
            foreach (var tile in discoveredFloorTiles)
            {
                var glass = tile.GetComponentInChildren<ClubGlassController>(true);
                if (glass != null) discoveredGlass.Add(glass);
            }

            discoveredGlassControllers = discoveredGlass.ToArray();
        }

        public IEnumerable<FacilityInstance> GetCashiers()
        {
            foreach (var tile in FloorTiles)
            {
                if (tile == null || tile.Interior == null) continue;
                foreach (var facility in tile.Interior.AllFacilities())
                {
                    if (facility.archetype is FacilityArchetype facilityArchetype
                        && facilityArchetype.facilityType == FacilityType.Cashier)
                    {
                        yield return facility;
                    }
                }
            }
        }

        public IEnumerable<ShelfInstance> GetShelves()
        {
            foreach (var tile in FloorTiles)
            {
                if (tile == null || tile.Interior == null) continue;
                foreach (var shelf in tile.Interior.AllShelves())
                {
                    yield return shelf;
                }
            }
        }

        public void SetOwnedByPlayer(bool owned)
        {
            if (isOwnedByPlayer == owned) return;

            isOwnedByPlayer = owned;
            RefreshRuntimeReferences();
            RefreshGlassState();
            ConstructionManager.NotifyBuildingChanged();
            OnOwnershipChanged?.Invoke(this, owned);
        }

        private void ValidateConfiguredTiles()
        {
            foreach (var tile in floorTiles)
            {
                if (tile == null) continue;
                if (!string.Equals(tile.StoreId, StoreId, StringComparison.Ordinal))
                {
                    Debug.LogError($"[ShopZone] Tile '{tile.name}' has storeId '{tile.StoreId}' but is assigned to zone '{StoreId}'.", tile);
                }
            }
        }

        private void RefreshGlassState()
        {
            var activeGlassControllers = glassControllers != null && glassControllers.Length > 0
                ? glassControllers
                : discoveredGlassControllers;

            if (activeGlassControllers == null || activeGlassControllers.Length == 0)
            {
                CollectGlassControllers();
                activeGlassControllers = glassControllers != null && glassControllers.Length > 0
                    ? glassControllers
                    : discoveredGlassControllers;
            }

            bool shouldShowLocked = useGlassOverlayWhenUnowned && !isOwnedByPlayer;
            foreach (var glass in activeGlassControllers)
            {
                if (glass == null) continue;
                if (shouldShowLocked)
                {
                    glass.SetLocked(true);
                }
                else
                {
                    glass.SetLocked(false);
                    glass.SetVisible(useGlassOverlayWhenUnowned);
                }
            }
        }

        private void CollectGlassControllers()
        {
            var collected = new List<ClubGlassController>();
            foreach (var tile in FloorTiles)
            {
                if (tile == null) continue;
                var glass = tile.GetComponentInChildren<ClubGlassController>(true);
                if (glass != null) collected.Add(glass);
            }

            discoveredGlassControllers = collected.ToArray();
        }
    }
}
