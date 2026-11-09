using System;
using UnityEngine;

/// <summary>
/// 背包/商店的 Unity 适配层（常驻 Boot 场景）。
///
/// 注意它**不是**规则层：
///   - 规则在 Inventory（容量/叠放）和 ShopService（交易顺序）
///   - 它只做三件事：
///       1. 持有模型对象，创建一次
///       2. 把 Unity 侧的东西接进来（玩家血量、存档、物品表）
///       3. 对外暴露简单 API，让战斗/UI 不用认识内部结构
/// </summary>
public class InventoryService : Singleton<InventoryService>
{
    [Header("数据")]
    [Tooltip("id → ItemSO 的查表，读档时用")]
    public ItemCatalogSO catalog;

    [Header("初始状态")]
    public int capacity = 24;
    public int startGold = 100;

    [Header("默认商店（还没有商店 NPC 时先写死在服务里）")]
    public ItemSO potionItem;      // 无限库存
    public ItemSO smallPotionItem; // 无限库存
    public ItemSO oreItem;         // 限购 5

    public Inventory Inventory { get; private set; }
    public Shop Shop { get; private set; }
    public ShopService Trading { get; private set; }

    /// <summary>背包或金币变化时触发（给 UI 用）。</summary>
    public event Action Changed;

    protected override void Awake()
    {
        base.Awake();
        BuildModel();
    }

    private void BuildModel()
    {
        Inventory = new Inventory(capacity, startGold);

        Shop = new Shop();
        if (potionItem != null) Shop.AddProduct(potionItem, Shop.Unlimited);
        if (smallPotionItem != null) Shop.AddProduct(smallPotionItem, Shop.Unlimited);
        if (oreItem != null) Shop.AddProduct(oreItem, 5);

        Trading = new ShopService(Inventory, Shop);
        Inventory.Changed += HandleInventoryChanged;
    }

    private void OnDestroy()
    {
        if (Inventory != null) Inventory.Changed -= HandleInventoryChanged;
    }

    private void HandleInventoryChanged()
    {
        if (Changed != null) Changed();
    }

    // ==================== 给游戏其他系统用的 API ====================

    /// <summary>打怪掉落 / 任务奖励。</summary>
    public void AddGold(int amount)
    {
        if (amount > 0) Inventory.AddGold(amount);
    }

    public bool AddItem(ItemSO item, int quantity)
    {
        return Inventory.Add(item, quantity);
    }

    /// <summary>
    /// 使用某个格子的物品（目前只有回血药水）。
    /// 满血时不消耗——避免玩家白白喝掉一瓶。
    /// </summary>
    public bool TryUse(int slotIndex)
    {
        ItemStack stack = Inventory.GetSlot(slotIndex);
        if (stack.IsEmpty || !stack.item.IsUsable) return false;

        CharacterStats stats = PlayerProgress.instance != null ? PlayerProgress.instance.stats : null;
        if (stats == null || stats.IsDead) return false;
        if (stats.CurHealth >= stats.MaxHealth) return false;   // 满血不浪费

        int healed = stats.Heal(stack.item.healAmount);
        if (healed <= 0) return false;

        Inventory.RemoveAt(slotIndex, 1);
        return true;
    }

    /// <summary>购买（UI 调用）。</summary>
    public TradeResult TryBuy(ItemSO item, int quantity)
    {
        return Trading.Buy(item, quantity);
    }

    /// <summary>出售某个格子（UI 调用）。</summary>
    public TradeResult TrySell(int slotIndex, int quantity)
    {
        return Trading.Sell(slotIndex, quantity);
    }

    // ==================== 存档 ====================

    /// <summary>把当前背包写进存档数据。</summary>
    public void WriteTo(SaveData data)
    {
        if (data == null || Inventory == null) return;

        data.gold = Inventory.Gold;
        Inventory.Export(out string[] ids, out int[] counts);
        data.slotIds = ids;
        data.slotCounts = counts;
    }

    /// <summary>读档：用 catalog 把 id 还原成物品。</summary>
    public void ReadFrom(SaveData data)
    {
        if (data == null || Inventory == null) return;

        Func<string, ItemSO> resolve = FindItem;
        Inventory.Restore(data.gold, data.slotIds, data.slotCounts, resolve);
    }

    /// <summary>新游戏：清空背包，给初始金币。</summary>
    public void ResetForNewGame()
    {
        if (Inventory != null) Inventory.Clear(startGold);
    }

    public ItemSO FindItem(string id)
    {
        return catalog != null ? catalog.Find(id) : null;
    }
}
