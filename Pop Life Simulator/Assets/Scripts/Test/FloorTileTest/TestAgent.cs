using UnityEngine;
using Pathfinding;

namespace PopLife.Test
{
    /// <summary>
    /// 测试用 agent — AILerp 寻路 + 状态颜色显示
    /// </summary>
    [RequireComponent(typeof(AILerp))]
    [RequireComponent(typeof(Seeker))]
    public class TestAgent : MonoBehaviour
    {
        private AILerp aiLerp;
        private Seeker seeker;
        private SpriteRenderer sr;

        private bool hasTarget;

        /// <summary> 设置 grid 引用，用于检测电梯格子 </summary>
        public TestFloorGrid grid;

        private static readonly Color colorIdle = new(0.8f, 0.8f, 0.8f, 1f);     // 灰色：空闲
        private static readonly Color colorMoving = new(0.3f, 0.6f, 1f, 1f);      // 蓝色：移动中
        private static readonly Color colorPending = new(1f, 0.9f, 0.3f, 1f);     // 黄色：寻路中
        private static readonly Color colorArrived = new(0.3f, 1f, 0.3f, 1f);     // 绿色：到达
        private static readonly Color colorFailed = new(1f, 0.3f, 0.3f, 1f);      // 红色：失败

        void Awake()
        {
            aiLerp = GetComponent<AILerp>();
            seeker = GetComponent<Seeker>();
            sr = GetComponent<SpriteRenderer>();

            // 初始停止
            aiLerp.isStopped = true;
        }

        /// <summary>
        /// 设置目标点并开始寻路
        /// </summary>
        public void SetDestination(Vector3 target)
        {
            hasTarget = true;
            aiLerp.isStopped = false;
            aiLerp.destination = target;
            aiLerp.SearchPath();
        }

        /// <summary>
        /// 获取当前状态描述
        /// </summary>
        public string GetStatusText()
        {
            if (!hasTarget) return "Idle";
            if (aiLerp.pathPending) return "Calculating path...";
            if (aiLerp.reachedDestination) return "Arrived";
            if (aiLerp.hasPath) return "Moving";
            return "No path";
        }

        void Update()
        {
            if (sr == null) return;

            if (!hasTarget)
            {
                sr.color = colorIdle;
                return;
            }

            // 电梯穿行时隐藏
            bool onElevator = false;
            if (grid != null && aiLerp.hasPath && !aiLerp.reachedDestination)
            {
                var gridPos = grid.WorldToGrid(transform.position);
                onElevator = grid.IsElevatorAt(gridPos);
            }
            sr.enabled = !onElevator;

            if (aiLerp.pathPending)
                sr.color = colorPending;
            else if (aiLerp.reachedDestination)
                sr.color = colorArrived;
            else if (aiLerp.hasPath)
                sr.color = colorMoving;
            else
                sr.color = colorFailed;
        }

        /// <summary>
        /// 运行时创建 agent GameObject
        /// </summary>
        public static TestAgent CreateAt(Vector3 position, Transform parent = null, TestFloorGrid grid = null)
        {
            var go = new GameObject("TestAgent");
            if (parent != null)
                go.transform.SetParent(parent);
            go.transform.position = position;

            // Sprite（小圆点）
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.sortingOrder = 50;
            sr.color = colorIdle;

            // A* 组件
            var seeker = go.AddComponent<Seeker>();
            var aiLerp = go.AddComponent<AILerp>();
            aiLerp.speed = 3f;
            aiLerp.enableRotation = false;
            aiLerp.orientation = OrientationMode.YAxisForward;

            var agent = go.AddComponent<TestAgent>();
            agent.grid = grid;
            return agent;
        }

        private static Sprite CreateCircleSprite()
        {
            int size = 16;
            var tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;

            float center = size * 0.5f - 0.5f;
            float radius = size * 0.4f;
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    pixels[y * size + x] = (dx * dx + dy * dy <= radius * radius)
                        ? Color.white
                        : Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
