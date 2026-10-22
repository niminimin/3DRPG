using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Header("感知")]
    public float sightRadius;

    private NavMeshAgent agent;
    private GameObject attackTarget;
    public Animator animator;
    private CharacterStats characterStats;

    [Header("巡逻")]
    public float patrolRadius;

    [Tooltip("走到一个巡逻点后原地停留的时间（秒）")]
    public float patrolWaitTime = 1.5f;

    public Vector3 curPatrolPoint;

    [Header("攻击")]
    [Tooltip("离开攻击距离时的额外余量：退出攻击的距离 = AttackRange + 这个值，避免在边界反复切状态")]
    public float attackExitBuffer = 0.5f;

    // 攻击距离 / 冷却 / 伤害都来自 AttackDataSO（经 CharacterStats 读出），不在这里写死

    // 出生点快照：必须存 Vector3 的值，不能存 Transform 引用。
    // 存 Transform 的话 initTrans.position 永远等于"敌人当前位置"，回位逻辑会原地打转。
    // 运行时由 Awake 自动写入，不需要在 Inspector 里配。
    [HideInInspector] public Vector3 initPosition;

    // 状态机相关
    private BaseState curEnemyState;
    public BaseState guardState;
    public BaseState patrolState;
    public BaseState chaseState;
    public BaseState attackState;
    public BaseState returnState;

    #region ReadFromSO
    /// <summary>角色数值（从 AttackDataSO 读攻击距离/冷却/伤害）。</summary>
    public CharacterStats Stats
    {
        get { return characterStats; }
    }
    public float CriticalChance
    {
        get { return characterStats == null ? 0f : Mathf.Clamp01(characterStats.CriticalChance); }
    }

    public float CriticalMultiplier
    {
        // 数据缺失时退回 1（等于不暴击）。返回 0 会把伤害算成 0，比不暴击更糟。
        get { return characterStats == null ? 1f : characterStats.CriticalMultiplier; }
    }
    /// <summary>攻击距离：来自 AttackDataSO。没配数据时为 0，表示不会进入攻击状态。</summary>
    public float AttackRange
    {
        get { return characterStats == null ? 0f : characterStats.AttackRange; }
    }

    /// <summary>攻击冷却：来自 AttackDataSO。下限 0.1 秒，防止数据缺失时每帧“出手”。</summary>
    public float AttackCoolDown
    {
        get { return characterStats == null ? 0.1f : Mathf.Max(0.1f, characterStats.CoolDown); }
    }

    /// <summary>退出攻击状态的距离：攻击距离 + 余量（滞回，避免在边界来回切）。</summary>
    public float AttackExitRange
    {
        get { return AttackRange + attackExitBuffer; }
    }
    #endregion

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        characterStats = GetComponent<CharacterStats>();

        guardState = new GuardState();
        chaseState = new ChaseState();
        patrolState = new PatrolState();
        attackState = new AttackState();
        returnState = new ReturnState();

        initPosition = transform.position;
        curPatrolPoint = transform.position;

        curEnemyState = patrolState;
        curEnemyState.OnEnter(this);

        if (AttackRange <= 0f)
            Debug.LogWarning("[" + name + "] 没有配置 AttackDataSO（或 attackRange <= 0），该敌人不会攻击", this);
    }

    private void Update()
    {
        curEnemyState.LogicUpdata();
    }

    public void SwitchState(EnemyState state)
    {
        BaseState next = null;
        switch (state)
        {
            case EnemyState.GUARD:
                next = guardState;
                break;
            case EnemyState.PATROL:
                next = patrolState;
                break;
            case EnemyState.CHASE:
                next = chaseState;
                break;
            case EnemyState.ATTACK:
                next = attackState;
                break;
            case EnemyState.RETURN:
                next = returnState;
                break;
            default:
                Debug.LogError("[" + name + "] 状态 " + state + " 尚未接入状态机");
                return;
        }

        if (next == null)
        {
            Debug.LogError("[" + name + "] 状态 " + state + " 未初始化");
            return;
        }

        if (next == curEnemyState) return;      // 同状态不重复进出

        curEnemyState.OnExit();
        curEnemyState = next;
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

    /// <summary>到玩家的水平距离；没有目标时返回极大值。</summary>
    public float DistanceToPlayer()
    {
        if (attackTarget == null) return float.MaxValue;

        Vector3 self = transform.position;
        Vector3 player = attackTarget.transform.position;
        self.y = 0f;
        player.y = 0f;
        return Vector3.Distance(self, player);
    }

    /// <summary>朝玩家转身（只转水平方向，避免身体前倾）。</summary>
    public void FaceToPlayer()
    {
        if (attackTarget == null) return;

        Vector3 dir = attackTarget.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
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
        if (NavMesh.SamplePosition(randomPoint, out hit, patrolRadius, 1))
        {
            curPatrolPoint = hit.position;
            return;
        }

        GetNewWayPoint();
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

    public void Attack()
    {
        if(Random.value <= CriticalChance)
        {
            animator.SetTrigger("CriticalAttack");
        }
        else
        {
            animator.SetTrigger("Attack");
        }
    }
}
