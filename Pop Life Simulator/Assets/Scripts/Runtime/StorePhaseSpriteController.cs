using System.Collections;
using UnityEngine;

namespace PopLife.Runtime
{
    /// <summary>
    /// 根据营业阶段切换 SpriteRenderer 的两张静态 sprite（开店/闭店）。
    /// 与 StorePhaseAnimatorController 对齐：不监听 OnStoreClose，
    /// 关店清场和结算面板期间保持 open sprite，直到次日 BuildPhase 才切回 closed。
    /// </summary>
    public class StorePhaseSpriteController : MonoBehaviour
    {
        [Header("Renderer")]
        [SerializeField] private SpriteRenderer targetRenderer;

        [Header("Sprites")]
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite openSprite;

        private bool isSubscribed;
        private Coroutine bindCoroutine;
        private DayLoopManager subscribedDayLoop;

        private void Reset()
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private void OnEnable()
        {
            TrySubscribe();
            SyncWithCurrentPhase();

            if (!isSubscribed && bindCoroutine == null)
            {
                bindCoroutine = StartCoroutine(BindWhenDayLoopReady());
            }
        }

        private void Start()
        {
            TrySubscribe();
            SyncWithCurrentPhase();
        }

        private void OnDisable()
        {
            if (bindCoroutine != null)
            {
                StopCoroutine(bindCoroutine);
                bindCoroutine = null;
            }

            Unsubscribe();
        }

        /// <summary>
        /// 供运行时生成后手动调用，立刻按当前游戏阶段同步 sprite。
        /// </summary>
        public void SyncWithCurrentPhase()
        {
            var dayLoop = DayLoopManager.Instance;
            if (dayLoop == null)
            {
                ApplyClosed();
                return;
            }

            if (dayLoop.currentPhase == GamePhase.OpenPhase)
            {
                ApplyOpen();
            }
            else
            {
                ApplyClosed();
            }
        }

        public void ApplyOpen()
        {
            if (targetRenderer == null || openSprite == null) return;
            targetRenderer.sprite = openSprite;
        }

        public void ApplyClosed()
        {
            if (targetRenderer == null || closedSprite == null) return;
            targetRenderer.sprite = closedSprite;
        }

        private IEnumerator BindWhenDayLoopReady()
        {
            while (enabled && !isSubscribed)
            {
                TrySubscribe();
                SyncWithCurrentPhase();

                if (isSubscribed)
                {
                    bindCoroutine = null;
                    yield break;
                }

                yield return null;
            }

            bindCoroutine = null;
        }

        private void TrySubscribe()
        {
            if (isSubscribed || DayLoopManager.Instance == null)
            {
                return;
            }

            subscribedDayLoop = DayLoopManager.Instance;
            subscribedDayLoop.OnStoreOpen += ApplyOpen;
            subscribedDayLoop.OnBuildPhaseStart += ApplyClosed;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || subscribedDayLoop == null)
            {
                isSubscribed = false;
                subscribedDayLoop = null;
                return;
            }

            subscribedDayLoop.OnStoreOpen -= ApplyOpen;
            subscribedDayLoop.OnBuildPhaseStart -= ApplyClosed;
            isSubscribed = false;
            subscribedDayLoop = null;
        }
    }
}
