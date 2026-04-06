using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PopLife.Runtime
{
    /// <summary>
    /// FloorTile 灯光自注册组件
    /// 扫描 LightGroup 子物体下的 Light2D，自动注册/注销到 LightingManager
    /// </summary>
    [RequireComponent(typeof(FloorTileInstance))]
    public class FloorTileLighting : MonoBehaviour
    {
        [Tooltip("拖入 LightGroup 子物体下的所有 Light2D")]
        [SerializeField] private List<Light2D> lights = new();

        private bool registered;

        private void Start()
        {
            Register();
        }

        private void OnEnable()
        {
            // Start 首次注册；OnEnable 处理 disable→re-enable 周期
            if (!registered)
                return;
            Register();
        }

        private void OnDisable()
        {
            if (!registered) return;
            LightingManager.Instance?.UnregisterTileLights(lights);
            registered = false;
        }

        private void Register()
        {
            if (lights.Count == 0) return;
            LightingManager.Instance?.RegisterTileLights(lights);
            registered = true;
        }
    }
}
