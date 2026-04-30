using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PopLife.Utility;

namespace PopLife.Data
{
    /// <summary>
    /// 建筑蓝图解锁配置
    /// - 模板（只读）：Assets/StreamingAssets/BlueprintProfile.json，由 BlueprintProfileEditor / PopLifeDataBrowserWindow 维护，进 git
    /// - 运行时存档：persistentDataPath/BlueprintProfile.json，首次读取自动从模板复制
    /// </summary>
    [Serializable]
    public class BlueprintProfile
    {
        [Tooltip("已解锁的货架蓝图ID列表")]
        public List<string> unlockedShelfIds = new List<string>();

        [Tooltip("已解锁的设施蓝图ID列表")]
        public List<string> unlockedFacilityIds = new List<string>();

        [Tooltip("已解锁的地板蓝图ID列表")]
        public List<string> unlockedFloorTileIds = new List<string>();

        /// <summary>
        /// 加载BlueprintProfile (使用SavePathManager自动处理路径)
        /// </summary>
        public static BlueprintProfile Load()
        {
            string path = SavePathManager.GetReadPath("BlueprintProfile.json");

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[BlueprintProfile] File not found at {path}, creating default profile");
                return CreateDefaultProfile();
            }

            try
            {
                string json = File.ReadAllText(path);
                var profile = JsonUtility.FromJson<BlueprintProfile>(json);

                if (profile == null)
                {
                    Debug.LogWarning("[BlueprintProfile] Failed to parse JSON, creating default profile");
                    return CreateDefaultProfile();
                }

                // 确保列表不为null
                if (profile.unlockedShelfIds == null)
                {
                    profile.unlockedShelfIds = new List<string>();
                }
                if (profile.unlockedFacilityIds == null)
                {
                    profile.unlockedFacilityIds = new List<string>();
                }
                if (profile.unlockedFloorTileIds == null)
                {
                    profile.unlockedFloorTileIds = new List<string>();
                }

                Debug.Log($"[BlueprintProfile] Loaded {profile.unlockedShelfIds.Count} shelves and {profile.unlockedFacilityIds.Count} facilities from {path}");
                return profile;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BlueprintProfile] Failed to load: {e.Message}");
                return CreateDefaultProfile();
            }
        }

        /// <summary>
        /// 保存BlueprintProfile
        /// </summary>
        public void Save()
        {
            string path = SavePathManager.GetWritePath("BlueprintProfile.json");

            try
            {
                SavePathManager.EnsureDirectoryExists(path);
                string json = JsonUtility.ToJson(this, true);
                File.WriteAllText(path, json);
                Debug.Log($"[BlueprintProfile] Saved {unlockedShelfIds.Count} shelves and {unlockedFacilityIds.Count} facilities to {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BlueprintProfile] Failed to save: {e.Message}");
            }
        }

        /// <summary>
        /// 创建默认配置（空白档案）
        /// </summary>
        private static BlueprintProfile CreateDefaultProfile()
        {
            return new BlueprintProfile
            {
                unlockedShelfIds = new List<string>(),
                unlockedFacilityIds = new List<string>()
            };
        }

        /// <summary>
        /// 检查是否已解锁某个货架
        /// </summary>
        public bool HasShelfBlueprint(string shelfId)
        {
            return unlockedShelfIds != null && unlockedShelfIds.Contains(shelfId);
        }

        /// <summary>
        /// 检查是否已解锁某个设施
        /// </summary>
        public bool HasFacilityBlueprint(string facilityId)
        {
            return unlockedFacilityIds != null && unlockedFacilityIds.Contains(facilityId);
        }

        public bool HasFloorTileBlueprint(string floorTileId)
        {
            return unlockedFloorTileIds != null && unlockedFloorTileIds.Contains(floorTileId);
        }

        /// <summary>
        /// 解锁货架蓝图
        /// </summary>
        public void UnlockShelf(string shelfId)
        {
            if (unlockedShelfIds == null)
                unlockedShelfIds = new List<string>();

            if (!unlockedShelfIds.Contains(shelfId))
            {
                unlockedShelfIds.Add(shelfId);
                Debug.Log($"[BlueprintProfile] Unlocked shelf blueprint: {shelfId}");
            }
        }

        /// <summary>
        /// 解锁设施蓝图
        /// </summary>
        public void UnlockFacility(string facilityId)
        {
            if (unlockedFacilityIds == null)
                unlockedFacilityIds = new List<string>();

            if (!unlockedFacilityIds.Contains(facilityId))
            {
                unlockedFacilityIds.Add(facilityId);
                Debug.Log($"[BlueprintProfile] Unlocked facility blueprint: {facilityId}");
            }
        }

        public void UnlockFloorTile(string floorTileId)
        {
            if (unlockedFloorTileIds == null)
                unlockedFloorTileIds = new List<string>();

            if (!unlockedFloorTileIds.Contains(floorTileId))
            {
                unlockedFloorTileIds.Add(floorTileId);
                Debug.Log($"[BlueprintProfile] Unlocked floor tile blueprint: {floorTileId}");
            }
        }

        /// <summary>
        /// 批量解锁货架
        /// </summary>
        public void UnlockShelves(IEnumerable<string> shelfIds)
        {
            if (unlockedShelfIds == null)
                unlockedShelfIds = new List<string>();

            foreach (var id in shelfIds)
            {
                if (!unlockedShelfIds.Contains(id))
                {
                    unlockedShelfIds.Add(id);
                }
            }
        }

        /// <summary>
        /// 批量解锁设施
        /// </summary>
        public void UnlockFacilities(IEnumerable<string> facilityIds)
        {
            if (unlockedFacilityIds == null)
                unlockedFacilityIds = new List<string>();

            foreach (var id in facilityIds)
            {
                if (!unlockedFacilityIds.Contains(id))
                {
                    unlockedFacilityIds.Add(id);
                }
            }
        }

        /// <summary>
        /// 移除货架蓝图（用于编辑器工具）
        /// </summary>
        public void RemoveShelf(string shelfId)
        {
            if (unlockedShelfIds != null)
            {
                unlockedShelfIds.Remove(shelfId);
            }
        }

        /// <summary>
        /// 移除设施蓝图（用于编辑器工具）
        /// </summary>
        public void RemoveFacility(string facilityId)
        {
            if (unlockedFacilityIds != null)
            {
                unlockedFacilityIds.Remove(facilityId);
            }
        }
    }
}
