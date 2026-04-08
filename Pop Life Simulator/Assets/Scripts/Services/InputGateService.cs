using UnityEngine;

namespace PopLife.Services
{
    /// <summary>
    /// 集中式输入仲裁服务 — 区分"点击"与"拖拽"
    /// 所有需要鼠标左键输入的系统应从此服务读取状态，而非直接使用 Input.GetMouseButtonDown(0)
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InputGateService : MonoBehaviour
    {
        public static InputGateService Instance { get; private set; }

        [Header("阈值设置")]
        [Tooltip("鼠标移动超过此像素距离则判定为拖拽（而非点击）")]
        [SerializeField] private float dragThreshold = 8f;

        // 状态
        private Vector3 mouseDownScreenPos;
        private bool pointerDown;
        private bool dragConfirmed;
        private bool wasClick;
        private Vector3 clickScreenPos;

        // ── 公共 API ──

        /// <summary>当前鼠标按下是否已确认为拖拽</summary>
        public bool IsDragging => pointerDown && dragConfirmed;

        /// <summary>本帧是否发生了一次"纯点击"（松开时移动距离 &lt; 阈值）</summary>
        public bool WasClickThisFrame => wasClick;

        /// <summary>点击释放时的屏幕坐标</summary>
        public Vector3 ClickScreenPos => clickScreenPos;

        /// <summary>鼠标是否处于按住状态</summary>
        public bool IsPointerDown => pointerDown;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            // 每帧开头清除上一帧的 wasClick
            wasClick = false;

            if (Input.GetMouseButtonDown(0))
            {
                pointerDown = true;
                dragConfirmed = false;
                mouseDownScreenPos = Input.mousePosition;
            }

            if (pointerDown && Input.GetMouseButton(0) && !dragConfirmed)
            {
                float dist = Vector3.Distance(Input.mousePosition, mouseDownScreenPos);
                if (dist >= dragThreshold)
                {
                    dragConfirmed = true;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (pointerDown && !dragConfirmed)
                {
                    wasClick = true;
                    clickScreenPos = Input.mousePosition;
                }
                pointerDown = false;
                dragConfirmed = false;
            }
        }

        /// <summary>消费本帧点击，同帧后续系统读到 WasClickThisFrame = false</summary>
        public void ConsumeClick()
        {
            wasClick = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
