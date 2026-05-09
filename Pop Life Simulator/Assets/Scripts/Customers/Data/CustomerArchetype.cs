using System;
using UnityEngine;


namespace PopLife.Customers.Data
{


[Serializable]
public class StatCurve
{
[Tooltip("按熟客等级采样的曲线，x=等级, y=数值")] public AnimationCurve curve = AnimationCurve.Linear(1, 1, 10, 1);
public float Eval(int level) => curve.Evaluate(level);
}


// —— 可组合的策略引用容器 ——
public abstract class BehaviorPolicySet : ScriptableObject
{
public TargetSelectorPolicy targetSelector;
public PurchasePolicy purchase;
public QueuePolicy queueing;
public PathPolicy path;
public EmbarrassmentPolicy embarrassment;
public CheckoutPolicy checkout;
}


[CreateAssetMenu(menuName = "PopLife/Customers/Archetype")]
public class CustomerArchetype : ScriptableObject
{
[Header("标识与外观")]
public string archetypeId; 
public string displayNameKey;
public string defaultAppearancePresetId;
public Sprite portrait;


[Header("分布与移动")]
[Range(0, 1)] public float spawnWeight = 0.1f;
public float moveSpeed = 2.0f;
[Range(0, 300)] public int queueToleranceSeconds = 60;

[Header("生成时间控制")]
[Tooltip("该原型可被生成的时间窗口（支持跨日，如27.0表示次日03:00）")]
public TimePreference spawnTimeWindow = new TimePreference
{
    startHour = 12f,   // 默认开店时间
    endHour = 22.5f    // 默认闭店前半小时（可设置为27.0以支持跨日营业）
};


[Header("上限（基线）")]
public StatCurve walletCapCurve = new();
public StatCurve patienceCurve = new();
public StatCurve embarrassmentCapCurve = new();


[Header("默认行为策略集合")]
public BehaviorPolicySet defaultPolicies;

[Header("经验值系统")]
[Tooltip("基础经验值增量")]
public float baseXpGain = 10f;

[Tooltip("消费金额对应的经验乘数阈值")]
public SpendingThreshold[] spendingThresholds = new SpendingThreshold[]
{
    new() { minSpent = 0,   maxSpent = 0,   multiplier = 0f },
    new() { minSpent = 1,   maxSpent = 30,  multiplier = 1.2f },
    new() { minSpent = 31,  maxSpent = 80,  multiplier = 1.4f },
    new() { minSpent = 81,  maxSpent = 150, multiplier = 1.6f },
    new() { minSpent = 151, maxSpent = -1,  multiplier = 1.8f }
};

[Header("等级系统")]
[Tooltip("累积经验阈值，达到阈值[i]时升到等级i+1")]
public int[] levelUpThresholds = new int[] { 80, 200, 400, 700 };


/// <summary>
/// 根据消费金额获取对应的经验乘数
/// </summary>
public float GetSpendingMultiplier(int moneySpent)
{
foreach (var threshold in spendingThresholds)
{
if (moneySpent >= threshold.minSpent &&
    (threshold.maxSpent == -1 || moneySpent <= threshold.maxSpent))
{
    return threshold.multiplier;
}
}
return 1.0f;
}
}


[Serializable]
public class SpendingThreshold
{
public int minSpent;
public int maxSpent;  // -1 表示无上限
public float multiplier;
}

}