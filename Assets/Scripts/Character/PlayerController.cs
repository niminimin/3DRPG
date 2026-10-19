using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private GameObject attackTarget;
    private float lastAttackTime;

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
        lastAttackTime -= Time.deltaTime;
    }
    public void SwitchAnimation()
    {
        animator.SetFloat("speed", agent.velocity.sqrMagnitude);
    }

    public void MoveToPosition(Vector3 target)
    {
        StopAllCoroutines();
        agent.isStopped = false;
        agent.destination = target;
    }

    private void AttackEvent(GameObject target)
    {
        if(target != null)
        {
            attackTarget = target;
            StartCoroutine(MoveToAttackTarget());
        }
    }

    private IEnumerator MoveToAttackTarget()
    {
        agent.isStopped = false;
        transform.LookAt(attackTarget.transform);

        //TODO：攻击范围待后期修改
        while(Vector3.Distance(transform.position, attackTarget.transform.position) > 1)
        {
            agent.destination = attackTarget.transform.position;
            yield return null;
        }

        agent.isStopped = true;
        if(lastAttackTime < 0)
        {
            lastAttackTime = 0.5f;
            animator.SetTrigger("attack");
        }
    }


}
