using System.Collections;
using UnityEngine;

namespace PopLife.Visuals
{
    /// <summary>
    /// Fades the upper day sprite over a fixed night sprite when BGM day/night transitions start.
    /// </summary>
    public class DayNightSpriteFader : MonoBehaviour
    {
        [Header("Background")]
        [SerializeField] private SpriteRenderer dayBackgroundSprite;
        [SerializeField] private SpriteRenderer nightBackgroundSprite;

        [Header("Parallax 0")]
        [SerializeField] private SpriteRenderer dayParallax0Sprite;
        [SerializeField] private SpriteRenderer nightParallax0Sprite;

        [Header("Parallax 1")]
        [SerializeField] private SpriteRenderer dayParallax1Sprite;
        [SerializeField] private SpriteRenderer nightParallax1Sprite;

        [Header("Parallax 2")]
        [SerializeField] private SpriteRenderer dayParallax2Sprite;
        [SerializeField] private SpriteRenderer nightParallax2Sprite;

        [Header("Parallax 3")]
        [SerializeField] private SpriteRenderer dayParallax3Sprite;
        [SerializeField] private SpriteRenderer nightParallax3Sprite;

        [Header("Fade")]
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private Coroutine fadeCoroutine;
        private bool subscribed;

        private void OnEnable()
        {
            ApplyInitialState();
            TrySubscribe();
        }

        private void Start()
        {
            ApplyInitialState();
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            Unsubscribe();
        }

        private void TrySubscribe()
        {
            if (subscribed || BGMController.Instance == null)
                return;

            BGMController.Instance.OnDayBGMTransitionStarted += FadeToDay;
            BGMController.Instance.OnNightBGMTransitionStarted += FadeToNight;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || BGMController.Instance == null)
                return;

            BGMController.Instance.OnDayBGMTransitionStarted -= FadeToDay;
            BGMController.Instance.OnNightBGMTransitionStarted -= FadeToNight;
            subscribed = false;
        }

        private void ApplyInitialState()
        {
            SetNightAlphaForAll(1f);

            float dayAlpha = 1f;
            if (DayLoopManager.Instance != null)
            {
                dayAlpha = DayLoopManager.Instance.currentHour >= DayLoopManager.Instance.NightBGMStartHour
                    ? 0f
                    : 1f;
            }

            SetDayAlphaForAll(dayAlpha);
        }

        private void FadeToDay(float duration)
        {
            StartFade(1f, duration);
        }

        private void FadeToNight(float duration)
        {
            StartFade(0f, duration);
        }

        private void StartFade(float targetAlpha, float duration)
        {
            SetNightAlphaForAll(1f);

            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            if (duration <= 0f || !HasAnyDaySprite())
            {
                SetDayAlphaForAll(targetAlpha);
                return;
            }

            fadeCoroutine = StartCoroutine(FadeDayAlpha(targetAlpha, duration));
        }

        private IEnumerator FadeDayAlpha(float targetAlpha, float duration)
        {
            float startAlpha = GetFirstDayAlpha();
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float curvedTime = fadeCurve != null ? fadeCurve.Evaluate(normalizedTime) : normalizedTime;
                SetDayAlphaForAll(Mathf.Lerp(startAlpha, targetAlpha, curvedTime));
                yield return null;
            }

            SetDayAlphaForAll(targetAlpha);
            fadeCoroutine = null;
        }

        private bool HasAnyDaySprite()
        {
            return dayBackgroundSprite != null
                || dayParallax0Sprite != null
                || dayParallax1Sprite != null
                || dayParallax2Sprite != null
                || dayParallax3Sprite != null;
        }

        private float GetFirstDayAlpha()
        {
            if (dayBackgroundSprite != null) return dayBackgroundSprite.color.a;
            if (dayParallax0Sprite != null) return dayParallax0Sprite.color.a;
            if (dayParallax1Sprite != null) return dayParallax1Sprite.color.a;
            if (dayParallax2Sprite != null) return dayParallax2Sprite.color.a;
            if (dayParallax3Sprite != null) return dayParallax3Sprite.color.a;
            return 1f;
        }

        private void SetDayAlphaForAll(float alpha)
        {
            SetSpriteAlpha(dayBackgroundSprite, alpha);
            SetSpriteAlpha(dayParallax0Sprite, alpha);
            SetSpriteAlpha(dayParallax1Sprite, alpha);
            SetSpriteAlpha(dayParallax2Sprite, alpha);
            SetSpriteAlpha(dayParallax3Sprite, alpha);
        }

        private void SetNightAlphaForAll(float alpha)
        {
            SetSpriteAlpha(nightBackgroundSprite, alpha);
            SetSpriteAlpha(nightParallax0Sprite, alpha);
            SetSpriteAlpha(nightParallax1Sprite, alpha);
            SetSpriteAlpha(nightParallax2Sprite, alpha);
            SetSpriteAlpha(nightParallax3Sprite, alpha);
        }

        private static void SetSpriteAlpha(SpriteRenderer spriteRenderer, float alpha)
        {
            if (spriteRenderer == null)
                return;

            Color color = spriteRenderer.color;
            color.a = Mathf.Clamp01(alpha);
            spriteRenderer.color = color;
        }
    }
}
