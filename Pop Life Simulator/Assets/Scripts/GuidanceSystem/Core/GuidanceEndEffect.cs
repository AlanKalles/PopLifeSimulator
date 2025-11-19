using UnityEngine;
using PopLife.Manager;

namespace PopLife.GuidanceSystem.Core
{
    /// <summary>
    /// 结束效果类型枚举
    /// </summary>
    public enum EndEffectType
    {
        /// <summary>
        /// 无效果
        /// </summary>
        None,

        /// <summary>
        /// 给予奖励（金钱/声望）
        /// </summary>
        Reward,

        /// <summary>
        /// 触发TutorialMarker
        /// </summary>
        Marker
    }

    /// <summary>
    /// Guidance结束时触发的效果配置
    /// </summary>
    [System.Serializable]
    public class GuidanceEndEffect
    {
        [Tooltip("效果类型")]
        public EndEffectType effectType = EndEffectType.None;

        [Header("奖励配置 (仅Reward时生效)")]
        [Tooltip("金钱奖励数量")]
        public int moneyReward;

        [Tooltip("声望奖励数量")]
        public int fameReward;

        [Header("Marker配置 (仅Marker时生效)")]
        [Tooltip("要触发的TutorialMarker")]
        public TutorialMarker markerToTrigger;

        /// <summary>
        /// 执行结束效果
        /// </summary>
        public void Execute()
        {
            switch (effectType)
            {
                case EndEffectType.Reward:
                    if (moneyReward > 0 && ResourceManager.Instance != null)
                    {
                        ResourceManager.Instance.AddMoney(moneyReward);
                    }
                    if (fameReward > 0 && ResourceManager.Instance != null)
                    {
                        ResourceManager.Instance.AddFame(fameReward);
                    }
                    break;

                case EndEffectType.Marker:
                    TutorialEventBus.RaiseMarker(markerToTrigger);
                    break;

                case EndEffectType.None:
                default:
                    break;
            }
        }
    }
}
