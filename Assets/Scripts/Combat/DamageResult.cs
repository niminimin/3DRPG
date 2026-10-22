using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一次攻击的结算结果：由 CharacterStats.CalculateDamage 产出，
/// 表现层（Controller/状态机）只消费结果，不再自己判断是否暴击。
/// </summary>
public struct DamageResult
{
    /// <summary>最终伤害（已乘暴击倍率、已减目标防御，最小 0）。</summary>
    public int amount;

    /// <summary>这次攻击是否命中暴击。</summary>
    public bool isCritical;
}
