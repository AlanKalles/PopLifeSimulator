using UnityEngine;

namespace PopLife.UI
{
    /// <summary>
    /// 筛选按钮统一接口
    /// 支持传统按钮和 Alpha Hit Test 检测两种实现
    /// </summary>
    public interface IFilterButton
    {
        /// <summary>
        /// 筛选值（SelectPage、ProductCategory 或 null 表示 "All"）
        /// </summary>
        object FilterValue { get; }

        /// <summary>
        /// 按钮是否被选中
        /// </summary>
        bool IsSelected { get; }

        /// <summary>
        /// 设置选中状态
        /// </summary>
        void SetSelected(bool selected);

        /// <summary>
        /// 初始化按钮
        /// </summary>
        /// <param name="filterValue">筛选值</param>
        /// <param name="displayText">显示文本（可选，某些实现可能不需要）</param>
        /// <param name="onClickCallback">点击回调</param>
        void Initialize(object filterValue, string displayText, System.Action<IFilterButton> onClickCallback);

        /// <summary>
        /// 获取 GameObject（用于管理和定位）
        /// </summary>
        GameObject GetGameObject();
    }
}
