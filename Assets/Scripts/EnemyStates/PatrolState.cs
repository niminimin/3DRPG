using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PatrolState : BaseState
{
    private float waitTimer;
    private bool isWaiting;

    // 走不到就换点的兜底计时：随机点如果落在不可达位置（没做 NavMesh 吸附时可能发生），
    // 不加这个会永远卡在"走向同一个点"上。
    private float walkTimer;
    private const float MaxWalkTime = 10f;

    public override void LogicUpdata()
    {
        if (enemy.Perception.CanSeePlayer())
        {
            enemy.SwitchState(EnemyState.CHASE);
            return;                                 // 切换后立刻返回，别用旧状态继续改新状态
        }

        // ---- 到了巡逻点：停下等一会儿，再取下一个点 ----
        if (enemy.Locomotion.HasArrivedAtDestination())
        {
            if (!isWaiting)
            {
                isWaiting = true;
                waitTimer = 0f;
                walkTimer = 0f;
                enemy.animator.SetBool("IsWalk", false);
                enemy.Locomotion.Stop();            // 站定，避免惯性往前滑
            }

            waitTimer += Time.deltaTime;
            if (waitTimer >= enemy.patrolWaitTime)
            {
                isWaiting = false;
                PickNewPatrolPoint();
                enemy.animator.SetBool("IsWalk", true);
            }
            return;
        }

        // ---- 还没到：继续走 ----
        walkTimer += Time.deltaTime;
        if (walkTimer >= MaxWalkTime)
        {
            walkTimer = 0f;
            PickNewPatrolPoint();                   // 走了太久还到不了，换一个点
        }
        enemy.Locomotion.MoveTo(enemy.curPatrolPoint);
    }

    /// <summary>取一个新巡逻点并立刻出发（否则下一帧又会被判定为"已到达"）。</summary>
    private void PickNewPatrolPoint()
    {
        enemy.curPatrolPoint = enemy.Locomotion.SamplePointAround(enemy.Locomotion.HomePosition, enemy.patrolRadius);
        enemy.Locomotion.MoveTo(enemy.curPatrolPoint);
    }

    public override void OnEnter(EnemyController target)
    {
        enemy = target;
        isWaiting = false;
        waitTimer = 0f;
        walkTimer = 0f;

        PickNewPatrolPoint();
        enemy.animator.SetBool("IsWalk", true);
    }

    public override void OnExit()
    {
        isWaiting = false;
        waitTimer = 0f;
        walkTimer = 0f;
        enemy.animator.SetBool("IsWalk", false);
    }
}
