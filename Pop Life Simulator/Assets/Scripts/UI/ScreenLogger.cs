using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

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
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Button clearButton;

        [Header("Settings")]
        [SerializeField] private int maxLines = 20;
        [SerializeField] private bool autoScroll = true;
        [SerializeField] private Color customerActionColor = Color.cyan;
        [SerializeField] private Color purchaseColor = Color.green;
        [SerializeField] private Color errorColor = Color.red;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;
        [SerializeField] private bool startCollapsed = false;

        private Queue<string> logLines = new Queue<string>();
        private static ScreenLogger _instance;
        public static ScreenLogger Instance => _instance;

        private bool _collapsed = false;
        private RectTransform _contentRoot; // ScrollView root for collapse

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

            // Wire clear button if present
            if (clearButton != null)
            {
                clearButton.onClick.AddListener(Clear);
            }

            if (startCollapsed)
            {
                SetCollapsed(true);
            }
        }

        private void OnDestroy()
        {
            if (clearButton != null)
            {
                clearButton.onClick.RemoveListener(Clear);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleVisible();
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

        public void ToggleVisible()
        {
            var target = panelRoot != null ? (Component)panelRoot : this;
            var go = target.gameObject;
            go.SetActive(!go.activeSelf);
        }

        public void ToggleCollapsed()
        {
            SetCollapsed(!_collapsed);
        }

        private void SetCollapsed(bool collapsed)
        {
            _collapsed = collapsed;
            if (_contentRoot != null)
            {
                _contentRoot.gameObject.SetActive(!collapsed);
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

            // Ensure EventSystem exists for clicks/drag
            var es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
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

            panelRoot = panelRect;

            // Header (drag handle + controls)
            const float headerH = 28f;
            GameObject headerGO = new GameObject("Header");
            headerGO.transform.SetParent(panelGO.transform, false);
            var headerRect = headerGO.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(0, headerH);
            var headerImg = headerGO.AddComponent<Image>();
            headerImg.color = new Color(0, 0, 0, 0.85f);

            // Title text
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(headerGO.transform, false);
            var titleRect = titleGO.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0);
            titleRect.anchorMax = new Vector2(0, 1);
            titleRect.pivot = new Vector2(0, 0.5f);
            titleRect.sizeDelta = new Vector2(120, 0);
            var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "Screen Log";
            titleTMP.fontSize = 16;
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            titleTMP.margin = new Vector4(8, 0, 0, 0);

            // Collapse button
            var collapseGO = CreateButton(headerGO.transform, "CollapseBtn", "▼", out var collapseBtn);
            var collapseRect = collapseGO.GetComponent<RectTransform>();
            collapseRect.anchorMin = new Vector2(1, 0.5f);
            collapseRect.anchorMax = new Vector2(1, 0.5f);
            collapseRect.pivot = new Vector2(1, 0.5f);
            collapseRect.anchoredPosition = new Vector2(-72, 0);
            collapseRect.sizeDelta = new Vector2(24, 22);
            collapseBtn.onClick.AddListener(() =>
            {
                ToggleCollapsed();
                var label = collapseGO.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = _collapsed ? "►" : "▼";
            });

            // Clear button
            var clearGO = CreateButton(headerGO.transform, "ClearBtn", "Clear", out var clrBtn);
            var clearRect = clearGO.GetComponent<RectTransform>();
            clearRect.anchorMin = new Vector2(1, 0.5f);
            clearRect.anchorMax = new Vector2(1, 0.5f);
            clearRect.pivot = new Vector2(1, 0.5f);
            clearRect.anchoredPosition = new Vector2(-8, 0);
            clearRect.sizeDelta = new Vector2(60, 22);
            clrBtn.onClick.AddListener(Clear);
            clearButton = clrBtn;

            // Create scroll view root
            GameObject scrollViewGO = new GameObject("ScrollView");
            scrollViewGO.transform.SetParent(panelGO.transform, false);

            RectTransform scrollViewRect = scrollViewGO.AddComponent<RectTransform>();
            scrollViewRect.anchorMin = new Vector2(0, 0);
            scrollViewRect.anchorMax = new Vector2(1, 1);
            scrollViewRect.offsetMin = new Vector2(8, 8);
            scrollViewRect.offsetMax = new Vector2(-8, -headerH);

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
            _contentRoot = scrollViewRect;

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
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            logText = tmp;

            // Make header draggable
            var drag = headerGO.AddComponent<UIDragHandle>();
            drag.target = panelRect;

            Debug.Log("[ScreenLogger] Default UI created successfully");
        }

        private static GameObject CreateButton(Transform parent, string name, string label, out Button button)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.06f);
            button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1, 1, 1, 0.15f);
            colors.pressedColor = new Color(1, 1, 1, 0.25f);
            button.colors = colors;

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);
            var txtRect = txtGO.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.fontSize = 14;

            return go;
        }

        // Simple drag handle for panel movement
        private class UIDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
        {
            public RectTransform target;
            private Vector2 _offset;

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (target == null) return;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint);
                _offset = (Vector2)target.localPosition - localPoint;
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (target == null) return;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint);
                target.localPosition = localPoint + _offset;
            }
        }
    }
}
