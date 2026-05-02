using System.Collections;
using UnityEngine;

namespace PopLife.Runtime
{
    /// <summary>
    /// 控制环境对象在营业阶段重复播放单次动画，并在结算 Continue 后回到静止状态。
    /// 不监听 OnStoreClose，确保关店清场与结算面板期间动画继续播放。
    /// </summary>
    public class StorePhaseAnimatorController : MonoBehaviour
    {
        [Header("Animator")]
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string openLoopStateName = "OpenLoop";
        [SerializeField] private int layerIndex = 0;
        [SerializeField] private bool disableAnimatorWhenIdle = true;
        [SerializeField][Min(0f)] private float loopGapSeconds = 5f;

        private bool isSubscribed;
        private bool isOpenLoopPlaying;
        private Coroutine bindCoroutine;
        private Coroutine loopCoroutine;
        private DayLoopManager subscribedDayLoop;

        private void Awake()
        {
            if (targetAnimator == null)
            {
                targetAnimator = GetComponent<Animator>();
            }

            if (targetAnimator == null)
            {
                targetAnimator = GetComponentInChildren<Animator>(true);
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

            StopOpenLoopRoutine();
            Unsubscribe();
        }

        /// <summary>
        /// 供运行时生成后手动调用，立刻按当前游戏阶段同步动画状态。
        /// </summary>
        public void SyncWithCurrentPhase()
        {
            var dayLoop = DayLoopManager.Instance;
            if (dayLoop == null)
            {
                PlayIdle();
                return;
            }

            if (dayLoop.currentPhase == GamePhase.OpenPhase)
            {
                PlayOpenLoop();
            }
            else
            {
                PlayIdle();
            }
        }

        public void PlayOpenLoop()
        {
            if (targetAnimator == null || isOpenLoopPlaying)
            {
                return;
            }

            targetAnimator.enabled = true;
            targetAnimator.updateMode = AnimatorUpdateMode.Normal;
            isOpenLoopPlaying = true;
            loopCoroutine = StartCoroutine(PlayOpenLoopRoutine());
        }

        public void PlayIdle()
        {
            StopOpenLoopRoutine();

            if (targetAnimator == null)
            {
                isOpenLoopPlaying = false;
                return;
            }

            targetAnimator.enabled = true;
            targetAnimator.updateMode = AnimatorUpdateMode.Normal;
            targetAnimator.Play(idleStateName, layerIndex, 0f);
            targetAnimator.Update(0f);

            if (disableAnimatorWhenIdle)
            {
                targetAnimator.enabled = false;
            }

            isOpenLoopPlaying = false;
        }

        private IEnumerator PlayOpenLoopRoutine()
        {
            while (isOpenLoopPlaying && targetAnimator != null)
            {
                targetAnimator.enabled = true;
                targetAnimator.updateMode = AnimatorUpdateMode.Normal;
                targetAnimator.Play(openLoopStateName, layerIndex, 0f);
                targetAnimator.Update(0f);

                yield return null;

                while (isOpenLoopPlaying && targetAnimator != null && IsInOpenStateBeforeEnd())
                {
                    yield return null;
                }

                if (loopGapSeconds > 0f)
                {
                    SnapToOpenFirstFrame();
                    targetAnimator.enabled = false;
                    yield return new WaitForSeconds(loopGapSeconds);
                }
            }

            loopCoroutine = null;
        }

        private void SnapToOpenFirstFrame()
        {
            if (targetAnimator == null)
            {
                return;
            }

            targetAnimator.enabled = true;
            targetAnimator.Play(openLoopStateName, layerIndex, 0f);
            targetAnimator.Update(0f);
        }

        private bool IsInOpenStateBeforeEnd()
        {
            var stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(layerIndex);
            return stateInfo.IsName(openLoopStateName) && stateInfo.normalizedTime < 1f;
        }

        private void StopOpenLoopRoutine()
        {
            if (loopCoroutine != null)
            {
                StopCoroutine(loopCoroutine);
                loopCoroutine = null;
            }

            isOpenLoopPlaying = false;
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
            subscribedDayLoop.OnStoreOpen += PlayOpenLoop;
            subscribedDayLoop.OnBuildPhaseStart += PlayIdle;
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

            subscribedDayLoop.OnStoreOpen -= PlayOpenLoop;
            subscribedDayLoop.OnBuildPhaseStart -= PlayIdle;
            isSubscribed = false;
            subscribedDayLoop = null;
        }
    }
}
