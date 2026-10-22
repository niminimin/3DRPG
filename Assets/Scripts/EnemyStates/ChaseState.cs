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
        enemy.MoveToPlayer();
    }

    public override void OnEnter(EnemyController target)
    {
        enemy = target;
        //Debug.Log("chase");
        //TODO 动画
        enemy.animator.SetBool("IsChase", true);
    }

    public override void OnExit()
    {
        enemy.animator.SetBool("IsChase", false);
    }
}
