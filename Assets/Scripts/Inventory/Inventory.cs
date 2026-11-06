using System;

/// <summary>
/// 背包模型（Model 层的核心）。
///
/// 设计要点：
/// 1. **纯 C#，不继承 MonoBehaviour** —— 可以脱离 Unity 单独测试，也不会被场景加载销毁
/// 2. **固定槽位数组** —— 支持"拖到指定格子"，如果换成 List，换位要重排所有索引
/// 3. **先校验后修改** —— Add 都是"能不能放下"先算清楚，再动数据
/// 4. **只发一个 Changed** —— UI 永远看不到中间状态（批量模式见 BeginBatch/EndBatch）
/// 5. **不认识 UI、不认识场景、不认识存档格式** —— 只提供数据进出
///
/// 它不知道"谁在听"，也不主动找 UI —— 这是 MVC 里 Model 该有的样子。
/// </summary>
public class Inventory
{
    private readonly ItemStack[] slots;

    /// <summary>金币（最小版本先放在背包里，以后要拆成 Wallet 也很容易）。</summary>
    public int Gold { get; private set; }

    public int Capacity { get { return slots.Length; } }

    /// <summary>背包内容或金币变化时触发（一次操作只触发一次）。</summary>
    public event Action Changed;

    // 批量模式深度：>0 时抑制事件，归零时统一发一次。
    private int batchDepth;

    public Inventory(int capacity, int startGold = 0)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException("capacity", "背包至少要有 1 格");
        slots = new ItemStack[capacity];
        Gold = Math.Max(0, startGold);
    }

    // ==================== 批量模式 ====================
    /// <summary>
    /// 开始一串操作：期间所有改动都不发事件，EndBatch 时统一发一次。
    /// 用于"买一件东西 = 扣金币 + 加物品"这种跨多步、但玩家看来只发生了一次变化的场景。
    /// 必须成对调用（推荐 try/finally，异常时也能恢复）。
    /// </summary>
    public void BeginBatch() { batchDepth++; }

    public void EndBatch()
    {
        if (batchDepth > 0) batchDepth--;
        if (batchDepth == 0) Changed?.Invoke();
    }

    /// <summary>所有内部改动都走这里：批量模式下只改数据，不通知 UI。</summary>
    private void NotifyChanged()
    {
        if (batchDepth == 0) Changed?.Invoke();
    }

    // ==================== 查询 ====================

    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= slots.Length) return ItemStack.Empty;
        return slots[index];
    }

    /// <summary>已占用的格子数（显示 "3 / 24" 用）。</summary>
    public int OccupiedSlots
    {
        get
        {
            int n = 0;
            for (int i = 0; i < slots.Length; i++) if (!slots[i].IsEmpty) n++;
            return n;
        }
    }

    /// <summary>某种物品的总数量（跨所有格子）。</summary>
    public int CountOf(ItemSO item)
    {
        if (item == null) return 0;

        int total = 0;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].item == item) total += slots[i].count;
        return total;
    }

    /// <summary>
    /// 还能再装下多少个这件物品。
    /// 关键：**只计算，不修改任何数据** —— 这样"容量不足就不扣钱"才成立。
    /// </summary>
    public int RoomFor(ItemSO item, int quantity)
    {
        if (item == null || quantity <= 0) return 0;

        long room = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack stack = slots[i];
            if (stack.IsEmpty) room += item.maxStack;                       // 空格
            else if (stack.item == item) room += item.maxStack - stack.count; // 没堆满的同类
        }
        return (int)Math.Min(room, int.MaxValue);
    }

    public bool CanFit(ItemSO item, int quantity)
    {
        return quantity > 0 && RoomFor(item, quantity) >= quantity;
    }

    // ==================== 增删 ====================

    /// <summary>往背包里放东西。放不下返回 false，且不会部分写入。</summary>
    public bool Add(ItemSO item, int quantity)
    {
        if (item == null || quantity <= 0) return false;
        if (!CanFit(item, quantity)) return false;

        AddUnchecked(item, quantity);
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// 从指定格子扣数量（出售/使用都走这里）。
    /// 按"具体格子"扣，避免同种物品分成两堆时误删另一堆。
    /// </summary>
    public bool RemoveAt(int index, int quantity)
    {
        if (index < 0 || index >= slots.Length || quantity <= 0) return false;

        ItemStack stack = slots[index];
        if (stack.IsEmpty || stack.count < quantity) return false;

        slots[index] = stack.count == quantity
            ? ItemStack.Empty
            : new ItemStack(stack.item, stack.count - quantity);

        NotifyChanged();
        return true;
    }

    /// <summary>按物品扣数量（不关心在哪个格子）。</summary>
    public bool Remove(ItemSO item, int quantity)
    {
        if (item == null || quantity <= 0) return false;
        if (CountOf(item) < quantity) return false;

        int remain = quantity;
        for (int i = 0; i < slots.Length && remain > 0; i++)
        {
            if (slots[i].item != item) continue;

            int take = Math.Min(remain, slots[i].count);
            slots[i] = slots[i].count == take
                ? ItemStack.Empty
                : new ItemStack(item, slots[i].count - take);
            remain -= take;
        }

        NotifyChanged();
        return true;
    }

    /// <summary>交换两个格子（允许一端为空，拖拽换位用）。</summary>
    public bool Swap(int a, int b)
    {
        if (a < 0 || b < 0 || a >= slots.Length || b >= slots.Length || a == b) return false;

        ItemStack temp = slots[a];
        slots[a] = slots[b];
        slots[b] = temp;

        NotifyChanged();
        return true;
    }

    // ==================== 金币 ====================

    public void AddGold(int amount)
    {
        if (amount <= 0) return;

        // 用 long 判断上限，避免 int 溢出变成负数
        long next = (long)Gold + amount;
        Gold = next > int.MaxValue ? int.MaxValue : (int)next;
        NotifyChanged();
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0 || amount > Gold) return false;

        Gold -= amount;
        NotifyChanged();
        return true;
    }

    // ==================== 内部 ====================

    /// <summary>
    /// 两趟扫描：先补没满的同类堆叠，再开新格子。
    /// 如果不分两趟，一件物品可能被塞进多个半空格，看起来会很乱。
    /// </summary>
    private void AddUnchecked(ItemSO item, int quantity)
    {
        for (int pass = 0; pass < 2 && quantity > 0; pass++)
        {
            for (int i = 0; i < slots.Length && quantity > 0; i++)
            {
                ItemStack stack = slots[i];
                bool usable = pass == 0
                    ? !stack.IsEmpty && stack.item == item
                    : stack.IsEmpty;
                if (!usable) continue;

                int added = Math.Min(quantity, item.maxStack - stack.count);
                slots[i] = new ItemStack(item, stack.count + added);
                quantity -= added;
            }
        }
    }

    // ==================== 存档进出 ====================
    // 模型不认识"存档文件"，只负责导出/导入纯数据，格式由外面决定。

    /// <summary>导出：每格的物品 id 和数量（空格为 null / 0）。</summary>
    public void Export(out string[] ids, out int[] counts)
    {
        ids = new string[slots.Length];
        counts = new int[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            ids[i] = slots[i].IsEmpty ? null : slots[i].item.id;
            counts[i] = slots[i].IsEmpty ? 0 : slots[i].count;
        }
    }

    /// <summary>
    /// 导入：外部提供"id → ItemSO"的解析器（因为模型不该自己去找资源）。
    /// 找不到的 id 会被跳过（比如删掉了某件物品）。
    /// </summary>
    public void Restore(int gold, string[] ids, int[] counts, Func<string, ItemSO> resolve)
    {
        Array.Clear(slots, 0, slots.Length);
        Gold = Math.Max(0, gold);

        if (ids != null && counts != null && resolve != null)
        {
            int n = Math.Min(ids.Length, Math.Min(counts.Length, slots.Length));
            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrEmpty(ids[i]) || counts[i] <= 0) continue;

                ItemSO item = resolve(ids[i]);
                if (item == null) continue;
                slots[i] = new ItemStack(item, Math.Min(counts[i], item.maxStack));
            }
        }

        Changed?.Invoke();
    }

    /// <summary>清空（新游戏用）。</summary>
    public void Clear(int startGold = 0)
    {
        Array.Clear(slots, 0, slots.Length);
        Gold = Math.Max(0, startGold);
        Changed?.Invoke();
    }

    /// <summary>调试用：打印当前背包。</summary>
    public string Dump()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("金币 ").Append(Gold).Append(" | ");
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsEmpty) continue;
            sb.Append('[').Append(i).Append("] ")
              .Append(slots[i].item.displayName).Append('×').Append(slots[i].count).Append("  ");
        }
        return sb.ToString();
    }
}
