using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    [Header("攻击")]
    [Tooltip("离开攻击距离时的额外余量：退出攻击的距离 = AttackRange + 这个值，避免在边界每帧来回切")]
    public float attackExitBuffer = 0.5f;

    // 攻击距离 / 冷却 / 伤害都来自 AttackDataSO（经 CharacterStats 读出），不在这里写死

    private NavMeshAgent agent;
    private Animator animator;
    private GameObject attackTarget;
    private CharacterStats characterStats;
    private CharacterStats targetStats;      // 当前攻击目标的数值组件（缓存，避免每帧 GetComponent）

    // 攻击开始就算好、但等动画命中帧才结算的那一下
    private DamageResult pendingDamage;
    private bool hasPendingDamage;

    /// <summary>是否处于击飞/眩晕中：期间不响应鼠标、不更新移动动画，但**照常吃伤害**。</summary>
    public bool IsStunned { get; private set; }

    // 正在打的"可打回的石头"（Boss 战机制）
    private RockProjectile rockTarget;

    #region ReadFromSO
    /// <summary>角色数值（从 AttackDataSO 读攻击距离/冷却/伤害）。</summary>
    public CharacterStats Stats
    {
        get { return characterStats; }
    }

    /// <summary>攻击距离：来自 AttackDataSO。没配数据时为 0，表示不会攻击。</summary>
    public float AttackRange
    {
        get { return characterStats == null ? 0f : characterStats.AttackRange; }
    }

    /// <summary>攻击冷却：来自 AttackDataSO。下限 0.1 秒，防止数据缺失时每帧"出手"。</summary>
    public float AttackCoolDown
    {
        get { return characterStats == null ? 0.1f : Mathf.Max(0.1f, characterStats.CoolDown); }
    }

    /// <summary>退出攻击的距离：攻击距离 + 余量（滞回）。</summary>
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

        if (AttackRange <= 0f)
            Debug.LogWarning("[" + name + "] 没有配置 AttackDataSO（或 attackRange <= 0），该角色不会攻击", this);
    }

    private void Start()
    {
        MouseManager.instance.OnMouseClicked += MoveToPosition;
        MouseManager.instance.OnEnemyClicked += AttackEvent;
        MouseManager.instance.OnRockClicked += AttackRock;
        MouseManager.instance.OnPortalClicked += MoveToPortal;
    }

    /// <summary>点击传送门：走到门的位置（靠近后由 Portal 自己触发传送）。</summary>
    private void MoveToPortal(GameObject portal)
    {
        if (portal == null) return;

        // 目的地取"门后 1.5 米"：让玩家穿过门，而不是停在门前 stoppingDistance 的位置
        Vector3 fromPlayer = portal.transform.position - transform.position;
        fromPlayer.y = 0f;

        Vector3 dir = fromPlayer.sqrMagnitude > 0.01f ? fromPlayer.normalized : portal.transform.forward;

        Vector3 target = portal.transform.position + dir * 1.5f;
        target.y = transform.position.y;

        MoveToPosition(target);
    }

    // 受击 / 死亡表现：由 CharacterStats 的事件驱动
    private void OnEnable()
    {
        if (characterStats != null)
        {
            characterStats.OnDamaged += HandleDamaged;
            characterStats.OnDead += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (characterStats != null)
        {
            characterStats.OnDamaged -= HandleDamaged;
            characterStats.OnDead -= HandleDeath;
        }

        if (MouseManager.instance == null) return;      // 退出 Play 时它可能已经被销毁

        MouseManager.instance.OnMouseClicked -= MoveToPosition;
        MouseManager.instance.OnEnemyClicked -= AttackEvent;
        MouseManager.instance.OnRockClicked -= AttackRock;
    }

    /// <summary>受击表现：只有暴击才播受击动画（致死的那一下交给死亡表现）。</summary>
    private void HandleDamaged(DamageResult result)
    {
        if (!result.isCritical) return;
        if (characterStats.IsDead) return;
        if (IsStunned) return;          // 眩晕期间不播受击，避免抢占 Dizzy 动画（伤害照扣）

        animator.SetTrigger("Hit");
    }

    /// <summary>死亡：停住、播死亡动画、断开输入（玩家没有状态机，所以统一在这里收尾）。</summary>
    private void HandleDeath()
    {
        Debug.Log("[" + name + "] 死亡");

        CancelLaunch();             // 如果正在空中，先落回 NavMesh
        StopAllCoroutines();
        CancelPendingAttack();
        attackTarget = null;
        targetStats = null;
        rockTarget = null;

        // 停住移动
        agent.isStopped = true;
        agent.ResetPath();

        // 播死亡动画（Player.controller: AnyState -> Die，条件 IsDead）
        animator.SetBool("IsDead", true);

        // 死后不再响应鼠标
        if (MouseManager.instance != null)
        {
            MouseManager.instance.OnMouseClicked -= MoveToPosition;
            MouseManager.instance.OnEnemyClicked -= AttackEvent;
            MouseManager.instance.OnRockClicked -= AttackRock;
        }

        // 尸体不再被敌人选中/点击
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    private void Update()
    {
        if (characterStats != null && characterStats.IsDead) return;   // 死后不再更新移动动画
        if (IsStunned) return;                                        // 击飞/眩晕期间不刷新移动速度

        SwitchAnimation();
    }

    public void SwitchAnimation()
    {
        animator.SetFloat("Speed", agent.velocity.sqrMagnitude);
    }

    public void MoveToPosition(Vector3 target)
    {
        if (characterStats != null && characterStats.IsDead) return;   // 死了不再响应移动
        if (IsStunned) return;                                         // 击飞/眩晕期间不响应

        StopAllCoroutines();        // 取消正在进行的追击 / 攻击
        CancelPendingAttack();      // 挥砍未命中就取消，避免点地面后还结算伤害
        attackTarget = null;
        targetStats = null;
        rockTarget = null;
        agent.isStopped = false;
        agent.destination = target;
    }

    /// <summary>跨场景传送后把玩家放到落点（会同时清掉当前的移动/攻击状态）。</summary>
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        StopAllCoroutines();
        CancelPendingAttack();

        attackTarget = null;
        targetStats = null;
        rockTarget = null;

        if (agent != null)
        {
            agent.isStopped = true;

            // 标准做法：先关 Agent → 改 Transform → 再开 Agent，
            // 这样 Agent 会重新吸附到新位置的 NavMesh 上（世界场景刚加载完时尤其重要）
            agent.enabled = false;
        }

        transform.position = position;
        transform.rotation = rotation;

        if (agent != null)
        {
            agent.enabled = true;

            if (agent.isOnNavMesh)
                agent.Warp(position);

            agent.ResetPath();
            agent.isStopped = false;
        }
    }

    private void AttackEvent(GameObject target)
    {
        if (target == null) return;
        if (characterStats != null && characterStats.IsDead) return;   // 死了不再攻击
        if (IsStunned) return;                                         // 击飞/眩晕期间不响应

        // 反复点同一个敌人时，先停掉上一轮，避免叠出多条协程 -> 攻击频率翻倍
        StopAllCoroutines();
        CancelPendingAttack();      // 换目标时丢弃上一刀未命中的结算
        rockTarget = null;
        attackTarget = target;
        targetStats = target.GetComponent<CharacterStats>();
        StartCoroutine(MoveToAttackTarget());
    }

    /// <summary>点击地上的石头（Boss 战机制）：走过去把它打回 Boss。</summary>
    public void AttackRock(GameObject rockObject)
    {
        if (rockObject == null) return;
        if (characterStats != null && characterStats.IsDead) return;
        if (IsStunned) return;

        RockProjectile rock = rockObject.GetComponent<RockProjectile>();
        if (rock == null || rock.State != RockProjectile.RockState.Grounded) return;

        StopAllCoroutines();
        CancelPendingAttack();
        attackTarget = rockObject;
        targetStats = null;             // 石头没有 CharacterStats
        rockTarget = rock;
        StartCoroutine(MoveToRock());
    }

    /// <summary>走到石头旁边并攻击它（石头不会动，所以逻辑比追击敌人简单）。</summary>
    private IEnumerator MoveToRock()
    {
        agent.isStopped = false;

        while (rockTarget != null && attackTarget != null)
        {
            // 石头被别的途径处理掉了 / 已经飞走
            if (rockTarget == null || rockTarget.State != RockProjectile.RockState.Grounded)
                yield break;

            float distance = Vector3.Distance(transform.position, attackTarget.transform.position);

            if (distance > AttackRange)
            {
                agent.isStopped = false;
                agent.destination = attackTarget.transform.position;
                FaceTarget();
                yield return null;
            }
            else
            {
                agent.isStopped = true;
                FaceTarget();
                BeginAttack();
                yield return new WaitForSeconds(AttackCoolDown);
            }
        }
    }

    /// <summary>
    /// 追到攻击范围内就站定循环攻击；目标逃跑会重新进入追击；
    /// 目标被销毁/死亡、或玩家点了地面（MoveToPosition 里 StopAllCoroutines）时结束。
    /// </summary>
    private IEnumerator MoveToAttackTarget()
    {
        agent.isStopped = false;
        bool inAttackRange = false;

        while (true)
        {
            if (attackTarget == null) yield break;              // 目标已被销毁
            if (targetStats != null && targetStats.IsDead)      // 目标已死亡，停止鞭尸
            {
                attackTarget = null;
                targetStats = null;
                yield break;
            }

            float distance = DistanceToTargetSurface(attackTarget);

            // 进入用 AttackRange，离开用 AttackExitRange（滞回，避免在边界每帧来回切）
            inAttackRange = distance <= (inAttackRange ? AttackExitRange : AttackRange);

            if (!inAttackRange)
            {
                // ---- 追击阶段：目标会移动，每帧刷新目的地 ----
                agent.isStopped = false;
                agent.destination = attackTarget.transform.position;
                FaceTarget();
                yield return null;
            }
            else
            {
                // ---- 攻击阶段：站定、转向、按 SO 里的冷却出手 ----
                agent.isStopped = true;
                FaceTarget();
                BeginAttack();
                yield return new WaitForSeconds(AttackCoolDown);
            }
        }
    }

    /// <summary>
    /// 攻击开始：决定是否暴击（要选 clip，所以必须提前掷骰）+ 播放动画 + 暂存结果。
    /// 真正的伤害在动画事件 <see cref="OnAttackHit"/> 里结算。
    /// </summary>
    private void BeginAttack()
    {
        if (attackTarget == null) return;

        // 打石头：不需要伤害数值，只播动画；命中帧由 OnAttackHit 处理（把石头打回 Boss）
        if (rockTarget != null)
        {
            hasPendingDamage = true;
            animator.SetTrigger("Attack");
            return;
        }

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
        hasPendingDamage = false;           // 一次挥砍只结算一次

        // 打石头：把它打回 Boss
        if (rockTarget != null)
        {
            RockProjectile rock = rockTarget;
            rockTarget = null;
            attackTarget = null;
            targetStats = null;
            rock.OnHitByPlayer(gameObject);
            return;
        }

        if (Stats != null && Stats.IsDead) return;                  // 自己已经死了
        if (attackTarget == null || targetStats == null) return;
        if (targetStats.IsDead) return;                             // 目标已经死了

        // 命中帧再校验距离：抬手后目标跑开就不该吃到伤害
        // 注意：量的是"到目标身体表面"的距离，大体积 Boss 才打得到
        if (DistanceToTargetSurface(attackTarget) > AttackRange) return;

        targetStats.TakeDamage(pendingDamage);
    }

    /// <summary>
    /// 到目标"身体表面"的水平距离（不是中心）。
    /// Boss 体积大、NavMeshAgent 半径也大，用中心距永远够不到攻击范围。
    /// </summary>
    private float DistanceToTargetSurface(GameObject target)
    {
        if (target == null) return float.MaxValue;

        Vector3 point = target.transform.position;

        Collider col = target.GetComponent<Collider>();
        if (col != null && col.enabled)
        {
            Vector3 closest = col.ClosestPoint(transform.position);
            // 玩家已经在碰撞体内部时 ClosestPoint 会原样返回，这时当作 0 距离
            if ((closest - transform.position).sqrMagnitude > 0.0001f)
                point = closest;
            else
                point = transform.position;
        }

        Vector3 self = transform.position;
        self.y = 0f;
        point.y = 0f;

        return Vector3.Distance(self, point);
    }

    /// <summary>受击表现（不区分是否暴击）：被石头砸中等场合用。</summary>
    public void PlayHitReaction()
    {
        if (characterStats != null && characterStats.IsDead) return;
        if (IsStunned) return;

        animator.SetTrigger("Hit");
    }

    /// <summary>取消尚未命中的这一下（点地面 / 切换目标 / 被打断时调用）。</summary>
    public void CancelPendingAttack()
    {
        hasPendingDamage = false;
    }

    #region 击飞 / 眩晕
    /// <summary>
    /// 被 Boss 技能击飞：脱离 NavMesh、按抛物线飞起来，落地后眩晕一段时间。
    /// 眩晕期间**不免疫伤害**（技能第二段还要打中），只是不能操作。
    /// </summary>
    public void ApplyLaunch(Vector3 fromPosition, float upSpeed, float dizzyTime)
    {
        if (characterStats != null && characterStats.IsDead) return;

        // 取消正在进行的移动/攻击，避免落地后继续往旧目的地跑
        StopAllCoroutines();
        CancelPendingAttack();
        attackTarget = null;
        targetStats = null;
        rockTarget = null;

        IsStunned = true;
        animator.SetBool("Dizzy", true);

        // NavMeshAgent 会一直把玩家按在导航网格上，必须先关掉才能离地
        agent.isStopped = true;
        agent.enabled = false;

        StartCoroutine(LaunchRoutine(fromPosition, upSpeed, dizzyTime));
    }

    private IEnumerator LaunchRoutine(Vector3 fromPosition, float upSpeed, float dizzyTime)
    {
        const float Gravity = 20f;                       // 自定义重力，手感统一
        float duration = 2f * upSpeed / Gravity;         // 上升 + 下落的总时长

        Vector3 start = transform.position;

        Vector3 dir = start - fromPosition;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward;
        // 水平只推一点点：推太远第二刀就够不到玩家了
        Vector3 end = start + dir.normalized * 0.6f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            Vector3 p = Vector3.Lerp(start, end, k);
            p.y = start.y + upSpeed * t - 0.5f * Gravity * t * t;   // 抛物线
            transform.position = p;

            yield return null;
        }

        // 落地：贴回 NavMesh 并恢复 agent
        LandOnNavMesh(end);

        // 眩晕剩余时间（落地后继续晕）
        yield return new WaitForSeconds(Mathf.Max(0f, dizzyTime - duration));

        animator.SetBool("Dizzy", false);
        IsStunned = false;

        if (agent != null) agent.isStopped = false;
    }

    /// <summary>
    /// 被 Boss 近身攻击击退：水平滑出去一段距离（不离地），短暂定身。
    /// 落点会吸附到 NavMesh 上，避免被推出地图。
    /// </summary>
    public void ApplyKnockback(Vector3 fromPosition, float distance, float stunTime)
    {
        if (characterStats != null && characterStats.IsDead) return;

        StopAllCoroutines();
        CancelPendingAttack();
        attackTarget = null;
        targetStats = null;
        rockTarget = null;

        IsStunned = true;
        if (stunTime > 0f) animator.SetBool("Dizzy", true);

        agent.isStopped = true;
        agent.enabled = false;

        StartCoroutine(KnockbackRoutine(fromPosition, distance, stunTime));
    }

    private IEnumerator KnockbackRoutine(Vector3 fromPosition, float distance, float stunTime)
    {
        Vector3 start = transform.position;

        Vector3 dir = start - fromPosition;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward;

        Vector3 desired = start + dir.normalized * distance;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(desired, out hit, 2f, NavMesh.AllAreas)) desired = hit.position;

        const float SlideTime = 0.25f;
        float t = 0f;
        while (t < SlideTime)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, desired, Mathf.Clamp01(t / SlideTime));
            yield return null;
        }

        LandOnNavMesh(desired);

        if (stunTime > 0f) yield return new WaitForSeconds(stunTime);

        animator.SetBool("Dizzy", false);
        IsStunned = false;

        if (agent != null) agent.isStopped = false;
    }

    /// <summary>把玩家放到离 point 最近的 NavMesh 点上，并重新启用 agent。</summary>
    private void LandOnNavMesh(Vector3 point)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(point, out hit, 3f, NavMesh.AllAreas)) point = hit.position;
        transform.position = point;

        if (agent == null) return;
        agent.enabled = true;
        agent.Warp(point);
        agent.isStopped = true;
    }

    /// <summary>中断击飞（例如空中就死了）：强制落地并恢复 agent 状态。</summary>
    private void CancelLaunch()
    {
        StopAllCoroutines();

        if (IsStunned)
        {
            IsStunned = false;
            if (animator != null) animator.SetBool("Dizzy", false);
        }

        if (agent != null && !agent.enabled) LandOnNavMesh(transform.position);
    }
    #endregion

    private void FaceTarget()
    {
        Vector3 dir = attackTarget.transform.position - transform.position;
        dir.y = 0f;                                     // 不清掉高度差，角色会朝下前倾
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
