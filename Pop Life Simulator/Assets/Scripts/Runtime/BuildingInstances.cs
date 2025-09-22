using System;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Runtime
{
    // 可序列化 DTO
    [Serializable]
    public class BuildingSaveData
    {
        public string archetypeId;
        public string instanceId;
        public Vector2Int position;
        public int floorId;
        public int rotation;
        public int level;
        public string payloadJson; // 子类自定义数据序列化
    }

    // 运行期实例基类
    public abstract class BuildingInstance : MonoBehaviour
    {
        [Header("原型引用")]
        public BuildingArchetype archetype;

        [Header("实例数据")]
        public string instanceId;
        public int currentLevel = 1;
        public Vector2Int gridPosition;
        public int floorId;
        public int rotation;

        [Header("运行状态")]
        public bool isOperational = true;
        protected float operationStartTime;

        protected virtual void Awake() { }
        protected virtual void OnInitialized() { }
        protected virtual void OnUpgraded() { }

        public virtual void Initialize(BuildingArchetype arch, Vector2Int pos, int floor)
        {
            archetype = arch;
            gridPosition = pos;
            floorId = floor;
            instanceId = Guid.NewGuid().ToString();
            operationStartTime = Time.time;
            OnInitialized();
        }

        public virtual bool TryUpgrade()
        {
            if (archetype == null) return false;
            if (currentLevel >= archetype.MaxLevel) return false;

            var next = archetype.GetLevel(currentLevel + 1);
            if (next == null) return false;

            // 外部资源系统：自行替换
            if (!ResourceManager.Instance.CanAfford(next.upgradeMoneyCost, next.upgradeFameCost))
                return false;

            ResourceManager.Instance.Spend(next.upgradeMoneyCost, next.upgradeFameCost);
            currentLevel++;
            OnUpgraded();
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
                floorId = floorId,
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
        }
    }

    

    
}
