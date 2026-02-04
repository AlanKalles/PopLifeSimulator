using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// Spotlight mask panel component
    /// Creates a darkened overlay with a cutout highlighting the target area
    ///
    /// Uses a custom shader material for the mask effect
    /// If no shader material is provided, falls back to a simple UI-based approach
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SpotlightPanel : MonoBehaviour
    {
        #region Serialized Fields

        [Title("Components")]
        [SerializeField] private Image maskImage;

        [SerializeField] private RectTransform highlightBorder;

        [SerializeField] private RectTransform cutoutArea;

        [Title("Material (Optional)")]
        [InfoBox("If using shader-based cutout, assign a material with spotlight shader")]
        [SerializeField] private Material spotlightMaterial;

        [Title("Settings")]
        [SerializeField] private float padding = 15f;

        [SerializeField] private Color overlayColor = new Color(0, 0, 0, 0.7f);

        [SerializeField] private Color borderColor = Color.yellow;

        [SerializeField] private float borderWidth = 3f;

        [SerializeField] private float cornerRadius = 10f;

        [Title("Fallback UI Components")]
        [InfoBox("Used if no shader material is assigned")]
        [SerializeField] private Image topMask;
        [SerializeField] private Image bottomMask;
        [SerializeField] private Image leftMask;
        [SerializeField] private Image rightMask;

        #endregion

        #region Properties

        public RectTransform HighlightBorder => highlightBorder;
        public SpotlightShape CurrentShape { get; private set; } = SpotlightShape.RoundedRectangle;

        #endregion

        #region Private Fields

        private RectTransform rectTransform;
        private Canvas parentCanvas;
        private bool useShaderMask;

        // Shader property IDs
        private static readonly int CutoutRectID = Shader.PropertyToID("_CutoutRect");
        private static readonly int CornerRadiusID = Shader.PropertyToID("_CornerRadius");
        private static readonly int ShapeID = Shader.PropertyToID("_Shape");

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();

            // Determine if we should use shader or UI-based mask
            useShaderMask = spotlightMaterial != null;

            if (!useShaderMask)
            {
                SetupUIBasedMask();
            }
            else
            {
                SetupShaderBasedMask();
            }
        }

        #endregion

        #region Setup

        private void SetupUIBasedMask()
        {
            // Create mask images if not assigned
            if (topMask == null) topMask = CreateMaskImage("TopMask");
            if (bottomMask == null) bottomMask = CreateMaskImage("BottomMask");
            if (leftMask == null) leftMask = CreateMaskImage("LeftMask");
            if (rightMask == null) rightMask = CreateMaskImage("RightMask");

            // Hide the main mask image if using UI-based
            if (maskImage != null)
            {
                maskImage.enabled = false;
            }
        }

        private void SetupShaderBasedMask()
        {
            if (maskImage != null)
            {
                // Create instance of material to avoid modifying the original
                maskImage.material = new Material(spotlightMaterial);
                maskImage.color = overlayColor;
            }

            // Hide UI-based masks
            if (topMask != null) topMask.gameObject.SetActive(false);
            if (bottomMask != null) bottomMask.gameObject.SetActive(false);
            if (leftMask != null) leftMask.gameObject.SetActive(false);
            if (rightMask != null) rightMask.gameObject.SetActive(false);
        }

        private Image CreateMaskImage(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var rt = go.AddComponent<RectTransform>();
            // Anchor preset: middle center
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<Image>();
            img.color = overlayColor;
            img.raycastTarget = true; // Block clicks on masked area

            return img;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the spotlight target area
        /// </summary>
        /// <param name="screenRect">Target rect in screen coordinates</param>
        /// <param name="shape">Shape of the cutout</param>
        public void SetTarget(Rect screenRect, SpotlightShape shape)
        {
            CurrentShape = shape;

            // Apply padding
            Rect paddedRect = new Rect(
                screenRect.x - padding,
                screenRect.y - padding,
                screenRect.width + padding * 2,
                screenRect.height + padding * 2
            );

            if (useShaderMask)
            {
                UpdateShaderMask(paddedRect, shape);
            }
            else
            {
                UpdateUIBasedMask(paddedRect);
            }

            // Update highlight border
            UpdateHighlightBorder(paddedRect, shape);
        }

        #endregion

        #region Shader-Based Mask

        private void UpdateShaderMask(Rect screenRect, SpotlightShape shape)
        {
            if (maskImage == null || maskImage.material == null) return;

            // Convert screen rect to normalized coordinates (0-1)
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            Vector4 normalizedRect = new Vector4(
                screenRect.x / screenWidth,
                screenRect.y / screenHeight,
                screenRect.width / screenWidth,
                screenRect.height / screenHeight
            );

            // Set shader parameters
            maskImage.material.SetVector(CutoutRectID, normalizedRect);
            maskImage.material.SetFloat(CornerRadiusID, cornerRadius / screenWidth);
            maskImage.material.SetFloat(ShapeID, (float)shape);
        }

        #endregion

        #region UI-Based Mask

        private void UpdateUIBasedMask(Rect screenRect)
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            // Calculate positions for the four mask pieces
            // These create the "frame" around the cutout

            // Top mask: from screen top to cutout top
            UpdateMaskRect(topMask,
                new Rect(0, screenRect.yMax, screenWidth, screenHeight - screenRect.yMax));

            // Bottom mask: from screen bottom to cutout bottom
            UpdateMaskRect(bottomMask,
                new Rect(0, 0, screenWidth, screenRect.y));

            // Left mask: fills the left side between top and bottom masks
            UpdateMaskRect(leftMask,
                new Rect(0, screenRect.y, screenRect.x, screenRect.height));

            // Right mask: fills the right side between top and bottom masks
            UpdateMaskRect(rightMask,
                new Rect(screenRect.xMax, screenRect.y, screenWidth - screenRect.xMax, screenRect.height));
        }

        private void UpdateMaskRect(Image mask, Rect screenRect)
        {
            if (mask == null) return;

            var rt = mask.rectTransform;

            // Convert screen rect to canvas coordinates
            Vector2 canvasSize = GetCanvasSize();
            float scaleX = canvasSize.x / Screen.width;
            float scaleY = canvasSize.y / Screen.height;

            rt.anchoredPosition = new Vector2(
                screenRect.x * scaleX + screenRect.width * scaleX / 2 - canvasSize.x / 2,
                screenRect.y * scaleY + screenRect.height * scaleY / 2 - canvasSize.y / 2
            );

            rt.sizeDelta = new Vector2(
                screenRect.width * scaleX,
                screenRect.height * scaleY
            );

            mask.gameObject.SetActive(screenRect.width > 0 && screenRect.height > 0);
        }

        private Vector2 GetCanvasSize()
        {
            if (parentCanvas != null)
            {
                var canvasRT = parentCanvas.GetComponent<RectTransform>();
                return canvasRT.sizeDelta;
            }
            return new Vector2(Screen.width, Screen.height);
        }

        #endregion

        #region Highlight Border

        private void UpdateHighlightBorder(Rect screenRect, SpotlightShape shape)
        {
            if (highlightBorder == null) return;

            // Convert screen rect to canvas coordinates
            Vector2 canvasSize = GetCanvasSize();
            float scaleX = canvasSize.x / Screen.width;
            float scaleY = canvasSize.y / Screen.height;

            // Position
            highlightBorder.anchoredPosition = new Vector2(
                screenRect.x * scaleX + screenRect.width * scaleX / 2 - canvasSize.x / 2,
                screenRect.y * scaleY + screenRect.height * scaleY / 2 - canvasSize.y / 2
            );

            // Size
            highlightBorder.sizeDelta = new Vector2(
                screenRect.width * scaleX,
                screenRect.height * scaleY
            );

            // Reset scale (in case it was modified by pulse animation)
            highlightBorder.localScale = Vector3.one;
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [Button("Test Spotlight")]
        private void TestSpotlight()
        {
            // Create a test rect in the center of the screen
            Rect testRect = new Rect(
                Screen.width / 2 - 100,
                Screen.height / 2 - 50,
                200,
                100
            );
            SetTarget(testRect, SpotlightShape.RoundedRectangle);
        }
#endif

        #endregion
    }
}
