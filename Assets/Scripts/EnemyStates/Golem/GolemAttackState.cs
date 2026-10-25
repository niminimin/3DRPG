using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss（Rock Golem）的攻击状态：
/// - 正在播攻击动画时：站着看完，绝不边投石边近身
/// - 玩家贴到近身范围 → 近身攻击（击退）
/// - 玩家在"靠近范围"内但还没到近身距离 → 慢慢挪两步
/// - 在远程范围内 → 站桩扔石头
/// - 更远：站着不动（Boss 不追人）
/// </summary>
public class GolemAttackState : BaseState
{
    private float meleeTimer;
    private float rangedTimer;

    public override void OnEnter(EnemyController enemy)
    {
        this.enemy = enemy;

        enemy.Locomotion.Stop();
        enemy.Locomotion.FaceTo(enemy.Perception.TargetPosition);
        enemy.animator.SetBool("IsWalk", false);
        enemy.animator.SetBool("IsChase", false);

        meleeTimer = 0f;
        rangedTimer = 0f;       // 一进战斗立刻扔一块石头
    }

    public override void LogicUpdata()
    {
        // 受击硬直：被打暴击时不出手
        if (enemy.IsStaggered) return;

        if (!enemy.Perception.CanSeePlayer())
        {
            enemy.SwitchState(EnemyState.RETURN);
            return;
        }

        enemy.Locomotion.FaceTo(enemy.Perception.TargetPosition);

        GolemCombat combat = enemy.Combat as GolemCombat;
        if (combat == null)
        {
            enemy.SwitchState(EnemyState.RETURN);
            return;
        }

        float distance = enemy.Perception.DistanceToTarget();

        // 冷却照常走（动画期间也在恢复）
        meleeTimer -= Time.deltaTime;
        rangedTimer -= Time.deltaTime;

        // 0) 正在播攻击动画：站着看完，别叠加下一段
        if (combat.IsBusy)
        {
            enemy.Locomotion.Stop();
            enemy.animator.SetBool("IsWalk", false);
            return;
        }

        // 1) 贴脸：近身攻击（击退）
        if (distance <= combat.MeleeRange && meleeTimer <= 0f)
        {
            meleeTimer = combat.meleeCoolDown;
            enemy.Locomotion.Stop();
            enemy.animator.SetBool("IsWalk", false);
            combat.BeginMelee(enemy.Perception.Target);
            return;
        }

        // 2) 玩家靠近但还没到近身距离：慢慢挪过去（这段不投石，动作更清楚）
        if (distance > combat.MeleeRange && distance <= combat.ApproachRange)
        {
            enemy.Locomotion.MoveTo(enemy.Perception.TargetPosition);
            enemy.animator.SetBool("IsWalk", true);
            return;
        }

        // 3) 远程范围：站桩投石
        if (distance <= combat.RangedRange && rangedTimer <= 0f)
        {
            rangedTimer = combat.rangedCoolDown;
            enemy.Locomotion.Stop();
            enemy.animator.SetBool("IsWalk", false);
            combat.BeginRanged(enemy.Perception.Target);
            return;
        }

        // 4) 其他情况：站着不动（Boss 不追人）
        enemy.Locomotion.Stop();
        enemy.animator.SetBool("IsWalk", false);
    }

    public override void OnExit()
    {
        enemy.Locomotion.Stop();
        enemy.animator.SetBool("IsWalk", false);
        enemy.Combat.CancelPendingAttack();
    }
}
