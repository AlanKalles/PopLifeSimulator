using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PopLife
{
    /// <summary>
    /// 光照管理器 - 管理场景中的所有 Light2D 组件
    /// 根据 DayLoopManager 的游戏阶段动态调整光照
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LightingManager : MonoBehaviour
    {
        public static LightingManager Instance { get; private set; }

        [Header("Light References")]
        [SerializeField] private Light2D globalLight;
        [SerializeField] private List<Light2D> freeformLights = new List<Light2D>();

        [Header("BuildPhase Lighting Config")]
        [Tooltip("建造阶段的固定光照颜色")]
        [SerializeField] private Color buildPhaseColor = Color.white;
        [Tooltip("建造阶段的固定光照强度")]
        [SerializeField] private float buildPhaseIntensity = 1.5f;

        [Header("OpenPhase Dynamic Lighting Config")]
        [Tooltip("营业阶段光照颜色渐变 (时间归一化 0-1)")]
        [SerializeField] private Gradient openPhaseColorGradient;
        [Tooltip("营业阶段光照强度曲线 (可选，留空则使用固定强度)")]
        [SerializeField] private AnimationCurve openPhaseIntensityCurve = AnimationCurve.Constant(0, 1, 1.0f);

        [Header("Tile Light Config")]
        [Tooltip("FloorTile 灯光颜色渐变（跟随营业时间）")]
        [SerializeField] private Gradient tileLightColorGradient;
        [Tooltip("FloorTile 灯光基础强度（未启用强度曲线时使用）")]
        [SerializeField] private float tileLightBaseIntensity = 2.0f;
        [Tooltip("FloorTile 灯光强度曲线（可选）")]
        [SerializeField] private AnimationCurve tileLightIntensityCurve = AnimationCurve.Constant(0, 1, 2.0f);
        [Tooltip("是否使用 FloorTile 灯光强度曲线")]
        [SerializeField] private bool useTileIntensityCurve = false;

        [Header("Advanced Settings")]
        [Tooltip("是否使用强度曲线（关闭则使用固定强度 1.0）")]
        [SerializeField] private bool useIntensityCurve = false;

        // FloorTile 灯光运行时管理（由 FloorTileLighting 自动注册/注销）
        private readonly List<Light2D> tileLights = new List<Light2D>();
        private readonly HashSet<Light2D> tileLightSet = new HashSet<Light2D>();

        private void Awake()
        {
            // 单例模式
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[LightingManager] Instance already exists, destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            // 订阅 DayLoopManager 事件
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += ApplyBuildPhaseLighting;
                DayLoopManager.Instance.OnStoreOpen += OnStoreOpenHandler;
                DayLoopManager.Instance.OnDailySettlement += OnDailySettlementHandler;
            }
            else
            {
                Debug.LogWarning("[LightingManager] DayLoopManager.Instance is null on OnEnable. Will retry in Start().");
            }
        }

        private void Start()
        {
            // 延迟订阅（确保 DayLoopManager 已初始化）
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= ApplyBuildPhaseLighting;
                DayLoopManager.Instance.OnStoreOpen -= OnStoreOpenHandler;
                DayLoopManager.Instance.OnDailySettlement -= OnDailySettlementHandler;

                DayLoopManager.Instance.OnBuildPhaseStart += ApplyBuildPhaseLighting;
                DayLoopManager.Instance.OnStoreOpen += OnStoreOpenHandler;
                DayLoopManager.Instance.OnDailySettlement += OnDailySettlementHandler;
            }

            // 初始化 Gradient（如果为空）
            if (openPhaseColorGradient == null)
                InitializeDefaultGradient();
            if (tileLightColorGradient == null)
                InitializeDefaultTileGradient();

            // 验证引用
            if (globalLight == null)
            {
                Debug.LogError("[LightingManager] Global Light is not assigned!");
            }

            // 应用初始光照（根据当前阶段）
            if (DayLoopManager.Instance != null)
            {
                if (DayLoopManager.Instance.currentPhase == GamePhase.BuildPhase)
                {
                    ApplyBuildPhaseLighting();
                    // BuildPhase 时 Freeform lights 应关闭
                    DisableFreeformLights();
                }
                else
                {
                    OnStoreOpenHandler();
                    // OpenPhase 时 Freeform lights 应开启
                    EnableFreeformLights();
                }
            }
            else
            {
                // 如果 DayLoopManager 尚未初始化，默认关闭 Freeform lights
                DisableFreeformLights();
            }
        }

        private void OnDisable()
        {
            // 取消订阅
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= ApplyBuildPhaseLighting;
                DayLoopManager.Instance.OnStoreOpen -= OnStoreOpenHandler;
                DayLoopManager.Instance.OnDailySettlement -= OnDailySettlementHandler;
            }
        }

        private void Update()
        {
            // 仅在 OpenPhase 时动态更新光照
            if (DayLoopManager.Instance == null) return;
            if (DayLoopManager.Instance.currentPhase != GamePhase.OpenPhase) return;
            if (globalLight == null) return;

            UpdateDynamicLighting();
        }

        #region Lighting Application

        /// <summary>
        /// 应用 BuildPhase 静态光照
        /// </summary>
        private void ApplyBuildPhaseLighting()
        {
            if (globalLight == null) return;

            globalLight.color = buildPhaseColor;
            globalLight.intensity = buildPhaseIntensity;

            // 闭店时关闭所有 Freeform lights（BuildPhase 开始即闭店状态）
            DisableFreeformLights();

            Debug.Log($"[LightingManager] Applied BuildPhase lighting: Color={buildPhaseColor}, Intensity={buildPhaseIntensity}");
        }

        /// <summary>
        /// 开店事件处理（OpenPhase 开始）
        /// </summary>
        private void OnStoreOpenHandler()
        {
            // OpenPhase 的光照将在 Update() 中动态更新
            // 开店时启用所有 Freeform lights
            EnableFreeformLights();

            Debug.Log("[LightingManager] Store opened, dynamic lighting enabled and freeform lights turned on.");
        }

        /// <summary>
        /// 结算界面显示时关闭灯光
        /// </summary>
        private void OnDailySettlementHandler(DailySettlementData data)
        {
            // 显示结算界面时关闭所有 Freeform lights
            DisableFreeformLights();

            Debug.Log("[LightingManager] Daily settlement shown, freeform lights turned off.");
        }

        /// <summary>
        /// 更新 OpenPhase 动态光照
        /// </summary>
        private void UpdateDynamicLighting()
        {
            float normalizedTime = GetNormalizedOpenTime();

            // 全局光渐变
            globalLight.color = openPhaseColorGradient.Evaluate(normalizedTime);
            globalLight.intensity = useIntensityCurve && openPhaseIntensityCurve != null
                ? openPhaseIntensityCurve.Evaluate(normalizedTime)
                : 1.0f;

            // tile 灯光渐变：每帧只 Evaluate 一次，缓存颜色/强度
            if (tileLights.Count > 0)
            {
                Color tileColor = tileLightColorGradient != null
                    ? tileLightColorGradient.Evaluate(normalizedTime)
                    : Color.white;
                float tileIntensity = useTileIntensityCurve && tileLightIntensityCurve != null
                    ? tileLightIntensityCurve.Evaluate(normalizedTime)
                    : tileLightBaseIntensity;

                for (int i = tileLights.Count - 1; i >= 0; i--)
                {
                    var light = tileLights[i];
                    if (light == null)
                    {
                        tileLights.RemoveAt(i);
                        tileLightSet.Remove(light);
                        continue;
                    }
                    light.color = tileColor;
                    light.intensity = tileIntensity;
                }
            }
        }

        /// <summary>
        /// 计算营业时间段的归一化时间 [0, 1]，直接读取 DayLoopManager 的营业时间
        /// </summary>
        private float GetNormalizedOpenTime()
        {
            var dlm = DayLoopManager.Instance;
            float normalizedTime = Mathf.InverseLerp(dlm.StoreOpenHour, dlm.StoreCloseHour, dlm.currentHour);
            return Mathf.Clamp01(normalizedTime);
        }

        /// <summary>
        /// 启用所有 Freeform lights 和 Tile lights
        /// </summary>
        private void EnableFreeformLights()
        {
            foreach (var light in freeformLights)
                if (light != null) light.enabled = true;
            foreach (var light in tileLights)
                if (light != null) light.enabled = true;
        }

        /// <summary>
        /// 禁用所有 Freeform lights 和 Tile lights
        /// </summary>
        private void DisableFreeformLights()
        {
            foreach (var light in freeformLights)
                if (light != null) light.enabled = false;
            foreach (var light in tileLights)
                if (light != null) light.enabled = false;
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取全局光照引用
        /// </summary>
        public Light2D GetGlobalLight()
        {
            return globalLight;
        }

        /// <summary>
        /// 获取所有非全局光照列表
        /// </summary>
        public List<Light2D> GetFreeformLights()
        {
            return freeformLights;
        }

        /// <summary>
        /// 获取当前光照颜色
        /// </summary>
        public Color GetCurrentLightColor()
        {
            return globalLight != null ? globalLight.color : Color.white;
        }

        /// <summary>
        /// 获取当前光照强度
        /// </summary>
        public float GetCurrentLightIntensity()
        {
            return globalLight != null ? globalLight.intensity : 1.0f;
        }

        /// <summary>
        /// 运行时设置 BuildPhase 光照（高级功能）
        /// </summary>
        public void SetBuildPhaseLighting(Color color, float intensity)
        {
            buildPhaseColor = color;
            buildPhaseIntensity = intensity;

            if (DayLoopManager.Instance != null && DayLoopManager.Instance.currentPhase == GamePhase.BuildPhase)
            {
                ApplyBuildPhaseLighting();
            }
        }

        /// <summary>
        /// 运行时覆盖颜色渐变（高级功能）
        /// </summary>
        public void OverrideGradient(Gradient newGradient)
        {
            if (newGradient != null)
            {
                openPhaseColorGradient = newGradient;
                Debug.Log("[LightingManager] Gradient overridden.");
            }
        }

        #endregion

        #region Tile Light Registration

        /// <summary>
        /// 注册 FloorTile 灯光（由 FloorTileLighting 自动调用）
        /// </summary>
        public void RegisterTileLights(List<Light2D> lights)
        {
            bool isOpenPhase = DayLoopManager.Instance != null
                && DayLoopManager.Instance.currentPhase == GamePhase.OpenPhase;
            float t = isOpenPhase ? GetNormalizedOpenTime() : 0f;

            foreach (var light in lights)
            {
                if (light == null || !tileLightSet.Add(light)) continue;
                tileLights.Add(light);

                if (isOpenPhase)
                {
                    light.enabled = true;
                    ApplyTileLightValues(light, t);
                }
                else
                {
                    light.enabled = false;
                }
            }
        }

        /// <summary>
        /// 注销 FloorTile 灯光（由 FloorTileLighting 自动调用）
        /// </summary>
        public void UnregisterTileLights(List<Light2D> lights)
        {
            foreach (var light in lights)
            {
                if (tileLightSet.Remove(light))
                    tileLights.Remove(light);
            }
        }

        /// <summary>
        /// 对单个 tile 灯光应用当前渐变值
        /// </summary>
        private void ApplyTileLightValues(Light2D light, float normalizedTime)
        {
            if (tileLightColorGradient != null)
                light.color = tileLightColorGradient.Evaluate(normalizedTime);
            light.intensity = useTileIntensityCurve && tileLightIntensityCurve != null
                ? tileLightIntensityCurve.Evaluate(normalizedTime)
                : tileLightBaseIntensity;
        }

        #endregion

        #region Initialization Helpers

        /// <summary>
        /// 初始化默认渐变（如果 Inspector 中未配置）
        /// </summary>
        private void InitializeDefaultGradient()
        {
            openPhaseColorGradient = new Gradient();

            // 默认颜色关键帧（示例）
            GradientColorKey[] colorKeys = new GradientColorKey[4];
            colorKeys[0] = new GradientColorKey(new Color(1.0f, 0.95f, 0.8f), 0.0f);  // 12:00 正午暖黄
            colorKeys[1] = new GradientColorKey(new Color(1.0f, 0.85f, 0.6f), 0.3f);  // ~15:00 下午
            colorKeys[2] = new GradientColorKey(new Color(1.0f, 0.6f, 0.3f), 0.6f);   // ~18:00 黄昏橙
            colorKeys[3] = new GradientColorKey(new Color(0.3f, 0.2f, 0.4f), 1.0f);   // 23:00 深夜蓝紫

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);

            openPhaseColorGradient.SetKeys(colorKeys, alphaKeys);

            Debug.Log("[LightingManager] Initialized default gradient.");
        }

        /// <summary>
        /// 初始化默认 tile 灯光渐变（暖白→暖橙→冷蓝，与全局光不同以突出层次）
        /// </summary>
        private void InitializeDefaultTileGradient()
        {
            tileLightColorGradient = new Gradient();

            var colorKeys = new GradientColorKey[4];
            colorKeys[0] = new GradientColorKey(new Color(0.8f, 0.95f, 1.0f), 0.0f);  // 12:00 冷白
            colorKeys[1] = new GradientColorKey(new Color(1.0f, 0.9f, 0.7f), 0.3f);   // ~15:00 暖黄
            colorKeys[2] = new GradientColorKey(new Color(1.0f, 0.7f, 0.4f), 0.6f);   // ~18:00 暖橙
            colorKeys[3] = new GradientColorKey(new Color(0.4f, 0.5f, 0.8f), 1.0f);   // 23:00 冷蓝

            var alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);

            tileLightColorGradient.SetKeys(colorKeys, alphaKeys);

            Debug.Log("[LightingManager] Initialized default tile light gradient.");
        }

        #endregion
    }
}
