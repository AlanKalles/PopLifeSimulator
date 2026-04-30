using System.Globalization;
using UnityEngine;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// Spotlight 目标规格（不可变描述子）。通过工厂方法构造：
    /// <list type="bullet">
    /// <item><see cref="ById"/> - 注册表 ID 查找</item>
    /// <item><see cref="ForRect"/> - 直传 RectTransform</item>
    /// <item><see cref="ForWorld"/> - 世界物体 GameObject</item>
    /// <item><see cref="ForScreenRect"/> - 屏幕像素坐标 Rect</item>
    /// <item><see cref="ForNormalizedRect"/> - 屏幕 normalized 坐标 Rect (0..1)</item>
    /// </list>
    /// </summary>
    public struct SpotlightTargetSpec
    {
        public enum Kind
        {
            None,
            Id,
            RectTransform,
            WorldObject,
            ScreenRect,
            NormalizedRect,
        }

        public Kind kind;

        // Id 路径
        public string id;

        // RectTransform 路径
        public RectTransform rectTarget;

        // WorldObject 路径
        public GameObject worldTarget;
        public Camera worldCameraOverride;   // 可选，null 时走 fallback 链

        // ScreenRect / NormalizedRect 路径
        public Rect rectValue;

        // ============== 工厂方法 ==============

        public static SpotlightTargetSpec ById(string id)
            => new SpotlightTargetSpec { kind = Kind.Id, id = id };

        public static SpotlightTargetSpec ForRect(RectTransform rt)
            => new SpotlightTargetSpec { kind = Kind.RectTransform, rectTarget = rt };

        public static SpotlightTargetSpec ForWorld(GameObject go, Camera cam = null)
            => new SpotlightTargetSpec { kind = Kind.WorldObject, worldTarget = go, worldCameraOverride = cam };

        public static SpotlightTargetSpec ForScreenRect(Rect r)
            => new SpotlightTargetSpec { kind = Kind.ScreenRect, rectValue = r };

        public static SpotlightTargetSpec ForNormalizedRect(Rect r)
            => new SpotlightTargetSpec { kind = Kind.NormalizedRect, rectValue = r };

        public bool IsValid => kind != Kind.None;

        // ============== 智能解析（供 Sequencer 命令使用） ==============

        /// <summary>
        /// 智能解析字符串参数：
        /// <list type="bullet">
        /// <item>"id:Foo" / "Foo" → Id 查找</item>
        /// <item>"rect:x,y,w,h" → 屏幕像素 Rect</item>
        /// <item>"norm:x,y,w,h" → normalized Rect</item>
        /// </list>
        /// </summary>
        public static SpotlightTargetSpec ParseSmart(string param)
        {
            if (string.IsNullOrEmpty(param)) return new SpotlightTargetSpec { kind = Kind.None };

            int colonIndex = param.IndexOf(':');
            if (colonIndex > 0)
            {
                string prefix = param.Substring(0, colonIndex).Trim().ToLowerInvariant();
                string rest = param.Substring(colonIndex + 1);

                switch (prefix)
                {
                    case "id":
                        return ById(rest.Trim());
                    case "rect":
                        if (TryParseRect(rest, out var r1)) return ForScreenRect(r1);
                        break;
                    case "norm":
                        if (TryParseRect(rest, out var r2)) return ForNormalizedRect(r2);
                        break;
                }
            }
            // 默认按 ID 查找（兼容旧 Spotlight(BuildButton) 语法）
            return ById(param.Trim());
        }

        private static bool TryParseRect(string csv, out Rect r)
        {
            r = default;
            var parts = csv.Split(',');
            if (parts.Length != 4) return false;
            // DialogueDatabase 是文本资产；解析必须用 InvariantCulture，避免 locale 差异
            const NumberStyles style = NumberStyles.Float;
            var ic = CultureInfo.InvariantCulture;
            if (!float.TryParse(parts[0].Trim(), style, ic, out var x)) return false;
            if (!float.TryParse(parts[1].Trim(), style, ic, out var y)) return false;
            if (!float.TryParse(parts[2].Trim(), style, ic, out var w)) return false;
            if (!float.TryParse(parts[3].Trim(), style, ic, out var h)) return false;
            r = new Rect(x, y, w, h);
            return true;
        }

        // ============== 解析（运行时使用） ==============

        /// <summary>
        /// 在 Show() 时解析 Spec，得到一个稳定的 ResolvedTarget。
        /// 后续 LateUpdate 中由 ResolvedTarget.GetScreenRect / IsLost 检测。
        ///
        /// World 路径在此处一次性解析 Camera（fallback：override → canvas.worldCamera → Camera.main）；
        /// 解析失败直接返回 Empty，避免 GetScreenRect 每帧重复 warning。
        /// </summary>
        public ResolvedTarget Resolve(Canvas spotlightCanvas)
        {
            switch (kind)
            {
                case Kind.Id:
                {
                    var t = SpotlightTargetRegistry.Find(id);
                    if (t == null)
                    {
                        Debug.LogWarning($"[Spotlight] 注册表中未找到 ID '{id}'，spotlight 将不显示");
                        return ResolvedTarget.Empty;
                    }
                    // 同一 SpotlightTarget 可标记 UI 或世界物体：有 RectTransform 走 UI，否则走 World
                    var rt = t.GetRect();
                    if (rt != null)
                    {
                        return ResolvedTarget.FromUITarget(t, rt);
                    }
                    var cam = ResolveCamera(null, spotlightCanvas);
                    if (cam == null) return ResolvedTarget.Empty;
                    return ResolvedTarget.FromWorldTarget(t, t.gameObject, cam);
                }

                case Kind.RectTransform:
                    if (rectTarget == null) return ResolvedTarget.Empty;
                    return ResolvedTarget.FromRect(rectTarget);

                case Kind.WorldObject:
                {
                    if (worldTarget == null) return ResolvedTarget.Empty;
                    var cam = ResolveCamera(worldCameraOverride, spotlightCanvas);
                    if (cam == null) return ResolvedTarget.Empty;
                    return ResolvedTarget.FromWorld(worldTarget, cam);
                }

                case Kind.ScreenRect:
                    return ResolvedTarget.FromScreenRect(rectValue);

                case Kind.NormalizedRect:
                    return ResolvedTarget.FromNormalizedRect(rectValue);

                default:
                    return ResolvedTarget.Empty;
            }
        }

        private static Camera ResolveCamera(Camera explicitCamera, Canvas spotlightCanvas)
        {
            if (explicitCamera != null) return explicitCamera;
            if (spotlightCanvas != null && spotlightCanvas.worldCamera != null) return spotlightCanvas.worldCamera;
            var main = Camera.main;
            if (main != null) return main;
            Debug.LogWarning("[Spotlight] 无可用 Camera 解析世界目标，spotlight 将不显示");
            return null;
        }
    }

    /// <summary>
    /// Spec 解析后的运行时目标。SpotlightManager 持有此结构追踪当前 spotlight 目标。
    /// IsLost / GetScreenRect 在 LateUpdate 每帧调用。Camera 已在 Resolve 阶段解析完毕，无需 fallback。
    /// </summary>
    public struct ResolvedTarget
    {
        public enum Kind { None, UI, World, ScreenRect, NormalizedRect }

        public Kind kind;
        public SpotlightTarget targetComponent;  // 来自 Id 路径；为 null 表示直传路径
        public bool requiresTargetComponent;     // ById 路径=true，直传路径=false
        public RectTransform rectTransform;      // UI 路径
        public GameObject worldGo;               // World 路径
        public Camera resolvedCamera;            // World 路径已解析的 Camera（必非 null）
        public Rect rectValue;                   // ScreenRect / NormalizedRect

        public static readonly ResolvedTarget Empty = new ResolvedTarget { kind = Kind.None };

        public static ResolvedTarget FromUITarget(SpotlightTarget t, RectTransform rt)
            => new ResolvedTarget
            {
                kind = Kind.UI,
                targetComponent = t,
                requiresTargetComponent = true,
                rectTransform = rt,
            };

        public static ResolvedTarget FromRect(RectTransform rt)
            => new ResolvedTarget { kind = Kind.UI, rectTransform = rt };

        public static ResolvedTarget FromWorld(GameObject go, Camera cam)
            => new ResolvedTarget { kind = Kind.World, worldGo = go, resolvedCamera = cam };

        public static ResolvedTarget FromWorldTarget(SpotlightTarget t, GameObject go, Camera cam)
            => new ResolvedTarget
            {
                kind = Kind.World,
                targetComponent = t,
                requiresTargetComponent = true,
                worldGo = go,
                resolvedCamera = cam,
            };

        public static ResolvedTarget FromScreenRect(Rect r)
            => new ResolvedTarget { kind = Kind.ScreenRect, rectValue = r };

        public static ResolvedTarget FromNormalizedRect(Rect r)
            => new ResolvedTarget { kind = Kind.NormalizedRect, rectValue = r };

        public bool IsValid => kind != Kind.None;

        /// <summary>
        /// 目标是否已失效（GameObject 销毁、注册表 unregister、Camera 不可用等）。
        /// SpotlightManager.LateUpdate 中检测到失效则触发 Hide(CloseReason.TargetLost)。
        /// </summary>
        public bool IsLost()
        {
            switch (kind)
            {
                case Kind.UI:
                    if (rectTransform == null) return true;
                    // ById 路径：SpotlightTarget 组件被销毁（fake-null）或 disabled 都算 lost
                    if (requiresTargetComponent && (targetComponent == null || !targetComponent.isActiveAndEnabled))
                        return true;
                    return false;
                case Kind.World:
                    if (worldGo == null) return true;
                    if (resolvedCamera == null) return true;   // Camera 被销毁也算 lost
                    if (requiresTargetComponent && (targetComponent == null || !targetComponent.isActiveAndEnabled))
                        return true;
                    return false;
                case Kind.ScreenRect:
                case Kind.NormalizedRect:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 返回当前屏幕坐标系下的 Rect（pixel）。失效时返回 Rect.zero。
        /// </summary>
        public Rect GetScreenRect()
        {
            if (IsLost()) return Rect.zero;

            switch (kind)
            {
                case Kind.UI:
                    return ComputeUIRect(rectTransform);

                case Kind.World:
                    return ComputeWorldRect(worldGo, resolvedCamera);

                case Kind.ScreenRect:
                    return rectValue;

                case Kind.NormalizedRect:
                    return new Rect(
                        rectValue.x * Screen.width,
                        rectValue.y * Screen.height,
                        rectValue.width * Screen.width,
                        rectValue.height * Screen.height);

                default:
                    return Rect.zero;
            }
        }

        // ============== 内部计算 ==============

        private static Rect ComputeUIRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = canvas.worldCamera;
            }

            // 投影所有 4 个 corner 取 min/max，覆盖旋转/负缩放/复杂父级 transform
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                min = Vector2.Min(min, sp);
                max = Vector2.Max(max, sp);
            }
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        private static Rect ComputeWorldRect(GameObject go, Camera cam)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var bounds = renderer.bounds;
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);

                // 8 个 bbox 角点投影到屏幕，取屏幕 AABB
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3(
                        (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector2 sp = cam.WorldToScreenPoint(c);
                    min = Vector2.Min(min, sp);
                    max = Vector2.Max(max, sp);
                }
                return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            }

            // 无 Renderer fallback：transform 位置 + 默认尺寸
            Vector2 center = cam.WorldToScreenPoint(go.transform.position);
            const float fallbackSize = 100f;
            return new Rect(center.x - fallbackSize / 2, center.y - fallbackSize / 2, fallbackSize, fallbackSize);
        }
    }
}
