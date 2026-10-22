using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Data", menuName = "CharacterStatus/CharacterDataSO")]
public class CharacterDataSO : ScriptableObject
{
    [Header("Status Info")]
    public int maxHealth;
    public int baseDefence;

}
