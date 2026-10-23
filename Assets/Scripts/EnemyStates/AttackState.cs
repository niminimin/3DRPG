using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻击状态：站定、面向玩家、按 AttackDataSO 里的攻击距离和冷却出手。
/// 目前只打印日志，动画和伤害逻辑留待后续实现。
/// </summary>
public class AttackState : BaseState
{
    private float attackTimer;

    public override void OnEnter(EnemyController enemy)
    {
        this.enemy = enemy;

        enemy.Locomotion.Stop();                // 站定，不能边追边打
        enemy.Locomotion.FaceTo(enemy.TargetPosition);   // 立刻转向玩家
        enemy.animator.SetBool("IsChase", false);

        attackTimer = 0f;                       // 进入状态立刻打第一下
    }

    public override void LogicUpdata()
    {
        // 目标丢失 → 回家
        if (!enemy.FoundPlayer())
        {
            enemy.SwitchState(EnemyState.RETURN);
            return;
        }

        enemy.Locomotion.FaceTo(enemy.TargetPosition);   // 玩家绕后时保持面向

        // 目标跑出攻击距离（AttackExitRange = AttackRange + 余量，避免边界抖动）
        if (enemy.DistanceToPlayer() > enemy.AttackExitRange)
        {
            enemy.SwitchState(EnemyState.CHASE);
            return;
        }

        // 冷却计时：到点就出手一次，间隔取自 AttackDataSO.coolDown
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = enemy.AttackCoolDown;
            enemy.Attack();                     // 只负责：算好伤害 + 播动画；伤害由动画事件 OnAttackHit 结算
        }
    }

    public override void OnExit()
    {
        // 离开攻击状态时，还没播到命中帧的那一下作废（例如追踪目标跑出范围）
        enemy.CancelPendingAttack();
    }
}
