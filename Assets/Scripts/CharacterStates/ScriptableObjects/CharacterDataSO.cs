using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Data", menuName = "CharacterStatus/CharacterDataSO")]
public class CharacterDataSO : ScriptableObject
{
    [Header("Status Info")]
    public int maxHealth;
    public int baseDefence;

    [Header("Reward")]
    [Tooltip("被击败后奖励给玩家的经验值（玩家自己这个值不用填）")]
    public int expReward;
}
