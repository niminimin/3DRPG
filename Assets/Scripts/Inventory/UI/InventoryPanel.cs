using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包 + 商店界面（View 层）。
///
/// 职责只有两件：
///   1. 把数据画出来（ShowXxx 方法）
///   2. 把玩家操作喊出去（OnXxxClicked 事件）
///
/// 它**不认识** Inventory / Shop / InventoryService —— 一行都没引用。
///
/// 布局（参考分辨率 1920x1080，窗口 1560x880）：
///   y = +385  标题 / 金币
///   y = +300  列标题（商店 / 背包）
///   y = +220  第一排格子（格子 90，间距 104）
///   y = -205  操作按钮
///   y = -335  详情框（1440x190）
/// </summary>
public class InventoryPanel : MonoBehaviour
{
    // ==================== 布局常量（改这里就能整体调） ====================
    private const float SlotSize = 90f;
    private const float SlotStep = 104f;
    private const float GridTop = 220f;
    private const int BagColumns = 6;
    private const int BagRows = 4;
    private const int ShopColumns = 4;
    private const int ShopRows = 2;

    private class Slot
    {
        public Button button;
        public Image background;
        public Image icon;
        public Text count;
    }

    private CanvasGroup group;
    private Text goldLabel;
    private Text detailName;
    private Text detailPrice;
    private Text detailDescription;
    private Text quantityLabel;
    private Text toastLabel;
    private Image detailIcon;
    private GameObject toastRoot;
    private Button buyButton;
    private Button sellButton;
    private Button useButton;
    private Button minusButton;
    private Button plusButton;
    private float toastUntil;

    private readonly List<Slot> bagSlots = new List<Slot>();
    private readonly List<Slot> shopSlots = new List<Slot>();

    public int BagSlotCount { get { return bagSlots.Count; } }
    public int ShopSlotCount { get { return shopSlots.Count; } }

    public bool IsVisible { get { return group != null && group.alpha > 0.5f; } }

    // ==================== 交给 Presenter 的事件 ====================
    public event Action<int> BagSlotClicked;
    public event Action<int> ShopSlotClicked;
    public event Action BuyClicked;
    public event Action SellClicked;
    public event Action UseClicked;
    public event Action CloseClicked;
    public event Action MinusClicked;
    public event Action PlusClicked;

    // ==================== 建界面 ====================

    private void Awake()
    {
        BuildUI();
        SetVisible(false);
    }

    private void Update()
    {
        if (toastRoot != null && toastRoot.activeSelf && Time.unscaledTime >= toastUntil)
            toastRoot.SetActive(false);
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        GameObject canvasGO = new GameObject("InventoryCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25000;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGO.AddComponent<CanvasGroup>();

        // 背景遮罩
        Image dim = CreateImage(canvasGO.transform, "Dim", Vector2.zero, Color.black);
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.78f);

        // 主窗口
        Image window = CreateImage(canvasGO.transform, "Window", new Vector2(1560f, 880f), Vector2.zero,
            new Color(0.11f, 0.12f, 0.15f, 0.98f));
        Transform root = window.transform;

        // ---------- 顶栏 ----------
        Text title = CreateText(root, "背包与商店", 42, new Vector2(-550f, 385f), new Vector2(420f, 56f));
        title.alignment = TextAnchor.MiddleLeft;
        title.fontStyle = FontStyle.Bold;

        goldLabel = CreateText(root, "金币 0", 32, new Vector2(500f, 385f), new Vector2(400f, 50f));
        goldLabel.alignment = TextAnchor.MiddleRight;
        goldLabel.color = new Color(1f, 0.85f, 0.35f);

        // ---------- 列标题 ----------
        Text shopTitle = CreateText(root, "商店", 30, new Vector2(-660f, 300f), new Vector2(200f, 40f));
        shopTitle.alignment = TextAnchor.MiddleLeft;
        shopTitle.color = new Color(0.55f, 0.85f, 0.95f);

        Text bagTitle = CreateText(root, "背包", 30, new Vector2(-25f, 300f), new Vector2(200f, 40f));
        bagTitle.alignment = TextAnchor.MiddleLeft;
        bagTitle.color = new Color(0.78f, 0.72f, 0.95f);

        // ---------- 格子 ----------
        BuildGrid(root, shopSlots, ShopColumns, ShopRows, new Vector2(-620f, GridTop), true);
        BuildGrid(root, bagSlots, BagColumns, BagRows, new Vector2(-25f, GridTop), false);

        // ---------- 操作行 ----------
        const float actionY = -205f;

        buyButton = CreateButton(root, "购买", new Vector2(-560f, actionY), new Vector2(180f, 56f),
            new Color(0.25f, 0.62f, 0.7f), () => { if (BuyClicked != null) BuyClicked(); });
        sellButton = CreateButton(root, "出售", new Vector2(-360f, actionY), new Vector2(180f, 56f),
            new Color(0.5f, 0.42f, 0.75f), () => { if (SellClicked != null) SellClicked(); });
        useButton = CreateButton(root, "使用", new Vector2(-160f, actionY), new Vector2(180f, 56f),
            new Color(0.32f, 0.62f, 0.36f), () => { if (UseClicked != null) UseClicked(); });

        minusButton = CreateButton(root, "－", new Vector2(90f, actionY), new Vector2(64f, 56f),
            new Color(0.22f, 0.24f, 0.28f), () => { if (MinusClicked != null) MinusClicked(); });
        quantityLabel = CreateText(root, "1", 32, new Vector2(175f, actionY), new Vector2(90f, 50f));
        plusButton = CreateButton(root, "＋", new Vector2(260f, actionY), new Vector2(64f, 56f),
            new Color(0.22f, 0.24f, 0.28f), () => { if (PlusClicked != null) PlusClicked(); });

        CreateButton(root, "关闭 (I)", new Vector2(600f, actionY), new Vector2(200f, 56f),
            new Color(0.35f, 0.2f, 0.2f), () => { if (CloseClicked != null) CloseClicked(); });

        // ---------- 详情框 ----------
        Image detailBox = CreateImage(root, "Detail", new Vector2(1440f, 190f), new Vector2(0f, -335f),
            new Color(0.16f, 0.18f, 0.22f, 1f));
        Transform detail = detailBox.transform;

        detailIcon = CreateImage(detail, "Icon", new Vector2(100f, 100f), new Vector2(-620f, 0f), Color.white);
        detailIcon.preserveAspect = true;
        detailIcon.enabled = false;

        detailName = CreateText(detail, "选择一件物品", 32, new Vector2(-300f, 55f), new Vector2(520f, 44f));
        detailName.alignment = TextAnchor.MiddleLeft;

        detailPrice = CreateText(detail, "", 24, new Vector2(-300f, 15f), new Vector2(520f, 32f));
        detailPrice.alignment = TextAnchor.MiddleLeft;
        detailPrice.color = new Color(1f, 0.85f, 0.4f);

        detailDescription = CreateText(detail, "", 24, new Vector2(230f, -10f), new Vector2(940f, 120f));
        detailDescription.alignment = TextAnchor.UpperLeft;
        detailDescription.color = new Color(0.75f, 0.78f, 0.82f);

        // ---------- 提示条 ----------
        Image toastBg = CreateImage(root, "Toast", new Vector2(480f, 56f), new Vector2(0f, 355f),
            new Color(0f, 0f, 0f, 0.88f));
        toastRoot = toastBg.gameObject;
        toastLabel = CreateText(toastBg.transform, "", 26, Vector2.zero, new Vector2(450f, 48f));
        toastRoot.SetActive(false);

        Debug.Log("[InventoryPanel] 界面已生成：背包 " + bagSlots.Count + " 格，商店 " + shopSlots.Count + " 格");
    }

    private void BuildGrid(Transform parent, List<Slot> slots, int columns, int rows,
                           Vector2 start, bool isShop)
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int index = slots.Count;
                Vector2 pos = start + new Vector2(c * SlotStep, -r * SlotStep);
                Slot slot = CreateSlot(parent, pos, isShop);

                slot.button.onClick.AddListener(() => {
                    if (isShop) { if (ShopSlotClicked != null) ShopSlotClicked(index); }
                    else { if (BagSlotClicked != null) BagSlotClicked(index); }
                });

                slots.Add(slot);
            }
        }
    }

    private Slot CreateSlot(Transform parent, Vector2 pos, bool isShop)
    {
        var slot = new Slot();

        GameObject go = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(SlotSize, SlotSize);
        rt.anchoredPosition = pos;

        slot.background = go.GetComponent<Image>();
        slot.background.color = SlotColor(isShop, false);

        slot.button = go.GetComponent<Button>();
        slot.button.targetGraphic = slot.background;

        // 图标：60，略微偏上；数量在下沿，两者不重叠
        slot.icon = CreateImage(go.transform, "Icon", new Vector2(60f, 60f), new Vector2(0f, 8f), Color.white);
        slot.icon.preserveAspect = true;
        slot.icon.enabled = false;

        slot.count = CreateText(go.transform, "", 22, new Vector2(0f, -31f), new Vector2(82f, 26f));
        slot.count.alignment = TextAnchor.MiddleCenter;
        slot.count.color = Color.white;

        return slot;
    }

    // ==================== 显示（Presenter 调用） ====================

    public void SetVisible(bool visible)
    {
        if (group == null) return;

        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;

        if (!visible && toastRoot != null) toastRoot.SetActive(false);
    }

    public void ShowGold(int gold)
    {
        if (goldLabel != null) goldLabel.text = "金币 " + gold;
    }

    public void ShowShopSlot(int index, Sprite icon, int stock, int price, bool selected)
    {
        if (index < 0 || index >= shopSlots.Count) return;

        Slot slot = shopSlots[index];
        bool hasItem = icon != null;

        slot.icon.enabled = hasItem;
        slot.icon.sprite = icon;

        if (!hasItem) slot.count.text = "";
        else if (stock < 0) slot.count.text = "∞";
        else slot.count.text = "×" + stock;

        slot.count.color = hasItem && stock == 0
            ? new Color(1f, 0.4f, 0.4f)
            : Color.white;

        slot.background.color = SlotColor(true, selected);
    }

    public void ShowBagSlot(int index, Sprite icon, int count, bool selected)
    {
        if (index < 0 || index >= bagSlots.Count) return;

        Slot slot = bagSlots[index];
        bool hasItem = icon != null;

        slot.icon.enabled = hasItem;
        slot.icon.sprite = icon;
        slot.count.text = hasItem && count > 1 ? "×" + count : "";
        slot.background.color = SlotColor(false, selected);
    }

    public void ShowDetail(Sprite icon, string name, string description, string priceText)
    {
        detailIcon.enabled = icon != null;
        detailIcon.sprite = icon;
        detailName.text = string.IsNullOrEmpty(name) ? "选择一件物品" : name;
        detailPrice.text = string.IsNullOrEmpty(priceText) ? "" : priceText;
        detailDescription.text = description;
    }

    public void ShowQuantity(int quantity)
    {
        if (quantityLabel != null) quantityLabel.text = quantity.ToString();
    }

    public void SetButtons(bool canBuy, bool canSell, bool canUse, bool canMinus, bool canPlus)
    {
        buyButton.interactable = canBuy;
        sellButton.interactable = canSell;
        useButton.interactable = canUse;
        minusButton.interactable = canMinus;
        plusButton.interactable = canPlus;
    }

    public void ShowToast(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        toastLabel.text = message;
        toastRoot.SetActive(true);
        toastUntil = Time.unscaledTime + 2.2f;
    }

    // ==================== 小工具 ====================

    private static Color SlotColor(bool isShop, bool selected)
    {
        if (isShop)
            return selected ? new Color(0.25f, 0.45f, 0.5f, 1f) : new Color(0.16f, 0.22f, 0.26f, 1f);

        return selected ? new Color(0.30f, 0.28f, 0.45f, 1f) : new Color(0.18f, 0.18f, 0.24f, 1f);
    }

    private static Image CreateImage(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Text CreateText(Transform parent, string content, int size, Vector2 pos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        Text t = go.GetComponent<Text>();
        t.text = content;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = pos;

        return t;
    }

    private static Button CreateButton(Transform parent, string label, Vector2 pos, Vector2 size,
                                       Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Image img = go.GetComponent<Image>();
        img.color = color;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        CreateText(go.transform, label, 26, Vector2.zero, size);
        return btn;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
    }
}
