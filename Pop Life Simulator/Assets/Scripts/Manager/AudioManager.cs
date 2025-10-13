using UnityEngine;
using PopLife.Data;

namespace PopLife
{
    /// <summary>
    /// 音频管理器 - 管理音效和背景音乐播放
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance;

        [Header("音频配置")]
        [SerializeField] private AudioConfigSO audioConfig;

        [Header("AudioSource 引用")]
        [SerializeField] private AudioSource sfxSource;     // 音效播放器
        [SerializeField] private AudioSource musicSource;   // 背景音乐播放器

        [Header("音量设置")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;

        [Header("自动播放设置")]
        [SerializeField] private bool playMusicOnStart = true;
        [SerializeField] private string startMusicKey = "";
        [Tooltip("如果为空，则使用 AudioKeys.BGM_SHOP")]

        private string currentMusicKey;

        void Awake()
        {
            Instance = this;

            // 自动创建 AudioSource（如果未手动分配）
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
            }

            // 验证配置
            if (audioConfig == null)
            {
                Debug.LogWarning("[AudioManager] AudioConfigSO 未分配！请在 Inspector 中分配配置文件。");
            }
        }

        void Start()
        {
            // 游戏开始时自动播放背景音乐
            if (playMusicOnStart)
            {
                string musicKey = string.IsNullOrEmpty(startMusicKey)
                    ? AudioKeys.BGM_SHOP
                    : startMusicKey;

                PlayMusic(musicKey);
            }
        }

        #region 音效播放

        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="key">音效键（如 "Build_Condom", "BuildingMoved"）</param>
        /// <param name="volumeScale">音量倍率（0-1），默认1</param>
        public void PlaySound(string key, float volumeScale = 1f)
        {
            if (audioConfig == null)
            {
                Debug.LogWarning($"[AudioManager] AudioConfig 未配置，无法播放音效: {key}");
                return;
            }

            AudioClip clip = audioConfig.GetSound(key);
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip, masterVolume * sfxVolume * volumeScale);
            }
            else
            {
                Debug.LogWarning($"[AudioManager] 未找到音效: {key}");
            }
        }

        /// <summary>
        /// 检查音效是否存在
        /// </summary>
        public bool HasSound(string key)
        {
            return audioConfig != null && audioConfig.HasSound(key);
        }

        #endregion

        #region 背景音乐播放

        /// <summary>
        /// 播放背景音乐
        /// </summary>
        /// <param name="key">音乐键（如 "BGM_Shop", "BGM_Menu"）</param>
        /// <param name="fadeInDuration">淡入时长（秒），0表示立即播放</param>
        public void PlayMusic(string key, float fadeInDuration = 0f)
        {
            if (audioConfig == null)
            {
                Debug.LogWarning($"[AudioManager] AudioConfig 未配置，无法播放音乐: {key}");
                return;
            }

            // 如果正在播放相同的音乐，不重复播放
            if (currentMusicKey == key && musicSource.isPlaying)
            {
                return;
            }

            AudioClip clip = audioConfig.GetMusic(key);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] 未找到音乐: {key}");
                return;
            }

            // 停止当前音乐
            if (musicSource.isPlaying)
            {
                musicSource.Stop();
            }

            // 播放新音乐
            musicSource.clip = clip;
            musicSource.volume = masterVolume * musicVolume;
            musicSource.Play();
            currentMusicKey = key;

            // TODO: 实现淡入效果（可选）
            if (fadeInDuration > 0f)
            {
                // 可以使用协程实现淡入
                Debug.Log($"[AudioManager] 淡入功能暂未实现，直接播放: {key}");
            }
        }

        /// <summary>
        /// 停止背景音乐
        /// </summary>
        /// <param name="fadeOutDuration">淡出时长（秒），0表示立即停止</param>
        public void StopMusic(float fadeOutDuration = 0f)
        {
            if (musicSource.isPlaying)
            {
                // TODO: 实现淡出效果（可选）
                if (fadeOutDuration > 0f)
                {
                    Debug.Log($"[AudioManager] 淡出功能暂未实现，直接停止");
                }

                musicSource.Stop();
                currentMusicKey = null;
            }
        }

        /// <summary>
        /// 暂停背景音乐
        /// </summary>
        public void PauseMusic()
        {
            if (musicSource.isPlaying)
            {
                musicSource.Pause();
            }
        }

        /// <summary>
        /// 恢复背景音乐
        /// </summary>
        public void ResumeMusic()
        {
            if (!musicSource.isPlaying && musicSource.clip != null)
            {
                musicSource.UnPause();
            }
        }

        /// <summary>
        /// 检查音乐是否存在
        /// </summary>
        public bool HasMusic(string key)
        {
            return audioConfig != null && audioConfig.HasMusic(key);
        }

        #endregion

        #region 音量控制

        /// <summary>
        /// 设置主音量
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            UpdateVolumes();
        }

        /// <summary>
        /// 设置音效音量
        /// </summary>
        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// 设置背景音乐音量
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            UpdateVolumes();
        }

        private void UpdateVolumes()
        {
            if (musicSource != null)
            {
                musicSource.volume = masterVolume * musicVolume;
            }
        }

        #endregion
    }
}
