using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人的战斗能力：开始攻击（掷暴击 + 播动画）、命中帧结算、取消未命中的攻击。
/// 数值全部来自 AttackDataSO（经 CharacterStats 读出），这里不写死。
/// 注意：不加 [RequireComponent(CharacterStats)]——那会在编辑预制体时
/// 自动补一个空数据的 CharacterStats，和实例上已有的那个重复。
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    [Header("攻击")]
    [Tooltip("离开攻击距离时的额外余量：退出攻击的距离 = AttackRange + 这个值")]
    public float attackExitBuffer = 0.5f;

    private CharacterStats stats;
    private Animator animator;

    // 攻击开始就算好、但等动画命中帧才结算的那一下（子类需要访问，所以 protected）
    protected GameObject currentTarget;
    protected DamageResult pendingDamage;
    protected bool hasPendingDamage;

    /// <summary>角色数值（懒加载，避免同物体 Awake 顺序问题）。</summary>
    public CharacterStats Stats
    {
        get
        {
            if (stats == null) stats = GetComponent<CharacterStats>();
            return stats;
        }
    }

    /// <summary>动画控制器（懒加载）。</summary>
    protected Animator Anim
    {
        get
        {
            if (animator == null) animator = GetComponent<Animator>();
            return animator;
        }
    }

    /// <summary>攻击距离：来自 AttackDataSO。没配数据时为 0，表示不会攻击。</summary>
    public float AttackRange
    {
        get { return Stats == null ? 0f : Stats.AttackRange; }
    }

    /// <summary>攻击冷却：下限 0.1 秒，防止数据缺失时每帧“出手”。</summary>
    public float AttackCoolDown
    {
        get { return Stats == null ? 0.1f : Mathf.Max(0.1f, Stats.CoolDown); }
    }

    /// <summary>退出攻击状态的距离：攻击距离 + 余量（滞回，避免在边界来回切）。</summary>
    public float AttackExitRange
    {
        get { return AttackRange + attackExitBuffer; }
    }

    protected virtual void Awake()
    {
        stats = GetComponent<CharacterStats>();
        animator = GetComponent<Animator>();

        if (AttackRange <= 0f)
            Debug.LogWarning("[" + name + "] 没有配置 AttackDataSO（或 attackRange <= 0），该敌人不会攻击", this);
    }

    /// <summary>
    /// 开始一次攻击：决定是否暴击（要选 clip，所以必须提前掷骰）+ 播放动画 + 暂存结果。
    /// 真正的伤害在动画事件 <see cref="OnAttackHit"/> 里结算。
    /// </summary>
    public virtual void BeginAttack(GameObject target)
    {
        currentTarget = target;

        CharacterStats targetStats = target != null ? target.GetComponent<CharacterStats>() : null;

        if (Stats == null || targetStats == null)
        {
            hasPendingDamage = false;
            if (Anim != null) Anim.SetTrigger("Attack");    // 缺数值组件时只做表现
            return;
        }

        pendingDamage = Stats.CalculateDamage(targetStats);
        hasPendingDamage = true;

        if (Anim != null)
            Anim.SetTrigger(pendingDamage.isCritical ? "CriticalAttack" : "Attack");
    }

    /// <summary>
    /// 【动画事件】攻击动作打到目标的那一帧调用（在 clip 上 Add Animation Event，函数名填 OnAttackHit）。
    /// 每个攻击 clip（Attack01/Attack02/CriticalAttack…）都要加同名的这个事件。
    /// </summary>
    public virtual void OnAttackHit()
    {
        if (!hasPendingDamage) return;      // 没有待结算的攻击（被打断 / 重复事件）
        hasPendingDamage = false;           // 一次攻击只结算一次

        ApplyDamage(currentTarget, pendingDamage, AttackRange);
    }

    /// <summary>
    /// 把一次攻击结算到目标身上：自己死/目标死/超出范围都会跳过。
    /// 子类（例如 Boss 技能）可以复用它。
    /// </summary>
    protected void ApplyDamage(GameObject target, DamageResult result, float range)
    {
        if (Stats != null && Stats.IsDead) return;      // 自己已经死了
        if (target == null) return;

        CharacterStats targetStats = target.GetComponent<CharacterStats>();
        if (targetStats == null || targetStats.IsDead) return;

        // 命中帧再校验距离：抬手后目标跑开就不该吃到伤害
        if (EnemyPerception.HorizontalDistance(transform.position, target.transform.position) > range)
            return;

        targetStats.TakeDamage(result);
    }

    /// <summary>取消尚未命中的这一下（离开攻击状态、被硬直打断时调用）。</summary>
    public virtual void CancelPendingAttack()
    {
        hasPendingDamage = false;
    }
}
