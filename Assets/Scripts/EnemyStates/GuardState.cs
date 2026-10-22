using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuardState : BaseState
{
    public override void LogicUpdata()
    {
        if (enemy.FoundPlayer())
        {
            enemy.SwitchState(EnemyState.CHASE);
        }
    }

    public override void OnEnter(EnemyController target)
    {
        enemy = target;
        enemy.StopAgent();      // 站定：停住并清掉残余路径
    }

    public override void OnExit()
    {
        
    }
}
