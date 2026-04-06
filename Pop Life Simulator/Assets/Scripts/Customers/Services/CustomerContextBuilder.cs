using System.Collections.Generic;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;
using PopLife.Runtime;
using PopLife.Data;
using UnityEngine;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// 构建运行时快照，供 Policy 决策使用
    /// </summary>
    public class CustomerContextBuilder : MonoBehaviour
    {
        // 静态实例供全局访问
        private static CustomerContextBuilder _instance;
        public static CustomerContextBuilder Instance => _instance;

        void Awake()
        {
            _instance = this;
        }
        /// <summary>
        /// 从 CustomerBlackboardAdapter 创建顾客上下文快照
        /// </summary>
        public static CustomerContext BuildCustomerContext(CustomerBlackboardAdapter adapter)
        {
            if (adapter == null)
            {
                Debug.LogError("[CustomerContextBuilder] Adapter is null");
                return default;
            }

            return new CustomerContext
            {
                customerId = adapter.customerId,
                loyaltyLevel = adapter.loyaltyLevel,
                trust = adapter.trust,
                favoriteShelfIds = adapter.favoriteShelfIds,
                favoriteCategoryIndices = adapter.favoriteCategoryIndices,
                favoriteBrands = adapter.favoriteBrands,
                currentPhase = adapter.currentFunnelPhase,
                currentFunnelTier = adapter.lastSelectionTier,
                walletAmount = adapter.moneyBag,
                embarrassmentCap = adapter.embarrassmentCap,
                moveSpeed = adapter.moveSpeed,
                queueToleranceSec = adapter.queueToleranceSec,
                purchasedArchetypes = adapter.purchasedArchetypes
            };
        }

        /// <summary>
        /// 从 CustomerAgent 创建顾客上下文快照
        /// </summary>
        public static CustomerContext BuildCustomerContext(CustomerAgent agent)
        {
            if (agent == null || agent.bb == null)
            {
                Debug.LogError("[CustomerContextBuilder] Agent or blackboard is null");
                return default;
            }

            return BuildCustomerContext(agent.bb);
        }

        /// <summary>
        /// 从 ShelfInstance 创建货架快照
        /// </summary>
        public static ShelfSnapshot BuildShelfSnapshot(ShelfInstance shelf)
        {
            if (shelf == null)
            {
                Debug.LogError("[CustomerContextBuilder] Shelf is null");
                return default;
            }

            var shelfArchetype = shelf.archetype as ShelfArchetype;
            if (shelfArchetype == null)
            {
                Debug.LogError("[CustomerContextBuilder] Shelf archetype is not a ShelfArchetype");
                return default;
            }

            // 使用 WorldGrid 坐标（非 interior 局部坐标），用于跨 tile 距离比较
            Vector2Int gridPos;
            if (WorldGrid.Instance != null)
            {
                gridPos = WorldGrid.Instance.WorldToGrid(shelf.transform.position);
            }
            else
            {
                gridPos = new Vector2Int(
                    Mathf.RoundToInt(shelf.transform.position.x),
                    Mathf.RoundToInt(shelf.transform.position.y)
                );
            }

            // 获取排队人数（从 ShelfQueueController）
            int queueLength = shelf.GetQueueLength();

            return new ShelfSnapshot
            {
                shelfId = shelf.instanceId,
                archetype = shelfArchetype,
                categoryIndex = (int)shelfArchetype.category,
                appeal = Mathf.RoundToInt(shelf.GetAppeal()),
                price = shelf.currentPrice,
                stock = shelf.currentStock,
                gridCell = gridPos,
                queueLength = queueLength
            };
        }

        /// <summary>
        /// 构建所有可用货架的快照列表
        /// </summary>
        public static List<ShelfSnapshot> BuildAllShelfSnapshots()
        {
            var snapshots = new List<ShelfSnapshot>();

            // 查找所有货架实例
            var allShelves = Object.FindObjectsByType<ShelfInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var shelf in allShelves)
            {
                // 只添加正在运营的货架
                if (shelf.isOperational && shelf.currentStock > 0)
                {
                    snapshots.Add(BuildShelfSnapshot(shelf));
                }
            }

            return snapshots;
        }

        /// <summary>
        /// 从 FacilityInstance 创建收银台快照（仅限收银台类型）
        /// </summary>
        public static CashierSnapshot BuildCashierSnapshot(FacilityInstance facility)
        {
            if (facility == null)
            {
                Debug.LogError("[CustomerContextBuilder] Facility is null");
                return default;
            }

            var facilityArchetype = facility.archetype as FacilityArchetype;
            if (facilityArchetype == null || facilityArchetype.facilityType != FacilityType.Cashier)
            {
                Debug.LogError("[CustomerContextBuilder] Facility is not a cashier");
                return default;
            }

            // 使用 WorldGrid 坐标（非 interior 局部坐标），用于跨 tile 距离比较
            Vector2Int gridPos;
            if (WorldGrid.Instance != null)
            {
                gridPos = WorldGrid.Instance.WorldToGrid(facility.transform.position);
            }
            else
            {
                gridPos = new Vector2Int(
                    Mathf.RoundToInt(facility.transform.position.x),
                    Mathf.RoundToInt(facility.transform.position.y)
                );
            }

            // 获取排队人数（从 CashierQueueController）
            int queueLength = facility.GetQueueLength();

            return new CashierSnapshot
            {
                cashierId = facility.instanceId,
                gridCell = gridPos,
                queueLength = queueLength
            };
        }

        /// <summary>
        /// 构建所有可用收银台的快照列表
        /// </summary>
        public static List<CashierSnapshot> BuildAllCashierSnapshots()
        {
            var snapshots = new List<CashierSnapshot>();

            // 查找所有设施实例
            var allFacilities = Object.FindObjectsByType<FacilityInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var facility in allFacilities)
            {
                var facilityArchetype = facility.archetype as FacilityArchetype;

                // 只添加运营中的收银台
                if (facility.isOperational &&
                    facilityArchetype != null &&
                    facilityArchetype.facilityType == FacilityType.Cashier)
                {
                    snapshots.Add(BuildCashierSnapshot(facility));
                }
            }

            return snapshots;
        }

        /// <summary>
        /// 根据类别筛选货架快照
        /// </summary>
        public static List<ShelfSnapshot> FilterShelfsByCategory(List<ShelfSnapshot> shelves, ProductCategory category)
        {
            var filtered = new List<ShelfSnapshot>();
            int categoryIndex = (int)category;

            foreach (var shelf in shelves)
            {
                if (shelf.categoryIndex == categoryIndex)
                {
                    filtered.Add(shelf);
                }
            }

            return filtered;
        }

        /// <summary>
        /// 根据库存筛选货架快照
        /// </summary>
        public static List<ShelfSnapshot> FilterShelfsByStock(List<ShelfSnapshot> shelves, int minStock = 1)
        {
            var filtered = new List<ShelfSnapshot>();

            foreach (var shelf in shelves)
            {
                if (shelf.stock >= minStock)
                {
                    filtered.Add(shelf);
                }
            }

            return filtered;
        }

        /// <summary>
        /// 将世界坐标转换为网格坐标（废弃方法，保留仅为兼容）
        /// </summary>
        [System.Obsolete("使用 WorldGrid.Instance.WorldToGrid 代替")]
        private static Vector2Int GetGridPosition(Vector3 worldPosition)
        {
            if (WorldGrid.Instance != null)
            {
                return WorldGrid.Instance.WorldToGrid(worldPosition);
            }

            // 备用方案：简单转换（假设网格单位为1）
            return new Vector2Int(
                Mathf.RoundToInt(worldPosition.x),
                Mathf.RoundToInt(worldPosition.y)
            );
        }
    }
}
