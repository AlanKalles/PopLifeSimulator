using System;
using UnityEngine;

namespace PopLife.Data
{
    /// <summary>
    /// 音频配置 ScriptableObject - 管理所有音效和背景音乐
    /// 支持一个键对应多个音频，播放时随机选择
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Audio/AudioConfig", fileName = "AudioConfig")]
    public class AudioConfigSO : ScriptableObject
    {
        [Header("音效配置")]
        [SerializeField] private SoundClipEntry[] soundClips;

        [Header("背景音乐配置")]
        [SerializeField] private MusicClipEntry[] musicClips;

        [Header("角色情感音效")]
        [Tooltip("每个角色的情感/反应语音配置（笑声、叹气、惊讶等）")]
        [SerializeField] private ActorEmoteSet[] actorEmotes;

        [Tooltip("角色未配置该情感时的全局回退音效")]
        [SerializeField] private EmoteEntry[] defaultEmotes;

        /// <summary>
        /// 根据键随机获取一个音效（支持多个音频的情况）
        /// </summary>
        public AudioClip GetSound(string key)
        {
            if (soundClips == null) return null;

            foreach (var entry in soundClips)
            {
                if (entry.key == key)
                    return entry.GetRandomClip();
            }

            return null;
        }

        /// <summary>
        /// 根据键随机获取一个背景音乐（支持多个音频的情况）
        /// </summary>
        public AudioClip GetMusic(string key)
        {
            if (musicClips == null) return null;

            foreach (var entry in musicClips)
            {
                if (entry.key == key)
                    return entry.GetRandomClip();
            }

            return null;
        }

        /// <summary>
        /// 检查音效是否存在
        /// </summary>
        public bool HasSound(string key)
        {
            return GetSound(key) != null;
        }

        /// <summary>
        /// 检查背景音乐是否存在
        /// </summary>
        public bool HasMusic(string key)
        {
            return GetMusic(key) != null;
        }

        /// <summary>
        /// 获取指定键的音乐配置（用于获取loop、volume等额外信息）
        /// </summary>
        public MusicClipEntry GetMusicEntry(string key)
        {
            if (musicClips == null) return null;

            foreach (var entry in musicClips)
            {
                if (entry.key == key)
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// 查询角色情感音效条目（含 fallback）
        /// 查找顺序：
        ///   1. actorEmotes[actorName][emoteType]
        ///   2. defaultEmotes[emoteType]
        ///   3. null（交由调用方处理 warning + 静默）
        /// </summary>
        public EmoteEntry GetEmote(string actorName, string emoteType)
        {
            if (string.IsNullOrEmpty(emoteType)) return null;

            // 1. 查该角色的专属情感
            if (!string.IsNullOrEmpty(actorName) && actorEmotes != null)
            {
                foreach (var set in actorEmotes)
                {
                    if (set == null || set.actorName != actorName || set.emotes == null) continue;
                    foreach (var emote in set.emotes)
                    {
                        if (emote != null && emote.emoteType == emoteType)
                            return emote;
                    }
                }
            }

            // 2. 回落到全局默认情感池
            if (defaultEmotes != null)
            {
                foreach (var emote in defaultEmotes)
                {
                    if (emote != null && emote.emoteType == emoteType)
                        return emote;
                }
            }

            return null;
        }

        [Serializable]
        public class SoundClipEntry
        {
            [Tooltip("音效键（如 Build_Condom, BuildingMoved）")]
            public string key;

            [Tooltip("音效文件列表（多个时随机播放）")]
            public AudioClip[] clips;

            /// <summary>
            /// 从clips数组中随机返回一个AudioClip
            /// </summary>
            public AudioClip GetRandomClip()
            {
                if (clips == null || clips.Length == 0)
                    return null;

                if (clips.Length == 1)
                    return clips[0];

                // 随机选择一个非空的clip
                int maxAttempts = clips.Length * 2; // 防止死循环
                int attempts = 0;

                while (attempts < maxAttempts)
                {
                    int randomIndex = UnityEngine.Random.Range(0, clips.Length);
                    if (clips[randomIndex] != null)
                        return clips[randomIndex];

                    attempts++;
                }

                // 如果所有随机尝试都失败，遍历找到第一个非空clip
                foreach (var clip in clips)
                {
                    if (clip != null)
                        return clip;
                }

                return null;
            }
        }

        [Serializable]
        public class EmoteEntry
        {
            [Tooltip("情感类型（如 Laugh, Sigh, Surprise, Gasp）")]
            public string emoteType;

            [Tooltip("音效文件列表（多个时随机播放）")]
            public AudioClip[] clips;

            [Range(0f, 2f)]
            [Tooltip("音量倍率（0-2），默认 1")]
            public float volumeScale = 1f;

            /// <summary>
            /// 从 clips 中随机选一个非空 AudioClip（同 SoundClipEntry 的逻辑）
            /// </summary>
            public AudioClip GetRandomClip()
            {
                if (clips == null || clips.Length == 0)
                    return null;

                if (clips.Length == 1)
                    return clips[0];

                int maxAttempts = clips.Length * 2;
                int attempts = 0;

                while (attempts < maxAttempts)
                {
                    int randomIndex = UnityEngine.Random.Range(0, clips.Length);
                    if (clips[randomIndex] != null)
                        return clips[randomIndex];

                    attempts++;
                }

                foreach (var clip in clips)
                {
                    if (clip != null)
                        return clip;
                }

                return null;
            }
        }

        [Serializable]
        public class ActorEmoteSet
        {
            [Tooltip("角色名称（对应 Dialogue Database 里的 Actor Name，即 speakerInfo.nameInDatabase）")]
            public string actorName;

            [Tooltip("该角色的情感音效列表")]
            public EmoteEntry[] emotes;
        }

        [Serializable]
        public class MusicClipEntry
        {
            [Tooltip("音乐键（如 BGM_Shop, BGM_Menu）")]
            public string key;

            [Tooltip("音乐文件列表（多个时随机播放）")]
            public AudioClip[] clips;

            [Tooltip("是否循环播放")]
            public bool loop = true;

            [Range(0f, 1f)]
            [Tooltip("音量（0-1）")]
            public float volume = 0.5f;

            /// <summary>
            /// 从clips数组中随机返回一个AudioClip
            /// </summary>
            public AudioClip GetRandomClip()
            {
                if (clips == null || clips.Length == 0)
                    return null;

                if (clips.Length == 1)
                    return clips[0];

                // 随机选择一个非空的clip
                int maxAttempts = clips.Length * 2; // 防止死循环
                int attempts = 0;

                while (attempts < maxAttempts)
                {
                    int randomIndex = UnityEngine.Random.Range(0, clips.Length);
                    if (clips[randomIndex] != null)
                        return clips[randomIndex];

                    attempts++;
                }

                // 如果所有随机尝试都失败，遍历找到第一个非空clip
                foreach (var clip in clips)
                {
                    if (clip != null)
                        return clip;
                }

                return null;
            }
        }
    }
}
