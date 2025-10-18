# Customer Level Up System - Designer Guide

**Version**: 1.0
**Last Updated**: 2025-10-13

---

## Overview

This system allows customers to gain experience (XP) and level up based on their purchases in your store. Higher-level customers are more loyal and valuable. This guide explains how to configure the system without coding.

---

## How It Works (Simple Explanation)

```
Customer visits store → Purchases items → Checks out → Leaves store
                                                          ↓
                                         System calculates XP earned
                                                          ↓
                                    XP added to customer's total XP
                                                          ↓
                              If total XP reaches threshold → Level Up!
```

**Key Rule**: Customers only earn XP if they actually spend money. No purchase = No XP.

---

## ⚙️ Configuration Locations

All settings are found in Unity Inspector when you select a **Customer Archetype** asset:

1. Navigate to: `Assets/Resources/ScriptableObjects/CustomerArchetypes/`
2. Select any customer archetype (e.g., "Student", "Office Worker")
3. Look for these sections in the Inspector:
   - **Experience System** (经验值系统)
   - **Level System** (等级系统)

---

## Setting 1: Base XP Gain

**What it does**: The starting amount of XP a customer earns per visit (before multipliers).

**Location**: `Base Xp Gain` field in Customer Archetype Inspector

**Default Value**: `10`

**How to adjust**:
- **Higher value** (e.g., 20) = Customers level up faster
- **Lower value** (e.g., 5) = Slower progression, more visits needed

---

## Setting 2: Spending Thresholds (Experience Multipliers)

**What it does**: Rewards customers who spend more money with bonus XP multipliers.

**Location**: `Spending Thresholds` array in Customer Archetype Inspector

### Default Configuration

| Money Spent | XP Multiplier | Description |
|-------------|---------------|-------------|
| $0 | 0× | No purchase = No XP |
| $1 - $15 | 1.2× | Small purchase (1-2 items) |
| $16 - $25 | 1.4× | Medium purchase (~3 items) |
| $26 - $45 | 1.6× | Large purchase (4-5 items) |
| $46+ | 1.8× | Huge purchase (6+ items, capped) |

### How to Edit

Each threshold has 3 fields:
1. **Min Spent**: Minimum money spent to qualify
2. **Max Spent**: Maximum money for this tier (-1 = unlimited)
3. **Multiplier**: XP bonus (1.0 = normal, 1.5 = +50% bonus)

---

## Setting 3: Level Up Thresholds

**What it does**: Defines how much **total XP** is needed to reach each level.

**Location**: `Level Up Thresholds` array in Customer Archetype Inspector

### Default Configuration

| Level | Total XP Required | Approximate Visits* |
|-------|-------------------|---------------------|
| Level 0 → 1 | 100 XP | 5-10 visits |
| Level 1 → 2 | 250 XP | 12-20 visits total |
| Level 2 → 3 | 500 XP | 25-40 visits total |
| Level 3 → 4 | 1000 XP | 50-80 visits total |

*Assuming 10-20 XP per visit with average spending

**Important**: This is **cumulative**, NOT per-level!
- A customer needs **250 total XP** to reach Level 2 (not 250 more after Level 1)
- XP never resets - it keeps accumulating

---

## Setting 4: Trait XP Multipliers

**What it does**: Certain personality traits can increase or decrease XP gain.

**Location**: Individual `Trait` assets in `Assets/Resources/ScriptableObjects/Traits/`

**Field Name**: `Xp Multiplier` (经验获取倍率)

**Default Value**: `1.0` (normal)

**How Traits Stack**:
If a customer has multiple traits:
- Student (1.5×) + Night Owl (1.2×) = **1.8× total** (multiplied together)
- Shy (0.8×) + Grumpy (0.7×) = **0.56× total** (both penalties apply)

---

## How XP is Calculated (The Formula)

```
Final XP Earned = Base XP × Trait Multiplier × Spending Multiplier
```

### Real Examples

**Example 1: Regular Customer**
- Base XP: 10
- Traits: None (1.0×)
- Money Spent: $30
- Spending Multiplier: 1.6× (falls in $26-45 range)

**Calculation**: 10 × 1.0 × 1.6 = **16 XP**

---

## Quick Balancing Guide

### Problem: Customers level up too slowly
**Solutions**:
1. Increase `Base Xp Gain` (10 → 15)
2. Lower `Level Up Thresholds` (100 → 75)
3. Increase `Spending Multipliers` (1.2 → 1.5)
4. Add positive trait multipliers (1.0 → 1.3)

---

### Problem: Customers reach max level too quickly
**Solutions**:
1. Add more level thresholds (4 levels → 8 levels)
2. Increase higher-level thresholds exponentially
3. Keep Base XP low (10 or below)
4. Add more "slow learner" traits with low multipliers

---

### Problem: Players ignore low-spending customers
**Solutions**:
1. Increase small-purchase multiplier (1.2 → 1.5)
2. Add special traits to low spenders (e.g., "Frugal" with 1.8× XP)
3. Lower Level 1 threshold (100 → 50) so they level up once quickly

---

## Testing Your Changes

### In Unity Editor:
1. **Start Play Mode**
2. **Open Customer Spawner** (manually spawn test customers)
3. **Watch them shop and leave**
4. **Check Daily Settlement Panel** - shows who leveled up today
5. **Inspect Customer Data**:
   - Open `StreamingAssets/Customers.json` after a few visits
   - Look for `xp` and `loyaltyLevel` values
   - Verify calculations match expectations
---

## Common Mistakes

### Setting Max Spent Incorrectly
**Wrong**: `Max Spent = 15` for last tier
**Right**: `Max Spent = -1` (unlimited) for last tier

### Overlapping Thresholds
**Wrong**:
- Tier 1: Min=1, Max=20
- Tier 2: Min=15, Max=30 (overlaps!)

**Right**:
- Tier 1: Min=1, Max=15
- Tier 2: Min=16, Max=30

### Forgetting to Save Changes
Always click **Apply** in Inspector after editing!

### Setting Base XP to 0
If Base XP = 0, no customer can ever earn XP (even with multipliers)

---

## Summary Cheat Sheet

| Setting | Controls | Typical Range | Impact |
|---------|----------|---------------|--------|
| **Base XP Gain** | Starting XP per visit | 5-20 | Overall progression speed |
| **Spending Thresholds** | Reward for big purchases | 1.0-2.0× | Encourages higher spending |
| **Level Thresholds** | XP needed per level | 50-5000 | Long-term progression curve |
| **Trait XP Multiplier** | Character-specific bonus | 0.5-2.5× | Personality diversity |
