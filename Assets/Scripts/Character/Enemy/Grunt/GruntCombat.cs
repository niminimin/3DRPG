using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss（Grunt）的战斗能力：
/// - 普通攻击：Attack01，范围 = AttackDataSO.attackRange（较大，例如 2.5）
/// - 技能：Attack02 二段，范围 = AttackDataSO.skillRange（较小，例如 1.5）
///   第一段命中 → 伤害 + 把玩家垂直击飞 + 眩晕
///   第二段命中 → 再补一刀（眩晕期间不免疫，所以能打中）
/// 技能和暴击解耦：暴击只影响伤害倍率，不影响播哪个 clip。
/// </summary>
public class GruntCombat : EnemyCombat
{
    [Header("技能（Attack02 二段）")]
    [Tooltip("技能冷却（秒）")]
    public float skillCoolDown = 6f;

    [Tooltip("击飞初速度（米/秒）。滞空时间 = 2 × 该值 / 20 秒（6 ≈ 0.9 米高、0.6 秒滞空）")]
    public float launchUpSpeed = 6f;

    [Tooltip("总眩晕时长（包含滞空）：落地后还会继续晕（dizzyTime − 滞空）秒")]
    public float dizzyTime = 1.6f;

    [Tooltip("第二刀判定范围：要比技能范围大，因为玩家被顶起来后位置可能有偏差")]
    public float secondHitRange = 2.2f;

    private GameObject skillTarget;
    private bool skillPending;

    /// <summary>技能范围：来自 AttackDataSO.skillRange，比普通攻击范围小。</summary>
    public float SkillRange
    {
        get { return Stats == null ? 0f : Stats.SkillRange; }
    }

    /// <summary>开始技能：播 Attack02（用 CriticalAttack 触发器），两段伤害在动画事件里结算。</summary>
    public void BeginSkill(GameObject target)
    {
        if (Stats == null || target == null) return;

        skillTarget = target;
        skillPending = true;
        hasPendingDamage = false;       // 技能期间不走普通攻击的结算

        if (Anim != null) Anim.SetTrigger("CriticalAttack");
    }

    /// <summary>
    /// 【动画事件】技能两段命中帧调用（Attack02 上放两个事件）：
    /// intParameter = 1 → 第一段（伤害 + 击飞），2 → 第二段（补刀伤害）。
    /// </summary>
    public void OnSkillHit(int phase)
    {
        if (!skillPending || skillTarget == null) return;
        if (Stats != null && Stats.IsDead) { skillPending = false; return; }

        CharacterStats targetStats = skillTarget.GetComponent<CharacterStats>();
        if (targetStats == null || targetStats.IsDead) { skillPending = false; return; }

        if (phase == 1)
        {
            ApplyDamage(skillTarget, Stats.CalculateDamage(targetStats), SkillRange);

            // 击飞 + 眩晕：眩晕期间不免疫伤害，所以第二段还能打中
            PlayerController player = skillTarget.GetComponent<PlayerController>();
            if (player != null) player.ApplyLaunch(transform.position, launchUpSpeed, dizzyTime);
            return;
        }

        if (phase == 2)
        {
            // 玩家这时在空中，水平距离几乎没变；用更宽松的判定确保第二刀打得到
            ApplyDamage(skillTarget, Stats.CalculateDamage(targetStats), secondHitRange);
            skillPending = false;
        }
    }

    /// <summary>普通攻击：Boss 的 Attack02 被技能占用，所以始终播 Attack01（不播暴击 clip）。</summary>
    public override void BeginAttack(GameObject target)
    {
        skillPending = false;           // 普通攻击打断挂起的技能

        currentTarget = target;
        CharacterStats targetStats = target != null ? target.GetComponent<CharacterStats>() : null;

        if (Stats == null || targetStats == null)
        {
            hasPendingDamage = false;
            if (Anim != null) Anim.SetTrigger("Attack");
            return;
        }

        pendingDamage = Stats.CalculateDamage(targetStats);
        hasPendingDamage = true;

        if (Anim != null) Anim.SetTrigger("Attack");
    }

    public override void CancelPendingAttack()
    {
        base.CancelPendingAttack();
        skillPending = false;
    }
}
