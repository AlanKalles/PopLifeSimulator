using UnityEngine;
using UnityEngine.EventSystems;

namespace PopLife.Runtime
{
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
        [SerializeField] private bool useDynamicBoundary = true;

        private Camera targetCamera;
        private float currentZoom = 1f;
        private float targetZoom = 1f;

        private bool isDragging = false;
        private Vector3 dragOrigin;
        private Vector3 lastMousePosition;

        private Vector2 originalCameraBounds;

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

                CalculateOriginalBounds();
            }
            else
            {
                Debug.LogError("CameraController需要一个正交相机!");
            }
        }

        private void Update()
        {
            if (targetCamera == null || !targetCamera.orthographic) return;

            HandleZoomInput();
            HandleDragInput();
            ApplyZoom();
            ApplyBoundaries();
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
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() && !isDragging)
                return;

            if (Input.GetMouseButtonDown(0) && !isDragging)
            {
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    isDragging = true;
                    dragOrigin = targetCamera.ScreenToWorldPoint(Input.mousePosition);
                    lastMousePosition = Input.mousePosition;
                }
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

            Vector2 effectiveBoundaryMin = boundaryMin;
            Vector2 effectiveBoundaryMax = boundaryMax;

            if (useDynamicBoundary)
            {
                float halfHeight = targetCamera.orthographicSize;
                float halfWidth = halfHeight * targetCamera.aspect;

                float maxHalfHeight = baseOrthographicSize * maxZoomMultiplier;
                float maxHalfWidth = maxHalfHeight * targetCamera.aspect;

                effectiveBoundaryMin = new Vector2(
                    originalCameraBounds.x - (maxHalfWidth - halfWidth),
                    originalCameraBounds.y - (maxHalfHeight - halfHeight)
                );

                effectiveBoundaryMax = new Vector2(
                    -originalCameraBounds.x + (maxHalfWidth - halfWidth),
                    -originalCameraBounds.y + (maxHalfHeight - halfHeight)
                );

                effectiveBoundaryMin = Vector2.Max(effectiveBoundaryMin, -originalCameraBounds);
                effectiveBoundaryMax = Vector2.Min(effectiveBoundaryMax, originalCameraBounds);
            }

            pos.x = Mathf.Clamp(pos.x, effectiveBoundaryMin.x, effectiveBoundaryMax.x);
            pos.y = Mathf.Clamp(pos.y, effectiveBoundaryMin.y, effectiveBoundaryMax.y);

            targetCamera.transform.position = pos;
        }

        private void CalculateOriginalBounds()
        {
            float maxHalfHeight = baseOrthographicSize * maxZoomMultiplier;
            float maxHalfWidth = maxHalfHeight * targetCamera.aspect;

            originalCameraBounds = new Vector2(maxHalfWidth, maxHalfHeight);

            if (!useDynamicBoundary)
            {
                boundaryMin = -originalCameraBounds;
                boundaryMax = originalCameraBounds;
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

        public float GetCurrentZoom()
        {
            return currentZoom;
        }

        public void SetBoundaries(Vector2 min, Vector2 max)
        {
            boundaryMin = min;
            boundaryMax = max;
            useDynamicBoundary = false;
        }

        public void EnableDynamicBoundaries()
        {
            useDynamicBoundary = true;
            CalculateOriginalBounds();
        }
    }
}