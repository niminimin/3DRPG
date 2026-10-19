using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseState 
{
    protected EnemyController enemy;
    public abstract void OnEnter(EnemyController enemy);
    public abstract void LogicUpdata();
    public abstract void OnExit();
}
