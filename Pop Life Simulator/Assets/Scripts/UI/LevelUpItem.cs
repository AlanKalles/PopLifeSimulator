using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PopLife.UI
{
    /// <summary>
    /// 顾客升级条目组件 — 管理单个升级条目UI元素的引用
    /// 挂载在 LevelUpItem 预制体的根对象上
    /// </summary>
    public class LevelUpItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI infoText;

        /// <summary>
        /// 设置升级条目数据
        /// </summary>
        /// <param name="portraitSprite">顾客头像（可为null）</param>
        /// <param name="customerName">顾客名</param>
        /// <param name="oldLevel">升级前等级</param>
        /// <param name="newLevel">升级后等级</param>
        /// <param name="xpGained">本次获得经验</param>
        public void SetData(Sprite portraitSprite, string customerName, int oldLevel, int newLevel, int xpGained)
        {
            if (portraitImage != null && portraitSprite != null)
                portraitImage.sprite = portraitSprite;

            if (infoText != null)
            {
                infoText.text = $"<b>{customerName}</b>: " +
                                $"Lv {oldLevel} \u2192 {newLevel} " +
                                $"<color=#FFD700>(+{xpGained} XP)</color>";

                // 跨级升级标记
                if (newLevel - oldLevel > 1)
                {
                    int skipped = newLevel - oldLevel - 1;
                    infoText.text += $" <color=#FF6347>Skipped {skipped} level(s)!</color>";
                }
            }
        }
    }
}
