using UnityEngine;

/// <summary>物品分类（用于商店筛选、背包排序）。</summary>
public enum ItemCategory
{
    Consumable = 0,   // 消耗品：药水
    Equipment = 1,    // 装备：武器、护甲
    Material = 2      // 材料：矿石、木材
}

/// <summary>
/// 物品定义（数据层）。
///
/// 这是 Model 的一部分：**只有数据，没有逻辑**。
/// 运行时绝不修改它 —— 它是共享资产，改了会污染整个工程。
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "GameData/Item")]
public class ItemSO : ScriptableObject
{
    [Header("身份")]
    [Tooltip("存档用的唯一 id，改了会让老存档读不到这件物品")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("分类与价格")]
    public ItemCategory category = ItemCategory.Material;
    public int buyPrice = 10;
    public int sellPrice = 5;

    [Tooltip("单格最多叠多少个（1 表示不可堆叠）")]
    public int maxStack = 1;

    [Header("消耗品效果")]
    [Tooltip("使用后回复的生命值，>0 才允许使用")]
    public int healAmount;

    /// <summary>能不能被"使用"（而不是只能买卖）。</summary>
    public bool IsUsable
    {
        get { return category == ItemCategory.Consumable && healAmount > 0; }
    }
}
