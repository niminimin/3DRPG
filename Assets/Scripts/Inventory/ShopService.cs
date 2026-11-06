using System;

/// <summary>交易结果。用枚举而不是 bool，这样 UI 能显示具体原因。</summary>
public enum TradeResult
{
    Success = 0,
    InvalidQuantity,   // 数量不对
    NotForSale,        // 这家店不卖
    OutOfStock,        // 库存不够
    NotEnoughGold,     // 钱不够
    BagFull,           // 背包放不下
    NoSuchSlot,        // 那个格子是空的
    NotEnoughItems     // 要卖的数量超过持有量
}

/// <summary>
/// 商店控制器（MVC 里的 Controller / 应用服务层）。
///
/// 它负责把三份数据串起来，并且**决定校验顺序**：
///     数量 → 是否在售 → 库存 → 金币 → 背包容量 → 提交
///
/// 为什么顺序重要：
///   - 先校验便宜的（数量、是否在售）
///   - 再校验可能失败的（库存、金币、容量）
///   - **全部通过后才改数据**，任何一步失败都不产生副作用
///
/// 纯 C#，不继承 MonoBehaviour —— 和模型一样可以脱离 Unity 单测。
/// </summary>
public class ShopService
{
    public Inventory Inventory { get; private set; }
    public Shop Shop { get; private set; }

    public ShopService(Inventory inventory, Shop shop)
    {
        if (inventory == null) throw new ArgumentNullException("inventory");
        if (shop == null) throw new ArgumentNullException("shop");

        Inventory = inventory;
        Shop = shop;
    }

    // ==================== 购买 ====================

    /// <summary>买 quantity 个 item。成功则扣金币、加物品、扣库存。</summary>
    public TradeResult Buy(ItemSO item, int quantity)
    {
        // ① 参数
        if (item == null || quantity <= 0) return TradeResult.InvalidQuantity;

        // ② 这家店卖不卖
        if (!Shop.Sells(item)) return TradeResult.NotForSale;

        // ③ 库存
        if (!Shop.HasStock(item, quantity)) return TradeResult.OutOfStock;

        // ④ 金币（用 long 算总价，防溢出）
        long cost = (long)item.buyPrice * quantity;
        if (cost > Inventory.Gold) return TradeResult.NotEnoughGold;

        // ⑤ 背包容量
        if (!Inventory.CanFit(item, quantity)) return TradeResult.BagFull;

        // ⑥ 全部通过才提交。三处修改合并成"一次变化"，UI 只刷新一次
        Inventory.BeginBatch();
        try
        {
            bool added = Inventory.Add(item, quantity);
            if (!added)
            {
                // 理论上不会走到这里（刚才 CanFit 通过了），但万一失败就把库存还回去
                return TradeResult.BagFull;
            }

            Shop.Consume(item, quantity);
            Inventory.SpendGold((int)cost);
        }
        finally
        {
            Inventory.EndBatch();
        }

        return TradeResult.Success;
    }

    // ==================== 出售 ====================

    /// <summary>卖掉指定格子里的 quantity 个。成功则给金币、减物品。</summary>
    public TradeResult Sell(int slotIndex, int quantity)
    {
        if (quantity <= 0) return TradeResult.InvalidQuantity;

        ItemStack stack = Inventory.GetSlot(slotIndex);
        if (stack.IsEmpty) return TradeResult.NoSuchSlot;
        if (stack.count < quantity) return TradeResult.NotEnoughItems;

        long income = (long)stack.item.sellPrice * quantity;

        Inventory.BeginBatch();
        try
        {
            Inventory.RemoveAt(slotIndex, quantity);
            Inventory.AddGold((int)Math.Min(income, int.MaxValue));
        }
        finally
        {
            Inventory.EndBatch();
        }

        return TradeResult.Success;
    }

    // ==================== 文案 ====================

    /// <summary>把结果翻译成给玩家看的提示。View 只负责显示，不负责措辞。</summary>
    public static string Describe(TradeResult result)
    {
        switch (result)
        {
            case TradeResult.Success:        return "交易成功";
            case TradeResult.InvalidQuantity:return "数量不正确";
            case TradeResult.NotForSale:     return "这件商品不出售";
            case TradeResult.OutOfStock:     return "库存不足";
            case TradeResult.NotEnoughGold:  return "金币不足";
            case TradeResult.BagFull:        return "背包空间不足";
            case TradeResult.NoSuchSlot:     return "这里没有物品";
            case TradeResult.NotEnoughItems: return "持有数量不足";
            default:                         return "交易失败";
        }
    }
}
