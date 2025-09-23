# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 重要说明
**所有对话请使用中文进行**

## 项目类型
Unity 2D 成人商店模拟经营游戏（"Pop Life Simulator"）

## Unity 版本和构建命令
- Unity 项目位置：`Pop Life Simulator/`
- 通过 Unity 编辑器构建：File → Build Settings → Build
- 运行模式：Unity 编辑器 Play 按钮或 Ctrl+P

## 架构概述

### 核心系统
游戏采用模块化架构，各层职责清晰：

1. **数据层** (`Assets/Scripts/Data/`)
   - `BuildingArchetypes.cs`：所有建筑的基础原型系统，支持等级进阶
   - `FacilityArchetype.cs`：提供特殊效果的设施（收银台、ATM等）
   - `ShelfArchetypes.cs`：商品展示货架，按类别区分

2. **运行时层** (`Assets/Scripts/Runtime/`)
   - `FloorGrid.cs`：基于网格的建筑放置系统，含碰撞检测和原点偏移支持
   - `ConstructionManager.cs`：处理建筑放置/移动模式，含预览功能
   - `BuildingInstances.cs`：建筑的运行时实例
   - 事务式放置系统：失败时自动回滚资源

3. **管理器层** (`Assets/Scripts/Manager/`)
   - 各游戏系统的单例管理器（音频、蓝图、资源、UI等）
   - `FloorManager`：控制活跃楼层（当前为单层原型）

### 关键设计模式
- **原型-实例模式**：ScriptableObject 原型定义建筑模板，运行时实例处理游戏逻辑
- **网格系统**：2D 网格放置，支持占地面积验证
- **事务模式**：建筑放置采用原子事务，支持资源回滚
- **旋转系统**：建筑支持 90 度旋转，自动重算占地面积
- **原点系统**：FloorGrid 支持自定义原点位置，便于场景布局

## 重要实现细节
- 商品类别：Lingerie（内衣）、Condom（避孕套）、Vibrator（振动器）、Fleshlights（飞机杯）、Lubricant（润滑剂）
- 设施类型：Cashier（收银台）、AirConditioner（空调）、ATM（取款机）、SecurityCamera（监控）、MusicPlayer（音乐播放器）
- 效果类型：ReduceEmbarrassment（减少尴尬）、IncreaseAttractiveness（增加吸引力）、IncreaseCustomerSpeed（加快顾客速度）、RestoreMoney（恢复金钱）
- 建筑可能需要蓝图，支持多级升级路径
- 网格单元跟踪占用状态和"尴尬值"供 AI 寻路使用

## 文件约定
- 跳过 `Assets/ThirdParty/` 文件夹 - 包含第三方包
- `.meta` 文件是 Unity 元数据（自动生成）
- 命名空间：`PopLife.Data`、`PopLife.Runtime`
- 代码文件使用英文命名，注释可用中文