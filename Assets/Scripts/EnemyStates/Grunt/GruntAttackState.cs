using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss（Grunt）的攻击状态：
/// - 玩家进入技能范围（AttackDataSO.skillRange）且技能冷却好了 → 优先放技能
/// - 技能就绪但还差一点距离 → 主动贴近（以前是站桩，导致永远吃不到技能）
/// - 否则按普通攻击冷却出手
/// 两者是独立的冷却，互不抢占。
/// </summary>
public class GruntAttackState : BaseState
{
    /// <summary>首次进战后多久才允许放技能（避免一进攻击状态就吃大招）</summary>
    private const float FirstSkillDelay = 1.5f;

    private float attackTimer;
    private float skillTimer;

    public override void OnEnter(EnemyController enemy)
    {
        this.enemy = enemy;

        StandStill();                                         // 站定，不能边追边打
        enemy.Locomotion.FaceTo(enemy.Perception.TargetPosition);

        attackTimer = 0f;                                     // 进状态立刻打第一下
        if (skillTimer <= 0f) skillTimer = FirstSkillDelay;
    }

    public override void LogicUpdata()
    {
        // 受击硬直：被打暴击时不出手，正在放的技能已被 CancelPendingAttack 作废
        if (enemy.IsStaggered) return;

        // 目标丢失 → 回家
        if (!enemy.Perception.CanSeePlayer())
        {
            enemy.SwitchState(EnemyState.RETURN);
            return;
        }

        float distance = enemy.Perception.DistanceToTarget();
        if (distance > enemy.Combat.AttackExitRange)
        {
            enemy.SwitchState(EnemyState.CHASE);
            return;
        }

        GruntCombat combat = enemy.Combat as GruntCombat;

        // ==================== 技能优先 ====================
        skillTimer -= Time.deltaTime;

        if (combat != null && skillTimer <= 0f)
        {
            // 已经在技能范围内 → 直接放
            if (distance <= combat.SkillRange)
            {
                skillTimer = combat.skillCoolDown;
                StandStill();
                enemy.Locomotion.FaceTo(enemy.Perception.TargetPosition);
                combat.BeginSkill(enemy.Perception.Target);
                return;
            }

            // 技能就绪但还差距离 → 主动贴近。
            // 之前的写法是"站定等玩家进 1.5 米"，但玩家攻击距离也是 1.5（量到碰撞体表面），
            // 双方都停在 2 米左右 → 技能几乎永远触发不了。
            WalkTowardsTarget();
            return;
        }

        // ==================== 普通攻击 ====================
        StandStill();
        enemy.Locomotion.FaceTo(enemy.Perception.TargetPosition);

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = enemy.Combat.AttackCoolDown;
            combat.BeginAttack(enemy.Perception.Target);
        }
    }

    public override void OnExit()
    {
        // 离开攻击状态时，还没落地的攻击/技能作废
        enemy.Combat.CancelPendingAttack();
    }

    // ==================== 移动小工具 ====================
    /// <summary>边走边播走路动画（IsChase 控制敌人的走/站动画）。</summary>
    private void WalkTowardsTarget()
    {
        if (!enemy.animator.GetBool("IsChase"))
            enemy.animator.SetBool("IsChase", true);

        enemy.Locomotion.MoveTo(enemy.Perception.TargetPosition);
    }

    /// <summary>站定：只在真的还在走的时候才停，避免每帧都 ResetPath。</summary>
    private void StandStill()
    {
        if (enemy.animator.GetBool("IsChase"))
            enemy.animator.SetBool("IsChase", false);

        if (enemy.Locomotion.Agent != null && !enemy.Locomotion.Agent.isStopped)
            enemy.Locomotion.Stop();
    }
}
