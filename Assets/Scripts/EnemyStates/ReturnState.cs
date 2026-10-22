using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReturnState : BaseState
{
    public override void LogicUpdata()
    {
        // 用 NavMeshAgent 的到达判定，不要比较 Vector3 相等：
        // agent 会在 stoppingDistance 之外就停下，position 几乎不可能与出生点完全相等。
        if (enemy.HasArrivedAtInit())
        {
            enemy.SwitchState(EnemyState.PATROL);
            return;
        }
    }

    public override void OnEnter(EnemyController target)
    {
        enemy = target;
        enemy.ReturnToInit();                       // 进入状态就出发
        enemy.animator.SetBool("IsWalk", true);
    }

    public override void OnExit()
    {
        enemy.animator.SetBool("IsWalk", false);
    }
}
