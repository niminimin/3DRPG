using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 守卫型敌人：出生就站在岗位上待机，不巡逻；脱战回位后继续站岗。
/// </summary>
public class GuardEnemyController : EnemyController
{
    protected override EnemyState InitialState
    {
        get { return EnemyState.GUARD; }
    }

    public override EnemyState StateAfterReturn
    {
        get { return EnemyState.GUARD; }
    }
}
