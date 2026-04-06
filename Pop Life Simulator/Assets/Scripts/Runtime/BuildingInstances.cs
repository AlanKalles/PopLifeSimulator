using System;
using UnityEngine;
using PopLife.Data;
using PopLife.Utility;

namespace PopLife.Runtime
{
    // 可序列化 DTO
    [Serializable]
    public class BuildingSaveData
    {
        public string archetypeId;
        public string instanceId;
        public Vector2Int position;
        public string hostFloorTileInstanceId;
        public int rotation;
        public int level;
        public string payloadJson; // 子类自定义数据序列化
    }

    // 运行期实例基类
    public abstract class BuildingInstance : MonoBehaviour
    {
        [Header("原型引用")]
        public BuildingArchetype archetype;

        [Header("实例数据（会被序列化保存）")]
        public string instanceId;
        public int currentLevel = 1;
        [SerializeField] // 确保被序列化保存到场景
        public Vector2Int gridPosition;
        [SerializeField]
        public string hostFloorTileInstanceId;
        [SerializeField] // 确保被序列化保存到场景
        public int rotation;

        [Header("运行状态")]
        public bool isOperational = true;
        protected float operationStartTime;

        [Header("Editor 锁定")]
        [SerializeField] private bool editorLockedMove;
        [SerializeField] private bool editorLockedDestroy;

        public bool EditorLockedMove => editorLockedMove;
        public bool EditorLockedDestroy => editorLockedDestroy;

        protected virtual void Awake() { }
        protected virtual void OnInitialized() { }
        protected virtual void OnUpgraded() { }

        public virtual void Initialize(BuildingArchetype arch, Vector2Int gridPos, string hostTileId)
        {
            archetype = arch;
            gridPosition = gridPos;
            hostFloorTileInstanceId = hostTileId;
            instanceId = Guid.NewGuid().ToString();
            operationStartTime = Time.time;
            UpdateSortingOrder();
            OnInitialized();
        }

        /// <summary>
        /// 获取下一级升级所需Fame（含全局修饰器），-1 表示无法升级
        /// </summary>
        public int GetModifiedUpgradeCost()
        {
            if (archetype == null || currentLevel >= archetype.MaxLevel) return -1;
            var next = archetype.GetLevel(currentLevel + 1);
            if (next == null) return -1;
            return GlobalModifierManager.Instance != null
                ? Mathf.RoundToInt(next.upgradeFameCost * GlobalModifierManager.Instance.GetUpgradeCostMultiplier())
                : next.upgradeFameCost;
        }

        public virtual bool TryUpgrade()
        {
            int finalFameCost = GetModifiedUpgradeCost();
            if (finalFameCost < 0) return false;

            if (!ResourceManager.Instance.CanAfford(0, finalFameCost))
                return false;

            ResourceManager.Instance.Spend(0, finalFameCost);
            currentLevel++;
            OnUpgraded();

            // 播放升级音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(AudioKeys.BUILDING_UPGRADED);
            }

            // 通知建筑变化（触发 Store Appeal 重算等）
            ConstructionManager.NotifyBuildingChanged();

            return true;
        }

        public virtual int GetMaintenanceFee()
        {
            var data = archetype?.GetLevel(currentLevel);
            return data?.maintenanceFee ?? 0;
        }

        public virtual BuildingSaveData GetSaveData()
        {
            return new BuildingSaveData
            {
                archetypeId = archetype ? archetype.archetypeId : "",
                instanceId = instanceId,
                position = gridPosition,
                hostFloorTileInstanceId = hostFloorTileInstanceId,
                rotation = rotation,
                level = currentLevel,
                payloadJson = null
            };
        }

        public virtual void LoadFromSaveData(BuildingSaveData data)
        {
            instanceId = data.instanceId;
            currentLevel = data.level;
            rotation = data.rotation;
            gridPosition = data.position;
            hostFloorTileInstanceId = data.hostFloorTileInstanceId;
            UpdateSortingOrder();
        }

        /// <summary> 世界网格位置（仅 FloorTileInstance 有意义） </summary>
        public Vector2Int GetWorldGridPosition()
        {
            Debug.Assert(this is FloorTileInstance,
                $"GetWorldGridPosition called on interior building: {GetType().Name}");
            return gridPosition;
        }

        /// <summary> Interior 局部位置（仅 shelf/facility 有意义） </summary>
        public Vector2Int GetInteriorLocalPosition()
        {
            Debug.Assert(!string.IsNullOrEmpty(hostFloorTileInstanceId),
                $"GetInteriorLocalPosition called on FloorTile: {GetType().Name}");
            return gridPosition;
        }

        /// <summary> 世界锚点（排序/高亮/寻路统一使用，不区分网格类型） </summary>
        public Vector3 GetWorldAnchorPosition() => transform.position;

        /// <summary>
        /// 更新建筑的 sorting order，用于实现透视关系
        /// 调用时机：初始化、移动、加载存档
        /// </summary>
        public void UpdateSortingOrder()
        {
            SortingOrderUtility.UpdateBuildingSortingOrder(gameObject, transform.position);
        }
    }




}
