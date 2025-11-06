using UnityEngine;
using PopLife.Data;
using System.Collections.Generic;

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

            // 检查是否在货架列表或设施列表中
            return profile.HasShelfBlueprint(archetypeId) || profile.HasFacilityBlueprint(archetypeId);
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
        /// 添加蓝图（运行时解锁，自动判断类型）
        /// </summary>
        public void AddBlueprint(string archetypeId)
        {
            if (profile == null)
            {
                Debug.LogError("[BlueprintManager] Profile is null, cannot add blueprint");
                return;
            }

            // 尝试从Resources加载以判断类型
            var building = Resources.Load<BuildingArchetype>($"ScriptableObjects/BuildingArchetype/{archetypeId}");

            if (building == null)
            {
                Debug.LogWarning($"[BlueprintManager] Cannot find BuildingArchetype with ID '{archetypeId}', adding as generic blueprint");
                // 无法判断类型，默认作为货架处理
                UnlockShelf(archetypeId);
                return;
            }

            if (building is ShelfArchetype)
            {
                UnlockShelf(archetypeId);
            }
            else if (building is FacilityArchetype)
            {
                UnlockFacility(archetypeId);
            }
            else
            {
                Debug.LogWarning($"[BlueprintManager] Unknown building type for '{archetypeId}', adding as generic blueprint");
                UnlockShelf(archetypeId);
            }
        }
    }
}
