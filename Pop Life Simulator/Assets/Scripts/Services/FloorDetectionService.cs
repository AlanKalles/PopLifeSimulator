using UnityEngine;

namespace PopLife.Services
{
    /// <summary>
    /// 楼层检测服务 - 用于鼠标自动检测地板瓦片
    /// 功能：检测鼠标位置对应的 FloorTileInstance，支持性能优化的间隔帧检测
    /// </summary>
    public class FloorDetectionService
    {
        // === 配置 ===
        private readonly int detectionInterval;     // 检测间隔（帧）
        private readonly Camera targetCamera;       // 目标相机

        // === 缓存 ===
        private Runtime.FloorTileInstance cachedTile; // 上一帧检测结果
        private int frameCounter;                     // 帧计数器

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="camera">用于坐标转换的相机</param>
        /// <param name="interval">检测间隔（帧），默认3帧检测一次以优化性能</param>
        public FloorDetectionService(Camera camera, int interval = 3)
        {
            targetCamera = camera;
            detectionInterval = Mathf.Max(1, interval);
        }

        /// <summary>
        /// 检测鼠标当前位置的地板瓦片
        /// </summary>
        /// <returns>检测到的 FloorTileInstance，如果没有检测到则返回 null</returns>
        public Runtime.FloorTileInstance DetectFloorTileAtMouse()
        {
            // 间隔帧检测（性能优化）
            frameCounter++;
            if (frameCounter < detectionInterval)
            {
                return cachedTile;
            }
            frameCounter = 0;

            // 检查鼠标是否在UI上
            if (IsPointerOverUI())
            {
                cachedTile = null;
                return null;
            }

            // 逻辑查表检测（不依赖 Collider）
            Vector2 mousePos = GetMouseWorldPosition();
            cachedTile = LogicDetectFloorTile(mousePos);
            return cachedTile;
        }

        /// <summary>
        /// 获取鼠标的世界坐标
        /// </summary>
        private Vector2 GetMouseWorldPosition()
        {
            if (targetCamera == null)
            {
                Debug.LogError("FloorDetectionService: Target camera is null!");
                return Vector2.zero;
            }

            Vector3 mousePos = Input.mousePosition;
            mousePos.z = 0f;
            return targetCamera.ScreenToWorldPoint(mousePos);
        }

        /// <summary>
        /// 逻辑查表检测：WorldGrid.WorldToGrid → GetFloorTileAt（不依赖 Collider）
        /// </summary>
        private Runtime.FloorTileInstance LogicDetectFloorTile(Vector2 worldPos)
        {
            var wg = Runtime.WorldGrid.Instance;
            if (wg == null) return null;
            var gridPos = wg.WorldToGrid(new Vector3(worldPos.x, worldPos.y, 0));
            return wg.GetFloorTileAt(gridPos);
        }

        /// <summary>
        /// 检查鼠标是否在UI元素上
        /// </summary>
        private bool IsPointerOverUI()
        {
            // 使用EventSystem检查鼠标是否在UI上
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            }

            return false;
        }

        /// <summary>
        /// 重置缓存（用于模式切换时强制重新检测）
        /// </summary>
        public void ResetCache()
        {
            cachedTile = null;
            frameCounter = 0;
        }

        /// <summary>
        /// 设置检测间隔（运行时调整性能）
        /// </summary>
        public void SetDetectionInterval(int interval)
        {
            // 通过反射修改readonly字段需要使用Reflection，这里记录需求
            Debug.LogWarning("FloorDetectionService: Detection interval cannot be changed after construction.");
        }
    }
}
