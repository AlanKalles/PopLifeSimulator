using UnityEngine;

namespace PopLife.Runtime
{
    /// <summary>
    /// 背景跟随器 - 使背景板选择性跟随相机的X/Y轴和缩放
    /// Background follower - Makes background selectively follow camera X/Y-axis and zoom
    /// </summary>
    public class BackgroundFollower : MonoBehaviour
    {
        [Header("跟随设置 / Follow Settings")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool followXAxis = false;
        [SerializeField] private bool followYAxis = true;
        [SerializeField] private bool followZoom = true;

        [Header("偏移设置 / Offset Settings")]
        [SerializeField] private float xOffset = 0f;
        [SerializeField] private float yOffset = 0f;
        [SerializeField] private float zPosition = 10f; // 背景的Z轴位置（应该在相机后面）

        private Vector3 initialScale;
        private float initialCameraSize;

        private void Awake()
        {
            // 如果没有指定相机，使用主相机
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                Debug.LogError("BackgroundFollower: 找不到目标相机！");
                enabled = false;
                return;
            }

            // 记录初始缩放和相机大小
            initialScale = transform.localScale;

            if (targetCamera.orthographic)
            {
                initialCameraSize = targetCamera.orthographicSize;
            }
            else
            {
                Debug.LogWarning("BackgroundFollower: 建议使用正交相机以获得最佳效果");
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;

            Vector3 newPosition = transform.position;

            // 跟随X轴
            if (followXAxis)
            {
                newPosition.x = targetCamera.transform.position.x + xOffset;
            }

            // 跟随Y轴
            if (followYAxis)
            {
                newPosition.y = targetCamera.transform.position.y + yOffset;
            }

            // 设置Z轴位置
            newPosition.z = zPosition;

            transform.position = newPosition;

            // 根据相机缩放调整背景大小
            if (followZoom && targetCamera.orthographic)
            {
                float zoomRatio = targetCamera.orthographicSize / initialCameraSize;
                transform.localScale = initialScale * zoomRatio;
            }
        }

        /// <summary>
        /// 设置目标相机
        /// </summary>
        public void SetTargetCamera(Camera camera)
        {
            targetCamera = camera;
            if (targetCamera != null && targetCamera.orthographic)
            {
                initialCameraSize = targetCamera.orthographicSize;
            }
        }

        /// <summary>
        /// 设置X轴偏移
        /// </summary>
        public void SetXOffset(float offset)
        {
            xOffset = offset;
        }

        /// <summary>
        /// 设置Y轴偏移
        /// </summary>
        public void SetYOffset(float offset)
        {
            yOffset = offset;
        }

        /// <summary>
        /// 设置Z轴位置
        /// </summary>
        public void SetZPosition(float zPos)
        {
            zPosition = zPos;
            Vector3 pos = transform.position;
            pos.z = zPosition;
            transform.position = pos;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 在Scene视图中显示背景位置信息
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
#endif
    }
}
