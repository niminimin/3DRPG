using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物品目录：id → ItemSO 的查表。
///
/// 存档里只存 id 字符串，读档时要能"用 id 找回物品资产"，
/// 这张表就是那个"翻译器"。（对应 Inventory.Restore 的 resolve 参数）
/// </summary>
[CreateAssetMenu(fileName = "ItemCatalog", menuName = "GameData/ItemCatalog")]
public class ItemCatalogSO : ScriptableObject
{
    [Tooltip("工程里所有的物品资产都拖进来")]
    public ItemSO[] items;

    private Dictionary<string, ItemSO> map;

    /// <summary>按 id 找物品；找不到返回 null（老存档里可能有已删除的物品）。</summary>
    public ItemSO Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureMap();
        return map.TryGetValue(id, out ItemSO item) ? item : null;
    }

    /// <summary>给下拉/调试用：列出所有物品。</summary>
    public IReadOnlyList<ItemSO> All { get { return items; } }

    private void EnsureMap()
    {
        if (map != null) return;

        map = new Dictionary<string, ItemSO>();
        if (items == null) return;

        for (int i = 0; i < items.Length; i++)
        {
            ItemSO item = items[i];
            if (item == null || string.IsNullOrEmpty(item.id)) continue;

            if (map.ContainsKey(item.id))
            {
                Debug.LogError("[ItemCatalog] 物品 id 重复：" + item.id + "（" + item.name + "）", item);
                continue;
            }
            map.Add(item.id, item);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        map = null;    // Inspector 里改了列表，重建缓存
    }
#endif
}
