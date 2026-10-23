using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 庆祝状态：玩家死亡后进入，站定循环播放胜利动画（IsCelebrate = true）。
/// 游戏结束，所以这里不再检测玩家、不再移动、不再攻击。
/// </summary>
public class CelebrateState : BaseState
{
    public override void LogicUpdata()
    {
        // 什么都不做：玩家已经死了，不需要 AI
    }

    public override void OnEnter(EnemyController enemy)
    {
        this.enemy = enemy;

        enemy.Locomotion.Stop();            // 站定
        enemy.CancelPendingAttack();        // 别再结算没命中的攻击

        enemy.animator.SetBool("IsWalk", false);
        enemy.animator.SetBool("IsChase", false);
        enemy.animator.SetBool("IsCelebrate", true);   // Victory 循环
    }

    public override void OnExit()
    {
        // 以后要支持"玩家复活/重开"时，在这里退出庆祝
        enemy.animator.SetBool("IsCelebrate", false);
    }
}
