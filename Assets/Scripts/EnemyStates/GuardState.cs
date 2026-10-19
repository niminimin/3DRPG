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
    }

    public override void OnExit()
    {
        
    }
}
