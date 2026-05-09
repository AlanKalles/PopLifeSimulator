using UnityEngine;
using PrimeTween;
using PopLife.Customers.Runtime;

namespace PopLife.Title
{
    /// <summary>
    /// Title 场景装饰顾客 walker
    /// 用 PrimeTween 在 waypoints 之间循环移动，每帧把 transform-delta velocity
    /// 写入 CustomerAnimationController.debugVelocityOverride 让走路动画自然触发
    /// 不调用任何 CustomerAgent.Initialize / 行为树 / 单例，纯视觉
    /// </summary>
    [RequireComponent(typeof(CustomerAnimationController))]
    public class TitleSceneCustomerWalker : MonoBehaviour
    {
        [Header("Waypoints")]
        [SerializeField] private Transform[] waypoints;

        [Header("移动参数")]
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private Vector2 idleDurationRange = new Vector2(0.5f, 2f);
        [SerializeField] private bool randomOrder = true;

        // 朝向翻转由 CustomerAnimationController.UpdateFacing() 自动处理
        // （它通过 TryGetVelocity 读取 debugVelocityOverride 后翻转 bodyPartsContainer.localScale）
        // walker 这里不能再翻 transform.localScale 否则会双重翻转

        private CustomerAnimationController animController;
        private Sequence currentSequence;
        private Vector3 lastPos;
        private int currentIndex = -1;

        public void SetWaypoints(Transform[] points)
        {
            waypoints = points;
            // 已经 Start 过，重启移动
            if (animController != null)
            {
                if (currentSequence.isAlive) currentSequence.Stop();
                if (waypoints != null && waypoints.Length > 0)
                    MoveToNext();
            }
        }

        private void Awake()
        {
            animController = GetComponent<CustomerAnimationController>();
            lastPos = transform.position;
        }

        private void Start()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                Debug.LogWarning($"[TitleSceneCustomerWalker] {gameObject.name} 缺少 waypoints，将原地待机");
                return;
            }
            MoveToNext();
        }

        private void MoveToNext()
        {
            if (this == null || !isActiveAndEnabled) return;
            if (waypoints == null || waypoints.Length == 0) return;

            int nextIdx;
            if (randomOrder)
            {
                nextIdx = Random.Range(0, waypoints.Length);
                // 多于 1 个点时避免连续选同一个
                if (waypoints.Length > 1 && nextIdx == currentIndex)
                    nextIdx = (nextIdx + 1) % waypoints.Length;
            }
            else
            {
                nextIdx = (currentIndex + 1) % waypoints.Length;
            }

            currentIndex = nextIdx;
            Transform target = waypoints[nextIdx];
            if (target == null)
            {
                // 跳过空位
                Sequence.Create().ChainDelay(0.1f).ChainCallback(MoveToNext);
                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);
            float duration = distance / Mathf.Max(moveSpeed, 0.01f);
            float idleDuration = Random.Range(idleDurationRange.x, idleDurationRange.y);

            currentSequence = Sequence.Create()
                .Chain(Tween.Position(transform, target.position, duration))
                .ChainDelay(idleDuration)
                .ChainCallback(MoveToNext);
        }

        private void Update()
        {
            // 把 transform-delta 喂给动画控制器，触发 walk/idle 状态切换
            if (animController != null)
            {
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                Vector3 v = (transform.position - lastPos) / dt;
                animController.debugVelocityOverride = v;
                lastPos = transform.position;
            }
        }

        private void OnDestroy()
        {
            if (currentSequence.isAlive) currentSequence.Stop();
            if (animController != null)
                animController.debugVelocityOverride = null;
        }
    }
}
