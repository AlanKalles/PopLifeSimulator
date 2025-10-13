using UnityEngine;

namespace PopLife.Services
{
    /// <summary>
    /// 楼层检测服务 - 用于鼠标自动检测楼层系统
    /// 功能：检测鼠标位置对应的楼层，支持性能优化的间隔帧检测
    /// </summary>
    public class FloorDetectionService
    {
        // === 配置 ===
        private readonly int detectionInterval;     // 检测间隔（帧）
        private readonly Camera targetCamera;       // 目标相机
        private readonly LayerMask floorLayer;      // 楼层Layer

        // === 缓存 ===
        private Runtime.FloorGrid cachedFloor;      // 上一帧检测结果
        private int frameCounter;                   // 帧计数器

        // === 性能优化：预分配Raycast缓冲区 ===
        private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[1];

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="camera">用于坐标转换的相机</param>
        /// <param name="interval">检测间隔（帧），默认3帧检测一次以优化性能</param>
        public FloorDetectionService(Camera camera, int interval = 3)
        {
            targetCamera = camera;
            detectionInterval = Mathf.Max(1, interval);
            floorLayer = LayerMask.GetMask("FloorDetection");

            if (floorLayer == 0)
            {
                Debug.LogWarning("FloorDetectionService: Layer 'FloorDetection' not found. Detection will not work.");
            }
        }

        /// <summary>
        /// 检测鼠标当前位置的楼层
        /// </summary>
        /// <returns>检测到的FloorGrid，如果没有检测到则返回null</returns>
        public Runtime.FloorGrid DetectFloorAtMouse()
        {
            // 间隔帧检测（性能优化）
            frameCounter++;
            if (frameCounter < detectionInterval)
            {
                return cachedFloor;
            }
            frameCounter = 0;

            // 检查鼠标是否在UI上
            if (IsPointerOverUI())
            {
                cachedFloor = null;
                return null;
            }

            // 执行检测
            Vector2 mousePos = GetMouseWorldPosition();
            Runtime.FloorGrid detected = RaycastFloor(mousePos);

            cachedFloor = detected;
            return detected;
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
        /// 使用Raycast检测指定位置的楼层
        /// </summary>
        private Runtime.FloorGrid RaycastFloor(Vector2 worldPos)
        {
            // 使用RaycastNonAlloc减少GC分配
            int hitCount = Physics2D.RaycastNonAlloc(
                worldPos,
                Vector2.zero,  // 零距离射线（点检测）
                hitBuffer,
                0f,
                floorLayer
            );

            if (hitCount > 0 && hitBuffer[0].collider != null)
            {
                return hitBuffer[0].collider.GetComponent<Runtime.FloorGrid>();
            }

            return null;
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
            cachedFloor = null;
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
