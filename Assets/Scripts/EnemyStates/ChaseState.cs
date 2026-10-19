using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseState : BaseState
{
    public override void LogicUpdata()
    {
        if (!enemy.FoundPlayer())
        {
            enemy.SwitchState(EnemyState.GUARD);
        }
    }

    public override void OnEnter(EnemyController target)
    {
        enemy = target;
        Debug.Log("chase");
        //TODO 动画
    }

    public override void OnExit()
    {
        
    }
}
