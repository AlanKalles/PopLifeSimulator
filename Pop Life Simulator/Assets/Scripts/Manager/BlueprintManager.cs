using UnityEngine;
using PopLife.Data;
using PopLife.Utility;
using System;
using System.Collections.Generic;
using System.IO;

namespace PopLife
{
    /// <summary>
    /// 蓝图管理器
    /// 功能：
    /// - 加载并管理 BlueprintProfile（记录已解锁的建筑ID）
    /// - 提供蓝图查询接口
    /// - 支持运行时解锁新蓝图
    /// </summary>
    public class BlueprintManager : MonoBehaviour
    {
        public static BlueprintManager Instance;

        /// <summary>
        /// 货架蓝图解锁时触发，参数为对应的 ShelfArchetype SO
        /// </summary>
        public static event System.Action<Data.ShelfArchetype> OnShelfUnlocked;

        /// <summary>
        /// 地板蓝图解锁时触发
        /// </summary>
        public static event System.Action<Data.FloorTileArchetype> OnFloorTileUnlocked;

        private BlueprintProfile profile;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            LoadProfile();
        }

        /// <summary>
        /// 加载蓝图配置文件
        /// </summary>
        public void LoadProfile()
        {
            profile = BlueprintProfile.Load();

            if (profile == null)
            {
                Debug.LogError("[BlueprintManager] Failed to load BlueprintProfile!");
                profile = new BlueprintProfile();
            }

            Debug.Log($"[BlueprintManager] Loaded blueprint profile: {profile.unlockedShelfIds.Count} shelves, {profile.unlockedFacilityIds.Count} facilities");
        }

        /// <summary>
        /// 重新加载蓝图配置文件（用于每日结算后刷新）
        /// </summary>
        public void ReloadProfile()
        {
            LoadProfile();
        }

        /// <summary>
        /// 检查是否拥有某个建筑的蓝图
        /// </summary>
        /// <param name="archetypeId">建筑archetype的ID</param>
        /// <returns>是否已解锁</returns>
        public bool HasBlueprint(string archetypeId)
        {
            if (profile == null)
            {
                Debug.LogWarning("[BlueprintManager] Profile is null, returning false");
                return false;
            }

            return profile.HasShelfBlueprint(archetypeId)
                || profile.HasFacilityBlueprint(archetypeId)
                || profile.HasFloorTileBlueprint(archetypeId);
        }

        /// <summary>
        /// 检查是否拥有货架蓝图
        /// </summary>
        public bool HasShelfBlueprint(string shelfId)
        {
            return profile != null && profile.HasShelfBlueprint(shelfId);
        }

        /// <summary>
        /// 检查是否拥有设施蓝图
        /// </summary>
        public bool HasFacilityBlueprint(string facilityId)
        {
            return profile != null && profile.HasFacilityBlueprint(facilityId);
        }

        /// <summary>
        /// 解锁货架蓝图
        /// </summary>
        public void UnlockShelf(string shelfId)
        {
            if (profile == null)
            {
                Debug.LogError("[BlueprintManager] Profile is null, cannot unlock shelf");
                return;
            }

            profile.UnlockShelf(shelfId);
            profile.Save();
        }

        /// <summary>
        /// 解锁设施蓝图
        /// </summary>
        public void UnlockFacility(string facilityId)
        {
            if (profile == null)
            {
                Debug.LogError("[BlueprintManager] Profile is null, cannot unlock facility");
                return;
            }

            profile.UnlockFacility(facilityId);
            profile.Save();
        }

        /// <summary>
        /// 批量解锁货架蓝图
        /// </summary>
        public void UnlockShelves(IEnumerable<string> shelfIds)
        {
            if (profile == null)
            {
                Debug.LogError("[BlueprintManager] Profile is null, cannot unlock shelves");
                return;
            }

            profile.UnlockShelves(shelfIds);
            profile.Save();
        }

        /// <summary>
        /// 批量解锁设施蓝图
        /// </summary>
        public void UnlockFacilities(IEnumerable<string> facilityIds)
        {
            if (profile == null)
            {
                Debug.LogError("[BlueprintManager] Profile is null, cannot unlock facilities");
                return;
            }

            profile.UnlockFacilities(facilityIds);
            profile.Save();
        }

        /// <summary>
        /// 解锁地板蓝图
        /// </summary>
        public void UnlockFloorTile(string floorTileId)
        {
            if (profile == null)
            {
                Debug.LogError("[BlueprintManager] Profile is null, cannot unlock floor tile");
                return;
            }

            profile.UnlockFloorTile(floorTileId);
            profile.Save();
        }

        /// <summary>
        /// 获取所有已解锁的地板ID
        /// </summary>
        public List<string> GetUnlockedFloorTileIds()
        {
            return profile?.unlockedFloorTileIds ?? new List<string>();
        }

        /// <summary>
        /// 获取所有已解锁的货架ID
        /// </summary>
        public List<string> GetUnlockedShelfIds()
        {
            return profile?.unlockedShelfIds ?? new List<string>();
        }

        /// <summary>
        /// 获取所有已解锁的设施ID
        /// </summary>
        public List<string> GetUnlockedFacilityIds()
        {
            return profile?.unlockedFacilityIds ?? new List<string>();
        }

        /// <summary>
        /// 消耗蓝图（当前设计为永久解锁，此方法留空）
        /// </summary>
        public void ConsumeBlueprint(string archetypeId)
        {
            // 如果未来设计为一次性消耗蓝图，在这里实现
            // 当前设计：蓝图永久解锁，不消耗
        }

        /// <summary>
        /// 删除运行时蓝图存档（不触碰内存）。GameStateManager 在 Instance 尚未初始化时使用。
        /// 始终删除 persistentDataPath/BlueprintProfile.json；下次 LoadProfile 会自动从 StreamingAssets 模板复制。
        /// 不会触碰 StreamingAssets 的设计模板。
        /// </summary>
        public static void ClearSaveFile()
        {
            try
            {
                string path = SavePathManager.GetWritePath("BlueprintProfile.json");
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log($"[BlueprintManager] 已删除 BlueprintProfile.json: {path}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BlueprintManager] 删除 BlueprintProfile.json 失败: {e.Message}");
            }
        }

        /// <summary>
        /// 清除存档并重置内存状态。
        /// 顺序：先删 persistent 文件 → 再 LoadProfile()，让 GetReadPath 检测到 persistent 缺失，
        /// 自动从 StreamingAssets 模板复制一份过来。这样内存中的 profile 直接恢复到"游戏初始模板"状态，
        /// 而不是空 profile（避免重置后玩家看到空蓝图列表）。
        /// 必须同时重置内存与磁盘，否则后续 UnlockShelf 调用 Save() 会把陈旧数据写回。
        /// </summary>
        public void ClearSaveAndResetState()
        {
            ClearSaveFile();
            LoadProfile();
        }

        /// <summary>
        /// 添加蓝图（运行时解锁，自动判断类型）
        /// </summary>
        public void AddBlueprint(string archetypeId)
        {
            if (profile == null)
            {
                Debug.LogError("[BlueprintManager] Profile is null, cannot add blueprint");
                return;
            }

            var building = ResolveBuildingArchetype(archetypeId);

            if (building == null)
            {
                Debug.LogWarning($"[BlueprintManager] Cannot find BuildingArchetype with ID '{archetypeId}', adding as generic blueprint");
                // 无法判断类型，默认作为货架处理
                UnlockShelf(archetypeId);
                return;
            }

            if (building is ShelfArchetype shelf)
            {
                UnlockShelf(archetypeId);
                OnShelfUnlocked?.Invoke(shelf);
            }
            else if (building is FacilityArchetype)
            {
                UnlockFacility(archetypeId);
            }
            else if (building is FloorTileArchetype floorTile)
            {
                UnlockFloorTile(archetypeId);
                OnFloorTileUnlocked?.Invoke(floorTile);
            }
            else
            {
                Debug.LogWarning($"[BlueprintManager] Unknown building type for '{archetypeId}', adding as generic blueprint");
                UnlockShelf(archetypeId);
            }
        }

        private static BuildingArchetype ResolveBuildingArchetype(string archetypeId)
        {
            var direct = Resources.Load<BuildingArchetype>($"ScriptableObjects/BuildingArchetype/{archetypeId}");
            if (direct != null)
                return direct;

            var allBuildings = Resources.LoadAll<BuildingArchetype>("ScriptableObjects/BuildingArchetype");
            foreach (var building in allBuildings)
            {
                if (building == null) continue;

                if (string.Equals(building.archetypeId, archetypeId, StringComparison.Ordinal)
                    || string.Equals(building.name, archetypeId, StringComparison.Ordinal)
                    || string.Equals(building.displayName, archetypeId, StringComparison.Ordinal))
                {
                    return building;
                }
            }

            return null;
        }
    }
}
