using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    public float sightRadius;
    private NavMeshAgent agent;
    private BaseState firstState;
    private BaseState curEnemyState;
    private BaseState newEnemyState;
    public BaseState guardState;
    public BaseState patrolState;
    public BaseState chaseState;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        guardState = new GuardState();
        chaseState = new ChaseState();
        patrolState = new PatrolState();
        //TODO 待确认
        curEnemyState = guardState;
        curEnemyState.OnEnter(this);
    }

    private void Update()
    {
        curEnemyState.LogicUpdata();
    }

    public void SwitchState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.GUARD:
                newEnemyState = guardState;
                break;
            case EnemyState.PATROL:
                newEnemyState = patrolState;
                break;
            case EnemyState.CHASE:
                newEnemyState = chaseState;
                break;
            default: break;
        }
        curEnemyState.OnExit();
        curEnemyState = newEnemyState;
        curEnemyState.OnEnter(this);
    }

    public bool FoundPlayer()
    {
        var colliders = Physics.OverlapSphere(transform.position, sightRadius);

        foreach (var target in colliders)
        {
            if (target.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }
}
