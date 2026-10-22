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

    /// <summary>暴击倍率：来自 AttackDataSO。没配数据时为 1（等于不暴击）。</summary>
    public float CriticalMultiplier
    {
        get { return characterStats == null ? 1f : characterStats.CriticalMultiplier; }
    }

    /// <summary>暴击率：来自 AttackDataSO，范围 0~1。</summary>
    public float CriticalChance
    {
        get { return characterStats == null ? 0f : Mathf.Clamp01(characterStats.CriticalChance); }
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
    }

    private void OnDisable()
    {
        if (MouseManager.instance == null) return;      // 退出 Play 时它可能已经被销毁

        MouseManager.instance.OnMouseClicked -= MoveToPosition;
        MouseManager.instance.OnEnemyClicked -= AttackEvent;
    }

    private void Update()
    {
        SwitchAnimation();
    }

    public void SwitchAnimation()
    {
        animator.SetFloat("Speed", agent.velocity.sqrMagnitude);
    }

    public void MoveToPosition(Vector3 target)
    {
        StopAllCoroutines();        // 取消正在进行的追击 / 攻击
        attackTarget = null;
        agent.isStopped = false;
        agent.destination = target;
    }

    private void AttackEvent(GameObject target)
    {
        if (target == null) return;

        // 反复点同一个敌人时，先停掉上一轮，避免叠出多条协程 -> 攻击频率翻倍
        StopAllCoroutines();
        attackTarget = target;
        StartCoroutine(MoveToAttackTarget());
    }

    /// <summary>
    /// 追到攻击范围内就站定循环攻击；目标逃跑会重新进入追击；
    /// 目标被销毁、或玩家点了地面（MoveToPosition 里 StopAllCoroutines）时结束。
    /// </summary>
    private IEnumerator MoveToAttackTarget()
    {
        agent.isStopped = false;
        bool inAttackRange = false;

        while (true)
        {
            if (attackTarget == null) yield break;      // 目标已被销毁

            float distance = Vector3.Distance(transform.position, attackTarget.transform.position);

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
                if(Random.value <= CriticalChance)
                {
                    animator.SetTrigger("CriticalAttack");
                }
                else
                {
                    animator.SetTrigger("Attack");
                }
                yield return new WaitForSeconds(AttackCoolDown);
            }
        }
    }

    private void FaceTarget()
    {
        Vector3 dir = attackTarget.transform.position - transform.position;
        dir.y = 0f;                                     // 不清掉高度差，角色会朝下前倾
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
