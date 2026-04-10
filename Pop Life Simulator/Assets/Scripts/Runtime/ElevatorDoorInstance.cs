using UnityEngine;
using PrimeTween;
using PopLife.Data;

namespace PopLife.Runtime
{
    /// <summary>
    /// 电梯门运行时实例。继承 BuildingInstance 以接入 InteriorGrid 占位体系。
    /// 每层楼一个门，占 3×4 interior 格子。门动画通过 PrimeTween 驱动。
    /// </summary>
    public class ElevatorDoorInstance : BuildingInstance
    {
        [Header("Elevator Door Visuals")]
        [SerializeField] private SpriteRenderer doorLeft;
        [SerializeField] private SpriteRenderer doorRight;
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private SpriteRenderer frame;
        [SerializeField] private TMPro.TextMeshPro floorLabel;
        [SerializeField] private Transform anchor; // NodeLink2 寻路锚点（prefab 中 Anchor 子对象）

        /// <summary> 寻路锚点 Transform（与 shelf 的 interactionAnchor 模式一致） </summary>
        public Transform Anchor => anchor != null ? anchor : transform;

        [Header("Animation")]
        [SerializeField] private float doorOpenDistance = 0.3f;
        [SerializeField] private float doorAnimDuration = 0.4f;

        /// <summary> 门动画时长（供 LinkTraversalDetector 读取） </summary>
        public float DoorAnimDuration => doorAnimDuration;

        // 不 override MaxLevel（archetype.levels=null → 基类返回 0）
        public override int GetMaintenanceFee() => 0;

        // 门状态
        private bool isOpen;
        private Sequence openSequence;
        private Sequence closeSequence;

        // 门初始位置（开关门始终从初始位置计算，避免多次调用累积偏移）
        private float doorLeftInitX;
        private float doorRightInitX;

        // 并发保护：多顾客同时使用同一门时防止动画互相打架
        private int activeTraversals;

        protected override void Awake()
        {
            base.Awake();
            if (doorLeft != null) doorLeftInitX = doorLeft.transform.localPosition.x;
            if (doorRight != null) doorRightInitX = doorRight.transform.localPosition.x;
        }

        /// <summary> 设置楼层标签文字 </summary>
        public void SetFloorLabel(string text)
        {
            if (floorLabel != null) floorLabel.text = text;
        }

        /// <summary>
        /// 开门。activeTraversals++ 后播放开门动画。
        /// 多人同时使用时门保持打开。
        /// </summary>
        public void OpenDoor()
        {
            activeTraversals++;
            if (isOpen) return;
            isOpen = true;

            if (closeSequence.isAlive) closeSequence.Stop();
            if (doorLeft == null || doorRight == null) return;

            openSequence = Sequence.Create()
                .Group(Tween.LocalPositionX(doorLeft.transform, doorLeftInitX - doorOpenDistance,
                    doorAnimDuration, Ease.OutBack))
                .Group(Tween.LocalPositionX(doorRight.transform, doorRightInitX + doorOpenDistance,
                    doorAnimDuration, Ease.OutBack));
        }

        /// <summary>
        /// 通行结束。activeTraversals-- 后，仅当计数归零时才播放关门动画。
        /// </summary>
        public void NotifyTraversalComplete()
        {
            activeTraversals = Mathf.Max(0, activeTraversals - 1);
            if (activeTraversals > 0 || !isOpen) return;
            isOpen = false;

            if (openSequence.isAlive) openSequence.Stop();
            if (doorLeft == null || doorRight == null) return;

            closeSequence = Sequence.Create()
                .Group(Tween.LocalPositionX(doorLeft.transform, doorLeftInitX,
                    doorAnimDuration, Ease.InBack))
                .Group(Tween.LocalPositionX(doorRight.transform, doorRightInitX,
                    doorAnimDuration, Ease.InBack));
        }
    }
}
