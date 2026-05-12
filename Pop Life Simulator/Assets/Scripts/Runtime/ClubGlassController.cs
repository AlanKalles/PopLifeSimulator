using UnityEngine;

namespace PopLife.Runtime
{
    public class ClubGlassController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;

        [Header("Unowned Overlay (Day / Night)")]
        [Tooltip("未购买 + 建造阶段（白天）时显示的玻璃 sprite。")]
        [SerializeField] private Sprite lockedDaySprite;
        [Tooltip("未购买 + 营业阶段（夜晚）时显示的玻璃 sprite。")]
        [SerializeField] private Sprite lockedNightSprite;

        [Header("Owned Glass")]
        [Tooltip("已购买时显示的玻璃 sprite（无日夜区分）。")]
        [SerializeField] private Sprite unlockedSprite;

        // 当前状态（locked 与 isNight 正交）
        private bool isLocked;
        private bool isNight;

        private void Reset()
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        public void SetVisible(bool visible)
        {
            if (targetRenderer == null) return;
            targetRenderer.enabled = visible;
        }

        public void SetLocked(bool locked)
        {
            isLocked = locked;
            ApplySprite();
            if (targetRenderer != null) targetRenderer.enabled = true;
        }

        /// <summary>
        /// 切换白天/夜晚 sprite。仅在 locked（玩家未拥有）时影响显示。
        /// BuildPhase 视为白天，OpenPhase 视为夜晚。
        /// </summary>
        public void SetTimeOfDay(bool night)
        {
            isNight = night;
            ApplySprite();
        }

        private void ApplySprite()
        {
            if (targetRenderer == null) return;

            Sprite chosen = isLocked
                ? (isNight ? lockedNightSprite : lockedDaySprite)
                : unlockedSprite;

            if (chosen != null)
            {
                targetRenderer.sprite = chosen;
            }
        }
    }
}
