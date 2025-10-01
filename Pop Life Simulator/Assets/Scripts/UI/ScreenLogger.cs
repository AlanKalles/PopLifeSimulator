using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PopLife.UI
{
    /// <summary>
    /// Displays debug logs on screen for customer behavior tracking
    /// </summary>
    public class ScreenLogger : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text logText;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Settings")]
        [SerializeField] private int maxLines = 20;
        [SerializeField] private bool autoScroll = true;
        [SerializeField] private Color customerActionColor = Color.cyan;
        [SerializeField] private Color purchaseColor = Color.green;
        [SerializeField] private Color errorColor = Color.red;
        [SerializeField] private Color warningColor = Color.yellow;

        private Queue<string> logLines = new Queue<string>();
        private static ScreenLogger _instance;
        public static ScreenLogger Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Auto-create UI if not assigned
            if (logText == null)
            {
                CreateDefaultUI();
            }
        }

        /// <summary>
        /// Log customer action to screen (cyan color)
        /// </summary>
        public static void LogCustomerAction(string customerId, string message)
        {
            if (Instance != null)
            {
                Instance.AddLog($"[Customer {customerId}] {message}", Instance.customerActionColor);
            }
        }

        /// <summary>
        /// Log purchase action to screen (green color)
        /// </summary>
        public static void LogPurchase(string customerId, string message)
        {
            if (Instance != null)
            {
                Instance.AddLog($"[Purchase {customerId}] {message}", Instance.purchaseColor);
            }
        }

        /// <summary>
        /// Log error to screen (red color)
        /// </summary>
        public static void LogError(string customerId, string message)
        {
            if (Instance != null)
            {
                Instance.AddLog($"[ERROR {customerId}] {message}", Instance.errorColor);
            }
        }

        /// <summary>
        /// Log warning to screen (yellow color)
        /// </summary>
        public static void LogWarning(string customerId, string message)
        {
            if (Instance != null)
            {
                Instance.AddLog($"[WARNING {customerId}] {message}", Instance.warningColor);
            }
        }

        /// <summary>
        /// Log general info to screen (white color)
        /// </summary>
        public static void LogInfo(string message)
        {
            if (Instance != null)
            {
                Instance.AddLog(message, Color.white);
            }
        }

        /// <summary>
        /// Clear all logs
        /// </summary>
        public static void Clear()
        {
            if (Instance != null)
            {
                Instance.logLines.Clear();
                Instance.UpdateDisplay();
            }
        }

        private void AddLog(string message, Color color)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(color);
            string coloredMessage = $"<color=#{colorHex}>{message}</color>";

            logLines.Enqueue(coloredMessage);

            // Remove old lines if exceeds max
            while (logLines.Count > maxLines)
            {
                logLines.Dequeue();
            }

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (logText != null)
            {
                logText.text = string.Join("\n", logLines);

                // Auto-scroll to bottom
                if (autoScroll && scrollRect != null)
                {
                    Canvas.ForceUpdateCanvases();
                    scrollRect.verticalNormalizedPosition = 0f;
                }
            }
        }

        private void CreateDefaultUI()
        {
            Debug.Log("[ScreenLogger] Auto-creating default UI. It's recommended to manually create UI in scene.");

            // Create Canvas if not exists
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("ScreenLoggerCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            // Create panel
            GameObject panelGO = new GameObject("LogPanel");
            panelGO.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0.4f, 0.5f);
            panelRect.offsetMin = new Vector2(10, 10);
            panelRect.offsetMax = new Vector2(-10, -10);

            Image panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);

            // Create scroll view root
            GameObject scrollViewGO = new GameObject("ScrollView");
            scrollViewGO.transform.SetParent(panelGO.transform, false);

            RectTransform scrollViewRect = scrollViewGO.AddComponent<RectTransform>();
            scrollViewRect.anchorMin = Vector2.zero;
            scrollViewRect.anchorMax = Vector2.one;
            scrollViewRect.sizeDelta = Vector2.zero;

            this.scrollRect = scrollViewGO.AddComponent<ScrollRect>();
            this.scrollRect.horizontal = false;
            this.scrollRect.vertical = true;

            // Create viewport for proper clipping
            GameObject viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollViewGO.transform, false);
            RectTransform viewportRect = viewportGO.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            Image viewportImage = viewportGO.AddComponent<Image>();
            viewportImage.color = new Color(0, 0, 0, 0); // invisible but needed for masking
            viewportGO.AddComponent<RectMask2D>();
            this.scrollRect.viewport = viewportRect;

            // Create content
            GameObject contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);

            RectTransform contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.sizeDelta = new Vector2(0, 500);

            ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            this.scrollRect.content = contentRect;

            // Create TMP text
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(contentGO.transform, false);

            RectTransform textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.pivot = new Vector2(0, 1);
            textRect.sizeDelta = new Vector2(0, 0);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            // Prefer default TMP font if available
            if (TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }
            tmp.fontSize = 14;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            logText = tmp;

            Debug.Log("[ScreenLogger] Default UI created successfully");
        }
    }
}
