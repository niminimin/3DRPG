using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Attack/AttackDataSO")]
public class AttackDataSO : ScriptableObject
{
    public float attackRange;
    public float skillRange;
    public float coolDown;
    public int maxDamage;
    public int minDamage; 

    public float criticalMultiplier;
    public float criticalChance;
}

