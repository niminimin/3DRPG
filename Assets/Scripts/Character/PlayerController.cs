using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    [Header("攻击参数")]
    [Tooltip("进入这个水平距离就开始攻击")]
    [SerializeField] private float attackRange = 1.5f;
    [Tooltip("两次攻击之间的间隔，必须 >= 攻击动画时长(Attack01 = 0.83s)，否则触发器会被吞掉")]
    [SerializeField] private float attackInterval = 1.0f;

    private NavMeshAgent agent;
    private Animator animator;
    private GameObject attackTarget;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        MouseManager.instance.OnMouseClicked += MoveToPosition;
        MouseManager.instance.OnEnemyClicked += AttackEvent;
    }

    private void OnDisable()
    {
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

        while (true)
        {
            if (attackTarget == null) yield break;      // 目标已被销毁

            float distance = Vector3.Distance(transform.position, attackTarget.transform.position);

            if (distance > attackRange)
            {
                // ---- 追击阶段：目标会移动，每帧刷新目的地 ----
                agent.isStopped = false;
                agent.destination = attackTarget.transform.position;
                FaceTarget();
                yield return null;
            }
            else
            {
                // ---- 攻击阶段：站定、转向、按间隔出手 ----
                agent.isStopped = true;
                FaceTarget();
                animator.SetTrigger("Attack");
                yield return new WaitForSeconds(attackInterval);
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
