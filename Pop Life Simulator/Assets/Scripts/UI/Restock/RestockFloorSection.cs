using UnityEngine;
using TMPro;

namespace PopLife.UI.Restock
{
    /// <summary>
    /// 单层楼的分段容器——包含一个 header ("FLOOR 1 STOCK") 和一个 cellContainer
    /// （通常挂 GridLayoutGroup，RestockPanel 将 Cell 放入此容器）。
    /// </summary>
    public class RestockFloorSection : MonoBehaviour
    {
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private Transform cellContainer;

        public Transform CellContainer => cellContainer;

        public void SetHeader(string text)
        {
            if (headerText != null)
                headerText.text = text;
        }
    }
}
