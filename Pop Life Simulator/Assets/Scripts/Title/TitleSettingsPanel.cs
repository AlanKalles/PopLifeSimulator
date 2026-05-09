using UnityEngine;
using UnityEngine.UI;

namespace PopLife.Title
{
    /// <summary>
    /// Title Settings 面板：三滑条调节 AudioManager 音量 + Close 按钮
    /// AudioManager 缺失时滑条仍可滑动但不生效（?. 安全访问）
    /// </summary>
    public class TitleSettingsPanel : MonoBehaviour
    {
        [Header("音量滑条")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("关闭按钮")]
        [SerializeField] private Button closeButton;

        private void Start()
        {
            // 初值：AudioManager 只对外暴露 GetMusicVolume，其他默认 1f
            if (musicSlider != null)
                musicSlider.SetValueWithoutNotify(AudioManager.Instance != null
                    ? AudioManager.Instance.GetMusicVolume()
                    : 0.5f);
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(1f);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(1f);

            if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDestroy()
        {
            if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnMasterChanged(float v) => AudioManager.Instance?.SetMasterVolume(v);
        private void OnMusicChanged(float v) => AudioManager.Instance?.SetMusicVolume(v);
        private void OnSfxChanged(float v) => AudioManager.Instance?.SetSfxVolume(v);

        private void OnCloseClicked()
        {
            gameObject.SetActive(false);
        }
    }
}
