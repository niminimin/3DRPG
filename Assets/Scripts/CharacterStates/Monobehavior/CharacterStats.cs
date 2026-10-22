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
        get { return attackData == null ? 0f : attackData.criticalMultiplier; }
        set { if (attackData != null) attackData.criticalMultiplier = value; }
    }

    public float CriticalChance
    {
        get { return attackData == null ? 0f : attackData.criticalChance; }
        set { if (attackData != null) attackData.criticalChance = value; }
    }
    #endregion
}
