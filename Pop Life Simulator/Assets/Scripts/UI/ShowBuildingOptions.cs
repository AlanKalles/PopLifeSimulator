using UnityEngine;
using UnityEngine.UI;
using PopLife;

public class ShowBuildingOptions : MonoBehaviour
{
    private bool off = true;
    public GameObject buildingOptions;

    [Header("Phase-Dependent Button")]
    public Button phaseButton; // 新增的按钮，仅在建造阶段可用

    void Start()
    {
        // 订阅游戏阶段切换事件
        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.OnBuildPhaseStart += OnBuildPhase;
            DayLoopManager.Instance.OnStoreOpen += OnStoreOpen;

            // 初始化按钮状态
            UpdateButtonState();
        }
    }

    void OnDestroy()
    {
        // 取消订阅事件
        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.OnBuildPhaseStart -= OnBuildPhase;
            DayLoopManager.Instance.OnStoreOpen -= OnStoreOpen;
        }
    }

    public void TurnButtons()
    {
        if (off == false)
        {
            off = !off;
            buildingOptions.SetActive(false);
        }
        else
        {
            off = !off;
            buildingOptions.SetActive(true);
        }
    }

    /// <summary>
    /// 建造阶段开始时调用
    /// </summary>
    private void OnBuildPhase()
    {
        UpdateButtonState();
    }

    /// <summary>
    /// 营业阶段开始时调用
    /// </summary>
    private void OnStoreOpen()
    {
        UpdateButtonState();
    }

    /// <summary>
    /// 根据当前游戏阶段更新按钮状态
    /// </summary>
    private void UpdateButtonState()
    {
        if (phaseButton == null) return;

        if (DayLoopManager.Instance != null)
        {
            // 仅在建造阶段可交互
            bool isBuildPhase = DayLoopManager.Instance.currentPhase == GamePhase.BuildPhase;
            phaseButton.interactable = isBuildPhase;
        }
    }
}
