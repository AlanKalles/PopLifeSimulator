using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrimeTween;

namespace PopLife.UI
{
    /// <summary>
    /// 单个数字位的老虎机滚轴组件
    /// 运行时在原 TMP 位置构建 viewport + RectMask2D + 数字条，
    /// 用 PrimeTween 实现过冲回弹的滚动效果
    /// </summary>
    public class LotteryDigitReel : MonoBehaviour
    {
        [Header("Scroll Settings")]
        [SerializeField] private float scrollDuration = 0.18f;
        [SerializeField] private Ease scrollEase = Ease.OutBack;

        // 运行时构建的 UI
        private RectTransform viewport;
        private RectTransform strip;
        private TextMeshProUGUI[] digitLabels; // 12个: [wrap9, 0, 1, ..., 9, wrap0]

        // 状态
        private int currentDigit;
        private int maxDigitValue = 9;
        private float cellHeight;
        private Tween scrollTween;
        private bool initialized;

        public int CurrentDigit => currentDigit;
        public bool IsAnimating => scrollTween.isAlive;

        /// <summary>
        /// 从原始 TMP 构建滚轴 UI
        /// </summary>
        public void Initialize(TextMeshProUGUI originalText, int maxDigit)
        {
            if (initialized) return;
            maxDigitValue = maxDigit;
            int totalDigits = maxDigitValue + 1; // 0 到 maxDigitValue
            int totalCells = totalDigits + 2;    // 首尾各一个环绕副本

            // 从原始 TMP 读取样式
            var font = originalText.font;
            float fontSize = originalText.fontSize;
            Color fontColor = originalText.color;
            var fontStyle = originalText.fontStyle;
            var alignment = originalText.alignment;
            float charSpacing = originalText.characterSpacing;

            // 获取原始 TMP 在父级中的位置和尺寸
            var originalRect = originalText.rectTransform;
            Transform parentTransform = originalRect.parent;
            int siblingIndex = originalRect.GetSiblingIndex();
            float viewWidth = originalRect.rect.width > 0 ? originalRect.rect.width : 101.45f;
            float viewHeight = originalRect.rect.height > 0 ? originalRect.rect.height : 50f;
            cellHeight = viewHeight;

            // 隐藏原始 TMP 并让 VerticalLayoutGroup 忽略它
            originalText.enabled = false;
            var originalLayout = originalText.GetComponent<LayoutElement>();
            if (originalLayout == null)
                originalLayout = originalText.gameObject.AddComponent<LayoutElement>();
            originalLayout.ignoreLayout = true;

            // === 构建 Viewport ===
            var viewportGO = new GameObject("ReelViewport");
            viewportGO.layer = 5; // UI layer
            viewportGO.transform.SetParent(parentTransform, false);
            viewportGO.transform.SetSiblingIndex(siblingIndex);

            viewport = viewportGO.AddComponent<RectTransform>();
            viewport.anchorMin = originalRect.anchorMin;
            viewport.anchorMax = originalRect.anchorMax;
            viewport.pivot = originalRect.pivot;
            viewport.anchoredPosition = originalRect.anchoredPosition;
            viewport.sizeDelta = new Vector2(viewWidth, viewHeight);

            // 添加 RectMask2D 实现裁剪
            viewportGO.AddComponent<RectMask2D>();

            // 添加 LayoutElement 保持 VerticalLayoutGroup 布局一致
            var layoutElement = viewportGO.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = viewWidth;
            layoutElement.preferredHeight = viewHeight;

            // === 构建 Strip（数字条）===
            var stripGO = new GameObject("ReelStrip");
            stripGO.layer = 5;
            stripGO.transform.SetParent(viewport, false);

            strip = stripGO.AddComponent<RectTransform>();
            strip.anchorMin = new Vector2(0.5f, 1f); // 顶部居中
            strip.anchorMax = new Vector2(0.5f, 1f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.sizeDelta = new Vector2(viewWidth, cellHeight * totalCells);
            strip.anchoredPosition = Vector2.zero;

            // === 创建 12 个数字标签 ===
            // 排列: [wrap(maxDigit), 0, 1, 2, ..., maxDigit, wrap(0)]
            digitLabels = new TextMeshProUGUI[totalCells];
            for (int i = 0; i < totalCells; i++)
            {
                int digitValue;
                if (i == 0)
                    digitValue = maxDigitValue; // 顶部环绕副本
                else if (i == totalCells - 1)
                    digitValue = 0;             // 底部环绕副本
                else
                    digitValue = i - 1;         // 正常数字 0-9

                var labelGO = new GameObject($"Digit_{digitValue}_{i}");
                labelGO.layer = 5;
                labelGO.transform.SetParent(strip, false);

                var labelRect = labelGO.AddComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 1f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.pivot = new Vector2(0.5f, 1f);
                labelRect.anchoredPosition = new Vector2(0f, -i * cellHeight);
                labelRect.sizeDelta = new Vector2(0f, cellHeight);

                labelGO.AddComponent<CanvasRenderer>();
                var tmp = labelGO.AddComponent<TextMeshProUGUI>();
                tmp.font = font;
                tmp.fontSize = fontSize;
                tmp.color = fontColor;
                tmp.fontStyle = fontStyle;
                tmp.alignment = alignment;
                tmp.characterSpacing = charSpacing;
                tmp.text = digitValue.ToString();
                tmp.raycastTarget = false;

                digitLabels[i] = tmp;
            }

            // 初始显示数字 0
            currentDigit = 0;
            SnapToDigit(0);

            initialized = true;
        }

        /// <summary>
        /// 滚动到指定数字
        /// </summary>
        public void ScrollToDigit(int digitValue, bool animate = true)
        {
            if (!initialized) return;

            digitValue = Mathf.Clamp(digitValue, 0, maxDigitValue);

            if (!animate)
            {
                currentDigit = digitValue;
                SnapToDigit(digitValue);
                return;
            }

            // 停止正在进行的动画
            if (scrollTween.isAlive)
                scrollTween.Stop();

            int totalDigits = maxDigitValue + 1;
            float currentY = strip.anchoredPosition.y;
            float canonicalTargetY = (digitValue + 1) * cellHeight;

            // 判断滚动方向和环绕
            float targetY = canonicalTargetY;
            int previousDigit = currentDigit;

            // 从 maxDigit 向上滚到 0（环绕）
            if (previousDigit == maxDigitValue && digitValue == 0)
            {
                // 滚到底部环绕副本（index = totalDigits + 1）
                targetY = (totalDigits + 1) * cellHeight;
            }
            // 从 0 向下滚到 maxDigit（环绕）
            else if (previousDigit == 0 && digitValue == maxDigitValue)
            {
                // 滚到顶部环绕副本（index = 0）
                targetY = 0f;
            }

            currentDigit = digitValue;

            float finalCanonicalY = canonicalTargetY;
            scrollTween = Tween.Custom(this, currentY, targetY, scrollDuration,
                (target, val) =>
                {
                    if (target.strip != null)
                        target.strip.anchoredPosition = new Vector2(0f, val);
                },
                scrollEase)
                .OnComplete(this, target =>
                {
                    // 环绕后瞬移到 canonical 位置
                    if (target.strip != null)
                        target.strip.anchoredPosition = new Vector2(0f, finalCanonicalY);
                });
        }

        /// <summary>
        /// 直接 snap 到指定数字（无动画）
        /// </summary>
        private void SnapToDigit(int digitValue)
        {
            if (strip == null) return;
            float y = (digitValue + 1) * cellHeight;
            strip.anchoredPosition = new Vector2(0f, y);
        }

        private void OnDestroy()
        {
            if (scrollTween.isAlive)
                scrollTween.Stop();
        }
    }
}
