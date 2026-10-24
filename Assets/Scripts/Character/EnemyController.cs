using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(CharacterStats))]
public abstract class EnemyController : MonoBehaviour
{
    /// <summary>移动能力（NavMeshAgent 的封装）。缺失时 Awake 会自动挂载。</summary>
    public EnemyLocomotion Locomotion { get; private set; }

    /// <summary>感知能力：视野内找玩家、记住目标。</summary>
    public EnemyPerception Perception { get; private set; }

    /// <summary>战斗能力：攻击、命中帧结算。</summary>
    public EnemyCombat Combat { get; private set; }

    [HideInInspector] public Animator animator;
    protected CharacterStats characterStats;

    [Header("受击硬直")]
    [Tooltip("被暴击时硬直多久（秒）：期间不会发起攻击/技能，正在放的那一下会作废")]
    public float staggerDuration = 0.4f;

    private float staggerEndTime;

    /// <summary>是否处于受击硬直中（状态类用来决定"这一帧要不要出手"）。</summary>
    public bool IsStaggered
    {
        get { return Time.time < staggerEndTime; }
    }

    [Header("巡逻")]
    public float patrolRadius;

    [Tooltip("走到一个巡逻点后原地停留的时间（秒）")]
    public float patrolWaitTime = 1.5f;

    public Vector3 curPatrolPoint;

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

    protected virtual void Awake()
    {
        animator = GetComponent<Animator>();
        characterStats = GetComponent<CharacterStats>();

        // RequireComponent 会自动补组件，但补出来的是"空数据"那个；
        // 万一物体上还有另一个配好的，GetComponent 拿到的是空的那个，表现为 0 血。
        if (characterStats == null)
            Debug.LogError("[" + name + "] 缺少 CharacterStats，这个敌人不会有血量/伤害", this);

        if (animator == null || animator.runtimeAnimatorController == null)
            Debug.LogWarning("[" + name + "] Animator 没有设置控制器，敌人不会有动画表现（预制体上没配？）", this);

        // 能力组件：场景/预制体上没挂就自动补，保证老资产也能跑
        Locomotion = EnsureComponent<EnemyLocomotion>();
        Perception = EnsureComponent<EnemyPerception>();
        Combat = EnsureComponent<EnemyCombat>();

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
    }

    /// <summary>取组件；没有就自动补一个。</summary>
    private T EnsureComponent<T>() where T : Component
    {
        T comp = GetComponent<T>();
        if (comp == null) comp = gameObject.AddComponent<T>();
        return comp;
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
        Combat.CancelPendingAttack();       // 还没命中的这一刀作废
        SwitchState(EnemyState.DEAD);       // 唯一入口
    }

    /// <summary>玩家死亡：切到庆祝状态（已经死了的敌人不庆祝）。</summary>
    private void HandlePlayerDied()
    {
        if (characterStats != null && characterStats.IsDead) return;

        SwitchState(EnemyState.CELEBRATE);
    }

    /// <summary>受击表现：只有暴击才播受击动画并进入硬直（致死的那一下交给死亡动画）。</summary>
    private void HandleDamaged(DamageResult result)
    {
        if (!result.isCritical) return;
        if (characterStats != null && characterStats.IsDead) return;

        // 硬直：受击期间不发起新的攻击/技能
        staggerEndTime = Time.time + staggerDuration;

        // 正在放的攻击/技能作废：即使动画播到命中帧，也不会再结算
        if (Combat != null) Combat.CancelPendingAttack();

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
