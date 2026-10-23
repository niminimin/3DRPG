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
            return;
        }
        enemy.transform.rotation = Quaternion.Lerp(enemy.transform.rotation, enemy.initRotation, 0.1f);
    }

    public override void OnEnter(EnemyController target)
    {
        enemy = target;
        enemy.Locomotion.Stop();      // 站定：停住并清掉残余路径
    }

    public override void OnExit()
    {
        
    }
}
