using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseState : BaseState
{
    public override void LogicUpdata()
    {
        if (!enemy.FoundPlayer())
        {
            enemy.SwitchState(EnemyState.RETURN);
            return;
        }

        // 攻击距离来自 AttackDataSO；没配数据（<= 0）时不会进入攻击状态
        float attackRange = enemy.AttackRange;
        if (attackRange > 0f && enemy.DistanceToPlayer() <= attackRange)
        {
            enemy.SwitchState(EnemyState.ATTACK);
            return;
        }

        enemy.Locomotion.MoveTo(enemy.TargetPosition);
    }

    public override void OnEnter(EnemyController target)
    {
        enemy = target;
        //TODO 动画
        enemy.animator.SetBool("IsChase", true);
    }

    public override void OnExit()
    {
        enemy.animator.SetBool("IsChase", false);
    }
}
