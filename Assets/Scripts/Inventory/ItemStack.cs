using System;

/// <summary>
/// 一格背包的内容：物品 + 数量。
///
/// 用 struct 而不是 class：
///   - 值语义：取出时是快照，外部改不到背包内部
///   - 不产生 GC 垃圾（背包每帧刷新也不会堆内存）
/// </summary>
[Serializable]
public struct ItemStack
{
    public ItemSO item;
    public int count;

    public bool IsEmpty
    {
        get { return item == null || count <= 0; }
    }

    public ItemStack(ItemSO item, int count)
    {
        this.item = item;
        this.count = count;
    }

    public static readonly ItemStack Empty = new ItemStack(null, 0);
}
