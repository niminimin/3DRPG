using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    public float sightRadius;
    private NavMeshAgent agent;

    private GameObject attackTarget;
    public Animator animator;

    public float patrolRadius;

    [Tooltip("走到一个巡逻点后原地停留的时间（秒）")]
    public float patrolWaitTime = 1.5f;

    public Vector3 curPatrolPoint;

    // 出生点快照：必须存 Vector3 的值，不能存 Transform 引用。
    // 存 Transform 的话 initTrans.position 永远等于"敌人当前位置"，回位逻辑会原地打转。
    // 运行时由 Awake 自动写入，不需要在 Inspector 里配。
    [HideInInspector] public Vector3 initPosition;

    //状态机相关
    private BaseState firstState;
    private BaseState curEnemyState;
    private BaseState newEnemyState;
    public BaseState guardState;
    public BaseState patrolState;
    public BaseState chaseState;
    public BaseState returnState;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        guardState = new GuardState();
        chaseState = new ChaseState();
        patrolState = new PatrolState();
        returnState = new ReturnState();

        initPosition = transform.position;
        curPatrolPoint = transform.position;
        //TODO 待确认
        curEnemyState = patrolState;
        curEnemyState.OnEnter(this);
    }

    private void Update()
    {
        curEnemyState.LogicUpdata();
    }

    public void SwitchState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.GUARD:
                newEnemyState = guardState;
                break;
            case EnemyState.PATROL:
                newEnemyState = patrolState;
                break;
            case EnemyState.CHASE:
                newEnemyState = chaseState;
                break;
            case EnemyState.RETURN:
                newEnemyState = returnState;
                break;
            default: break;
        }
        curEnemyState.OnExit();
        curEnemyState = newEnemyState;
        curEnemyState.OnEnter(this);
    }

    public bool FoundPlayer()
    {
        var colliders = Physics.OverlapSphere(transform.position, sightRadius);

        foreach (var target in colliders)
        {
            if (target.CompareTag("Player"))
            {
                attackTarget = target.gameObject;
                return true;
            }
        }
        return false;
    }

    public void MoveToPlayer()
    {
        // GuardState 会停住 agent，追击前必须先恢复，否则设了 destination 也不会动
        agent.isStopped = false;
        agent.destination = attackTarget.transform.position;
    }

    public void ReturnToInit()
    {
        agent.isStopped = false;
        agent.destination = initPosition;
    }

    /// <summary>是否已经回到出生点。</summary>
    public bool HasArrivedAtInit()
    {
        // pathPending 期间 remainingDistance 不可信（可能是 0 或 Infinity）
        if (agent.pathPending) return false;

        // 双重判定：NavMesh 的到达距离 + 与出生点的实际距离
        if (agent.remainingDistance > agent.stoppingDistance + 0.1f) return false;
        return Vector3.Distance(transform.position, initPosition) <= agent.stoppingDistance + 0.3f;
    }

    /// <summary>站定：停住并清掉残余路径，避免回到岗位后还在微微滑动。</summary>
    public void StopAgent()
    {
        agent.isStopped = true;
        agent.ResetPath();
    }

    /// <summary>在出生点周围 patrolRadius 的圆内取一个巡逻点。</summary>
    public void GetNewWayPoint()
    {
        // 圆形范围：先取单位圆内的随机点，再按半径缩放
        // （用 Random.Range 分别取 X/Z 得到的是方形范围）
        Vector2 circle = Random.insideUnitCircle * patrolRadius;

        Vector3 randomPoint = new Vector3(initPosition.x + circle.x,
                                     initPosition.y,
                                     initPosition.z + circle.y);
        NavMeshHit hit;
        if(NavMesh.SamplePosition(randomPoint, out hit, patrolRadius, 1))
        {
            curPatrolPoint = randomPoint;
            return;
        }
        else
        {
            GetNewWayPoint();
        }

    }

    public void MoveToPatrolPoint()
    {
        // GuardState 会停住 agent，出发前必须先恢复
        agent.isStopped = false;

        // 只有换点时才重新设置目的地，避免每帧重复寻路
        if (agent.destination != curPatrolPoint)
            agent.destination = curPatrolPoint;
    }

    /// <summary>是否已到达当前巡逻点。</summary>
    public bool HasArrivedAtPatrolPoint()
    {
        if (agent.pathPending) return false;

        // 用 NavMesh 的剩余距离判断：agent 会在 stoppingDistance 处停下，
        // 用 transform 距离硬比 0.3 永远到不了（stoppingDistance 现在是 1）
        return agent.remainingDistance <= agent.stoppingDistance + 0.2f;
    }
}
