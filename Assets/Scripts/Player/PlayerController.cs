using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        MouseManager.instance.OnMouseClicked += MoveToTarget;
    }

    private void OnDisable()
    {
        MouseManager.instance.OnMouseClicked -= MoveToTarget;
    }

    private void Update()
    {
        SwitchAnimation();
    }

    public void MoveToTarget(Vector3 target)
    {
        agent.destination = target;
    }

    public void SwitchAnimation()
    {
        animator.SetFloat("speed", agent.velocity.sqrMagnitude);
    }
}
