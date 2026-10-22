using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    public CharacterDataSO characterData;
    public AttackDataSO attackData;

    // 运行时状态：每个角色实例各自一份，不写回 SO 资产，
    // 也不会被同预制体的其他实例共享。
    private int curHealth;
    private int curDefence;

    /// <summary>血量变化时通知（参数为当前血量），UI 血条等外部系统订阅用。</summary>
    public event System.Action<int> OnHealthChanged;

    /// <summary>血量归零时通知一次，死亡状态/掉落等订阅用。</summary>
    public event System.Action OnDead;

    private void Awake()
    {
        ResetCurStats();
    }

    /// <summary>把运行时状态恢复成模板值（初始满血、防御回到基础值）。</summary>
    public void ResetCurStats()
    {
        if (characterData == null)
        {
            curHealth = 0;
            curDefence = 0;
            return;
        }
        curHealth = characterData.maxHealth;
        curDefence = characterData.baseDefence;
    }

    #region readCharacterData
    public int MaxHealth
    {
        get { return characterData == null ? 0 : characterData.maxHealth; }
        set { if (characterData != null) characterData.maxHealth = value; }
    }

    public int CurHealth
    {
        get { return curHealth; }
        set { curHealth = value; }
    }

    public int BaseDefence
    {
        get { return characterData == null ? 0 : characterData.baseDefence; }
        set { if (characterData != null) characterData.baseDefence = value; }
    }

    public int CurDefence
    {
        get { return curDefence; }
        set { curDefence = value; }
    }
    #endregion

    #region ReadAttackData
    public float AttackRange
    {
        get { return attackData == null ? 0f : attackData.attackRange; }
        set { if (attackData != null) attackData.attackRange = value; }
    }

    public float SkillRange
    {
        get { return attackData == null ? 0f : attackData.skillRange; }
        set { if (attackData != null) attackData.skillRange = value; }
    }

    public float CoolDown
    {
        get { return attackData == null ? 0f : attackData.coolDown; }
        set { if (attackData != null) attackData.coolDown = value; }
    }

    public int MaxDamage
    {
        get { return attackData == null ? 0 : attackData.maxDamage; }
        set { if (attackData != null) attackData.maxDamage = value; }
    }

    public int MinDamage
    {
        get { return attackData == null ? 0 : attackData.minDamage; }
        set { if (attackData != null) attackData.minDamage = value; }
    }

    public float CriticalMultiplier
    {
        get { return attackData == null ? 1f : attackData.criticalMultiplier; }
        set { if (attackData != null) attackData.criticalMultiplier = value; }
    }

    public float CriticalChance
    {
        get { return attackData == null ? 0f : attackData.criticalChance; }
        set { if (attackData != null) attackData.criticalChance = value; }
    }
    #endregion

    #region 伤害结算
    /// <summary>
    /// 攻击方：计算一次伤害（含暴击判定）。
    /// 暴击只在这里掷一次骰子，表现层（动画）和扣血共用同一个结果。
    /// </summary>
    public DamageResult CalculateDamage(CharacterStats target)
    {
        DamageResult result = new DamageResult();

        // Random.Range(int, int) 是开区间，+1 变成闭区间
        int damage = Random.Range(MinDamage, MaxDamage + 1);

        result.isCritical = Random.value < CriticalChance;
        if (result.isCritical)
            damage = Mathf.RoundToInt(damage * Mathf.Max(1f, CriticalMultiplier));

        int defence = target == null ? 0 : target.CurDefence;
        result.amount = Mathf.Max(damage - defence, 0);

        return result;
    }

    /// <summary>防御方：扣血并广播事件（环境伤害等无暴击来源可直接用这个重载）。</summary>
    public void TakeDamage(int amount)
    {
        TakeDamage(new DamageResult { amount = amount, isCritical = false });
    }

    /// <summary>防御方：扣血并广播事件。</summary>
    public void TakeDamage(DamageResult result)
    {
        if (result.amount <= 0) return;

        CurHealth = Mathf.Max(CurHealth - result.amount, 0);

        Debug.Log("[" + name + "] 受到 " + result.amount + " 点伤害"
                  + (result.isCritical ? "（暴击）" : "")
                  + "，剩余 " + CurHealth + "/" + MaxHealth);

        if (OnHealthChanged != null) OnHealthChanged(CurHealth);
        if (CurHealth <= 0 && OnDead != null) OnDead();
    }
    #endregion
}
