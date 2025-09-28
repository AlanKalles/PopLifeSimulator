using System;
using System.Collections.Generic;
using UnityEngine;


namespace PopLife.Customers.Data
{

[Serializable]
public class InterestArray
{
[Tooltip("兴趣对齐 ProductCategory 的索引长度")] public int[] values = Array.Empty<int>();
public void EnsureSize(int size, int defaultValue = 50)
{
if (values == null) values = Array.Empty<int>();
if (values.Length == size) return;
var newArr = new int[size];
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
public string archetypeId; // GUID/地址
public string displayNameKey;
public string defaultAppearancePresetId;
public Sprite portrait;


[Header("分布与移动")]
[Range(0, 1)] public float spawnWeight = 0.1f;
public float moveSpeed = 2.0f;
[Range(0, 300)] public int queueToleranceSeconds = 60;


[Header("兴趣与上限（基线）")]
public InterestArray baseInterest = new(); // 对齐你项目里的 ProductCategory
public StatCurve walletCapCurve = new();
public StatCurve patienceCurve = new();
public StatCurve embarrassmentCapCurve = new();


[Header("默认行为策略集合")]
public BehaviorPolicySet defaultPolicies;


public int[] GetBaseInterestClamped(int categories)
{
baseInterest.EnsureSize(categories, 50);
var arr = new int[categories];
for (int i = 0; i < categories; i++) arr[i] = Mathf.Clamp(baseInterest.values[i], 0, 100);
return arr;
}
}

}