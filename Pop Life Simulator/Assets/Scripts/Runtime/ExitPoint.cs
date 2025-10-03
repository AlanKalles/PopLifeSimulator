using UnityEngine;

namespace PopLife.Runtime
{
    /// <summary>
    /// 商店离店点 - 标记顾客离开的出口位置
    /// </summary>
    public class ExitPoint : MonoBehaviour
    {
        [Header("出口标识")]
        [Tooltip("出口唯一ID")]
        public string exitId;

        [Header("位置信息")]
        [Tooltip("出口的网格位置（可选，用于寻路）")]
        public Vector2Int gridCell;

        public bool directionToRight;

        void Awake()
        {
            // 如果没有设置ID，自动生成
            if (string.IsNullOrEmpty(exitId))
            {
                exitId = $"Exit_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            }
        }

        void OnDrawGizmos()
        {
            // 绘制出口标识
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // 绘制箭头指向外部
            Gizmos.color = Color.yellow;
            Vector3 direction = directionToRight? transform.right:-1*transform.right; // 假设朝右为出口方向
            Gizmos.DrawRay(transform.position, direction * 1f);
        }

        void OnDrawGizmosSelected()
        {
            // 选中时绘制更明显的标识
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.position, 0.3f);
        }
    }
}
