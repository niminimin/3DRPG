using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReturnState : BaseState
{
    public override void LogicUpdata()
    {
        // 用 NavMeshAgent 的到达判定，不要比较 Vector3 相等：
        // agent 会在 stoppingDistance 之外就停下，position 几乎不可能与出生点完全相等。
        if (enemy.Locomotion.HasArrivedAtHome())
        {
            // 回位后去哪由敌人类型决定：巡逻型回 GUARD，巡逻型敌人可重写为 PATROL
            enemy.SwitchState(enemy.StateAfterReturn);
            return;
        }
    }

    public override void OnEnter(EnemyController target)
    {
        enemy = target;
        enemy.Locomotion.MoveTo(enemy.initPosition);   // 进入状态就出发
        enemy.animator.SetBool("IsWalk", true);
    }

    public override void OnExit()
    {
        enemy.animator.SetBool("IsWalk", false);
    }
}
