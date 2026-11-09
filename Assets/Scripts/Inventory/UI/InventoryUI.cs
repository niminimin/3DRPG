using UnityEngine;

/// <summary>
/// 背包界面的 Presenter（Controller 层）。
///
/// 它同时认识 View（InventoryPanel）和 Model（通过 InventoryService），
/// 但两边都不认识它：
///   - Panel 只发事件、只接受 ShowXxx
///   - Service 只提供数据和方法
///
/// 这里放的是"界面状态"：选中哪一格、买几个，这些不属于模型。
/// </summary>
public class InventoryUI : Singleton<InventoryUI>
{
    [Header("输入")]
    public KeyCode toggleKey = KeyCode.I;

    private InventoryPanel panel;
    private InventoryService service;

    private int selectedBag = -1;
    private int selectedShop = -1;
    private int quantity = 1;

    public bool IsOpen { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        BuildPanel();
    }

    private void Start()
    {
        service = InventoryService.instance;
        if (service == null)
            Debug.LogWarning("[InventoryUI] 找不到 InventoryService，背包界面不可用", this);
    }

    private void BuildPanel()
    {
        GameObject go = new GameObject("InventoryPanel");
        go.transform.SetParent(transform, false);
        panel = go.AddComponent<InventoryPanel>();

        panel.BagSlotClicked += OnBagSlotClicked;
        panel.ShopSlotClicked += OnShopSlotClicked;
        panel.BuyClicked += OnBuy;
        panel.SellClicked += OnSell;
        panel.UseClicked += OnUse;
        panel.CloseClicked += Close;
        panel.MinusClicked += OnMinus;
        panel.PlusClicked += OnPlus;
    }

    private void Update()
    {
        // 暂停菜单开着的时候不响应背包键
        if (PauseMenu.instance != null && PauseMenu.instance.IsOpen) return;

        if (Input.GetKeyDown(toggleKey))
        {
            if (IsOpen) Close();
            else Open();
            return;
        }

        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    private void OnDestroy()
    {
        if (service != null) service.Changed -= Refresh;
    }

    // ==================== 开关 ====================

    public void Open()
    {
        if (IsOpen) return;
        if (service == null) service = InventoryService.instance;
        if (service == null || panel == null) return;

        IsOpen = true;
        selectedBag = -1;
        selectedShop = -1;
        quantity = 1;

        panel.SetVisible(true);
        Time.timeScale = 0f;                       // 暂停世界，同时让 MouseManager 不再处理点击
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        service.Changed += Refresh;
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        panel.SetVisible(false);
        service.Changed -= Refresh;
        Time.timeScale = 1f;
    }

    // ==================== 刷新（数据 → 视图） ====================

    private void Refresh()
    {
        if (panel == null || service == null) return;

        panel.ShowGold(service.Inventory.Gold);

        // 商店格
        var products = service.Shop.Products;
        for (int i = 0; i < panel.ShopSlotCount; i++)
        {
            if (i < products.Count)
            {
                ItemSO item = products[i];
                panel.ShowShopSlot(i, item.icon, service.Shop.StockOf(item), item.buyPrice, i == selectedShop);
            }
            else
            {
                panel.ShowShopSlot(i, null, 0, 0, false);
            }
        }

        // 背包格
        for (int i = 0; i < panel.BagSlotCount; i++)
        {
            ItemStack stack = service.Inventory.GetSlot(i);
            panel.ShowBagSlot(i, stack.IsEmpty ? null : stack.item.icon, stack.count, i == selectedBag);
        }

        RefreshDetail();
    }

    private void RefreshDetail()
    {
        ItemSO item = SelectedItem();
        bool valid = item != null;

        int maxQuantity = MaxQuantity();
        quantity = Mathf.Clamp(quantity, 1, Mathf.Max(1, maxQuantity));

        string priceText = "";
        if (valid)
        {
            int unit = selectedShop >= 0 ? item.buyPrice : item.sellPrice;
            priceText = "单价 " + unit + " 金币";
        }

        panel.ShowDetail(
            valid ? item.icon : null,
            valid ? item.displayName : null,
            valid ? item.description : "左边是商店，右边是背包。点选商品后点「购买」，点选背包物品后点「出售」或「使用」。",
            priceText);

        panel.ShowQuantity(quantity);
        panel.SetButtons(
            canBuy: selectedShop >= 0 && valid,
            canSell: selectedBag >= 0 && valid,
            canUse: selectedBag >= 0 && valid && item.IsUsable,
            canMinus: quantity > 1,
            canPlus: quantity < maxQuantity);
    }

    private ItemSO SelectedItem()
    {
        if (service == null) return null;

        if (selectedShop >= 0 && selectedShop < service.Shop.Products.Count)
            return service.Shop.Products[selectedShop];

        if (selectedBag >= 0)
        {
            ItemStack stack = service.Inventory.GetSlot(selectedBag);
            return stack.IsEmpty ? null : stack.item;
        }

        return null;
    }

    private int MaxQuantity()
    {
        if (service == null) return 1;

        if (selectedShop >= 0 && selectedShop < service.Shop.Products.Count)
        {
            ItemSO item = service.Shop.Products[selectedShop];
            int stock = service.Shop.StockOf(item);
            if (stock < 0) return 99;
            return Mathf.Max(1, Mathf.Min(stock, 99));
        }

        if (selectedBag >= 0)
        {
            ItemStack stack = service.Inventory.GetSlot(selectedBag);
            return Mathf.Max(1, stack.count);
        }

        return 1;
    }

    // ==================== 输入处理 ====================

    private void OnBagSlotClicked(int index)
    {
        selectedBag = index;
        selectedShop = -1;
        quantity = 1;
        Refresh();
    }

    private void OnShopSlotClicked(int index)
    {
        selectedShop = index;
        selectedBag = -1;
        quantity = 1;
        Refresh();
    }

    private void OnMinus()
    {
        quantity = Mathf.Max(1, quantity - 1);
        Refresh();
    }

    private void OnPlus()
    {
        quantity = Mathf.Min(MaxQuantity(), quantity + 1);
        Refresh();
    }

    private void OnBuy()
    {
        if (service == null || selectedShop < 0) return;

        ItemSO item = service.Shop.Products[selectedShop];
        TradeResult result = service.TryBuy(item, quantity);
        panel.ShowToast(DescribeTrade(result, item, quantity));
        Refresh();
    }

    private void OnSell()
    {
        if (service == null || selectedBag < 0) return;

        ItemStack stack = service.Inventory.GetSlot(selectedBag);
        string name = stack.IsEmpty ? "物品" : stack.item.displayName;

        TradeResult result = service.TrySell(selectedBag, quantity);
        if (result == TradeResult.Success)
            panel.ShowToast("已出售 " + name + " ×" + quantity);
        else
            panel.ShowToast(ShopService.Describe(result));

        Refresh();
    }

    private void OnUse()
    {
        if (service == null || selectedBag < 0) return;

        CharacterStats stats = PlayerProgress.instance != null ? PlayerProgress.instance.stats : null;
        int before = stats != null ? stats.CurHealth : 0;

        bool used = service.TryUse(selectedBag);
        if (!used)
        {
            panel.ShowToast(stats != null && stats.CurHealth >= stats.MaxHealth ? "生命值已满" : "无法使用");
        }
        else
        {
            int healed = stats != null ? stats.CurHealth - before : 0;
            panel.ShowToast("回复 " + healed + " 点生命值");

            // 用完了就把选择清掉
            if (service.Inventory.GetSlot(selectedBag).IsEmpty) selectedBag = -1;
        }

        Refresh();
    }

    private static string DescribeTrade(TradeResult result, ItemSO item, int quantity)
    {
        if (result == TradeResult.Success)
            return "已购买 " + (item != null ? item.displayName : "物品") + " ×" + quantity;

        return ShopService.Describe(result);
    }
}
