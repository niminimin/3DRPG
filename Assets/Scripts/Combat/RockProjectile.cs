using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 丢出的石头，有三种状态：
/// 1) Flying     抛物线飞向玩家；砸中就造成伤害 + 短距离击退，然后落地（不销毁）
/// 2) Grounded   躺在地上，tag = "Rock"，玩家可以点击攻击它
/// 3) Reflecting 被玩家打回去，飞向 Boss；命中后扣 Boss 血并销毁
///
/// 这就是 Boss 战的核心机制：把飞来的石头打回去反伤 Boss。
/// </summary>
public class RockProjectile : MonoBehaviour
{
    public enum RockState { Flying, Grounded, Reflecting, Done }

    [Header("命中玩家")]
    [Tooltip("命中判定半径（米）")]
    public float hitRadius = 1.5f;

    [Tooltip("重力加速度（越大弧线越扁）")]
    public float gravity = 18f;

    [Tooltip("砸中玩家后的击退距离（米）")]
    public float playerKnockback = 1.5f;

    [Tooltip("砸中玩家后的定身时长（秒）；0 = 只播受击动画、不进眩晕")]
    public float playerStun = 0f;

    [Header("被玩家打回")]
    [Tooltip("反弹命中 Boss 造成的伤害")]
    public int reflectDamage = 25;

    [Tooltip("反弹后飞向 Boss 的时间（秒）")]
    public float reflectFlightTime = 0.9f;

    [Tooltip("命中 Boss 的判定半径")]
    public float ownerHitRadius = 2.5f;

    [Header("落地")]
    [Tooltip("落地后停留多久自动消失；0 = 一直留着")]
    public float groundLifetime = 0f;

    private RockState state = RockState.Done;

    private Rigidbody rb;
    private Collider col;

    // 投掷者
    private CharacterStats attacker;        // Boss 的数值（算伤害用）
    private GameObject owner;               // Boss 本体（反弹目标）
    private CharacterStats ownerStats;      // Boss 的 CharacterStats

    private GameObject playerTarget;

    private Vector3 startPos;
    private Vector3 targetPoint;
    private Vector3 velocity;
    private float flightTime;
    private float timer;

    // 落地流程：到达瞄准点后继续下落到 groundY 才冻结
    private bool aimResolved;
    private float groundY;

    public RockState State
    {
        get { return state; }
    }

    /// <summary>刚体（懒加载：编辑器/测试环境下 Awake 不一定跑过）。</summary>
    private Rigidbody Rb
    {
        get
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            return rb;
        }
    }

    /// <summary>碰撞体（懒加载）。</summary>
    private Collider Col
    {
        get
        {
            if (col == null) col = GetComponent<Collider>();
            return col;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // 飞行由脚本控制，物理只在落地后接管
        if (rb != null) rb.isKinematic = true;
    }

    // ==================== 1. 投掷 ====================
    /// <summary>
    /// 由 Boss 调用：抛物线飞向玩家。落地后变成可攻击状态。
    /// </summary>
    public void Launch(CharacterStats attacker, GameObject player, GameObject owner,
                       CharacterStats ownerStats, float flightTime, float hitRadius)
    {
        this.attacker = attacker;
        this.playerTarget = player;
        this.owner = owner;
        this.ownerStats = ownerStats;
        this.flightTime = Mathf.Max(0.3f, flightTime);
        this.hitRadius = hitRadius;

        startPos = transform.position;
        targetPoint = player != null
            ? player.transform.position + Vector3.up * 1f
            : startPos + transform.forward * 15f;

        velocity = BallisticVelocity(startPos, targetPoint, this.flightTime, gravity);
        timer = 0f;
        state = RockState.Flying;
        aimResolved = false;
        groundY = 0f;

        gameObject.tag = "Untagged";        // 飞行中不可点击

        // 飞行期间：脚本控制位置、关碰撞（避免空中互相撞 / 撞玩家）
        if (Rb != null) Rb.isKinematic = true;
        if (Col != null) Col.enabled = false;
    }

    // ==================== 每帧 ====================
    private void Update()
    {
        if (state == RockState.Flying)
        {
            timer += Time.deltaTime;

            Vector3 p = startPos
                        + velocity * timer
                        + 0.5f * Vector3.down * gravity * timer * timer;

            transform.position = p;
            transform.Rotate(540f * Time.deltaTime, 260f * Time.deltaTime, 0f);

            // 到达瞄准点：结算伤害（但石头继续往下落，不在这里停）
            if (!aimResolved && timer >= flightTime)
            {
                ResolveHitAtAim();
                aimResolved = true;
                groundY = FindGroundY(p) + 0.35f;
            }

            // 落到地面：原地冻结（不交给物理，避免滚走）
            if (aimResolved && p.y <= groundY)
            {
                transform.position = new Vector3(p.x, groundY, p.z);
                FreezeOnGround();
            }
            return;
        }

        if (state == RockState.Reflecting)
        {
            timer += Time.deltaTime;

            transform.position = startPos
                                 + velocity * timer
                                 + 0.5f * Vector3.down * gravity * timer * timer;

            transform.Rotate(540f * Time.deltaTime, 260f * Time.deltaTime, 0f);

            if (timer >= flightTime) HitOwner();
        }
    }

    // ==================== 2. 命中玩家 ====================
    private void ResolveHitAtAim()
    {
        if (playerTarget == null || attacker == null) return;

        CharacterStats playerStats = playerTarget.GetComponent<CharacterStats>();

        if (playerStats != null && !playerStats.IsDead
            && Vector3.Distance(transform.position, playerTarget.transform.position) <= hitRadius)
        {
            playerStats.TakeDamage(attacker.CalculateDamage(playerStats));

            PlayerController player = playerTarget.GetComponent<PlayerController>();
            if (player != null)
            {
                player.PlayHitReaction();
                player.ApplyKnockback(transform.position, playerKnockback, playerStun);
            }
        }
    }

    /// <summary>落到地面：原地冻结成可攻击状态（保持 kinematic，绝不滚动）。</summary>
    private void FreezeOnGround()
    {
        state = RockState.Grounded;
        gameObject.tag = "Rock";            // ★ 落地后才允许鼠标点击

        if (Col != null) Col.enabled = true;
        if (Rb != null)
        {
            Rb.position = transform.position;
            Rb.rotation = transform.rotation;
            Rb.isKinematic = true;          // 不被物理推走
            Rb.useGravity = false;
        }

        if (groundLifetime > 0f) Destroy(gameObject, groundLifetime);
    }

    /// <summary>找石头下方的地面高度：优先取 tag = Ground 的碰撞体，避免打到别的石头/角色。</summary>
    private float FindGroundY(Vector3 from)
    {
        RaycastHit[] hits = Physics.RaycastAll(from + Vector3.up * 2f, Vector3.down, 30f);
        if (hits == null || hits.Length == 0) return 0f;

        float highestGround = float.NegativeInfinity;   // Ground 里最高的那个（最近的地面）
        float lowestAny = float.PositiveInfinity;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.CompareTag("Ground") && h.point.y > highestGround) highestGround = h.point.y;
            if (h.point.y < lowestAny) lowestAny = h.point.y;
        }

        if (highestGround > float.NegativeInfinity) return highestGround;
        return lowestAny < float.PositiveInfinity ? lowestAny : 0f;
    }

    // ==================== 3. 被玩家打回 ====================
    /// <summary>玩家命中石头时调用：石头朝 Boss 飞回去。</summary>
    public void OnHitByPlayer(GameObject player)
    {
        if (state != RockState.Grounded) return;
        if (owner == null || ownerStats == null) return;

        state = RockState.Reflecting;
        gameObject.tag = "Untagged";

        // 反弹期间交回脚本控制，关掉碰撞避免空中乱撞
        if (Rb != null) Rb.isKinematic = true;
        if (Col != null) Col.enabled = false;

        startPos = transform.position;
        targetPoint = owner.transform.position + Vector3.up * 2f;
        velocity = BallisticVelocity(startPos, targetPoint, reflectFlightTime, gravity);
        flightTime = reflectFlightTime;
        timer = 0f;
    }

    private void HitOwner()
    {
        if (ownerStats != null && !ownerStats.IsDead
            && owner != null
            && Vector3.Distance(transform.position, owner.transform.position) <= ownerHitRadius)
        {
            // isCritical = true 只是为了让 Boss 播受击动画/进硬直，伤害就是 reflectDamage
            ownerStats.TakeDamage(new DamageResult { amount = reflectDamage, isCritical = true });
        }

        state = RockState.Done;
        Destroy(gameObject);
    }

    // ==================== 工具 ====================
    /// <summary>给定飞行时间反解抛物线初速度（水平匀速 + 竖直向上冲击）。</summary>
    private static Vector3 BallisticVelocity(Vector3 from, Vector3 to, float time, float g)
    {
        Vector3 delta = to - from;
        float vx = delta.x / time;
        float vz = delta.z / time;
        float vy = (delta.y + 0.5f * g * time * time) / time;
        return new Vector3(vx, vy, vz);
    }
}
