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
    }
}