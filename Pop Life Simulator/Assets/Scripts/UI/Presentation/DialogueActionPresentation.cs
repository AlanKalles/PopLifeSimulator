using System.Collections;

namespace PopLife.UI
{
    /// <summary>
    /// 把一条已有的对话串行协程（如 TutorialMarkerBridge.ExecuteActionsSerially）
    /// 包装为通道里的一等演出项。默认优先级 = 当天剧情对话。
    /// </summary>
    public class DialogueActionPresentation : IPresentation
    {
        private readonly IEnumerator routine;

        public DialogueActionPresentation(IEnumerator actionRoutine,
            int priority = PresentationChannel.PRIORITY_DAY_STORY_DIALOGUE)
        {
            routine = actionRoutine;
            Priority = priority;
        }

        public int Priority { get; }

        public IEnumerator Play()
        {
            if (routine != null) yield return routine;
        }
    }
}
