using UnityEngine;

namespace PopLife.UI
{
    /// <summary>
    /// 浮动文字生成器 - 在指定位置生成浮动扣钱文字
    /// </summary>
    public class FloatingTextSpawner : MonoBehaviour
    {
        public static FloatingTextSpawner Instance { get; private set; }

        [Header("World Space 预制体（建筑位置向上浮动）")]
        [SerializeField] private FloatingCostText worldPrefab;
        [SerializeField] private float worldSpawnOffsetY = 0.5f;

        [Header("Screen Space 预制体（UI位置向下浮动）")]
        [SerializeField] private FloatingCostText uiPrefab;
        [SerializeField] private Transform moneyUIAnchor;

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
        /// 在建筑位置和UI位置同时生成扣钱文字
        /// </summary>
        /// <param name="worldPos">建筑世界坐标</param>
        /// <param name="cost">扣除金额</param>
        public void SpawnCostText(Vector3 worldPos, int cost)
        {
            // 建筑位置：向上浮动
            if (worldPrefab != null)
            {
                var spawnPos = worldPos + Vector3.up * worldSpawnOffsetY;
                var worldInstance = Instantiate(worldPrefab, spawnPos, Quaternion.identity);
                worldInstance.Play(cost, FloatDirection.Up, false);
            }

            // UI位置：向下浮动
            if (uiPrefab != null && moneyUIAnchor != null)
            {
                var uiInstance = Instantiate(uiPrefab, moneyUIAnchor);
                uiInstance.transform.localPosition = Vector3.zero;
                uiInstance.Play(cost, FloatDirection.Down, true);
            }
        }
    }
}
