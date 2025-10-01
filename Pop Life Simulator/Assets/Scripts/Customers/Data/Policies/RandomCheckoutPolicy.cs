using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Customers.Data
{
    /// <summary>
    /// 随机收银台选择策略
    /// 随机选择一个可用的收银台
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Policies/Checkout/RandomCheckout", fileName = "RandomCheckoutPolicy")]
    public class RandomCheckoutPolicy : CheckoutPolicy
    {
        [Header("调试选项")]
        public bool enableDebugLog = false;

        /// <summary>
        /// 随机选择一个收银台
        /// </summary>
        /// <param name="ctx">顾客上下文信息</param>
        /// <param name="cashiers">所有可用收银台</param>
        /// <returns>选中收银台的索引，-1表示没有可用收银台</returns>
        public override int ChooseCashier(in CustomerContext ctx, List<CashierSnapshot> cashiers)
        {
            if (cashiers == null || cashiers.Count == 0)
            {
                if (enableDebugLog)
                    Debug.LogWarning($"[RandomCheckout] 顾客 {ctx.customerId}: 没有可用收银台");
                return -1;
            }

            // 随机选择
            int selectedIndex = Random.Range(0, cashiers.Count);

            if (enableDebugLog)
            {
                Debug.Log($"[RandomCheckout] 顾客 {ctx.customerId} 随机选择了收银台 #{selectedIndex}");
            }

            return selectedIndex;
        }
    }
}