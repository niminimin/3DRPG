using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 存档数据（会被 JsonUtility 序列化成 JSON）。
///
/// 只存"必要"的东西：
///   - 玩家：场景、位置、朝向、等级、经验、血量
///   - 敌人：只记"哪些死了"（位置和血量不存：敌人本来就在固定范围巡逻，残血回来回满也合理）
///
/// JsonUtility 不支持 Dictionary，所以死亡表用 List 存。
/// </summary>
[Serializable]
public class SaveData
{
    [Header("玩家")]
    public string sceneName = "";
    public Vector3 playerPosition;
    public float playerYaw;
    public int level = 1;
    public int exp;
    public int curHealth;

    [Header("敌人：只记死亡的 ID")]
    public List<string> deadEnemyIds = new List<string>();

    [Header("背包")]
    public int gold;
    [Tooltip("每格的物品 id（空格为 null）")]
    public string[] slotIds = new string[0];
    [Tooltip("每格的数量（空格为 0）")]
    public int[] slotCounts = new int[0];

    [Header("存档时间（用于显示）")]
    public string savedAt = "";

    /// <summary>运行时查询用：把 List 转成 Set 加速 Contains。</summary>
    [NonSerialized] private HashSet<string> deadSet;

    public bool IsEnemyDead(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        if (deadSet == null)
        {
            deadSet = new HashSet<string>(deadEnemyIds);
        }

        return deadSet.Contains(id);
    }

    public void MarkEnemyDead(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        if (deadEnemyIds == null) deadEnemyIds = new List<string>();

        if (!deadEnemyIds.Contains(id))
        {
            deadEnemyIds.Add(id);
            deadSet = null;          // 让下一次查询重建
        }
    }

    public void ClearDeadEnemies()
    {
        deadEnemyIds.Clear();
        deadSet = null;
    }
}
