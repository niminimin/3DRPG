using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyController : MonoBehaviour
{
    [Header("感知")]
    public float sightRadius;

    /// <summary>移动能力（NavMeshAgent 的封装），由 Awake 自动挂载。</summary>
    public EnemyLocomotion Locomotion { get; private set; }

    protected GameObject attackTarget;
    public Animator animator;
    protected CharacterStats characterStats;

    // 攻击开始就算好、但等动画命中帧才结算的那一下
    protected DamageResult pendingDamage;
    protected bool hasPendingDamage;

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
    [HideInInspector] public Quaternion initRotation;

    // 状态机相关
    protected BaseState curEnemyState;
    public BaseState guardState;
    public BaseState patrolState;
    public BaseState chaseState;
    public BaseState attackState;
    public BaseState returnState;
    public BaseState deadState;
    public BaseState celebrateState;

    #region 子类必须实现的定制点
    /// <summary>出生时进入的状态：巡逻型 = PATROL，守卫型 = GUARD。</summary>
    protected abstract EnemyState InitialState { get; }

    /// <summary>脱战回位后进入的状态（ReturnState 会读它）：巡逻型回巡逻，守卫型回站岗。</summary>
    public abstract EnemyState StateAfterReturn { get; }
    #endregion

    #region ReadFromSO
    /// <summary>角色数值（从 AttackDataSO 读攻击距离/冷却/伤害）。</summary>
    public CharacterStats Stats
    {
        get { return characterStats; }
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

    protected virtual void Awake()
    {
        animator = GetComponent<Animator>();
        characterStats = GetComponent<CharacterStats>();

        // 移动能力组件：场景/预制体上没挂就自动补一个，保证老资产也能跑
        Locomotion = GetComponent<EnemyLocomotion>();
        if (Locomotion == null)
            Locomotion = gameObject.AddComponent<EnemyLocomotion>();

        guardState = new GuardState();
        chaseState = new ChaseState();
        patrolState = new PatrolState();
        attackState = new AttackState();
        returnState = new ReturnState();
        deadState = new DeadState();
        celebrateState = new CelebrateState();

        initPosition = transform.position;
        curPatrolPoint = transform.position;
        initRotation = transform.rotation;

        // 出生点快照同步给移动组件，两边用同一份数据
        Locomotion.SetHome(initPosition, initRotation);
        // 初始状态由子类决定（巡逻型 = PATROL，守卫型 = GUARD）
        curEnemyState = GetState(InitialState);
        if (curEnemyState == null)
        {
            Debug.LogError("[" + name + "] 初始状态 " + InitialState + " 无法使用，退回巡逻", this);
            curEnemyState = patrolState;
        }
        curEnemyState.OnEnter(this);

        if (AttackRange <= 0f)
            Debug.LogWarning("[" + name + "] 没有配置 AttackDataSO（或 attackRange <= 0），该敌人不会攻击", this);
    }

    private void Update()
    {
        curEnemyState.LogicUpdata();
    }

    // 死亡/受击是"跨状态"事件：由控制器统一订阅，各状态不需要自己判断血量
    protected virtual void OnEnable()
    {
        if (characterStats != null)
        {
            characterStats.OnDead += HandleDeath;
            characterStats.OnDamaged += HandleDamaged;
        }
    }

    // 玩家死亡是全局事件，由 GameManager 广播；用 Start 订阅，保证 GameManager 已完成 Awake
    protected virtual void Start()
    {
        if (GameManager.instance != null)
            GameManager.instance.OnPlayerDied += HandlePlayerDied;
        else
            Debug.LogWarning("[" + name + "] 场景里没有 GameManager，玩家死亡后不会庆祝", this);
    }

    protected virtual void OnDisable()
    {
        if (characterStats != null)
        {
            characterStats.OnDead -= HandleDeath;
            characterStats.OnDamaged -= HandleDamaged;
        }

        if (GameManager.instance != null)
            GameManager.instance.OnPlayerDied -= HandlePlayerDied;
    }

    private void HandleDeath()
    {
        CancelPendingAttack();              // 还没命中的这一刀作废
        SwitchState(EnemyState.DEAD);       // 唯一入口
    }

    /// <summary>玩家死亡：切到庆祝状态（已经死了的敌人不庆祝）。</summary>
    private void HandlePlayerDied()
    {
        if (characterStats != null && characterStats.IsDead) return;

        SwitchState(EnemyState.CELEBRATE);
    }

    /// <summary>受击表现：只有暴击才播受击动画（致死的那一下交给死亡动画）。</summary>
    private void HandleDamaged(DamageResult result)
    {
        if (!result.isCritical) return;
        if (characterStats.IsDead) return;

        animator.SetTrigger("Hit");
    }

    public void SwitchState(EnemyState state)
    {
        if (curEnemyState == deadState) return;     // 死亡后不再接受任何切换

        BaseState next = GetState(state);

        if (next == null)
        {
            Debug.LogError("[" + name + "] 状态 " + state + " 尚未接入状态机");
            return;
        }

        if (next == curEnemyState) return;      // 同状态不重复进出

        curEnemyState.OnExit();
        curEnemyState = next;
        curEnemyState.OnEnter(this);
    }

    /// <summary>把枚举映射成状态实例；未接入的状态返回 null。</summary>
    protected BaseState GetState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.GUARD: return guardState;
            case EnemyState.PATROL: return patrolState;
            case EnemyState.CHASE: return chaseState;
            case EnemyState.ATTACK: return attackState;
            case EnemyState.RETURN: return returnState;
            case EnemyState.DEAD: return deadState;
            case EnemyState.CELEBRATE: return celebrateState;
            default: return null;
        }
    }

    public bool FoundPlayer()
    {
        var colliders = Physics.OverlapSphere(transform.position, sightRadius);

        foreach (var target in colliders)
        {
            if (!target.CompareTag("Player")) continue;

            // 玩家已经死了就不再追着打
            CharacterStats targetStats = target.GetComponent<CharacterStats>();
            if (targetStats != null && targetStats.IsDead) return false;

            attackTarget = target.gameObject;
            return true;
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

    /// <summary>当前目标的世界坐标；没有目标时返回自己的位置（移动/转向会变成原地不动）。</summary>
    public Vector3 TargetPosition
    {
        get { return attackTarget != null ? attackTarget.transform.position : transform.position; }
    }

    /// <summary>
    /// 攻击开始（由 AttackState 按冷却调用）：决定是否暴击（要选 clip）+ 播放动画 + 暂存结果。
    /// 真正的伤害在动画事件 <see cref="OnAttackHit"/> 里结算。
    /// </summary>
    public void Attack()
    {
        CharacterStats targetStats = attackTarget != null ? attackTarget.GetComponent<CharacterStats>() : null;

        if (Stats == null || targetStats == null)
        {
            hasPendingDamage = false;
            animator.SetTrigger("Attack");      // 缺数值组件时只做表现，不结算
            return;
        }

        pendingDamage = Stats.CalculateDamage(targetStats);
        hasPendingDamage = true;

        animator.SetTrigger(pendingDamage.isCritical ? "CriticalAttack" : "Attack");
    }

    /// <summary>
    /// 【动画事件】攻击动作打到目标的那一帧调用（在 clip 上 Add Animation Event，函数名填 OnAttackHit）。
    /// 每个攻击 clip（Attack01/Attack02/CriticalAttack…）都要加同名的这个事件。
    /// </summary>
    public void OnAttackHit()
    {
        if (!hasPendingDamage) return;      // 没有待结算的攻击（被打断 / 重复事件）
        hasPendingDamage = false;           // 一次攻击只结算一次

        if (characterStats != null && characterStats.IsDead) return;   // 自己已经死了

        if (attackTarget == null) return;

        CharacterStats targetStats = attackTarget.GetComponent<CharacterStats>();
        if (targetStats == null || targetStats.IsDead) return;

        // 命中帧再校验距离：抬手后目标跑开就不该吃到伤害
        if (DistanceToPlayer() > AttackRange) return;

        targetStats.TakeDamage(pendingDamage);
    }

    /// <summary>取消尚未命中的这一下（离开攻击状态、被硬直打断时调用）。</summary>
    public void CancelPendingAttack()
    {
        hasPendingDamage = false;
    }

    /// <summary>
    /// 【动画事件】死亡动画最后一帧调用：等尸体保留一段时间后销毁敌人。
    /// 在 Die clip 的最后一帧 Add Animation Event，函数名填 OnDeathAnimationEnd。
    /// </summary>
    public void OnDeathAnimationEnd()
    {
        // 防御：只有真的死了才销毁，避免事件被误配到别的 clip 上
        if (characterStats != null && !characterStats.IsDead) return;

        if (!DestroyAfterDeath) return;

        Debug.Log("[" + name + "] 死亡动画结束，尸体保留 " + corpseKeepTime + " 秒后销毁");
        Destroy(gameObject, corpseKeepTime);   // 延迟销毁：尸体先留在场景里
    }

    [Header("死亡")]
    [Tooltip("死亡动画播完后是否销毁自己；以后要做对象池就关掉这个开关")]
    public bool DestroyAfterDeath = true;

    [Tooltip("死亡动画播完后，尸体保留多久再销毁（秒）")]
    public float corpseKeepTime = 1f;
}
