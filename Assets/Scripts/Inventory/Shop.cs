using System.Collections.Generic;

/// <summary>
/// 商店模型（Model 层）。
///
/// 只回答三个问题：
///   1. 卖不卖这件东西？
///   2. 还剩几件？
///   3. 扣掉几件库存。
///
/// **不知道金币、不知道背包、不知道 UI** —— 那些是控制器的事。
/// 价格也不存在这里，价格是物品本身的属性（ItemSO.buyPrice / sellPrice）。
/// </summary>
public class Shop
{
    /// <summary>无限库存的约定值。</summary>
    public const int Unlimited = -1;

    private readonly List<ItemSO> products = new List<ItemSO>();   // 保持上架顺序
    private readonly Dictionary<ItemSO, int> stock = new Dictionary<ItemSO, int>();

    /// <summary>在售商品（按上架顺序）。</summary>
    public IReadOnlyList<ItemSO> Products { get { return products; } }

    /// <summary>上架一件商品。initialStock 传 -1 表示无限库存。</summary>
    public void AddProduct(ItemSO item, int initialStock)
    {
        if (item == null) return;
        if (initialStock < Unlimited) initialStock = Unlimited;

        if (stock.ContainsKey(item))
        {
            stock[item] = initialStock;    // 重复上架 = 改库存
            return;
        }

        products.Add(item);
        stock[item] = initialStock;
    }

    /// <summary>这家店卖不卖它。</summary>
    public bool Sells(ItemSO item)
    {
        return item != null && stock.ContainsKey(item);
    }

    /// <summary>剩余库存；无限库存返回 -1；不卖返回 0。</summary>
    public int StockOf(ItemSO item)
    {
        if (!Sells(item)) return 0;
        return stock[item];
    }

    /// <summary>够不够卖这么多。</summary>
    public bool HasStock(ItemSO item, int quantity)
    {
        if (!Sells(item) || quantity <= 0) return false;

        int current = stock[item];
        return current == Unlimited || current >= quantity;
    }

    /// <summary>扣库存（无限库存不变）。调用前应先用 HasStock 校验。</summary>
    public void Consume(ItemSO item, int quantity)
    {
        if (!HasStock(item, quantity)) return;
        if (stock[item] != Unlimited) stock[item] -= quantity;
    }

    public void Clear()
    {
        products.Clear();
        stock.Clear();
    }
}
