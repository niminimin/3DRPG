using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 跨场景保留的玩家进度快照。
/// 场景加载会销毁 Player，等级/经验会丢，所以传送前 Capture、到新场景后 Restore。
/// 纯静态类，不占用场景物体。
/// </summary>
public static class PlayerSession
{
    public static bool HasData;

    public static int Level = 1;
    public static int Exp;

    public static int CurHealth;
    public static int MaxHealth;

    public static int BonusHealth;
    public static int BonusAttack;
    public static int BonusDefence;

    /// <summary>把当前玩家进度存进静态变量。</summary>
    public static void Capture()
    {
        PlayerProgress progress = PlayerProgress.instance;
        if (progress == null) return;

        Level = progress.Level;
        Exp = progress.CurrentExp;

        CharacterStats stats = progress.stats;
        if (stats != null)
        {
            CurHealth = stats.CurHealth;
            MaxHealth = stats.MaxHealth;

            BonusHealth = stats.BonusMaxHealth;
            BonusAttack = stats.BonusAttack;
            BonusDefence = stats.BonusDefence;
        }

        HasData = true;
    }

    /// <summary>新场景里把进度套回玩家身上。</summary>
    public static void Restore()
    {
        if (!HasData) return;

        PlayerProgress progress = PlayerProgress.instance;
        if (progress == null) return;

        CharacterStats stats = progress.stats != null
            ? progress.stats
            : progress.GetComponent<CharacterStats>();

        if (stats != null)
        {
            // 先套加成（会把血填满），再把血量还原成传送前的值
            stats.ApplyBonuses(BonusHealth, BonusAttack, BonusDefence);
            stats.SetCurHealth(CurHealth);
        }

        progress.RestoreState(Level, Exp);
    }

    /// <summary>重新开始游戏时清空。</summary>
    public static void Clear()
    {
        HasData = false;
        Level = 1;
        Exp = 0;
        CurHealth = 0;
        MaxHealth = 0;
        BonusHealth = 0;
        BonusAttack = 0;
        BonusDefence = 0;
    }
}
