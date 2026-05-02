using UnityEngine;

namespace PopLife.Runtime
{
    public class ClubGlassController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Sprite unlockedSprite;

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
            if (targetRenderer == null) return;

            var sprite = locked ? lockedSprite : unlockedSprite;
            if (sprite != null)
            {
                targetRenderer.sprite = sprite;
            }

            targetRenderer.enabled = true;
        }
    }
}
