using System.Collections;
using UnityEngine;
using PrimeTween;
using Sirenix.OdinInspector;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// Spotlight/Coach Mark effect manager
    /// Highlights specific UI elements or world objects during tutorials
    ///
    /// Supports:
    /// - UI elements (RectTransform)
    /// - World objects (GameObject with Renderer)
    /// - Custom shapes (Rectangle, Circle, RoundedRectangle)
    /// - Pulse animation
    /// - Automatic position tracking
    /// </summary>
    public class SpotlightManager : MonoBehaviour
    {
        #region Singleton

        public static SpotlightManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region Serialized Fields

        [Title("References")]
        [Required]
        [SerializeField] private SpotlightPanel spotlightPanel;

        [SerializeField] private Canvas spotlightCanvas;

        [Title("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private float pulseScale = 1.08f;
        [SerializeField] private float pulseDuration = 0.8f;
        [SerializeField] private Ease pulseEase = Ease.InOutSine;

        [Title("Target Tracking")]
        [Tooltip("If true, spotlight will follow moving targets")]
        [SerializeField] private bool trackTarget = true;

        [Tooltip("How often to update tracking (seconds)")]
        [SerializeField] private float trackingInterval = 0.1f;

        [Title("Debug")]
        [SerializeField] private bool debugMode = false;

        #endregion

        #region Private Fields

        private RectTransform currentUITarget;
        private GameObject currentWorldTarget;
        private Camera mainCamera;
        private Coroutine pulseCoroutine;
        private Coroutine trackingCoroutine;
        private bool isActive = false;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            mainCamera = Camera.main;

            if (spotlightPanel != null)
            {
                spotlightPanel.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        #endregion

        #region Public API - UI Elements

        /// <summary>
        /// Show spotlight on a UI element
        /// </summary>
        public void ShowSpotlight(RectTransform target, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            if (target == null)
            {
                Debug.LogWarning("[SpotlightManager] ShowSpotlight called with null target");
                return;
            }

            currentUITarget = target;
            currentWorldTarget = null;

            ShowSpotlightInternal(shape);

            if (trackTarget)
            {
                StartTracking();
            }

            if (debugMode)
            {
                Debug.Log($"[SpotlightManager] Showing spotlight on UI: {target.name}");
            }
        }

        /// <summary>
        /// Show spotlight on a UI element by name
        /// Searches entire hierarchy
        /// </summary>
        public void ShowSpotlightByName(string targetName, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                Debug.LogWarning("[SpotlightManager] ShowSpotlightByName called with empty name");
                return;
            }

            var target = GameObject.Find(targetName);
            if (target != null)
            {
                var rectTransform = target.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    ShowSpotlight(rectTransform, shape);
                    return;
                }
            }

            Debug.LogWarning($"[SpotlightManager] UI target not found: {targetName}");
        }

        #endregion

        #region Public API - World Objects

        /// <summary>
        /// Show spotlight on a world object (3D/2D)
        /// </summary>
        public void ShowSpotlightOnWorldObject(GameObject target, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            if (target == null)
            {
                Debug.LogWarning("[SpotlightManager] ShowSpotlightOnWorldObject called with null target");
                return;
            }

            currentUITarget = null;
            currentWorldTarget = target;

            ShowSpotlightInternal(shape);

            if (trackTarget)
            {
                StartTracking();
            }

            if (debugMode)
            {
                Debug.Log($"[SpotlightManager] Showing spotlight on world object: {target.name}");
            }
        }

        /// <summary>
        /// Show spotlight on a world object by name
        /// </summary>
        public void ShowSpotlightOnWorldObjectByName(string targetName, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                Debug.LogWarning("[SpotlightManager] ShowSpotlightOnWorldObjectByName called with empty name");
                return;
            }

            var target = GameObject.Find(targetName);
            if (target != null)
            {
                ShowSpotlightOnWorldObject(target, shape);
                return;
            }

            Debug.LogWarning($"[SpotlightManager] World object not found: {targetName}");
        }

        #endregion

        #region Public API - Direct Rect

        /// <summary>
        /// Show spotlight on a specific screen rect
        /// </summary>
        /// <param name="screenRect">Target rect in screen coordinates (pixels)</param>
        /// <param name="shape">Shape of the spotlight cutout</param>
        public void ShowSpotlightRect(Rect screenRect, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            currentUITarget = null;
            currentWorldTarget = null;

            if (spotlightPanel == null)
            {
                Debug.LogError("[SpotlightManager] SpotlightPanel is not assigned!");
                return;
            }

            isActive = true;
            spotlightPanel.gameObject.SetActive(true);

            // Set target directly
            spotlightPanel.SetTarget(screenRect, shape);

            // Fade in
            var cg = spotlightPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                Tween.Alpha(cg, 1f, fadeInDuration, useUnscaledTime: true);
            }

            // Start pulse animation
            StartPulse();

            if (debugMode)
            {
                Debug.Log($"[SpotlightManager] Showing spotlight on rect: {screenRect}");
            }
        }

        /// <summary>
        /// Show spotlight on a specific screen rect (convenience overload)
        /// </summary>
        public void ShowSpotlightRect(float x, float y, float width, float height, SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            ShowSpotlightRect(new Rect(x, y, width, height), shape);
        }

        #endregion

        #region Public API - General

        /// <summary>
        /// Hide the spotlight
        /// </summary>
        [Button("Hide Spotlight")]
        public void HideSpotlight()
        {
            if (!isActive) return;

            StopTracking();
            StopPulse();

            isActive = false;
            currentUITarget = null;
            currentWorldTarget = null;

            // Fade out
            var cg = spotlightPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                Tween.Alpha(cg, 0f, fadeOutDuration, useUnscaledTime: true)
                    .OnComplete(() => {
                        spotlightPanel.gameObject.SetActive(false);
                    });
            }
            else
            {
                spotlightPanel.gameObject.SetActive(false);
            }

            if (debugMode)
            {
                Debug.Log("[SpotlightManager] Spotlight hidden");
            }
        }

        /// <summary>
        /// Check if spotlight is currently active
        /// </summary>
        public bool IsActive => isActive;

        #endregion

        #region Internal Methods

        private void ShowSpotlightInternal(SpotlightShape shape)
        {
            if (spotlightPanel == null)
            {
                Debug.LogError("[SpotlightManager] SpotlightPanel is not assigned!");
                return;
            }

            isActive = true;
            spotlightPanel.gameObject.SetActive(true);

            // Update spotlight position
            UpdateSpotlightPosition(shape);

            // Fade in
            var cg = spotlightPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                Tween.Alpha(cg, 1f, fadeInDuration, useUnscaledTime: true);
            }

            // Start pulse animation
            StartPulse();
        }

        private void UpdateSpotlightPosition(SpotlightShape shape)
        {
            if (spotlightPanel == null) return;

            Rect screenRect;

            if (currentUITarget != null)
            {
                screenRect = GetUIElementScreenRect(currentUITarget);
            }
            else if (currentWorldTarget != null)
            {
                screenRect = GetWorldObjectScreenRect(currentWorldTarget);
            }
            else
            {
                return;
            }

            spotlightPanel.SetTarget(screenRect, shape);
        }

        /// <summary>
        /// Get screen rect for UI element
        /// </summary>
        private Rect GetUIElementScreenRect(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            // Convert to screen coordinates
            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            Camera cam = canvas?.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas?.worldCamera ?? mainCamera;

            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        /// <summary>
        /// Get screen rect for world object
        /// </summary>
        private Rect GetWorldObjectScreenRect(GameObject target)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            // Try to get bounds from renderer
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;

                // Get all 8 corners of the bounding box
                Vector3[] worldCorners = new Vector3[8];
                worldCorners[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
                worldCorners[1] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
                worldCorners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
                worldCorners[3] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
                worldCorners[4] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
                worldCorners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
                worldCorners[6] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
                worldCorners[7] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);

                // Convert to screen space and find min/max
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);

                foreach (var corner in worldCorners)
                {
                    Vector2 screenPoint = mainCamera.WorldToScreenPoint(corner);
                    min = Vector2.Min(min, screenPoint);
                    max = Vector2.Max(max, screenPoint);
                }

                return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            }

            // Fallback: use transform position
            Vector2 center = mainCamera.WorldToScreenPoint(target.transform.position);
            float size = 100f; // Default size
            return new Rect(center.x - size / 2, center.y - size / 2, size, size);
        }

        #endregion

        #region Animation

        private void StartPulse()
        {
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
            }
            pulseCoroutine = StartCoroutine(PulseAnimation());
        }

        private void StopPulse()
        {
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }
        }

        private IEnumerator PulseAnimation()
        {
            if (spotlightPanel.HighlightBorder == null) yield break;

            var border = spotlightPanel.HighlightBorder;

            while (isActive)
            {
                // Scale up
                yield return Tween.Scale(border, Vector3.one * pulseScale, pulseDuration / 2, pulseEase, useUnscaledTime: true)
                    .ToYieldInstruction();

                // Scale down
                yield return Tween.Scale(border, Vector3.one, pulseDuration / 2, pulseEase, useUnscaledTime: true)
                    .ToYieldInstruction();
            }
        }

        #endregion

        #region Target Tracking

        private void StartTracking()
        {
            if (trackingCoroutine != null)
            {
                StopCoroutine(trackingCoroutine);
            }
            trackingCoroutine = StartCoroutine(TrackTargetCoroutine());
        }

        private void StopTracking()
        {
            if (trackingCoroutine != null)
            {
                StopCoroutine(trackingCoroutine);
                trackingCoroutine = null;
            }
        }

        private IEnumerator TrackTargetCoroutine()
        {
            var wait = new WaitForSecondsRealtime(trackingInterval);

            while (isActive && (currentUITarget != null || currentWorldTarget != null))
            {
                UpdateSpotlightPosition(spotlightPanel.CurrentShape);
                yield return wait;
            }
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [Button("Test UI Spotlight")]
        private void TestUISpotlight()
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                var firstButton = canvas.GetComponentInChildren<UnityEngine.UI.Button>();
                if (firstButton != null)
                {
                    ShowSpotlight(firstButton.GetComponent<RectTransform>());
                }
            }
        }
#endif

        #endregion
    }

    /// <summary>
    /// Spotlight shape type
    /// </summary>
    public enum SpotlightShape
    {
        Rectangle,
        Circle,
        RoundedRectangle
    }
}
