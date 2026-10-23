using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人的感知能力：视野范围内找玩家、记住目标、算到目标的距离。
/// 状态机只问"看不看得见""离多远"，不关心 OverlapSphere 和标签判断的细节。
/// </summary>
public class EnemyPerception : MonoBehaviour
{
    [Header("感知")]
    [Tooltip("视野半径（米）")]
    public float sightRadius = 8f;

    /// <summary>当前目标；没有目标时为 null。</summary>
    public GameObject Target { get; private set; }

    /// <summary>目标的世界坐标；没有目标时返回自己的位置（移动/转向会变成原地不动）。</summary>
    public Vector3 TargetPosition
    {
        get { return Target != null ? Target.transform.position : transform.position; }
    }

    /// <summary>扫一眼视野内有没有活着的玩家；找到就记为目标并返回 true。</summary>
    public bool CanSeePlayer()
    {
        var colliders = Physics.OverlapSphere(transform.position, sightRadius);

        foreach (var col in colliders)
        {
            if (!col.CompareTag("Player")) continue;

            // 玩家已经死了就不再追着打
            CharacterStats targetStats = col.GetComponent<CharacterStats>();
            if (targetStats != null && targetStats.IsDead) return false;

            Target = col.gameObject;
            return true;
        }

        return false;
    }

    /// <summary>丢掉当前目标（比如它被销毁了）。</summary>
    public void ClearTarget()
    {
        Target = null;
    }

    /// <summary>到目标的水平距离；没有目标时返回极大值。</summary>
    public float DistanceToTarget()
    {
        if (Target == null) return float.MaxValue;

        return HorizontalDistance(transform.position, Target.transform.position);
    }

    /// <summary>忽略高度差的水平距离。</summary>
    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
