# Narrative Quest Activations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire up trigger markers for Auditor/3–9, BDSM/1, VIP/SoloFans/1–4, and VIP/ShyGirl/1–3 so each conversation fires at the right game moment without overlapping other events.

**Architecture:** Each conversation is activated by a `DialogueAction` ScriptableObject (auto-loaded from `Resources/ScriptableObjects/DialogueActions/`) whose `activationMarker` is listened to by `TutorialMarkerBridge`. Auditor/3–9 are quest-completion-gated. VIP/BDSM first visits are day-gated; follow-up visits are quest-completion-gated. A new `failureMarker` field on `QuestDefinition` lets `Auditor/9` fire when `Main/8` deadline is missed.

**Tech Stack:** Unity 6, C#, YAML asset editing, `TutorialMarker` enum, `QuestDefinition`, `QuestLogicManager`, `TutorialMarkerBridge`

---

## Event calendar — the big picture

```
Day 1-2   Tutorial / Main/1 (First Shelf)
Day 3     Auditor/1 + Calendar tutorial (already live)
Day 5     Auditor/2 DDL check (already live)
          ↓ player works through Main/2 (5-shelf quest)
          Auditor/3 → fires when Main/2 completes  ← quest-gated
          ↓ player works through Main/4
          Auditor/4 → fires when Main/4 completes  ← quest-gated
Day 8     VIP/SoloFans/1 — Omega first visit       ← day-gated
          ↓ player works through Main/5, Main/6
          Auditor/5 / Auditor/6 → fire on completions ← quest-gated
Day 12    VIP/ShyGirl/1 — Hana first visit         ← day-gated
          ↓ player works through Main/7
          Auditor/7 → fires on Main/7 completion   ← quest-gated
Day 15    BDSM/1 — Henya first visit               ← day-gated
          ↓ player works through Main/8 (Cucci removal)
          Auditor/8 (success) or Auditor/9 (failure) ← quest-gated
          ↓ VIP chains continue from quest completions
```

Day-gated slots used: **8, 12, 15** (Days 2, 3, 4, 10 left free — no new events there)

---

## Full trigger mapping

| Conversation | Trigger | Marker int | New? |
|---|---|---|---|
| Auditor/3 | Main/2 completes (5 shelves) | 71 `a_FiveshelfC` | existing |
| Auditor/4 | Main/4 completes (200 vibrators) | 104 `Main4Completed` | new |
| Auditor/5 | Main/5 completes (member loyalty) | 105 `Main5Completed` | new |
| Auditor/6 | Main/6 completes (holiday revenue) | 106 `Main6Completed` | new |
| Auditor/7 | Main/7 completes (tourist surge) | 107 `Main7Completed` | new |
| Auditor/8 | Main/8 completes (Cucci removed) | 108 `Main8Completed` | new |
| Auditor/9 | Main/8 deadline missed | 109 `Main8DeadlineMissed` | new |
| VIP/SoloFans/1 | Day 8 build phase | 35 `Day8Trigger` | existing |
| VIP/SoloFans/2 | VIPSoloFans1 quest completes | 110 `VIPSoloFans1Completed` | new |
| VIP/SoloFans/3 | VIPSoloFans2 quest completes | 111 `VIPSoloFans2Completed` | new |
| VIP/SoloFans/4 | VIPSoloFans3 quest completes | 112 `VIPSoloFans3Completed` | new |
| VIP/ShyGirl/1 | Day 12 build phase | 37 `Day12Trigger` | existing |
| VIP/ShyGirl/2 | VIPShyGirl1 quest completes | 113 `VIPShyGirl1Completed` | new |
| VIP/ShyGirl/3 | VIPShyGirl2 quest completes | 114 `VIPShyGirl2Completed` | new |
| BDSM/1 | Day 15 build phase | 38 `Day15Trigger` | existing |

---

## Files to create or modify

**Code changes:**
- `Assets/Scripts/Manager/TutorialMarker.cs` — add 11 new markers (104–114)
- `Assets/Scripts/Data/QuestDefinition.cs` — add `failureMarker` serialized field + property
- `Assets/Scripts/Quest/QuestLogicManager.cs` — call `RaiseMarker(def.FailureMarker)` in `FailQuest()`

**Quest SO edits (set completionMarker int field):**
- `Resources/ScriptableObjects/Quests/Main4.asset` → `completionMarker: 104`
- `Resources/ScriptableObjects/Quests/Main5.asset` → `completionMarker: 105`
- `Resources/ScriptableObjects/Quests/Main6.asset` → `completionMarker: 106`
- `Resources/ScriptableObjects/Quests/Main7.asset` → `completionMarker: 107`
- `Resources/ScriptableObjects/Quests/Main8.asset` → `completionMarker: 108`, add `failureMarker: 109`
- `Resources/ScriptableObjects/Quests/VIPSoloFans1.asset` → `completionMarker: 110`
- `Resources/ScriptableObjects/Quests/VIPSoloFans2.asset` → `completionMarker: 111`
- `Resources/ScriptableObjects/Quests/VIPSoloFans3.asset` → `completionMarker: 112`
- `Resources/ScriptableObjects/Quests/VIPShyGirl1.asset` → `completionMarker: 113`
- `Resources/ScriptableObjects/Quests/VIPShyGirl2.asset` → `completionMarker: 114`

**New DialogueAction SOs (15 files)** in `Resources/ScriptableObjects/DialogueActions/`:

| Filename | activationMarker | conversationToStart |
|---|---|---|
| `Auditor3Visit.asset` | 71 | Auditor/3 |
| `Auditor4Visit.asset` | 104 | Auditor/4 |
| `Auditor5Visit.asset` | 105 | Auditor/5 |
| `Auditor6Visit.asset` | 106 | Auditor/6 |
| `Auditor7Visit.asset` | 107 | Auditor/7 |
| `Auditor8Visit.asset` | 108 | Auditor/8 |
| `Auditor9Visit.asset` | 109 | Auditor/9 |
| `SoloFans1Visit.asset` | 35 | VIP/SoloFans/1 |
| `SoloFans2Visit.asset` | 110 | VIP/SoloFans/2 |
| `SoloFans3Visit.asset` | 111 | VIP/SoloFans/3 |
| `SoloFans4Visit.asset` | 112 | VIP/SoloFans/4 |
| `ShyGirl1Visit.asset` | 37 | VIP/ShyGirl/1 |
| `ShyGirl2Visit.asset` | 113 | VIP/ShyGirl/2 |
| `ShyGirl3Visit.asset` | 114 | VIP/ShyGirl/3 |
| `BDSM1Visit.asset` | 38 | BDSM/1 |

**Manual step in Pixel Crushers Dialogue Editor:**
- `Auditor/7` is missing its Lua script. Add to the last node's Script field:
  `SetQuestStateAfter("Main/8", "active"); RaiseMarkerAfter("Main8Activated");`

---

## How the system works

```
TutorialEventBus.RaiseMarker(marker)
  → TutorialMarkerBridge looks up DialogueAction SOs with matching activationMarker
  → starts conversationToStart via DialogueTriggerHelper

Quest completion path (all target quests have TriggerMarkerAfterToast = true):
  QuestLogicManager.CompleteQuest()
  → does NOT fire marker immediately (TriggerMarkerAfterToast = true)
  → QuestNotificationToast fires CompletionMarker after player dismisses toast
  → TutorialMarkerBridge receives it → starts next conversation

Quest failure path (Main/8 deadline missed):
  QuestLogicManager.FailQuest()
  → currently fires OnQuestFailed event only
  → Plan adds: RaiseMarker(def.FailureMarker) when FailureMarker != None
```

---

## Task 1: Add new TutorialMarker entries

**Files:**
- Modify: `Assets/Scripts/Manager/TutorialMarker.cs`

- [ ] **Step 1: Find the last enum entry (line ~177) and append 11 new markers**

Current last line:
```csharp
        a_TwentyshelfC,           // = 103
    }
```

Replace with:
```csharp
        a_TwentyshelfC,           // = 103

        // Auditor visit chain — fired by Main quest completion/failure
        Main4Completed,           // = 104  Main/4 done → triggers Auditor/4
        Main5Completed,           // = 105  Main/5 done → triggers Auditor/5
        Main6Completed,           // = 106  Main/6 done → triggers Auditor/6
        Main7Completed,           // = 107  Main/7 done → triggers Auditor/7
        Main8Completed,           // = 108  Main/8 done (Cucci removed) → triggers Auditor/8
        Main8DeadlineMissed,      // = 109  Main/8 expired → triggers Auditor/9

        // VIP SoloFans chain — each quest completion triggers next conversation
        VIPSoloFans1Completed,    // = 110  VIPSoloFans1 done → VIP/SoloFans/2
        VIPSoloFans2Completed,    // = 111  VIPSoloFans2 done → VIP/SoloFans/3
        VIPSoloFans3Completed,    // = 112  VIPSoloFans3 done → VIP/SoloFans/4

        // VIP ShyGirl chain
        VIPShyGirl1Completed,     // = 113  VIPShyGirl1 done → VIP/ShyGirl/2
        VIPShyGirl2Completed,     // = 114  VIPShyGirl2 done → VIP/ShyGirl/3
    }
```

- [ ] **Step 2: Verify count** — `None = -1` is the anchor. Count up from `GameStarted = 0`. The 104th entry (0-indexed) should be `Main4Completed`. If the count is off, the SO int values in later tasks will be wrong. Count manually or use: `grep -c "^\s*[A-Za-z]" TutorialMarker.cs`

- [ ] **Step 3: Commit**
```bash
git add "Pop Life Simulator/Assets/Scripts/Manager/TutorialMarker.cs"
git commit -m "feat: add completion/failure markers for Auditor, VIPSoloFans, VIPShyGirl chains"
```

---

## Task 2: Add failureMarker to QuestDefinition

**Files:**
- Modify: `Assets/Scripts/Data/QuestDefinition.cs`

- [ ] **Step 1: Find the `completionMarker` serialized field and add `failureMarker` directly after it**

Find:
```csharp
        [SerializeField] private TutorialMarker completionMarker;
```

Add on the next line:
```csharp
        [SerializeField] private TutorialMarker failureMarker;
```

- [ ] **Step 2: Find the `CompletionMarker` property (around line 114) and add `FailureMarker` after it**

Find:
```csharp
        public TutorialMarker CompletionMarker => completionMarker;
        public bool TriggerMarkerAfterToast => triggerMarkerAfterToast;
```

Replace with:
```csharp
        public TutorialMarker CompletionMarker => completionMarker;
        public TutorialMarker FailureMarker => failureMarker;
        public bool TriggerMarkerAfterToast => triggerMarkerAfterToast;
```

- [ ] **Step 3: Commit**
```bash
git add "Pop Life Simulator/Assets/Scripts/Data/QuestDefinition.cs"
git commit -m "feat: add failureMarker to QuestDefinition for deadline-missed dialogue triggers"
```

---

## Task 3: Fire failureMarker in QuestLogicManager

**Files:**
- Modify: `Assets/Scripts/Quest/QuestLogicManager.cs`

- [ ] **Step 1: Find `FailQuest()` (around line 391) and add the RaiseMarker call after `OnQuestFailed?.Invoke()`**

Find this block:
```csharp
            OnQuestFailed?.Invoke(questName);

            // 音效
            AudioManager.Instance?.PlaySound(AudioKeys.QUEST_FAILED);
```

Replace with:
```csharp
            OnQuestFailed?.Invoke(questName);

            var failDef = QuestDataService.Instance?.GetDefinition(questName);
            if (failDef != null && failDef.FailureMarker != TutorialMarker.None)
                TutorialEventBus.RaiseMarker(failDef.FailureMarker);

            // 音效
            AudioManager.Instance?.PlaySound(AudioKeys.QUEST_FAILED);
```

- [ ] **Step 2: Check Unity Console — no compile errors**

- [ ] **Step 3: Commit**
```bash
git add "Pop Life Simulator/Assets/Scripts/Quest/QuestLogicManager.cs"
git commit -m "feat: fire failureMarker when quest deadline expires"
```

---

## Task 4: Update quest SO completionMarker values

**Files:** 10 `.asset` files — edit `completionMarker:` field directly in the YAML text.
All quests already have `triggerMarkerAfterToast: 1` so the marker fires after toast dismiss, not immediately.

- [ ] **Step 1: `Main4.asset`** — change `completionMarker: -1` → `completionMarker: 104`
  `Pop Life Simulator/Assets/Resources/ScriptableObjects/Quests/Main4.asset`

- [ ] **Step 2: `Main5.asset`** — `completionMarker: -1` → `completionMarker: 105`

- [ ] **Step 3: `Main6.asset`** — `completionMarker: -1` → `completionMarker: 106`

- [ ] **Step 4: `Main7.asset`** — `completionMarker: -1` → `completionMarker: 107`

- [ ] **Step 5: `Main8.asset`** — `completionMarker: -1` → `completionMarker: 108`
  Then add `failureMarker: 109` on the next line (the field now exists from Task 2; Unity serializes it as 0 = `GameStarted` if omitted, so we must set it explicitly to 109)

- [ ] **Step 6: `VIPSoloFans1.asset`** — `completionMarker: -1` → `completionMarker: 110`

- [ ] **Step 7: `VIPSoloFans2.asset`** — `completionMarker: -1` → `completionMarker: 111`

- [ ] **Step 8: `VIPSoloFans3.asset`** — `completionMarker: -1` → `completionMarker: 112`

- [ ] **Step 9: `VIPShyGirl1.asset`** — `completionMarker: -1` → `completionMarker: 113`

- [ ] **Step 10: `VIPShyGirl2.asset`** — `completionMarker: -1` → `completionMarker: 114`

- [ ] **Step 11: Open Unity and spot-check** — open `Main4.asset` in Inspector. `Completion Marker` should show `Main4Completed`, not a raw number. If it shows a raw number, Task 1 wasn't saved before this task was done.

- [ ] **Step 12: Commit**
```bash
git add \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/Quests/Main4.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/Quests/Main5.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/Quests/Main6.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/Quests/Main7.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/Quests/Main8.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/Quests/VIPSoloFans1.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/Quests/VIPSoloFans2.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/Quests/VIPSoloFans3.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/Quests/VIPShyGirl1.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/Quests/VIPShyGirl2.asset"
git commit -m "data: set completionMarker and failureMarker on quest SOs"
```

---

## Task 5: Create Auditor DialogueAction SOs

**Files:** 7 new `.asset` files in `Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/`

Script GUID (copied from existing Auditor SOs): `3992bcc563d458741a863415dc67327b`

- [ ] **Step 1: Create `Auditor3Visit.asset`**
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: Auditor3Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 71
  executionOrder: 100
  conversationToStart: Auditor/3
  conversationDelay: 0
  luaScript: 
  description: Auditor/3 visit — fires when Main/2 (5 shelves) completes
```

- [ ] **Step 2: Create `Auditor4Visit.asset`**
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: Auditor4Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 104
  executionOrder: 100
  conversationToStart: Auditor/4
  conversationDelay: 0
  luaScript: 
  description: Auditor/4 visit — fires when Main/4 (200 vibrators) completes
```

- [ ] **Step 3: Create `Auditor5Visit.asset`**
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: Auditor5Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 105
  executionOrder: 100
  conversationToStart: Auditor/5
  conversationDelay: 0
  luaScript: 
  description: Auditor/5 visit — fires when Main/5 (member loyalty) completes
```

- [ ] **Step 4: Create `Auditor6Visit.asset`**
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: Auditor6Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 106
  executionOrder: 100
  conversationToStart: Auditor/6
  conversationDelay: 0
  luaScript: 
  description: Auditor/6 visit — fires when Main/6 (holiday revenue) completes
```

- [ ] **Step 5: Create `Auditor7Visit.asset`**
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: Auditor7Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 107
  executionOrder: 100
  conversationToStart: Auditor/7
  conversationDelay: 0
  luaScript: 
  description: Auditor/7 visit — fires when Main/7 (tourist surge) completes; gives Cucci removal order
```

- [ ] **Step 6: Create `Auditor8Visit.asset`**
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: Auditor8Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 108
  executionOrder: 100
  conversationToStart: Auditor/8
  conversationDelay: 0
  luaScript: 
  description: Auditor/8 visit — fires when Main/8 (Cucci removal) succeeds
```

- [ ] **Step 7: Create `Auditor9Visit.asset`**
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: Auditor9Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 109
  executionOrder: 100
  conversationToStart: Auditor/9
  conversationDelay: 0
  luaScript: 
  description: Auditor/9 visit — fires when Main/8 deadline expires (Cucci still in store)
```

- [ ] **Step 8: Commit**
```bash
git add \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/Auditor3Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/Auditor4Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/Auditor5Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/Auditor6Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/Auditor7Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/Auditor8Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/Auditor9Visit.asset"
git commit -m "data: add DialogueAction SOs for Auditor/3-9"
```

---

## Task 6: Create VIP and BDSM DialogueAction SOs

**Files:** 8 new `.asset` files in `Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/`

- [ ] **Step 1: Create `SoloFans1Visit.asset`** — Day 8 (marker 35 = `Day8Trigger`)
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: SoloFans1Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 35
  executionOrder: 100
  conversationToStart: VIP/SoloFans/1
  conversationDelay: 0
  luaScript: 
  description: VIP/SoloFans/1 — Omega first visit; Day 8 build phase
```

- [ ] **Step 2: Create `SoloFans2Visit.asset`** — VIPSoloFans1Completed (marker 110)
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: SoloFans2Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 110
  executionOrder: 100
  conversationToStart: VIP/SoloFans/2
  conversationDelay: 0
  luaScript: 
  description: VIP/SoloFans/2 — Omega second visit; fires after VIPSoloFans1 quest completes
```

- [ ] **Step 3: Create `SoloFans3Visit.asset`** — VIPSoloFans2Completed (marker 111)
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: SoloFans3Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 111
  executionOrder: 100
  conversationToStart: VIP/SoloFans/3
  conversationDelay: 0
  luaScript: 
  description: VIP/SoloFans/3 — Omega third visit; fires after VIPSoloFans2 quest completes
```

- [ ] **Step 4: Create `SoloFans4Visit.asset`** — VIPSoloFans3Completed (marker 112)
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: SoloFans4Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 112
  executionOrder: 100
  conversationToStart: VIP/SoloFans/4
  conversationDelay: 0
  luaScript: 
  description: VIP/SoloFans/4 — Omega final visit; fires after VIPSoloFans3 quest completes
```

- [ ] **Step 5: Create `ShyGirl1Visit.asset`** — Day 12 (marker 37 = `Day12Trigger`)
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: ShyGirl1Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 37
  executionOrder: 100
  conversationToStart: VIP/ShyGirl/1
  conversationDelay: 0
  luaScript: 
  description: VIP/ShyGirl/1 — Hana first visit; Day 12 build phase
```

- [ ] **Step 6: Create `ShyGirl2Visit.asset`** — VIPShyGirl1Completed (marker 113)
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: ShyGirl2Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 113
  executionOrder: 100
  conversationToStart: VIP/ShyGirl/2
  conversationDelay: 0
  luaScript: 
  description: VIP/ShyGirl/2 — Hana second visit; fires after VIPShyGirl1 quest completes
```

- [ ] **Step 7: Create `ShyGirl3Visit.asset`** — VIPShyGirl2Completed (marker 114)
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: ShyGirl3Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 114
  executionOrder: 100
  conversationToStart: VIP/ShyGirl/3
  conversationDelay: 0
  luaScript: 
  description: VIP/ShyGirl/3 — Hana final visit; fires after VIPShyGirl2 quest completes
```

- [ ] **Step 8: Create `BDSM1Visit.asset`** — Day 15 (marker 38 = `Day15Trigger`)
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3992bcc563d458741a863415dc67327b, type: 3}
  m_Name: BDSM1Visit
  m_EditorClassIdentifier: Assembly-CSharp::PopLife.Data.DialogueAction
  activationMarker: 38
  executionOrder: 100
  conversationToStart: BDSM/1
  conversationDelay: 0
  luaScript: 
  description: BDSM/1 — Henya (Auditor's twin) first visit; Day 15 build phase
```

- [ ] **Step 9: Commit**
```bash
git add \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/SoloFans1Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/SoloFans2Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/SoloFans3Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/SoloFans4Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/ShyGirl1Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/ShyGirl2Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/ShyGirl3Visit.asset" \
  "Pop Life Simulator/Assets/Resources/ScriptableObjects/DialogueActions/BDSM1Visit.asset"
git commit -m "data: add DialogueAction SOs for VIP SoloFans, ShyGirl, and BDSM/1"
```

---

## Task 7: Manual — fix Auditor/7 missing Lua script

**Must be done in the Pixel Crushers Dialogue Editor — cannot be scripted.**

- [ ] **Step 1:** Tools → Pixel Crushers → Dialogue System → Dialogue Editor → open `Assets/Data/Dialogue Database.asset`

- [ ] **Step 2:** Find conversation `Auditor/7` and open it

- [ ] **Step 3:** Find the last dialogue node (the one with no outgoing links to another dialogue node)

- [ ] **Step 4:** In that node's **Script** field, enter:
  ```
  SetQuestStateAfter("Main/8", "active"); RaiseMarkerAfter("Main8Activated");
  ```

- [ ] **Step 5:** Save (Ctrl+S in the Dialogue Editor)

- [ ] **Step 6:** Commit
  ```bash
  git add "Pop Life Simulator/Assets/Data/Dialogue Database.asset"
  git commit -m "data: add missing Lua script to Auditor/7 to activate Main/8 quest"
  ```

---

## Verification checklist

After all tasks, use the debug panel / cheat tools to fast-forward to specific days and manually complete quests. Confirm:

- [ ] Auditor/3 fires after Main/2 completes (cheat: place 5 shelves)
- [ ] Auditor/4–7 fire in sequence as each Main quest completes
- [ ] Auditor/8 fires after Main/8 is completed (Cucci removed)
- [ ] Auditor/9 fires when Main/8 deadline expires with Cucci still present
- [ ] VIP/SoloFans/1 fires at Day 8 build phase — not Day 4, not Day 10
- [ ] VIP/SoloFans/2–4 chain correctly after each quest completion
- [ ] VIP/ShyGirl/1 fires at Day 12 build phase
- [ ] VIP/ShyGirl/2–3 chain correctly
- [ ] BDSM/1 fires at Day 15 build phase — confirm no other event fires that same day
- [ ] No two conversations stack on the same frame (check Console for queued dialogue warnings)

---

## Warnings

- **Append-only enum rule:** New markers must go at the end of `TutorialMarker`. Inserting anywhere in the middle shifts all existing int values and silently breaks SO references — Unity won't warn you.
- **`failureMarker` default is 0 = `GameStarted`** once the field is added to `QuestDefinition`. Every existing quest SO will serialize as `failureMarker: 0` on next Unity save unless explicitly set. This would incorrectly fire `GameStarted` when those quests fail. Fix: after Task 2, open each quest SO in Inspector and verify `Failure Marker` shows `None` (= -1). If not, set it to None manually. Only `Main8.asset` should have a non-None value.
- **Auditor/9 already has its Lua:** `SetQuestStateAfter("Main/9", "active"); RaiseMarkerAfter("Main9Activated");` is already in the dialogue database — confirmed from earlier inspection. No Dialogue Editor work needed for it.
- **VIPSoloFans4 and VIPShyGirl3 have no completion chain** — they are the last in their respective series, so no `completionMarker` needed. Leave them at `-1`.
