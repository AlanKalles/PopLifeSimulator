using System;
using UnityEngine;
using UnityEngine.EventSystems;
using PopLife.Services;

namespace PopLife.Runtime
{
    /// <summary>
    /// 建筑前景透明度配置
    /// </summary>
    [Serializable]
    public class ForegroundAlphaConfig
    {
        [Tooltip("建筑前景GameObject（需要包含SpriteRenderer组件）")]
        public GameObject targetObject;

        [Tooltip("透明度调整的缩放范围最小值（zoom multiplier）")]
        public float alphaAdjustZoomMin = 0.5f;

        [Tooltip("透明度调整的缩放范围最大值（zoom multiplier）")]
        public float alphaAdjustZoomMax = 1.2f;

        [Tooltip("在alphaAdjustZoomMin时的透明度（0-1）")]
        public float alphaAtMinZoom = 1f;

        [Tooltip("在alphaAdjustZoomMax时的透明度（0-1）")]
        public float alphaAtMaxZoom = 0.3f;

        [HideInInspector]
        public SpriteRenderer cachedRenderer;
    }

    public class CameraController : MonoBehaviour
    {
        [Header("缩放设置")]
        [SerializeField] private float baseOrthographicSize = 5f;
        [SerializeField] private float maxZoomMultiplier = 2f;
        [SerializeField] private float minZoomMultiplier = 0.5f;
        [SerializeField] private float zoomSpeed = 0.1f;
        [SerializeField] private float smoothZoomSpeed = 5f;

        [Header("平移设置")]
        [SerializeField] private float dragSpeed = 1f;
        [SerializeField] private bool invertDrag = false;

        [Header("边界设置")]
        [SerializeField] private Vector2 boundaryMin = new Vector2(-10f, -10f);
        [SerializeField] private Vector2 boundaryMax = new Vector2(10f, 10f);

        [Header("建筑前景透明度设置")]
        [Tooltip("多个建筑前景的透明度配置列表")]
        [SerializeField] private ForegroundAlphaConfig[] foregroundConfigs;

        private Camera targetCamera;
        private float currentZoom = 1f;
        private float targetZoom = 1f;

        private bool isDragging = false;
        private bool pendingDragStart = false; // 等待 InputGateService 确认拖拽
        private Vector3 dragOrigin;
        private Vector3 lastMousePosition;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null && targetCamera.orthographic)
            {
                baseOrthographicSize = targetCamera.orthographicSize;
                currentZoom = 1f;
                targetZoom = 1f;
            }
            else
            {
                Debug.LogError("CameraController需要一个正交相机!");
            }

            // 初始化所有建筑前景的SpriteRenderer
            if (foregroundConfigs != null)
            {
                foreach (var config in foregroundConfigs)
                {
                    if (config != null && config.targetObject != null)
                    {
                        config.cachedRenderer = config.targetObject.GetComponent<SpriteRenderer>();
                        if (config.cachedRenderer == null)
                        {
                            Debug.LogWarning($"建筑前景GameObject '{config.targetObject.name}' 没有SpriteRenderer组件！");
                        }
                    }
                }
            }
        }

        private void Update()
        {
            if (targetCamera == null || !targetCamera.orthographic) return;

            HandleZoomInput();
            HandleDragInput();
            ApplyZoom();
            ApplyBoundaries();
            UpdateForegroundAlpha();
        }

        private void HandleZoomInput()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                targetZoom -= scrollDelta * zoomSpeed;
                targetZoom = Mathf.Clamp(targetZoom, minZoomMultiplier, maxZoomMultiplier);
            }
        }

        private void HandleDragInput()
        {
            var gate = InputGateService.Instance;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() && !isDragging)
                return;

            // 鼠标按下：预记录拖拽起点，但不立即移动相机
            if (Input.GetMouseButtonDown(0) && !isDragging && !pendingDragStart)
            {
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    pendingDragStart = true;
                    dragOrigin = targetCamera.ScreenToWorldPoint(Input.mousePosition);
                    lastMousePosition = Input.mousePosition;
                }
            }

            // 等待 InputGateService 确认超过拖拽阈值后才开始移动
            if (pendingDragStart && !isDragging && gate != null && gate.IsDragging)
            {
                isDragging = true;
                // 重新采样 dragOrigin 以消除阈值阶段的跳跃
                dragOrigin = targetCamera.ScreenToWorldPoint(Input.mousePosition);
            }

            if (Input.GetMouseButton(0) && isDragging)
            {
                Vector3 currentMouseWorldPos = targetCamera.ScreenToWorldPoint(Input.mousePosition);
                Vector3 difference = dragOrigin - currentMouseWorldPos;

                if (invertDrag)
                {
                    difference = -difference;
                }

                Vector3 newPosition = targetCamera.transform.position + difference * dragSpeed;
                newPosition.z = targetCamera.transform.position.z;
                targetCamera.transform.position = newPosition;

                lastMousePosition = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                pendingDragStart = false;
            }
        }

        private void ApplyZoom()
        {
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * smoothZoomSpeed);
            targetCamera.orthographicSize = baseOrthographicSize * currentZoom;
        }

        private void ApplyBoundaries()
        {
            Vector3 pos = targetCamera.transform.position;

            // 计算当前相机视野的半宽和半高
            float cameraHalfHeight = targetCamera.orthographicSize;
            float cameraHalfWidth = cameraHalfHeight * targetCamera.aspect;

            // 计算相机中心点的有效边界（确保相机视野不超出矩形框）
            float minX = boundaryMin.x + cameraHalfWidth;
            float maxX = boundaryMax.x - cameraHalfWidth;
            float minY = boundaryMin.y + cameraHalfHeight;
            float maxY = boundaryMax.y - cameraHalfHeight;

            // 防止边界翻转（当相机视野大于边界框时，限制在中心）
            if (minX > maxX)
            {
                float centerX = (boundaryMin.x + boundaryMax.x) * 0.5f;
                minX = maxX = centerX;
            }
            if (minY > maxY)
            {
                float centerY = (boundaryMin.y + boundaryMax.y) * 0.5f;
                minY = maxY = centerY;
            }

            // 约束相机中心点位置
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

            targetCamera.transform.position = pos;
        }

        /// <summary>
        /// 根据相机缩放更新所有建筑前景的透明度
        /// </summary>
        private void UpdateForegroundAlpha()
        {
            if (foregroundConfigs == null) return;

            foreach (var config in foregroundConfigs)
            {
                if (config == null || config.cachedRenderer == null) continue;

                float targetAlpha;

                // 如果缩放值在调整范围内，线性插值透明度
                if (currentZoom >= config.alphaAdjustZoomMin && currentZoom <= config.alphaAdjustZoomMax)
                {
                    // 计算在调整范围内的归一化位置（0-1）
                    float t = (currentZoom - config.alphaAdjustZoomMin) / (config.alphaAdjustZoomMax - config.alphaAdjustZoomMin);
                    // 线性插值：从alphaAtMinZoom到alphaAtMaxZoom
                    targetAlpha = Mathf.Lerp(config.alphaAtMinZoom, config.alphaAtMaxZoom, t);
                }
                // 如果缩放值小于最小值，使用最小值对应的透明度
                else if (currentZoom < config.alphaAdjustZoomMin)
                {
                    targetAlpha = config.alphaAtMinZoom;
                }
                // 如果缩放值大于最大值（但仍在max范围内），保持最低透明度
                else
                {
                    targetAlpha = config.alphaAtMaxZoom;
                }

                // 应用透明度
                Color color = config.cachedRenderer.color;
                color.a = targetAlpha;
                config.cachedRenderer.color = color;
            }
        }


        public void SetZoom(float zoomMultiplier)
        {
            targetZoom = Mathf.Clamp(zoomMultiplier, minZoomMultiplier, maxZoomMultiplier);
        }

        public void ResetCamera()
        {
            targetCamera.transform.position = new Vector3(0, 0, targetCamera.transform.position.z);
            targetZoom = 1f;
            currentZoom = 1f;
            targetCamera.orthographicSize = baseOrthographicSize;
        }

        public void SetImmediateView(Vector3 position, float orthographicSize)
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
                if (targetCamera == null) targetCamera = Camera.main;
            }

            if (targetCamera == null) return;

            targetCamera.transform.position = position;

            if (targetCamera.orthographic && orthographicSize > 0f)
            {
                targetCamera.orthographicSize = orthographicSize;
                float zoom = baseOrthographicSize > 0f
                    ? orthographicSize / baseOrthographicSize
                    : 1f;
                currentZoom = zoom;
                targetZoom = zoom;
            }
        }

        public float GetCurrentZoom()
        {
            return currentZoom;
        }

        public void SetBoundaries(Vector2 min, Vector2 max)
        {
            boundaryMin = min;
            boundaryMax = max;
        }

        private void OnDrawGizmosSelected()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
                if (targetCamera == null) return;
            }

            // 绘制边界矩形框（红色）
            Gizmos.color = Color.red;
            Vector3 bottomLeft = new Vector3(boundaryMin.x, boundaryMin.y, 0);
            Vector3 bottomRight = new Vector3(boundaryMax.x, boundaryMin.y, 0);
            Vector3 topRight = new Vector3(boundaryMax.x, boundaryMax.y, 0);
            Vector3 topLeft = new Vector3(boundaryMin.x, boundaryMax.y, 0);

            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);

            // 计算相机视野大小
            float cameraHalfHeight = targetCamera.orthographic ? targetCamera.orthographicSize : 5f;
            float cameraHalfWidth = cameraHalfHeight * targetCamera.aspect;

            // 计算相机中心点可移动范围
            float minX = boundaryMin.x + cameraHalfWidth;
            float maxX = boundaryMax.x - cameraHalfWidth;
            float minY = boundaryMin.y + cameraHalfHeight;
            float maxY = boundaryMax.y - cameraHalfHeight;

            // 防止边界翻转
            if (minX > maxX)
            {
                float centerX = (boundaryMin.x + boundaryMax.x) * 0.5f;
                minX = maxX = centerX;
            }
            if (minY > maxY)
            {
                float centerY = (boundaryMin.y + boundaryMax.y) * 0.5f;
                minY = maxY = centerY;
            }

            // 绘制相机中心点可移动范围（绿色）
            Gizmos.color = Color.green;
            Vector3 moveBottomLeft = new Vector3(minX, minY, 0);
            Vector3 moveBottomRight = new Vector3(maxX, minY, 0);
            Vector3 moveTopRight = new Vector3(maxX, maxY, 0);
            Vector3 moveTopLeft = new Vector3(minX, maxY, 0);

            Gizmos.DrawLine(moveBottomLeft, moveBottomRight);
            Gizmos.DrawLine(moveBottomRight, moveTopRight);
            Gizmos.DrawLine(moveTopRight, moveTopLeft);
            Gizmos.DrawLine(moveTopLeft, moveBottomLeft);

            // 绘制当前相机视野边界（黄色）
            if (Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Vector3 camPos = targetCamera.transform.position;
                Vector3 viewBottomLeft = new Vector3(camPos.x - cameraHalfWidth, camPos.y - cameraHalfHeight, 0);
                Vector3 viewBottomRight = new Vector3(camPos.x + cameraHalfWidth, camPos.y - cameraHalfHeight, 0);
                Vector3 viewTopRight = new Vector3(camPos.x + cameraHalfWidth, camPos.y + cameraHalfHeight, 0);
                Vector3 viewTopLeft = new Vector3(camPos.x - cameraHalfWidth, camPos.y + cameraHalfHeight, 0);

                Gizmos.DrawLine(viewBottomLeft, viewBottomRight);
                Gizmos.DrawLine(viewBottomRight, viewTopRight);
                Gizmos.DrawLine(viewTopRight, viewTopLeft);
                Gizmos.DrawLine(viewTopLeft, viewBottomLeft);
            }
        }
    }
}
