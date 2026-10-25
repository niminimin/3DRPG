using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家升级进度：经验、等级、属性成长。
/// - 经验来源：敌人死亡时按 CharacterDataSO.expReward 发放
/// - 升级曲线与成长值都在 PlayerLevelDataSO 里配
/// - 通过事件通知 UI（不需要每帧轮询）
/// 继承 Singleton&lt;PlayerProgress&gt;，与其他管理器保持一致（防重复、销毁自动清空）。
/// </summary>
public class PlayerProgress : Singleton<PlayerProgress>
{
    [Header("配置")]
    public PlayerLevelDataSO levelData;

    [Header("绑定")]
    public CharacterStats stats;

    [Tooltip("升级时在头顶冒出的提示文字")]
    public bool showLevelUpPopup = true;

    /// <summary>当前等级（从 1 开始）。</summary>
    public int Level { get; private set; } = 1;

    /// <summary>当前等级已累计的经验。</summary>
    public int CurrentExp { get; private set; }

    /// <summary>是否已满级。</summary>
    public bool IsMaxLevel
    {
        get { return levelData == null || Level >= levelData.maxLevel; }
    }

    /// <summary>升到下一级还需要多少经验（满级为 0）。</summary>
    public int ExpToNext
    {
        get { return levelData == null ? 0 : levelData.ExpToNext(Level); }
    }

    /// <summary>经验变化：(当前经验, 升级所需经验)。</summary>
    public event Action<int, int> OnExpChanged;

    /// <summary>升级：(新等级)。</summary>
    public event Action<int> OnLevelUp;

    protected override void Awake()
    {
        base.Awake();       // ★ 基类负责 instance 赋值 + 重复实例销毁

        if (stats == null) stats = GetComponent<CharacterStats>();
        if (levelData == null)
            Debug.LogWarning("[" + name + "] 没有配置 PlayerLevelDataSO，升级不会生效", this);
    }

    private void Start()
    {
        // 通知一次初始值，让 UI 显示 Lv.1 0/xx
        if (OnExpChanged != null) OnExpChanged(CurrentExp, ExpToNext);
    }

    /// <summary>获得经验（可一次升多级）。</summary>
    public void AddExp(int amount)
    {
        if (amount <= 0) return;
        if (levelData == null) return;

        if (IsMaxLevel)
        {
            // 满级后不再积累
            if (OnExpChanged != null) OnExpChanged(CurrentExp, 0);
            return;
        }

        CurrentExp += amount;

        while (!IsMaxLevel && CurrentExp >= ExpToNext)
        {
            CurrentExp -= ExpToNext;
            LevelUp();
        }

        if (IsMaxLevel) CurrentExp = 0;     // 满级后经验条清空

        if (OnExpChanged != null) OnExpChanged(CurrentExp, ExpToNext);
    }

    private void LevelUp()
    {
        Level++;

        if (stats != null && levelData != null)
        {
            stats.AddBonusMaxHealth(levelData.healthPerLevel);
            stats.AddBonusAttack(levelData.attackPerLevel);
            stats.AddBonusDefence(levelData.defencePerLevel);
        }

        Debug.Log("[Player] 升级到 Lv." + Level);

        if (OnLevelUp != null) OnLevelUp(Level);

        if (showLevelUpPopup) SpawnLevelUpPopup();
    }

    private void SpawnLevelUpPopup()
    {
        if (DamagePopupPool.instance == null) return;

        DamagePopup popup = DamagePopupPool.instance.Get(transform.position + Vector3.up * 2.4f);
        if (popup != null)
            popup.SetupText("LEVEL UP!  Lv." + Level, new Color(1f, 0.85f, 0.25f), 28);
    }
}
