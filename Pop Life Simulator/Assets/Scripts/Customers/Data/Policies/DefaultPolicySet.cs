using UnityEngine;

namespace PopLife.Customers.Data
{
    /// <summary>
    /// 默认策略集合实现
    /// 继承自抽象的 BehaviorPolicySet，允许在 Unity 中创建 ScriptableObject 实例
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Policies/DefaultPolicySet", fileName = "PolicySet_Default")]
    public class DefaultPolicySet : BehaviorPolicySet
    {
        // 所有策略字段已在基类 BehaviorPolicySet 中定义
        // 这里只需要提供 CreateAssetMenu 让 Unity 能创建实例
    }
}