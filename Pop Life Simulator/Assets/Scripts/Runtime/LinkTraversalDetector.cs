using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using PrimeTween;

namespace PopLife.Runtime
{
    /// <summary>
    /// 挂载在 AILerp agent 上。
    /// 监听路径回调，检测路径中的电梯 NodeLink2，
    /// 接管 AILerp 控制权执行 Teleport 穿越（隐藏 sprite → 瞬移 → 延迟 → 显形 → 门动画）。
    /// Portal NodeLink2 不处理，由 AILerp 自行走过。
    /// </summary>
    [RequireComponent(typeof(Seeker))]
    public class LinkTraversalDetector : MonoBehaviour
    {
        [UnityEngine.Header("Elevator Traversal")]
        [SerializeField] private float enterOffset = 0.1f;
        [SerializeField] private float waitPerFloor = 0.5f;
        [SerializeField] private float arrivalThreshold = 0.3f;
        [SerializeField] private Transform visualAnchor; // Inspector 拖入 customer 的 VisualAnchor 子对象

        private const string ElevatorSortingLayer = "ElevatorBackgroundLayer";

        private Seeker seeker;
        private AILerp aiLerp;
        private SpriteRenderer[] allRenderers;

        // visualAnchor 初始位置缓存（防止中断累积偏移）
        private Vector3 visualAnchorOriginLocalPos;

        // 电梯穿越状态
        private bool isTraversing;
        public bool IsTraversingElevator => isTraversing;

        private readonly Queue<ElevatorLinkInfo> pendingElevatorLinks = new();

        // 门回收追踪（独立于 isTraversing）
        private bool entryDoorPendingClose;
        private bool exitDoorPendingClose;
        private ElevatorDoorInstance currentEntryDoor;
        private ElevatorDoorInstance currentExitDoor;

        // sorting 恢复
        private string originalSortingLayer;

        private struct ElevatorLinkInfo
        {
            public ElevatorDoorInstance entryDoor;
            public ElevatorDoorInstance exitDoor;
        }

        void Awake()
        {
            seeker = GetComponent<Seeker>();
            aiLerp = GetComponent<AILerp>();
            allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (visualAnchor != null) visualAnchorOriginLocalPos = visualAnchor.localPosition;
        }

        void OnEnable() => seeker.postProcessPath += OnPathComplete;

        void OnDisable()
        {
            seeker.postProcessPath -= OnPathComplete;
            if (isTraversing) AbortTraversal();
            pendingElevatorLinks.Clear();
        }

        void OnDestroy()
        {
            // 按独立标志回收门计数，不依赖 isTraversing
            if (entryDoorPendingClose && currentEntryDoor != null)
                currentEntryDoor.NotifyTraversalComplete();
            if (exitDoorPendingClose && currentExitDoor != null)
                currentExitDoor.NotifyTraversalComplete();
        }

        // ================================================================
        //  路径回调
        // ================================================================

        private void OnPathComplete(Path p)
        {
            // 电梯穿越中，忽略所有新路径（包括 autoRepath）
            // 协程结束后会自行调用 SearchPath() 重新寻路
            if (isTraversing) return;

            pendingElevatorLinks.Clear();

            if (p == null || p.error || p.path == null || p.path.Count < 2) return;

            // 扫描路径中的电梯 NodeLink2
            for (int i = 0; i < p.path.Count - 1; i++)
            {
                if (p.path[i] is LinkNode startNode && p.path[i + 1] is LinkNode endNode)
                {
                    var nodeLink = NodeLink2.GetNodeLink(startNode);
                    if (nodeLink == null) continue;

                    // 只处理电梯 link（有 AILerpLinkTeleporter = 电梯）
                    var teleporter = nodeLink.GetComponent<AILerpLinkTeleporter>();
                    if (teleporter == null) continue; // portal，AILerp 自己走

                    var (entry, exit) = teleporter.ResolveDoors((Vector3)startNode.position);
                    pendingElevatorLinks.Enqueue(new ElevatorLinkInfo
                    {
                        entryDoor = entry,
                        exitDoor = exit
                    });
                    i++; // 跳过 endNode
                }
            }
        }

        // ================================================================
        //  Update: 检测是否到达电梯入口
        // ================================================================

        void Update()
        {
            if (pendingElevatorLinks.Count == 0 || isTraversing) return;

            var next = pendingElevatorLinks.Peek();
            if (next.entryDoor == null)
            {
                pendingElevatorLinks.Dequeue();
                return;
            }

            float dist = Vector3.Distance(transform.position, next.entryDoor.Anchor.position);
            if (dist <= arrivalThreshold)
            {
                pendingElevatorLinks.Dequeue();
                StartCoroutine(ElevatorTraversal(next.entryDoor, next.exitDoor));
            }
        }

        // ================================================================
        //  电梯穿越协程
        // ================================================================

        private IEnumerator ElevatorTraversal(ElevatorDoorInstance entryDoor, ElevatorDoorInstance exitDoor)
        {
            isTraversing = true;

            // ── 入口阶段 ──

            // 1. 暂停 AILerp
            aiLerp.simulateMovement = false;

            // 2. 入口门开门 + 标记待回收
            currentEntryDoor = entryDoor;
            entryDoorPendingClose = true;
            entryDoor.OpenDoor();
            yield return new WaitForSeconds(entryDoor.DoorAnimDuration);

            // 3. visualAnchor 向上移动（走进电梯）— 不动 transform，不影响 AILerp 状态
            if (visualAnchor != null)
            {
                yield return Tween.LocalPosition(visualAnchor,
                    visualAnchorOriginLocalPos + Vector3.up * enterOffset,
                    0.2f, Ease.Linear).ToYieldInstruction();
            }

            // 4. 切换 sorting layer
            SaveOriginalSorting();
            SetSortingInsideElevator();

            // 5. 入口门关门 + 标记已回收
            entryDoor.NotifyTraversalComplete();
            entryDoorPendingClose = false;
            yield return new WaitForSeconds(entryDoor.DoorAnimDuration);

            // 6. 隐藏 sprite（此时已被门挡住）
            SetAllSpritesAlpha(0f);

            // 7. Teleport 到出口 Anchor（clearPath: true 清旧路径，防止恢复后沿旧路径飞行）
            var exitAnchor = exitDoor.Anchor.position;
            aiLerp.Teleport(exitAnchor, true);

            // ── 等待阶段 ──

            // 8. 按楼层差等待
            int floorDiff = Mathf.Abs(GetFloorLevel(entryDoor) - GetFloorLevel(exitDoor));
            float waitTime = Mathf.Max(1, floorDiff) * waitPerFloor;
            yield return new WaitForSeconds(waitTime);

            // ── 出口阶段 ──

            // 9. 恢复 alpha（门关着，被门遮挡）
            SetAllSpritesAlpha(1f);

            // 10. 出口门开门 + 标记待回收
            currentExitDoor = exitDoor;
            exitDoorPendingClose = true;
            exitDoor.OpenDoor();
            yield return new WaitForSeconds(exitDoor.DoorAnimDuration);

            // 11. 恢复 sorting
            RestoreOriginalSorting();

            // 12. visualAnchor 恢复到初始位置（走出电梯）
            if (visualAnchor != null)
            {
                yield return Tween.LocalPosition(visualAnchor,
                    visualAnchorOriginLocalPos,
                    0.2f, Ease.Linear).ToYieldInstruction();
            }

            // 13. 标记穿越结束（必须在 SearchPath 之前）
            isTraversing = false;

            // 14. 恢复 AILerp
            aiLerp.simulateMovement = true;
            aiLerp.isStopped = false;
            aiLerp.SearchPath();

            // 15. 延迟后关出口门 + 标记已回收
            yield return new WaitForSeconds(0.5f);
            exitDoor.NotifyTraversalComplete();
            exitDoorPendingClose = false;
        }

        // ================================================================
        //  中断恢复
        // ================================================================

        private void AbortTraversal()
        {
            StopAllCoroutines();
            SetAllSpritesAlpha(1f);
            RestoreOriginalSorting();

            // 复原 visualAnchor 位置
            if (visualAnchor != null)
                visualAnchor.localPosition = visualAnchorOriginLocalPos;

            // 按 pendingClose 标志回收门计数
            if (entryDoorPendingClose && currentEntryDoor != null)
                currentEntryDoor.NotifyTraversalComplete();
            if (exitDoorPendingClose && currentExitDoor != null)
                currentExitDoor.NotifyTraversalComplete();

            entryDoorPendingClose = false;
            exitDoorPendingClose = false;
            currentEntryDoor = null;
            currentExitDoor = null;

            if (aiLerp != null)
            {
                aiLerp.simulateMovement = true;
                aiLerp.isStopped = false;
            }
            isTraversing = false;
        }

        // ================================================================
        //  辅助方法
        // ================================================================

        private int GetFloorLevel(ElevatorDoorInstance door)
        {
            if (door == null) return 0;
            var tile = door.GetComponentInParent<FloorTileInstance>();
            if (tile == null) return 0;
            return tile.FloorLevel ?? 0;
        }

        private void SaveOriginalSorting()
        {
            if (allRenderers != null && allRenderers.Length > 0 && allRenderers[0] != null)
                originalSortingLayer = allRenderers[0].sortingLayerName;
        }

        private void SetSortingInsideElevator()
        {
            if (allRenderers == null) return;
            foreach (var sr in allRenderers)
            {
                if (sr != null) sr.sortingLayerName = ElevatorSortingLayer;
            }
        }

        private void RestoreOriginalSorting()
        {
            if (allRenderers == null || string.IsNullOrEmpty(originalSortingLayer)) return;
            foreach (var sr in allRenderers)
            {
                if (sr != null) sr.sortingLayerName = originalSortingLayer;
            }
        }

        private void SetAllSpritesAlpha(float alpha)
        {
            if (allRenderers == null) return;
            foreach (var sr in allRenderers)
            {
                if (sr == null) continue;
                var c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }
    }
}
