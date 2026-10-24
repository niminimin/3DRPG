using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss（Rock Golem）的待机状态：站在岗位上，看到玩家直接进入攻击状态（**不追击**）。
/// 普通的 GuardState 会切 CHASE 去追人，Boss 不需要——它宁可站桩扔石头。
/// </summary>
public class GolemGuardState : BaseState
{
    public override void LogicUpdata()
    {
        if (enemy.Perception.CanSeePlayer())
        {
            enemy.SwitchState(EnemyState.ATTACK);
            return;
        }

        // 站定，慢慢转回初始朝向
        enemy.transform.rotation = Quaternion.Lerp(
            enemy.transform.rotation,
            enemy.Locomotion.HomeRotation,
            0.1f);
    }

    public override void OnEnter(EnemyController enemy)
    {
        this.enemy = enemy;
        enemy.Locomotion.Stop();
    }

    public override void OnExit()
    {
    }
}
