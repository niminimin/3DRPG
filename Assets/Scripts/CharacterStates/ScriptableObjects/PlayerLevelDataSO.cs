using UnityEngine;

/// <summary>
/// 玩家升级配置：
/// - 每级所需经验（逐级递增）
/// - 每升一级增加的属性
/// 全部数据驱动，改数值不用改代码。
/// </summary>
[CreateAssetMenu(fileName = "PlayerLevelData", menuName = "CharacterStatus/PlayerLevelDataSO")]
public class PlayerLevelDataSO : ScriptableObject
{
    [Header("等级")]
    [Tooltip("最高等级")]
    public int maxLevel = 10;

    [Header("升级所需经验")]
    [Tooltip("下标 0 = 1→2 级，下标 1 = 2→3 级 …… 长度应为 maxLevel - 1")]
    public int[] expToNextLevel = new int[9];

    [Header("每级成长")]
    public int healthPerLevel = 15;
    public int attackPerLevel = 3;
    public int defencePerLevel = 1;

    /// <summary>从 level 级升到 level+1 级需要多少经验；满级返回 0。</summary>
    public int ExpToNext(int level)
    {
        if (level >= maxLevel) return 0;
        if (expToNextLevel == null || expToNextLevel.Length == 0) return 100;

        int index = Mathf.Clamp(level - 1, 0, expToNextLevel.Length - 1);
        return Mathf.Max(1, expToNextLevel[index]);
    }
}
