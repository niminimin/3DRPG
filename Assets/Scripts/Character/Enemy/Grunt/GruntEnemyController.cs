using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss（Grunt）：守卫型敌人，攻击状态换成带技能的 GruntAttackState。
/// </summary>
public class GruntEnemyController : EnemyController
{
    protected override EnemyState InitialState
    {
        get { return EnemyState.GUARD; }
    }

    public override EnemyState StateAfterReturn
    {
        get { return EnemyState.GUARD; }
    }

    protected override BaseState CreateAttackState()
    {
        return new GruntAttackState();
    }

    protected override void Awake()
    {
        // 先确保挂着的是 GruntCombat 而不是普通 EnemyCombat，再让基类去装配
        if (GetComponent<GruntCombat>() == null && GetComponent<EnemyCombat>() == null)
            gameObject.AddComponent<GruntCombat>();

        base.Awake();
    }
}
