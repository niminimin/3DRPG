using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 巡逻型敌人：出生后在自己岗位周围巡逻，脱战回位后继续巡逻。
/// </summary>
public class PatrolEnemyController : EnemyController
{
    protected override EnemyState InitialState
    {
        get { return EnemyState.PATROL; }
    }

    public override EnemyState StateAfterReturn
    {
        get { return EnemyState.PATROL; }
    }
}
