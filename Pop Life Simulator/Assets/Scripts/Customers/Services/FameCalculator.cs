using UnityEngine;
using UnityEngine.Serialization;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// Fame计算服务 - 作为MonoBehaviour挂载以支持Inspector调参
    /// 每次顾客从货架取货时计算获得的Fame值
    /// </summary>
    public class FameCalculator : MonoBehaviour
    {
        public static FameCalculator Instance { get; private set; }

        [Header("Fame计算参数")]
        [Tooltip("商品价格权重")]
        [SerializeField] private float priceWeight = 0.05f;

        [FormerlySerializedAs("attractivenessWeight")]
        [Tooltip("货架吸引力权重")]
        [SerializeField] private float appealWeight = 0.08f;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        /// <summary>
        /// 计算单次购买获得的Fame
        /// </summary>
        /// <param name="price">商品价格</param>
        /// <param name="appeal">货架吸引力</param>
        /// <returns>获得的Fame值</returns>
        public float Calculate(int price, float appeal)
        {
            return price * priceWeight + appeal * appealWeight;
        }

        /// <summary>
        /// 静态便捷方法（使用Instance调用）
        /// </summary>
        public static float CalculateFame(int price, float appeal)
        {
            if (Instance == null)
            {
                // 降级：使用默认参数
                Debug.LogWarning("[FameCalculator] Instance not found, using default values");
                return price * 0.05f + appeal * 0.08f;
            }
            return Instance.Calculate(price, appeal);
        }
    }
}
