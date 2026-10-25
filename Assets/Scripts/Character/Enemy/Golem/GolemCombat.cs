using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss（Rock Golem）的战斗能力：
/// - Attack01 = 远程：投掷石头（第 45 帧生成在 Index_Proximal_R 的位置，飞向玩家）
/// - Attack02 = 近身：把玩家击退一段距离，然后继续扔石头
/// 两个攻击各有独立冷却；靠距离决定用哪个。
/// </summary>
public class GolemCombat : EnemyCombat
{
    [Header("近身攻击（Attack02）")]
    [Tooltip("近身攻击冷却（秒）")]
    public float meleeCoolDown = 3f;

    [Tooltip("击退玩家的距离（米）")]
    public float knockbackDistance = 6f;

    [Tooltip("击退后玩家定身时长（秒）")]
    public float knockbackStun = 0.4f;

    [Header("远程攻击（Attack01）")]
    [Tooltip("投石冷却（秒）")]
    public float rangedCoolDown = 2.5f;

    [Tooltip("靠近范围：玩家进到这个距离内，Boss 才会挪动脚步（更远就站着扔石头）")]
    public float approachRange = 6f;

    [Tooltip("石头预制体（带 RockProjectile）")]
    public GameObject rockPrefab;

    [Tooltip("石头从这里生成：一般是 Index_Proximal_R 骨骼")]
    public Transform throwAnchor;

    [Tooltip("石头飞行时间（秒）：越短飞得越快、弧线越扁")]
    public float rockFlightTime = 1.0f;

    [Header("触发帧")]
    [Tooltip("投掷动画里生成石头的时刻（第 45 帧 @30fps = 1.50 秒）")]
    public float throwTime = 1.5f;

    [Header("动画时长（用于锁住攻击，避免边投石边近身）")]
    [Tooltip("Attack01 投石动画长度（秒）")]
    public float rangedAnimLength = 2.67f;

    [Tooltip("Attack02 近身动画长度（秒）")]
    public float meleeAnimLength = 2.0f;

    private bool meleePending;
    private bool rangedPending;
    private float busyUntil;

    // 这个 Boss 丢出去的石头（死亡时统一清除）
    private readonly List<RockProjectile> spawnedRocks = new List<RockProjectile>();

    /// <summary>是否正在播攻击动画：播放期间不允许开始下一段攻击。</summary>
    public bool IsBusy
    {
        get { return Time.time < busyUntil; }
    }

    /// <summary>登记一块刚生成的石头，Boss 死亡时统一清除。</summary>
    public void RegisterRock(RockProjectile rock)
    {
        if (rock != null) spawnedRocks.Add(rock);
    }

    /// <summary>清除这个 Boss 丢出的所有石头（死的、在飞的、躺在地上的）。</summary>
    public void ClearRocks()
    {
        foreach (var rock in spawnedRocks)
        {
            if (rock != null) rock.ForceFinish();   // 回池或销毁
        }
        spawnedRocks.Clear();
    }

    /// <summary>近身攻击范围（来自 AttackDataSO.attackRange）。</summary>
    public float MeleeRange
    {
        get { return Stats == null ? 0f : Stats.AttackRange; }
    }

    /// <summary>远程攻击范围（来自 AttackDataSO.skillRange）。</summary>
    public float RangedRange
    {
        get { return Stats == null ? 0f : Stats.SkillRange; }
    }

    /// <summary>靠近范围：玩家进到这个距离内，Boss 才会挪动脚步。</summary>
    public float ApproachRange
    {
        get { return approachRange; }
    }

    /// <summary>开始近身攻击：播 Attack02（CriticalAttack 触发器）。</summary>
    public void BeginMelee(GameObject target)
    {
        currentTarget = target;
        hasPendingDamage = false;
        rangedPending = false;
        meleePending = true;
        busyUntil = Time.time + meleeAnimLength;      // 播放期间不再开始新攻击

        if (Anim != null) Anim.SetTrigger("CriticalAttack");
    }

    /// <summary>开始远程攻击：播 Attack01（Attack 触发器），石头在动画事件里生成。</summary>
    public void BeginRanged(GameObject target)
    {
        currentTarget = target;
        hasPendingDamage = false;
        meleePending = false;
        rangedPending = true;
        busyUntil = Time.time + rangedAnimLength;

        if (Anim != null) Anim.SetTrigger("Attack");
    }

    /// <summary>【动画事件】Attack01 第 45 帧：在手上生成石头，飞向玩家。</summary>
    public void OnThrowRock()
    {
        if (!rangedPending) return;
        rangedPending = false;

        if (rockPrefab == null)
        {
            Debug.LogWarning("[" + name + "] 没有配置 rockPrefab，投石不会生效", this);
            return;
        }
        if (currentTarget == null) return;

        Vector3 spawnPos = throwAnchor != null ? throwAnchor.position : transform.position + Vector3.up * 2.5f;

        // 优先从对象池借石头；没有池子时退化为生成
        RockProjectile projectile = null;
        if (RockPool.instance != null)
        {
            projectile = RockPool.instance.Get(spawnPos);
        }
        else
        {
            GameObject rock = Instantiate(rockPrefab, spawnPos, Quaternion.identity);
            projectile = rock.GetComponent<RockProjectile>();
        }

        if (projectile != null)
        {
            projectile.Launch(Stats, currentTarget, gameObject, Stats, rockFlightTime, 1.5f);
            RegisterRock(projectile);       // 记下来，Boss 死亡时统一清除
        }
    }

    /// <summary>【动画事件】Attack02 命中帧：伤害 + 把玩家击退。</summary>
    public void OnMeleeHit()
    {
        if (!meleePending) return;
        meleePending = false;

        if (currentTarget == null) return;

        CharacterStats targetStats = currentTarget.GetComponent<CharacterStats>();
        if (targetStats == null || targetStats.IsDead) return;

        ApplyDamage(currentTarget, Stats != null ? Stats.CalculateDamage(targetStats) : default(DamageResult), MeleeRange);

        // 击退：把玩家推远，好接着扔石头
        PlayerController player = currentTarget.GetComponent<PlayerController>();
        if (player != null) player.ApplyKnockback(transform.position, knockbackDistance, knockbackStun);
    }

    public override void CancelPendingAttack()
    {
        base.CancelPendingAttack();
        meleePending = false;
        rangedPending = false;
    }
}
