using System;
using UnityEngine;

namespace PopLife.Data
{
    /// <summary>
    /// 音频配置 ScriptableObject - 管理所有音效和背景音乐
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Audio/AudioConfig", fileName = "AudioConfig")]
    public class AudioConfigSO : ScriptableObject
    {
        [Header("音效配置")]
        [SerializeField] private SoundClipEntry[] soundClips;

        [Header("背景音乐配置")]
        [SerializeField] private MusicClipEntry[] musicClips;

        /// <summary>
        /// 根据键获取音效
        /// </summary>
        public AudioClip GetSound(string key)
        {
            if (soundClips == null) return null;

            foreach (var entry in soundClips)
            {
                if (entry.key == key)
                    return entry.clip;
            }

            return null;
        }

        /// <summary>
        /// 根据键获取背景音乐
        /// </summary>
        public AudioClip GetMusic(string key)
        {
            if (musicClips == null) return null;

            foreach (var entry in musicClips)
            {
                if (entry.key == key)
                    return entry.clip;
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

        [Serializable]
        public class SoundClipEntry
        {
            [Tooltip("音效键（如 Build_Condom, BuildingMoved）")]
            public string key;

            [Tooltip("音效文件")]
            public AudioClip clip;
        }

        [Serializable]
        public class MusicClipEntry
        {
            [Tooltip("音乐键（如 BGM_Shop, BGM_Menu）")]
            public string key;

            [Tooltip("音乐文件")]
            public AudioClip clip;

            [Tooltip("是否循环播放")]
            public bool loop = true;

            [Range(0f, 1f)]
            [Tooltip("音量（0-1）")]
            public float volume = 0.5f;
        }
    }
}
