using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to give rewards to the player
    ///
    /// Usage in Dialogue Editor Sequence field:
    ///   GiveReward(type, value)
    ///
    /// Parameters:
    ///   type: Money, Fame, Blueprint, or Customer
    ///   value: Amount (for Money/Fame) or ID (for Blueprint/Customer)
    ///
    /// Examples:
    ///   GiveReward(Money, 100)
    ///   GiveReward(Fame, 50)
    ///   GiveReward(Blueprint, ShelfVibrator)
    ///   GiveReward(Customer, Customer_001)
    /// </summary>
    public class SequencerCommandGiveReward : SequencerCommand
    {
        public void Awake()
        {
            string rewardType = GetParameter(0);
            string value = GetParameter(1);

            if (string.IsNullOrEmpty(rewardType) || string.IsNullOrEmpty(value))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: GiveReward() - missing parameters");
                }
                Stop();
                return;
            }

            if (PopLifeLuaFunctions.Instance != null)
            {
                PopLifeLuaFunctions.Instance.GiveReward(rewardType, value);

                if (DialogueDebug.logInfo)
                {
                    Debug.Log($"Sequencer: GiveReward({rewardType}, {value})");
                }
            }
            else
            {
                Debug.LogWarning("Sequencer: GiveReward() - PopLifeLuaFunctions.Instance is null");
            }

            Stop();
        }
    }
}
