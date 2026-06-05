using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using PrimeTween;

namespace PopLife.Title
{
    /// <summary>
    /// 标题场景到主场景的过渡控制器
    /// 流程：黑屏淡入 → Logo + 文本淡入 → 异步加载主场景（不阻塞）→
    ///       激活主场景 → Logo + 文本淡出 → 黑屏淡出 → 销毁自身
    /// 通过 DontDestroyOnLoad 让过渡 UI 跨场景存活，实现首尾无缝衔接
    /// </summary>
    public class TitleSceneTransition : MonoBehaviour
    {
        [Header("目标场景")]
        [Tooltip("LatestUpdate 或主场景文件名")]
        [SerializeField] private string sceneToLoad = "LatestUpdate";

        [Header("UI 引用")]
        [Tooltip("全屏黑色遮罩的 CanvasGroup，初始 alpha=0")]
        [SerializeField] private CanvasGroup blackCanvasGroup;
        [Tooltip("Logo + 文本容器的 CanvasGroup，初始 alpha=0")]
        [SerializeField] private CanvasGroup loadingContentCanvasGroup;
        [Tooltip("加载文本，用于动态省略号动画")]
        [SerializeField] private TMP_Text loadingText;

        [Header("时间参数")]
        [SerializeField] private float blackFadeInDuration = 0.5f;
        [SerializeField] private float loadingContentFadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.6f;
        [Tooltip("黑屏 + 加载页的最短显示时间，避免加载太快闪屏")]
        [SerializeField] private float minBlackDuration = 0.8f;

        [Header("加载文本动画")]
        [SerializeField] private string baseLoadingText = "Opening shop";
        [Tooltip("省略号切换间隔（秒）")]
        [SerializeField] private float dotsInterval = 0.4f;
        [Tooltip("省略号最大数量（实际循环 0 ~ maxDots）")]
        [SerializeField] private int maxDots = 3;

        [Header("音乐淡出")]
        [SerializeField] private bool fadeOutMusic = true;
        [SerializeField] private float musicFadeDuration = 0.5f;

        private bool isTransitioning;
        private Coroutine dotsCoroutine;

        /// <summary>
        /// 启动过渡——由 TitleMenuController 在 Start 按钮点击时调用
        /// </summary>
        public void BeginTransition()
        {
            if (isTransitioning) return;
            if (string.IsNullOrEmpty(sceneToLoad))
            {
                Debug.LogError("[TitleSceneTransition] sceneToLoad 未配置");
                return;
            }
            isTransitioning = true;
            StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            // 1. 让自身（含 UI 子物体）跨场景存活
            PreserveAcrossScene(gameObject);

            // 2. 阻挡输入 + 黑屏淡入 + 音乐淡出（并行启动）
            if (blackCanvasGroup != null)
            {
                blackCanvasGroup.blocksRaycasts = true;
                blackCanvasGroup.interactable = true;
                Tween.Alpha(blackCanvasGroup, 1f, blackFadeInDuration, Ease.InOutQuad);
            }

            if (fadeOutMusic)
            {
                // 闭包捕获 TitleScene 的 AudioManager 引用——之后它会被销毁，
                // 用局部引用 + Unity 风格 null check 避免误改主场景 AudioManager
                var titleAudio = AudioManager.Instance;
                if (titleAudio != null)
                {
                    float startVol = titleAudio.GetMusicVolume();
                    Tween.Custom(startVol, 0f, musicFadeDuration, v =>
                    {
                        if (titleAudio != null) titleAudio.SetMusicVolume(v);
                    });
                }
            }

            yield return new WaitForSeconds(blackFadeInDuration);

            // 3. Logo + 文本淡入
            if (loadingContentCanvasGroup != null)
            {
                Tween.Alpha(loadingContentCanvasGroup, 1f, loadingContentFadeInDuration, Ease.InOutQuad);
            }

            // 4. 启动省略号动画
            if (loadingText != null)
            {
                dotsCoroutine = StartCoroutine(AnimateDots());
            }

            yield return new WaitForSeconds(loadingContentFadeInDuration);

            // 5. 异步加载主场景，禁止自动激活以便控制最短显示时间
            var op = SceneManager.LoadSceneAsync(sceneToLoad);
            if (op == null)
            {
                Debug.LogError($"[TitleSceneTransition] 加载场景失败: {sceneToLoad}（检查 Build Settings 是否包含此场景）");
                Cleanup();
                yield break;
            }
            op.allowSceneActivation = false;

            float loadStartTime = Time.realtimeSinceStartup;

            // 6. 等加载到 90%（Unity 把 0.9-1.0 留给激活步骤）
            while (op.progress < 0.9f)
            {
                yield return null;
            }

            // 7. 保证黑屏 + 加载页至少展示 minBlackDuration
            float elapsed = Time.realtimeSinceStartup - loadStartTime;
            float remainingMin = minBlackDuration - elapsed;
            if (remainingMin > 0f)
            {
                yield return new WaitForSecondsRealtime(remainingMin);
            }

            // 8. 激活新场景，等场景完全加载
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // 多等一帧给主场景 Awake / Start 完成
            yield return null;

            // 9. 停止省略号动画
            if (dotsCoroutine != null)
            {
                StopCoroutine(dotsCoroutine);
                dotsCoroutine = null;
            }

            // 10. Logo+文本淡出 → 黑屏淡出
            if (loadingContentCanvasGroup != null)
            {
                Tween.Alpha(loadingContentCanvasGroup, 0f, fadeOutDuration, Ease.InOutQuad);
            }
            if (blackCanvasGroup != null)
            {
                Tween.Alpha(blackCanvasGroup, 0f, fadeOutDuration, Ease.InOutQuad);
            }

            yield return new WaitForSeconds(fadeOutDuration);

            Cleanup();
        }

        private IEnumerator AnimateDots()
        {
            int n = 0;
            var wait = new WaitForSeconds(dotsInterval);
            while (true)
            {
                int dots = n % (maxDots + 1);
                loadingText.text = baseLoadingText + new string('.', dots);
                n++;
                yield return wait;
            }
        }

        private static void PreserveAcrossScene(GameObject go)
        {
            // DontDestroyOnLoad 要求 GameObject 在 scene root，先 detach
            if (go.transform.parent != null)
            {
                go.transform.SetParent(null, true);
            }
            DontDestroyOnLoad(go);
        }

        private void Cleanup()
        {
            if (blackCanvasGroup != null)
            {
                blackCanvasGroup.blocksRaycasts = false;
                blackCanvasGroup.interactable = false;
            }
            // 整个 LoadingCanvas（即自身 root）销毁
            Destroy(gameObject);
        }
    }
}
