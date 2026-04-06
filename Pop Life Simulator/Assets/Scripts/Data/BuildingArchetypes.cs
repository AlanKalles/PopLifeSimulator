using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Data
{
    // 等级数据（基类）
    [Serializable]
    public class BuildingLevelData
    {
        public int level = 1;
        public int upgradeFameCost;
        public int maintenanceFee;
    }

    public enum ProductCategory
    {
        Accessories, Anal, DigitalMedia, Dildo, Enhancements,
        Fleshlight, Furniture, Instruments, Lingerie, Vibrator, Wellness
    }
    public enum FacilityType { Cashier, AirConditioner, ATM, SecurityCamera, MusicPlayer }
    public enum EffectType { ReduceEmbarrassment, IncreaseAppeal, IncreaseCustomerSpeed, RestoreMoney }

    /// <summary> FloorTile 放置到世界网格的验证接口 </summary>
    public interface IWorldPlaceable
    {
        bool ValidateWorldPlacement(PopLife.Runtime.WorldGrid grid, Vector2Int position, int rotation);
    }

    /// <summary> Shelf/Facility 放置到 interior 的验证接口 </summary>
    public interface IInteriorPlaceable
    {
        bool ValidateInteriorPlacement(PopLife.Runtime.InteriorGrid interior, Vector2Int localPos, int rotation);
    }

    // 原型基类
    public abstract class BuildingArchetype : ScriptableObject, IInteriorPlaceable
    {
        [Header("基础信息")]
        [HideInInspector] public string archetypeId; // 由 OnValidate 自动同步为资产文件名
        public string displayName;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            archetypeId = name;

            // icon未设置时，尝试从prefab的SpriteRenderer取sprite作为回退
            if (icon == null && prefab != null)
            {
                var sr = prefab.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                    icon = sr.sprite;
            }
        }
#endif
        public Sprite icon;
        public GameObject prefab;

        [Header("建造信息")]
        public int buildCost;
        public int moveCost = 0;

        [Header("拆除返还")]
        [Range(0f, 1f)]
        [Tooltip("Percentage of buildCost refunded when destroyed (0-100%)")]
        public float destroyRefundRate = 0.8f;

        [Header("占用空间")]
        [SerializeField] protected List<Vector2Int> footprintPattern = new(); // 相对原点

        public bool canRotate = false;

        [Header("等级系统")]
        [SerializeField][HideInInspector] protected BuildingLevelData[] levels;
        public virtual int MaxLevel => levels?.Length ?? 0;

        // 统一取级别数据
        public virtual BuildingLevelData GetLevel(int lvl)
        {
            if (levels == null || levels.Length == 0) return null;
            int i = Mathf.Clamp(lvl - 1, 0, levels.Length - 1);
            return levels[i];
        }

        /// <summary> Interior 放置验证默认实现 </summary>
        public virtual bool ValidateInteriorPlacement(PopLife.Runtime.InteriorGrid interior, Vector2Int localPos, int rotation)
        {
            return interior.CanPlace(GetRotatedFootprint(rotation), localPos);
        }

        // 返回旋转后占格（始终返回新列表，避免外部修改污染原型）
        public List<Vector2Int> GetRotatedFootprint(int rotation)
        {
            int r = ((rotation % 4) + 4) % 4;
            var src = footprintPattern;
            var res = new List<Vector2Int>(src.Count);
            if (!canRotate) r = 0;
            foreach (var v in src) res.Add(Rotate(v, r));
            return res;
        }

        protected static Vector2Int Rotate(Vector2Int v, int r)
        {
            return r switch
            {
                1 => new Vector2Int(-v.y, v.x),   // 90
                2 => new Vector2Int(-v.x, -v.y),  // 180
                3 => new Vector2Int(v.y, -v.x),   // 270
                _ => v
            };
        }
    }
    
}
