using System.Collections;
using UnityEngine;

namespace PopLife.Visuals
{
    /// <summary>
    /// Fades the upper day sprites over fixed night sprites when BGM day/night transitions start.
    /// 每个槽位支持多个 SpriteRenderer，便于复用同一组 sprite 在多个位置摆放。
    /// </summary>
    public class DayNightSpriteFader : MonoBehaviour
    {
        [Header("Background")]
        [SerializeField] private SpriteRenderer[] dayBackgroundSprites;
        [SerializeField] private SpriteRenderer[] nightBackgroundSprites;

        [Header("Parallax 0")]
        [SerializeField] private SpriteRenderer[] dayParallax0Sprites;
        [SerializeField] private SpriteRenderer[] nightParallax0Sprites;

        [Header("Parallax 1")]
        [SerializeField] private SpriteRenderer[] dayParallax1Sprites;
        [SerializeField] private SpriteRenderer[] nightParallax1Sprites;

        [Header("Parallax 2")]
        [SerializeField] private SpriteRenderer[] dayParallax2Sprites;
        [SerializeField] private SpriteRenderer[] nightParallax2Sprites;

        [Header("Parallax 3")]
        [SerializeField] private SpriteRenderer[] dayParallax3Sprites;
        [SerializeField] private SpriteRenderer[] nightParallax3Sprites;

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
            return HasAny(dayBackgroundSprites)
                || HasAny(dayParallax0Sprites)
                || HasAny(dayParallax1Sprites)
                || HasAny(dayParallax2Sprites)
                || HasAny(dayParallax3Sprites);
        }

        private float GetFirstDayAlpha()
        {
            if (TryGetFirstAlpha(dayBackgroundSprites, out float a)) return a;
            if (TryGetFirstAlpha(dayParallax0Sprites, out a)) return a;
            if (TryGetFirstAlpha(dayParallax1Sprites, out a)) return a;
            if (TryGetFirstAlpha(dayParallax2Sprites, out a)) return a;
            if (TryGetFirstAlpha(dayParallax3Sprites, out a)) return a;
            return 1f;
        }

        private void SetDayAlphaForAll(float alpha)
        {
            SetGroupAlpha(dayBackgroundSprites, alpha);
            SetGroupAlpha(dayParallax0Sprites, alpha);
            SetGroupAlpha(dayParallax1Sprites, alpha);
            SetGroupAlpha(dayParallax2Sprites, alpha);
            SetGroupAlpha(dayParallax3Sprites, alpha);
        }

        private void SetNightAlphaForAll(float alpha)
        {
            SetGroupAlpha(nightBackgroundSprites, alpha);
            SetGroupAlpha(nightParallax0Sprites, alpha);
            SetGroupAlpha(nightParallax1Sprites, alpha);
            SetGroupAlpha(nightParallax2Sprites, alpha);
            SetGroupAlpha(nightParallax3Sprites, alpha);
        }

        private static bool HasAny(SpriteRenderer[] group)
        {
            if (group == null) return false;
            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] != null) return true;
            }
            return false;
        }

        private static bool TryGetFirstAlpha(SpriteRenderer[] group, out float alpha)
        {
            if (group != null)
            {
                for (int i = 0; i < group.Length; i++)
                {
                    if (group[i] != null)
                    {
                        alpha = group[i].color.a;
                        return true;
                    }
                }
            }
            alpha = 0f;
            return false;
        }

        private static void SetGroupAlpha(SpriteRenderer[] group, float alpha)
        {
            if (group == null) return;
            float clamped = Mathf.Clamp01(alpha);
            for (int i = 0; i < group.Length; i++)
            {
                SpriteRenderer sr = group[i];
                if (sr == null) continue;
                Color color = sr.color;
                color.a = clamped;
                sr.color = color;
            }
        }
    }
}
