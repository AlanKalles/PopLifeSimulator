using UnityEditor;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Editor
{
    /// <summary>
    /// 编辑器工具状态持久化（跨 editor session 保持）。
    /// 记录当前模式、选中原型、旋转等。
    /// </summary>
    [FilePath("PopLife/WorldGridAuthoringState.asset", FilePathAttribute.Location.PreferencesFolder)]
    public class WorldGridAuthoringState : ScriptableSingleton<WorldGridAuthoringState>
    {
        public enum AuthoringMode
        {
            None,
            PlaceFloorTile,
            EraseFloorTile,
            PlaceInteriorBuilding,
            EraseInteriorBuilding,
            PlaceElevator,
            PlaceAlanBot
        }

        public AuthoringMode mode;
        public BuildingArchetype selectedArchetype;
        public int rotation;
        public bool markAsDefault;

        // Interior 模式选项
        public bool lockMove;
        public bool lockDestroy;

        // AlanBot 放置
        public GameObject alanBotPrefab;

        // 网格可视化开关（独立于操作模式）
        public bool showWorldGrid;
        public bool showInteriorGrid;

        public void Save() => Save(true);
    }
}
