using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人的移动能力：把 NavMeshAgent 的常用操作包装成"落地到用途"的接口，
/// 让状态机只关心"走到哪"，不关心 agent 的 isStopped / pathPending 细节。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyLocomotion : MonoBehaviour
{
    private NavMeshAgent agent;

    /// <summary>出生点 / 出生朝向（控制器在 Awake 里写入）。</summary>
    public Vector3 HomePosition { get; private set; }
    public Quaternion HomeRotation { get; private set; }

    public NavMeshAgent Agent
    {
        get
        {
            // 懒加载：同一 GameObject 上组件的 Awake 顺序不确定，
            // 控制器可能先于本组件 Awake 就调用 MoveTo，不能只在 Awake 里缓存。
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            return agent;
        }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        HomePosition = transform.position;
        HomeRotation = transform.rotation;
    }

    /// <summary>由控制器把出生点快照同步过来，保证两边一致。</summary>
    public void SetHome(Vector3 position, Quaternion rotation)
    {
        HomePosition = position;
        HomeRotation = rotation;
    }

    /// <summary>走向某个点。目标没变时不会重复寻路，可以每帧调用。</summary>
    public void MoveTo(Vector3 destination)
    {
        if (Agent == null) return;

        Agent.isStopped = false;        // 之前可能被 Stop() 停住
        if (Agent.destination != destination)
            Agent.destination = destination;
    }

    /// <summary>站定：停住并清掉残余路径，避免停下后还在微微滑动。</summary>
    public void Stop()
    {
        if (Agent == null) return;

        Agent.isStopped = true;
        Agent.ResetPath();
    }

    /// <summary>是否已经回到出生点。</summary>
    public bool HasArrivedAtHome(float extra = 0.3f)
    {
        if (Agent == null) return false;

        // pathPending 期间 remainingDistance 不可信（可能是 0 或 Infinity）
        if (Agent.pathPending) return false;

        // 双重判定：NavMesh 的到达距离 + 与出生点的实际距离
        if (Agent.remainingDistance > Agent.stoppingDistance + 0.1f) return false;
        return Vector3.Distance(transform.position, HomePosition) <= Agent.stoppingDistance + extra;
    }

    /// <summary>是否已到达当前目的地。</summary>
    public bool HasArrivedAtDestination(float extra = 0.2f)
    {
        if (Agent == null) return false;
        if (Agent.pathPending) return false;

        // 用剩余距离判断：agent 会在 stoppingDistance 处停下，
        // 直接比 transform 距离会因为 stoppingDistance 而永远到不了
        return Agent.remainingDistance <= Agent.stoppingDistance + extra;
    }

    /// <summary>朝某个世界坐标转身（只转水平方向，避免身体前倾）。</summary>
    public void FaceTo(Vector3 worldPosition)
    {
        Vector3 dir = worldPosition - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    /// <summary>
    /// 在 center 周围 radius 的圆内采样一个可行走点。
    /// 有限次尝试 + 兜底，绝不递归（踩过无限递归的坑）。
    /// </summary>
    public Vector3 SamplePointAround(Vector3 center, float radius, int maxAttempts = 10)
    {
        // 采样半径至少 1 米：radius 为 0 时 maxDistance 也是 0，几乎必然失败
        float sampleDistance = Mathf.Max(1f, radius);

        for (int i = 0; i < maxAttempts; i++)
        {
            // 圆形范围：先取单位圆内的随机点，再按半径缩放
            // （分别取 X/Z 的 Random.Range 得到的是方形范围）
            Vector2 circle = Random.insideUnitCircle * radius;

            Vector3 candidate = new Vector3(center.x + circle.x, center.y, center.z + circle.y);

            NavMeshHit hit;
            // AllAreas：只传 1 的话只匹配 area id = 1 的网格，自定义烘焙 area 会永远失败
            if (NavMesh.SamplePosition(candidate, out hit, sampleDistance, NavMesh.AllAreas))
                return hit.position;        // 用吸附后的合法点，而不是原始随机点
        }

        Debug.LogWarning("[" + name + "] 连续 " + maxAttempts + " 次找不到可用的巡逻点，先回出生点", this);
        return center;
    }
}
