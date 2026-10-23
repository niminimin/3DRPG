using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 死亡状态：什么都不做。
/// 用状态而不是 isDead 布尔量，是为了让"死后不跑 AI"在结构上成立——
/// 只要当前状态是它，LogicUpdata 就是空的，不需要在每个调用点补 if。
/// 死亡不可逆，所以没有出边，OnExit 也不会被调用。
/// </summary>
public class DeadState : BaseState
{
    public override void LogicUpdata()
    {
        // 死后不跑任何 AI：不检测玩家、不移动、不攻击
    }

    public override void OnEnter(EnemyController enemy)
    {
        this.enemy = enemy;

        enemy.StopAgent();                      // 停住并清掉残余路径
        enemy.animator.SetBool("IsDead", true); // 播死亡动画

        // 尸体不再参与点击/碰撞，免得玩家还能点着打
        Collider col = enemy.GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    public override void OnExit()
    {
        // 死亡不可逆，不做出边清理
    }
}
