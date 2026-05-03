#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using UnityEngine;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// 开发调试工具：实时拖框获取 spotlight norm/rect 坐标，松开自动复制到剪贴板并预览高亮。
    ///
    /// 使用：挂到场景任意 GameObject → Play → 默认 F8 进入找框模式 → 按住中键拖矩形 → 松开。
    /// 松开后：
    ///   1. norm 字符串自动复制到系统剪贴板（直接 Ctrl+V 粘贴到 Sequence 字段）
    ///   2. Console 输出完整 ShowSpotlight(...) 行
    ///   3. SpotlightManager 自动以当前形状显示预览
    ///
    /// 仅在 Editor / Development Build 中编译，发布版 0 开销。
    /// </summary>
    [DisallowMultipleComponent]
    public class SpotlightRectFinder : MonoBehaviour
    {
        [Header("快捷键")]
        [Tooltip("切换工具开/关")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;
        [Tooltip("循环切换形状（Rectangle / RoundedRectangle / Circle）")]
        [SerializeField] private KeyCode cycleShapeKey = KeyCode.F9;
        [Tooltip("隐藏当前预览的 spotlight")]
        [SerializeField] private KeyCode hidePreviewKey = KeyCode.Escape;
        [Tooltip("0=左键, 1=右键, 2=中键。默认中键，避免与游戏左键交互冲突")]
        [SerializeField, Range(0, 2)] private int dragMouseButton = 2;

        [Header("行为")]
        [Tooltip("松开拖框后是否自动调用 SpotlightManager.Show 预览效果")]
        [SerializeField] private bool autoPreviewOnRelease = true;
        [Tooltip("自动复制 norm 字符串到系统剪贴板")]
        [SerializeField] private bool copyToClipboard = true;
        [Tooltip("Console 输出小数位数")]
        [SerializeField, Range(2, 5)] private int outputDecimals = 3;

        [Header("外观")]
        [SerializeField] private Color dragBoxFill = new Color(0f, 1f, 0f, 0.18f);
        [SerializeField] private Color dragBoxBorder = new Color(0.2f, 1f, 0.2f, 1f);

        // ============== 运行时状态 ==============
        private bool active;
        private bool dragging;
        private Vector2 dragStartPx;          // 屏幕坐标（左下原点）
        private Rect currentRectPx;           // 屏幕坐标
        private string lastNormString = "";
        private SpotlightShape currentShape = SpotlightShape.RoundedRectangle;

        // ============== GUI 资源 ==============
        private Texture2D whiteTex;
        private GUIStyle hudStyle;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) ToggleActive();
            if (!active) return;

            if (Input.GetKeyDown(hidePreviewKey)) HidePreview();
            if (Input.GetKeyDown(cycleShapeKey)) CycleShape();

            if (Input.GetMouseButtonDown(dragMouseButton)) BeginDrag();
            else if (Input.GetMouseButton(dragMouseButton) && dragging) UpdateDrag();
            else if (Input.GetMouseButtonUp(dragMouseButton) && dragging) EndDrag();
        }

        // ============== Drag 流程 ==============

        private void ToggleActive()
        {
            active = !active;
            if (!active)
            {
                dragging = false;
                HidePreview();
            }
            Debug.Log($"[SpotlightRectFinder] {(active ? "ACTIVE — drag mouse button " + dragMouseButton + " to find rect" : "off")}");
        }

        private void BeginDrag()
        {
            dragging = true;
            dragStartPx = Input.mousePosition;
            currentRectPx = new Rect(dragStartPx.x, dragStartPx.y, 0, 0);
        }

        private void UpdateDrag()
        {
            Vector2 cur = Input.mousePosition;
            float x = Mathf.Min(dragStartPx.x, cur.x);
            float y = Mathf.Min(dragStartPx.y, cur.y);
            float w = Mathf.Abs(cur.x - dragStartPx.x);
            float h = Mathf.Abs(cur.y - dragStartPx.y);
            currentRectPx = new Rect(x, y, w, h);
            lastNormString = FormatNorm(currentRectPx);
        }

        private void EndDrag()
        {
            dragging = false;
            if (currentRectPx.width < 4f || currentRectPx.height < 4f)
            {
                Debug.Log("[SpotlightRectFinder] 矩形过小（<4px），忽略");
                return;
            }

            string norm = FormatNorm(currentRectPx);
            string rect = FormatRect(currentRectPx);
            string sequenceLine = $"ShowSpotlight(\"{norm}\", NULL, Auto, {currentShape}, blocking)";

            Debug.Log(
                $"[SpotlightRectFinder]\n" +
                $"  {norm}\n" +
                $"  {rect}\n" +
                $"  Sequence:  {sequenceLine}");

            if (copyToClipboard) GUIUtility.systemCopyBuffer = norm;
            if (autoPreviewOnRelease) PreviewSpotlight();
        }

        // ============== Preview ==============

        private void CycleShape()
        {
            currentShape = currentShape switch
            {
                SpotlightShape.Rectangle => SpotlightShape.RoundedRectangle,
                SpotlightShape.RoundedRectangle => SpotlightShape.Circle,
                SpotlightShape.Circle => SpotlightShape.Rectangle,
                _ => SpotlightShape.RoundedRectangle,
            };
            Debug.Log($"[SpotlightRectFinder] shape = {currentShape}");
            // 当前正预览 → 重新以新形状展示
            if (SpotlightManager.Instance != null && SpotlightManager.Instance.IsActive
                && currentRectPx.width > 0 && currentRectPx.height > 0)
            {
                PreviewSpotlight();
            }
        }

        private void PreviewSpotlight()
        {
            var mgr = SpotlightManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning("[SpotlightRectFinder] SpotlightManager.Instance 未就绪，无法预览");
                return;
            }
            mgr.Show(new SpotlightRequest
            {
                target = SpotlightTargetSpec.ForScreenRect(currentRectPx),
                text = SpotlightRequest.NullTextSentinel,
                position = TooltipPosition.Auto,
                shape = currentShape,
                mode = InteractionMode.Blocking,
            });
        }

        private void HidePreview()
        {
            var mgr = SpotlightManager.Instance;
            if (mgr != null && mgr.IsActive) mgr.Hide(CloseReason.Manual);
        }

        // ============== 格式化 ==============

        private string FormatNorm(Rect px)
        {
            float nx = px.x / Screen.width;
            float ny = px.y / Screen.height;
            float nw = px.width / Screen.width;
            float nh = px.height / Screen.height;
            string fmt = "F" + outputDecimals;
            var ic = CultureInfo.InvariantCulture;
            return $"norm:{nx.ToString(fmt, ic)},{ny.ToString(fmt, ic)},{nw.ToString(fmt, ic)},{nh.ToString(fmt, ic)}";
        }

        private string FormatRect(Rect px)
        {
            return $"rect:{(int)px.x},{(int)px.y},{(int)px.width},{(int)px.height}";
        }

        // ============== OnGUI: 绘制拖框 + HUD ==============

        private void OnGUI()
        {
            if (!active) return;
            EnsureGuiResources();

            // 绘制拖框（OnGUI 是左上原点 → Y 翻转）
            if (currentRectPx.width > 0 && currentRectPx.height > 0)
            {
                Rect guiRect = ScreenToGUI(currentRectPx);
                GUI.color = dragBoxFill;
                GUI.DrawTexture(guiRect, whiteTex);
                GUI.color = dragBoxBorder;
                DrawBorder(guiRect, 2);
                GUI.color = Color.white;
            }

            // HUD（左上）
            string status = dragging ? "DRAGGING" : (currentRectPx.width > 0 ? "RELEASED" : "READY");
            string mouseLabel = MouseButtonLabel(dragMouseButton);
            string norm = currentRectPx.width > 0 ? lastNormString : "norm: <drag to measure>";
            string hud =
                $"[SpotlightRectFinder] {status}   shape={currentShape}   {Screen.width}x{Screen.height}\n" +
                $"  {norm}\n" +
                $"  Drag {mouseLabel}   Toggle [{toggleKey}]   CycleShape [{cycleShapeKey}]   HidePreview [{hidePreviewKey}]";
            GUI.Label(new Rect(10, 10, Screen.width - 20, 80), hud, hudStyle);
        }

        private static Rect ScreenToGUI(Rect screenRect)
        {
            return new Rect(screenRect.x, Screen.height - screenRect.y - screenRect.height,
                            screenRect.width, screenRect.height);
        }

        private void DrawBorder(Rect r, int thickness)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), whiteTex);                              // top
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - thickness, r.width, thickness), whiteTex);       // bottom
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), whiteTex);                             // left
            GUI.DrawTexture(new Rect(r.x + r.width - thickness, r.y, thickness, r.height), whiteTex);       // right
        }

        private static string MouseButtonLabel(int b) => b switch
        {
            0 => "[LMB]",
            1 => "[RMB]",
            2 => "[MMB]",
            _ => "[M" + b + "]",
        };

        private void EnsureGuiResources()
        {
            if (whiteTex == null)
            {
                whiteTex = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                whiteTex.SetPixel(0, 0, Color.white);
                whiteTex.Apply();
            }
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin != null ? GUI.skin.label : new GUIStyle())
                {
                    fontSize = 13,
                    alignment = TextAnchor.UpperLeft,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.4f, 1f, 0.4f, 1f) },
                };
            }
        }

        private void OnDestroy()
        {
            if (whiteTex != null) Destroy(whiteTex);
        }
    }
}
#endif
