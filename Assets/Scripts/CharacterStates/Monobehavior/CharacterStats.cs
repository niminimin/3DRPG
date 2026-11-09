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

    // 升级带来的永久加成（只加在运行时，绝不去改 ScriptableObject 资产）
    private int bonusMaxHealth;
    private int bonusAttack;
    private int bonusDefence;

    // 死亡标记：保证 OnDead 只触发一次，同时死后不再结算伤害
    private bool isDead;

    /// <summary>是否已经死亡。</summary>
    public bool IsDead
    {
        get { return isDead; }
    }

    /// <summary>血量变化时通知（参数为当前血量），UI 血条等外部系统订阅用。</summary>
    public event System.Action<int> OnHealthChanged;

    /// <summary>受到伤害时通知（携带本次伤害结果），受击表现等订阅用。</summary>
    public event System.Action<DamageResult> OnDamaged;

    /// <summary>血量归零时通知一次，死亡状态/掉落等订阅用。</summary>
    public event System.Action OnDead;

    private void Awake()
    {
        ResetCurStats();
    }

    /// <summary>把运行时状态恢复成模板值（初始满血、防御回到基础值）。</summary>
    public void ResetCurStats()
    {
        isDead = false;

        if (characterData == null)
        {
            curHealth = 0;
            curDefence = 0;
            return;
        }
        curHealth = MaxHealth;
        curDefence = MaxDefence;
    }

    #region readCharacterData
    /// <summary>最大生命 = 模板值 + 升级加成。</summary>
    public int MaxHealth
    {
        get { return (characterData == null ? 0 : characterData.maxHealth) + bonusMaxHealth; }
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

    /// <summary>防御上限 = 模板值 + 升级加成。</summary>
    public int MaxDefence
    {
        get { return (characterData == null ? 0 : characterData.baseDefence) + bonusDefence; }
    }

    public int CurDefence
    {
        get { return curDefence; }
        set { curDefence = value; }
    }

    /// <summary>攻击加成（升级获得），会加到伤害区间上。</summary>
    public int BonusAttack
    {
        get { return bonusAttack; }
    }

    /// <summary>生命加成（升级获得）。</summary>
    public int BonusMaxHealth
    {
        get { return bonusMaxHealth; }
    }

    /// <summary>防御加成（升级获得）。</summary>
    public int BonusDefence
    {
        get { return bonusDefence; }
    }

    /// <summary>被击败时给玩家的经验（来自 CharacterDataSO）。</summary>
    public int ExpReward
    {
        get { return characterData == null ? 0 : characterData.expReward; }
    }

    /// <summary>被击败时掉落的金币（来自 CharacterDataSO）。</summary>
    public int GoldReward
    {
        get { return characterData == null ? 0 : characterData.goldReward; }
    }
    #endregion

    #region 成长（升级用）
    /// <summary>永久增加生命上限，同时补等量血（升级时回血）。</summary>
    public void AddBonusMaxHealth(int amount)
    {
        if (amount == 0) return;

        bonusMaxHealth += amount;
        curHealth = Mathf.Clamp(curHealth + amount, 0, MaxHealth);

        if (OnHealthChanged != null) OnHealthChanged(curHealth);
    }

    /// <summary>永久增加攻击力。</summary>
    public void AddBonusAttack(int amount)
    {
        bonusAttack += amount;
    }

    /// <summary>永久增加防御力（运行时防御同步到新上限）。</summary>
    public void AddBonusDefence(int amount)
    {
        bonusDefence += amount;
        curDefence = MaxDefence;
    }

    /// <summary>直接套用一组加成（跨场景恢复进度用）。</summary>
    public void ApplyBonuses(int health, int attack, int defence)
    {
        bonusMaxHealth = health;
        bonusAttack = attack;
        bonusDefence = defence;

        curHealth = MaxHealth;
        curDefence = MaxDefence;

        if (OnHealthChanged != null) OnHealthChanged(curHealth);
    }

    /// <summary>直接设置当前血量（跨场景恢复血量用，会夹在上限内）。</summary>
    public void SetCurHealth(int value)
    {
        curHealth = Mathf.Clamp(value, 0, MaxHealth);
        if (OnHealthChanged != null) OnHealthChanged(curHealth);
    }

    /// <summary>复活：清掉死亡标记并回血（读档时玩家可能是死的）。</summary>
    public void Revive(int health)
    {
        isDead = false;
        curHealth = Mathf.Clamp(health, 1, MaxHealth);
        curDefence = MaxDefence;

        if (OnHealthChanged != null) OnHealthChanged(curHealth);
    }

    /// <summary>
    /// 回血（药水用）。返回实际回复量：满血时返回 0，调用方据此决定要不要消耗药水。
    /// </summary>
    public int Heal(int amount)
    {
        if (amount <= 0 || isDead) return 0;

        int before = curHealth;
        curHealth = Mathf.Min(curHealth + amount, MaxHealth);
        int healed = curHealth - before;

        if (healed > 0 && OnHealthChanged != null) OnHealthChanged(curHealth);
        return healed;
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
        // 升级获得的攻击力直接加到伤害区间上
        int damage = Random.Range(MinDamage + bonusAttack, MaxDamage + bonusAttack + 1);

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
        if (result.amount <= 0 || isDead) return;       // 已经死了就不再吃伤害

        CurHealth = Mathf.Max(CurHealth - result.amount, 0);

        Debug.Log("[" + name + "] 受到 " + result.amount + " 点伤害"
                  + (result.isCritical ? "（暴击）" : "")
                  + "，剩余 " + CurHealth + "/" + MaxHealth);

        if (OnHealthChanged != null) OnHealthChanged(CurHealth);

        // 致死的一击：直接走死亡流程，不再播受击表现（避免和死亡动画抢）
        if (CurHealth <= 0)
        {
            isDead = true;
            if (OnDead != null) OnDead();
            return;
        }

        // 还活着才通知受击（暴击与否由订阅方决定播不播）
        if (OnDamaged != null) OnDamaged(result);
    }
    #endregion
}
