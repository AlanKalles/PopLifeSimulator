using UnityEngine;

namespace PopLife.Customers.Data
{
    [CreateAssetMenu(menuName = "PopLife/Customers/Trait")]
    public class Trait : ScriptableObject
    {
        public string traitId;
        public string displayNameKey;
        [TextArea] public string description;

        [Header("数值修饰（加法/乘法/覆盖）")]
        public float[] interestAdd;     // 与类别等长，可为空
        public float[] interestMul;     // 与类别等长，每个类别独立乘数，可为空（为空时视为全1）
        public float walletCapMul = 1f;
        public float patienceMul = 1f;
        public float embarrassmentCapMul = 1f;
        public float priceSensitivityMul = 1f;
        public float moveSpeedMul = 1f;

        [Header("经验影响")]
        [Tooltip("经验获取倍率，1.0为正常，大于1增加经验，小于1减少经验")]
        public float xpMultiplier = 1.0f;

        [Header("时间倾向")]
        [Tooltip("该特质偏好的时间段（可多个）")]
        public TimePreference[] preferredTimeRanges;

        [Tooltip("在偏好时间段内的权重倍率")]
        [Range(0f, 3f)]
        public float timePreferenceWeight = 1f;
    }
}