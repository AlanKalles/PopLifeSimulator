using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Testing
{
    /// <summary>
    /// 顾客动画测试工具
    /// 挂载到测试场景中的 customer prefab 上，运行时在屏幕左侧显示按钮面板
    /// 支持无 AILerp / 无 A* 寻路图的独立测试场景
    /// </summary>
    public class CustomerAnimationTester : MonoBehaviour
    {
        [Header("引用（不填则自动查找）")]
        [SerializeField] private CustomerAnimationController animController;

        [Header("测试配置")]
        [SerializeField] private string customerID = "C001";
        [SerializeField] private float simulatedWalkSpeed = 2f;

        // 行走模拟状态
        private bool isWalking;
        private float walkDirection = 1f;

        // UI 状态
        private string currentAnimLabel = "Idle";
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle statusStyle;
        private bool stylesInitialized;

        void Start()
        {
            if (animController == null)
                animController = GetComponent<CustomerAnimationController>();

            if (animController == null)
                Debug.LogError("[CustomerAnimationTester] 找不到 CustomerAnimationController！请手动拖拽赋值。");
        }

        void Update()
        {
            if (animController == null) return;

            // 持续向动画控制器提供模拟速度
            if (isWalking)
                animController.debugVelocityOverride = new Vector3(walkDirection * simulatedWalkSpeed, 0, 0);
            else
                animController.debugVelocityOverride = null;
        }

        void OnDestroy()
        {
            // 清理调试覆盖
            if (animController != null)
                animController.debugVelocityOverride = null;
        }

        // ──────────────────── GUI ────────────────────

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.9f, 1f) }
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.yellow }
            };
        }

        void OnGUI()
        {
            if (animController == null) return;

            InitStyles();

            float panelWidth = 240f;
            float panelX = 10f;
            float panelY = 10f;

            // 半透明背景
            GUI.Box(new Rect(panelX - 5, panelY - 5, panelWidth + 10, 520f), "");

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, 510f));

            // ── 标题 ──
            GUILayout.Label("Animation Tester", titleStyle);
            GUILayout.Label($"State: {currentAnimLabel}", statusStyle);
            GUILayout.Space(8);

            // ── 角色加载 ──
            GUILayout.Label("Load Character", sectionStyle);
            GUILayout.BeginHorizontal();
            customerID = GUILayout.TextField(customerID, GUILayout.Width(80));
            if (GUILayout.Button("Load Parts"))
            {
                LoadCharacterParts();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // ── 行走模拟 ──
            GUILayout.Label("Walk Simulation", sectionStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<< Walk Left"))
            {
                StartWalk(-1f);
            }
            if (GUILayout.Button("Walk Right >>"))
            {
                StartWalk(1f);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Stop Walk"))
            {
                StopWalk();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Speed: {simulatedWalkSpeed:F1}", GUILayout.Width(80));
            simulatedWalkSpeed = GUILayout.HorizontalSlider(simulatedWalkSpeed, 0.5f, 10f);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // ── 单次动画 ──
            GUILayout.Label("One-Shot Animations", sectionStyle);
            if (GUILayout.Button("PickProduct  (拾取商品 ~2s)"))
            {
                StopWalk();
                animController.PlayPickProduct();
                currentAnimLabel = "PickProduct";
            }
            if (GUILayout.Button("Checkout  (结账 0.67s)"))
            {
                StopWalk();
                animController.PlayCheckout();
                currentAnimLabel = "Checkout";
            }
            if (GUILayout.Button("Upset  (不满 0.85s)"))
            {
                StopWalk();
                animController.PlayUpset();
                currentAnimLabel = "Upset";
            }
            GUILayout.Space(8);

            // ── 循环动画 ──
            GUILayout.Label("Loop Animations", sectionStyle);
            if (GUILayout.Button("Think  (思考循环)"))
            {
                StopWalk();
                animController.PlayThink();
                currentAnimLabel = "Think (loop)";
            }
            if (GUILayout.Button("Interaction  (交互循环)"))
            {
                StopWalk();
                animController.PlayInteraction();
                currentAnimLabel = "Interaction (loop)";
            }
            GUILayout.Space(8);

            // ── 控制 ──
            GUILayout.Label("Controls", sectionStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Stop Loop"))
            {
                animController.StopLoop();
                currentAnimLabel = "Idle";
            }
            if (GUILayout.Button("Stop All"))
            {
                StopWalk();
                animController.StopCurrentAnimation();
                currentAnimLabel = "Idle";
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        // ──────────────────── 内部方法 ────────────────────

        private void StartWalk(float direction)
        {
            // 先停掉可能正在播放的特殊动画
            animController.StopCurrentAnimation();
            isWalking = true;
            walkDirection = direction;
            currentAnimLabel = direction > 0 ? "Walk >>" : "<< Walk";
        }

        private void StopWalk()
        {
            isWalking = false;
            // 不在此处调 StopCurrentAnimation，避免打断即将播放的特殊动画
        }

        private void LoadCharacterParts()
        {
            var parts = CustomerPartLoader.LoadParts(customerID);
            if (parts != null && parts.Length >= 6)
            {
                animController.SetupParts(parts);
                Debug.Log($"[CustomerAnimationTester] 已加载 {customerID} 的 6 个部件精灵");
            }
            else
            {
                Debug.LogWarning($"[CustomerAnimationTester] 加载 {customerID} 失败，请检查 Resources/CustomerParts/{customerID.ToLower()}_sheet 是否存在");
            }
        }
    }
}
