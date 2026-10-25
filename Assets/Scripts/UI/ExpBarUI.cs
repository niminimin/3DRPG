using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 经验条 UI：显示等级、当前经验 / 升级所需经验。
/// 订阅 PlayerProgress 的事件，不轮询。
/// </summary>
public class ExpBarUI : MonoBehaviour
{
    [Header("数据源")]
    public PlayerProgress progress;

    [Header("显示")]
    public Image fillImage;

    [Tooltip("如：Lv.3   120 / 260")]
    public Text valueText;

    [Tooltip("满级时显示的文字")]
    public string maxLevelText = "MAX";

    private void Awake()
    {
        if (fillImage != null) fillImage.type = Image.Type.Filled;

        if (progress == null)
            progress = FindObjectOfType<PlayerProgress>();
    }

    private void OnEnable()
    {
        if (progress == null)
        {
            Debug.LogWarning("[" + name + "] 找不到 PlayerProgress，经验条不会动", this);
            return;
        }

        progress.OnExpChanged += Refresh;
        progress.OnLevelUp += HandleLevelUp;

        Refresh(progress.CurrentExp, progress.ExpToNext);
    }

    private void OnDisable()
    {
        if (progress == null) return;

        progress.OnExpChanged -= Refresh;
        progress.OnLevelUp -= HandleLevelUp;
    }

    private void Refresh(int current, int required)
    {
        if (progress == null) return;

        if (fillImage != null)
        {
            fillImage.fillAmount = progress.IsMaxLevel || required <= 0
                ? 1f
                : Mathf.Clamp01((float)current / required);
        }

        if (valueText != null)
        {
            valueText.text = progress.IsMaxLevel
                ? "Lv." + progress.Level + "   " + maxLevelText
                : "Lv." + progress.Level + "   " + current + " / " + required;
        }
    }

    private void HandleLevelUp(int newLevel)
    {
        // 升级瞬间把经验条刷成新等级的进度
        Refresh(progress.CurrentExp, progress.ExpToNext);
    }
}
