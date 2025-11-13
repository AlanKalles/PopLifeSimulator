using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PopLife
{
    /// <summary>
    /// 光照管理器 - 管理场景中的所有 Light2D 组件
    /// 根据 DayLoopManager 的游戏阶段动态调整光照
    /// </summary>
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
        [Tooltip("营业开始小时")]
        [SerializeField] private float openHourStart = 12f;
        [Tooltip("营业结束小时")]
        [SerializeField] private float openHourEnd = 23f;

        [Header("Advanced Settings")]
        [Tooltip("是否使用强度曲线（关闭则使用固定强度 1.0）")]
        [SerializeField] private bool useIntensityCurve = false;

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
            {
                InitializeDefaultGradient();
            }

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

            // 应用颜色渐变
            Color targetColor = openPhaseColorGradient.Evaluate(normalizedTime);
            globalLight.color = targetColor;

            // 应用强度曲线（可选）
            if (useIntensityCurve && openPhaseIntensityCurve != null)
            {
                float targetIntensity = openPhaseIntensityCurve.Evaluate(normalizedTime);
                globalLight.intensity = targetIntensity;
            }
            else
            {
                globalLight.intensity = 1.0f; // 固定强度
            }
        }

        /// <summary>
        /// 计算营业时间段的归一化时间 [0, 1]
        /// </summary>
        private float GetNormalizedOpenTime()
        {
            float currentHour = DayLoopManager.Instance.currentHour;
            float normalizedTime = Mathf.InverseLerp(openHourStart, openHourEnd, currentHour);
            return Mathf.Clamp01(normalizedTime);
        }

        /// <summary>
        /// 启用所有 Freeform lights
        /// </summary>
        private void EnableFreeformLights()
        {
            foreach (var light in freeformLights)
            {
                if (light != null)
                {
                    light.enabled = true;
                }
            }

            if (freeformLights.Count > 0)
            {
                Debug.Log($"[LightingManager] Enabled {freeformLights.Count} freeform lights.");
            }
        }

        /// <summary>
        /// 禁用所有 Freeform lights
        /// </summary>
        private void DisableFreeformLights()
        {
            foreach (var light in freeformLights)
            {
                if (light != null)
                {
                    light.enabled = false;
                }
            }

            if (freeformLights.Count > 0)
            {
                Debug.Log($"[LightingManager] Disabled {freeformLights.Count} freeform lights.");
            }
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

        #endregion
    }
}
