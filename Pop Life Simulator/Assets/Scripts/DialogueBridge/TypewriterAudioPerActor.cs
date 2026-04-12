using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using Sirenix.OdinInspector;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// Switches Dialogue System typewriter audio per speaking actor.
    /// Attach to Dialogue Manager and configure actor name to audio mappings in Inspector.
    /// </summary>
    public class TypewriterAudioPerActor : MonoBehaviour
    {
        [Serializable]
        public struct ActorTypewriterAudio
        {
            [Tooltip("Actor Name in Dialogue Database.")]
            public string actorName;

            [Tooltip("Clips used by this actor's typewriter. Multiple clips are chosen randomly.")]
            public AudioClip[] clips;
        }

        private struct TypewriterAudioState
        {
            public AudioClip audioClip;
            public AudioClip[] alternateAudioClips;
        }

        [Title("Default Audio")]
        [Tooltip("Fallback typewriter clips when actor has no custom mapping. Leave empty to restore the typewriter's original clips.")]
        [SerializeField] private AudioClip[] defaultClips;

        [Title("Per Actor Audio")]
        [Tooltip("Actor name to typewriter audio mapping.")]
        [SerializeField] private ActorTypewriterAudio[] actorAudioMap;

        [Title("Debug")]
        [SerializeField] private bool debugMode = false;

        private readonly Dictionary<int, TypewriterAudioState> originalAudioByTypewriterId = new();

        private void OnEnable()
        {
            if (DialogueManager.instance != null)
            {
                DialogueManager.instance.conversationLinePrepared += OnConversationLinePrepared;
            }
        }

        private void OnDisable()
        {
            if (DialogueManager.instance != null)
            {
                DialogueManager.instance.conversationLinePrepared -= OnConversationLinePrepared;
            }
        }

        private void OnConversationLinePrepared(Subtitle subtitle)
        {
            if (subtitle == null || subtitle.speakerInfo == null)
            {
                return;
            }

            var standardDialogueUI = DialogueManager.dialogueUI as StandardDialogueUI;
            if (standardDialogueUI == null)
            {
                if (debugMode)
                {
                    Debug.Log("[TypewriterAudioPerActor] Current dialogue UI is not StandardDialogueUI, skipping.");
                }
                return;
            }

            var subtitleControls = standardDialogueUI.conversationUIElements?.standardSubtitleControls;
            if (subtitleControls == null)
            {
                if (debugMode)
                {
                    Debug.LogWarning("[TypewriterAudioPerActor] Standard subtitle controls not found.");
                }
                return;
            }

            DialogueActor dialogueActor;
            var panel = subtitleControls.GetPanel(subtitle, out dialogueActor);
            if (panel == null)
            {
                if (debugMode)
                {
                    Debug.LogWarning($"[TypewriterAudioPerActor] Subtitle panel not found for actor '{subtitle.speakerInfo.nameInDatabase}'.");
                }
                return;
            }

            var typewriter = TypewriterUtility.GetTypewriter(panel.subtitleText);
            if (typewriter == null)
            {
                if (debugMode)
                {
                    Debug.Log($"[TypewriterAudioPerActor] No typewriter found on panel '{panel.name}'.");
                }
                return;
            }

            CacheOriginalAudio(typewriter);

            if (TryGetActorClips(subtitle.speakerInfo.nameInDatabase, out var actorClips) && actorClips.Length > 0)
            {
                ApplyClips(typewriter, actorClips);

                if (debugMode)
                {
                    Debug.Log($"[TypewriterAudioPerActor] Applied actor audio for '{subtitle.speakerInfo.nameInDatabase}' ({actorClips.Length} clips).");
                }
                return;
            }

            if (defaultClips != null && defaultClips.Length > 0)
            {
                ApplyClips(typewriter, defaultClips);

                if (debugMode)
                {
                    Debug.Log($"[TypewriterAudioPerActor] Applied default audio for '{subtitle.speakerInfo.nameInDatabase}'.");
                }
            }
            else
            {
                RestoreOriginalAudio(typewriter);

                if (debugMode)
                {
                    Debug.Log($"[TypewriterAudioPerActor] Restored original audio for '{subtitle.speakerInfo.nameInDatabase}'.");
                }
            }
        }

        private bool TryGetActorClips(string actorNameInDatabase, out AudioClip[] clips)
        {
            if (!string.IsNullOrEmpty(actorNameInDatabase) && actorAudioMap != null)
            {
                for (int i = 0; i < actorAudioMap.Length; i++)
                {
                    if (string.Equals(actorAudioMap[i].actorName, actorNameInDatabase, StringComparison.OrdinalIgnoreCase))
                    {
                        clips = actorAudioMap[i].clips ?? Array.Empty<AudioClip>();
                        return true;
                    }
                }
            }

            clips = Array.Empty<AudioClip>();
            return false;
        }

        private void CacheOriginalAudio(AbstractTypewriterEffect typewriter)
        {
            int key = typewriter.GetInstanceID();
            if (originalAudioByTypewriterId.ContainsKey(key))
            {
                return;
            }

            originalAudioByTypewriterId[key] = new TypewriterAudioState
            {
                audioClip = typewriter.audioClip,
                alternateAudioClips = CloneClips(typewriter.alternateAudioClips)
            };
        }

        private void RestoreOriginalAudio(AbstractTypewriterEffect typewriter)
        {
            int key = typewriter.GetInstanceID();
            if (!originalAudioByTypewriterId.TryGetValue(key, out var state))
            {
                return;
            }

            typewriter.audioClip = state.audioClip;
            typewriter.alternateAudioClips = CloneClips(state.alternateAudioClips);
        }

        private void ApplyClips(AbstractTypewriterEffect typewriter, AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                typewriter.audioClip = null;
                typewriter.alternateAudioClips = Array.Empty<AudioClip>();
                return;
            }

            typewriter.audioClip = clips[0];
            typewriter.alternateAudioClips = BuildAlternateClips(clips);
        }

        private AudioClip[] BuildAlternateClips(AudioClip[] clips)
        {
            if (clips == null || clips.Length <= 1)
            {
                return Array.Empty<AudioClip>();
            }

            var alternates = new AudioClip[clips.Length - 1];
            for (int i = 1; i < clips.Length; i++)
            {
                alternates[i - 1] = clips[i];
            }
            return alternates;
        }

        private AudioClip[] CloneClips(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return Array.Empty<AudioClip>();
            }

            var clone = new AudioClip[clips.Length];
            Array.Copy(clips, clone, clips.Length);
            return clone;
        }
    }
}
