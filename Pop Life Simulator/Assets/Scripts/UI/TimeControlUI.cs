using UnityEngine;
using UnityEngine.UI;

namespace PopLife.UI
{
    /// <summary>
    /// 时间控制UI - 提供暂停、原速（1x）、加速（2x）功能
    /// ⚠️ 仅在营业阶段（OpenPhase）有效，建造阶段自动禁用
    /// </summary>
    public class TimeControlUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button normalSpeedButton;
        [SerializeField] private Button fastSpeedButton;

        [Header("Button Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color activeColor = new Color(0.5f, 1f, 0.5f); // 浅绿色表示激活
        [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f); // 灰色表示不可用

        [Header("Settings")]
        [SerializeField] private float normalSpeed = 1f;
        [SerializeField] private float fastSpeed = 2f;

        private TimeControlState currentState = TimeControlState.Normal;

        private enum TimeControlState
        {
            Paused,
            Normal,
            Fast
        }

        private void Start()
        {
            // 绑定按钮事件
            if (pauseButton != null)
                pauseButton.onClick.AddListener(OnPauseClicked);

            if (normalSpeedButton != null)
                normalSpeedButton.onClick.AddListener(OnNormalSpeedClicked);

            if (fastSpeedButton != null)
                fastSpeedButton.onClick.AddListener(OnFastSpeedClicked);

            // 订阅游戏阶段事件
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += OnBuildPhaseStart;
                DayLoopManager.Instance.OnStoreOpen += OnStoreOpen;
            }

            // 初始状态：建造阶段时禁用所有按钮
            UpdateButtonStates();
        }

        private void OnDestroy()
        {
            // 取消事件订阅
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= OnBuildPhaseStart;
                DayLoopManager.Instance.OnStoreOpen -= OnStoreOpen;
            }
        }

        /// <summary>
        /// 建造阶段开始时禁用时间控制
        /// </summary>
        private void OnBuildPhaseStart()
        {
            SetButtonsInteractable(false);
            UpdateButtonStates();
            Debug.Log("[TimeControlUI] Build phase started - time controls disabled");
        }

        /// <summary>
        /// 开店时启用时间控制，默认恢复为原速
        /// </summary>
        private void OnStoreOpen()
        {
            SetButtonsInteractable(true);
            currentState = TimeControlState.Normal;
            ApplyTimeControl();
            UpdateButtonStates();
            Debug.Log("[TimeControlUI] Store opened - time controls enabled, set to normal speed");
        }

        private void OnPauseClicked()
        {
            // ⚠️ 安全检查：仅在营业阶段有效
            if (!IsInOpenPhase())
            {
                Debug.LogWarning("[TimeControlUI] Cannot pause - not in open phase");
                return;
            }

            currentState = TimeControlState.Paused;
            ApplyTimeControl();
            UpdateButtonStates();
            Debug.Log("[TimeControlUI] Time paused");
        }

        private void OnNormalSpeedClicked()
        {
            // ⚠️ 安全检查：仅在营业阶段有效
            if (!IsInOpenPhase())
            {
                Debug.LogWarning("[TimeControlUI] Cannot set normal speed - not in open phase");
                return;
            }

            currentState = TimeControlState.Normal;
            ApplyTimeControl();
            UpdateButtonStates();
            Debug.Log("[TimeControlUI] Time set to normal speed (1x)");
        }

        private void OnFastSpeedClicked()
        {
            // ⚠️ 安全检查：仅在营业阶段有效
            if (!IsInOpenPhase())
            {
                Debug.LogWarning("[TimeControlUI] Cannot set fast speed - not in open phase");
                return;
            }

            currentState = TimeControlState.Fast;
            ApplyTimeControl();
            UpdateButtonStates();
            Debug.Log("[TimeControlUI] Time set to fast speed (2x)");
        }

        /// <summary>
        /// 应用时间控制到DayLoopManager
        /// </summary>
        private void ApplyTimeControl()
        {
            if (DayLoopManager.Instance == null) return;

            switch (currentState)
            {
                case TimeControlState.Paused:
                    DayLoopManager.Instance.PauseTime();
                    break;

                case TimeControlState.Normal:
                    DayLoopManager.Instance.ResumeTime();
                    DayLoopManager.Instance.SetTimeScale(normalSpeed);
                    break;

                case TimeControlState.Fast:
                    DayLoopManager.Instance.ResumeTime();
                    DayLoopManager.Instance.SetTimeScale(fastSpeed);
                    break;
            }
        }

        /// <summary>
        /// 更新按钮视觉状态（高亮当前激活的按钮）
        /// </summary>
        private void UpdateButtonStates()
        {
            if (!IsInOpenPhase())
            {
                // 建造阶段：所有按钮显示为禁用状态
                SetButtonColor(pauseButton, disabledColor);
                SetButtonColor(normalSpeedButton, disabledColor);
                SetButtonColor(fastSpeedButton, disabledColor);
                return;
            }

            // 营业阶段：根据当前状态高亮对应按钮
            SetButtonColor(pauseButton, currentState == TimeControlState.Paused ? activeColor : normalColor);
            SetButtonColor(normalSpeedButton, currentState == TimeControlState.Normal ? activeColor : normalColor);
            SetButtonColor(fastSpeedButton, currentState == TimeControlState.Fast ? activeColor : normalColor);
        }

        /// <summary>
        /// 设置按钮颜色
        /// </summary>
        private void SetButtonColor(Button button, Color color)
        {
            if (button == null) return;

            var colors = button.colors;
            colors.normalColor = color;
            colors.selectedColor = color;
            button.colors = colors;
        }

        /// <summary>
        /// 设置所有按钮的可交互状态
        /// </summary>
        private void SetButtonsInteractable(bool interactable)
        {
            if (pauseButton != null)
                pauseButton.interactable = interactable;

            if (normalSpeedButton != null)
                normalSpeedButton.interactable = interactable;

            if (fastSpeedButton != null)
                fastSpeedButton.interactable = interactable;
        }

        /// <summary>
        /// 检查是否在营业阶段
        /// </summary>
        private bool IsInOpenPhase()
        {
            return DayLoopManager.Instance != null &&
                   DayLoopManager.Instance.currentPhase == GamePhase.OpenPhase;
        }
    }
}
