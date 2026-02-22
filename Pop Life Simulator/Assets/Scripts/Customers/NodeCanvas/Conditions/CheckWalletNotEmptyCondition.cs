using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Conditions
{
    [Category("PopLife/Customer")]
    [Description("检查顾客钱包是否还有钱 (moneyBag > 0)")]
    public class CheckWalletNotEmptyCondition : ConditionTask
    {
        protected override string info
        {
            get { return "钱包还有钱?"; }
        }

        protected override bool OnCheck()
        {
            var adapter = agent.GetComponent<CustomerBlackboardAdapter>();
            if (adapter == null) return false;

            return adapter.moneyBag > 0;
        }
    }
}
