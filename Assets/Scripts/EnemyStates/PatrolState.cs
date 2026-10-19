using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PatrolState : BaseState
{
    public override void LogicUpdata()
    {
        throw new System.NotImplementedException();
    }

    public override void OnEnter(EnemyController target)
    {
        enemy = target;
    }

    public override void OnExit()
    {
        throw new System.NotImplementedException();
    }
}
