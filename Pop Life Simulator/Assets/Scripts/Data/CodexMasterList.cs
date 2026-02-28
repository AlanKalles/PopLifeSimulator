using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Data
{
    /// <summary>
    /// Codex总表 - 定义所有货架在图鉴中的默认排列顺序
    /// 由设计师在编辑器中手动排序
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Codex/CodexMasterList")]
    public class CodexMasterList : ScriptableObject
    {
        [Tooltip("按图鉴默认排序排列的所有货架原型")]
        [SerializeField] private List<ShelfArchetype> shelves = new List<ShelfArchetype>();

        /// <summary>
        /// 获取排序后的货架列表（只读）
        /// </summary>
        public IReadOnlyList<ShelfArchetype> Shelves => shelves;

        /// <summary>
        /// 获取某个货架在主列表中的排序索引（未找到返回int.MaxValue）
        /// </summary>
        public int GetSortIndex(ShelfArchetype shelf)
        {
            int idx = shelves.IndexOf(shelf);
            return idx >= 0 ? idx : int.MaxValue;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器辅助：自动扫描Resources中的所有ShelfArchetype，填充到列表中
        /// 仅添加尚未存在的SO，不改变已有条目的顺序
        /// </summary>
        [ContextMenu("Auto-fill from Resources")]
        private void AutoFillFromResources()
        {
            var allShelves = Resources.LoadAll<ShelfArchetype>("ScriptableObjects/BuildingArchetype/Shelf");
            int added = 0;
            foreach (var shelf in allShelves)
            {
                if (!shelves.Contains(shelf))
                {
                    shelves.Add(shelf);
                    added++;
                }
            }

            if (added > 0)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"[CodexMasterList] 自动添加了 {added} 个货架，当前共 {shelves.Count} 个");
            }
            else
            {
                Debug.Log("[CodexMasterList] 没有新的货架需要添加");
            }
        }
#endif
    }
}
