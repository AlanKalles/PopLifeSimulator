using System;
using System.Collections.Generic;
using UnityEngine;


namespace PopLife.Customers.Data
{

[Serializable]
public class InterestArray
{
[Tooltip("兴趣对齐 ProductCategory 的索引长度")] public float[] values = Array.Empty<float>();
public void EnsureSize(int size, float defaultValue = 2f)
{
if (values == null) values = Array.Empty<float>();
if (values.Length == size) return;
var newArr = new float[size];
for (int i = 0; i < size; i++) newArr[i] = (i < values.Length) ? values[i] : defaultValue;
values = newArr;
}
}


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
[Tooltip("该原型可被生成的时间窗口")]
public TimePreference spawnTimeWindow = new TimePreference
{
    startHour = 12f,   // 默认开店时间
    endHour = 22.5f    // 默认闭店前半小时
};


[Header("兴趣与上限（基线）")]
public InterestArray baseInterest = new(); // 对齐你项目里的 ProductCategory
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
    new() { minSpent = 0,  maxSpent = 0,   multiplier = 0f },
    new() { minSpent = 1,  maxSpent = 15,  multiplier = 1.2f },
    new() { minSpent = 16, maxSpent = 25,  multiplier = 1.4f },
    new() { minSpent = 26, maxSpent = 45,  multiplier = 1.6f },
    new() { minSpent = 46, maxSpent = -1,  multiplier = 1.8f }
};

[Header("等级系统")]
[Tooltip("累积经验阈值，达到阈值[i]时升到等级i+1")]
public int[] levelUpThresholds = new int[] { 100, 250, 500, 1000 };


public float[] GetBaseInterest(int categories)
{
baseInterest.EnsureSize(categories, 2f);
var arr = new float[categories];
for (int i = 0; i < categories; i++) arr[i] = Mathf.Max(baseInterest.values[i], 0f);
return arr;
}

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