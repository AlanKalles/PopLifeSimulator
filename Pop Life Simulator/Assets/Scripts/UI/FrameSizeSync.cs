using UnityEngine;
using UnityEngine.UI;

namespace PopLife.UI
{
    /// <summary>
    /// 同步 frame 的尺寸到 screenshot Image 的实际显示尺寸（考虑 Preserve Aspect）。
    /// 挂在 frame 上，拖入 screenshot 引用。
    /// </summary>
    [ExecuteAlways]
    public class FrameSizeSync : MonoBehaviour
    {
        [SerializeField] Image screenshotImage;
        [SerializeField] Vector2 borderPadding = new Vector2(16, 16); // frame边框额外尺寸

        RectTransform rectTransform;
        Sprite lastSprite;
        Vector2 lastParentSize;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        void LateUpdate()
        {
            if (screenshotImage == null || screenshotImage.sprite == null) return;

            var screenshotRect = screenshotImage.rectTransform;
            var currentSprite = screenshotImage.sprite;
            var parentSize = screenshotRect.rect.size;

            // 只在sprite或容器尺寸变化时重算
            if (currentSprite == lastSprite && parentSize == lastParentSize) return;
            lastSprite = currentSprite;
            lastParentSize = parentSize;

            var displayedSize = CalculatePreserveAspectSize(currentSprite, parentSize);
            rectTransform.sizeDelta = displayedSize + borderPadding;
        }

        /// <summary>
        /// 计算 Preserve Aspect 模式下图片的实际显示尺寸
        /// </summary>
        static Vector2 CalculatePreserveAspectSize(Sprite sprite, Vector2 containerSize)
        {
            float spriteW = sprite.rect.width;
            float spriteH = sprite.rect.height;
            float spriteAspect = spriteW / spriteH;
            float containerAspect = containerSize.x / containerSize.y;

            if (spriteAspect > containerAspect)
            {
                // 图片更宽 → 宽度撑满，高度缩小（上下留黑边）
                float w = containerSize.x;
                float h = w / spriteAspect;
                return new Vector2(w, h);
            }
            else
            {
                // 图片更高 → 高度撑满，宽度缩小（左右留黑边）
                float h = containerSize.y;
                float w = h * spriteAspect;
                return new Vector2(w, h);
            }
        }
    }
}
