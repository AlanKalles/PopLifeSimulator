using UnityEngine;

namespace PopLife.UI
{
    /// <summary>
    /// 浮动文字生成器 - 在指定位置生成浮动资源文字
    /// </summary>
    public class FloatingTextSpawner : MonoBehaviour
    {
        public static FloatingTextSpawner Instance { get; private set; }

        [Header("World Space 预制体")]
        [SerializeField] private FloatingCostText worldPrefab;
        [SerializeField] private float worldSpawnOffsetY = 0.5f;

        [Header("Screen Space 预制体")]
        [SerializeField] private FloatingCostText uiPrefab;

        [Header("UI 锚点")]
        [SerializeField] private Transform moneyUIAnchor;
        [SerializeField] private Transform fameUIAnchor;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// 扣钱文字（建造时）- 红色
        /// </summary>
        public void SpawnCostText(Vector3 worldPos, int cost)
        {
            // 建筑位置：向上浮动
            if (worldPrefab != null)
            {
                var spawnPos = worldPos + Vector3.up * worldSpawnOffsetY;
                var worldInstance = Instantiate(worldPrefab, spawnPos, Quaternion.identity);
                worldInstance.PlayCost(cost, FloatDirection.Up, false);
            }

            // Money UI：向下浮动
            if (uiPrefab != null && moneyUIAnchor != null)
            {
                var uiInstance = Instantiate(uiPrefab, moneyUIAnchor);
                uiInstance.transform.localPosition = Vector3.zero;
                uiInstance.PlayCost(cost, FloatDirection.Down, true);
            }
        }

        /// <summary>
        /// 收入文字（结账时）- 绿色
        /// </summary>
        public void SpawnIncomeText(Vector3 worldPos, int amount)
        {
            // 收银台位置：向上浮动
            if (worldPrefab != null)
            {
                var spawnPos = worldPos + Vector3.up * worldSpawnOffsetY;
                var worldInstance = Instantiate(worldPrefab, spawnPos, Quaternion.identity);
                worldInstance.PlayIncome(amount, FloatDirection.Up, false);
            }

            // Money UI：向下浮动
            if (uiPrefab != null && moneyUIAnchor != null)
            {
                var uiInstance = Instantiate(uiPrefab, moneyUIAnchor);
                uiInstance.transform.localPosition = Vector3.zero;
                uiInstance.PlayIncome(amount, FloatDirection.Down, true);
            }
        }

        /// <summary>
        /// Fame 文字（拿货时）- 金色
        /// </summary>
        public void SpawnFameText(Vector3 worldPos, float amount)
        {
            // 货架位置：向上浮动
            if (worldPrefab != null)
            {
                var spawnPos = worldPos + Vector3.up * worldSpawnOffsetY;
                var worldInstance = Instantiate(worldPrefab, spawnPos, Quaternion.identity);
                worldInstance.PlayFame(amount, FloatDirection.Up, false);
            }

            // Fame UI：向下浮动
            if (uiPrefab != null && fameUIAnchor != null)
            {
                var uiInstance = Instantiate(uiPrefab, fameUIAnchor);
                uiInstance.transform.localPosition = Vector3.zero;
                uiInstance.PlayFame(amount, FloatDirection.Down, true);
            }
        }
    }
}
