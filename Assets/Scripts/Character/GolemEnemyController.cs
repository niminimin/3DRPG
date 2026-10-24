using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss（Rock Golem）：守卫型 + 原地站桩输出。
/// 几乎不移动：待机时看到玩家直接进攻击状态（不追击）；
/// 近身用 Attack02 击退玩家，远处用 Attack01 丢石头。
/// </summary>
public class GolemEnemyController : EnemyController
{
    protected override EnemyState InitialState
    {
        get { return EnemyState.GUARD; }
    }

    public override EnemyState StateAfterReturn
    {
        get { return EnemyState.GUARD; }
    }

    // Boss 不追人：待机状态直接进攻击，不经过 CHASE
    protected override BaseState CreateGuardState()
    {
        return new GolemGuardState();
    }

    protected override BaseState CreateAttackState()
    {
        return new GolemAttackState();
    }

    protected override void Awake()
    {
        if (GetComponent<GolemCombat>() == null && GetComponent<EnemyCombat>() == null)
            gameObject.AddComponent<GolemCombat>();

        base.Awake();

        // 自动绑定投石点：Index_Proximal_R
        GolemCombat golemCombat = Combat as GolemCombat;
        if (golemCombat != null && golemCombat.throwAnchor == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Index_Proximal_R")
                {
                    golemCombat.throwAnchor = t;
                    break;
                }
            }
        }
    }

    /// <summary>Boss 死亡：把它丢出去的石头全部清掉（地上的、空中的都清）。</summary>
    protected override void HandleDeath()
    {
        base.HandleDeath();

        GolemCombat golemCombat = Combat as GolemCombat;
        if (golemCombat != null) golemCombat.ClearRocks();
    }
}
