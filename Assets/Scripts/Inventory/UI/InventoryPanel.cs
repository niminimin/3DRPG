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
/// 谁买了东西、失败原因是什么，全由 Presenter（InventoryUI）决定。
/// </summary>
public class InventoryPanel : MonoBehaviour
{
    // ==================== 一格的引用 ====================
    private class Slot
    {
        public Button button;
        public Image background;
        public Image icon;
        public Text count;
        public Text price;      // 商店格用
    }

    private const int BagColumns = 6;
    private const int BagRows = 4;
    private const int ShopColumns = 4;
    private const int ShopRows = 2;

    private CanvasGroup group;
    private Text goldLabel;
    private Text detailName;
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
        canvas.sortingOrder = 25000;          // 在 HUD 之上、暂停菜单(31000)之下

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGO.AddComponent<CanvasGroup>();

        // 半透明背景（挡住后面的世界，也挡住点击）
        Image dim = CreateImage(canvasGO.transform, "Dim", new Vector2(0f, 0f), Vector2.zero, Color.black);
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.78f);

        // 主窗口
        Image window = CreateImage(canvasGO.transform, "Window", new Vector2(1560f, 880f), Vector2.zero,
            new Color(0.11f, 0.12f, 0.15f, 0.98f));

        Transform root = window.transform;

        // 标题 + 金币
        Text title = CreateText(root, "背包与商店", 44, new Vector2(-660f, 390f), new Vector2(400f, 60f));
        title.alignment = TextAnchor.MiddleLeft;
        title.fontStyle = FontStyle.Bold;

        goldLabel = CreateText(root, "金币 0", 32, new Vector2(520f, 390f), new Vector2(360f, 50f));
        goldLabel.alignment = TextAnchor.MiddleRight;
        goldLabel.color = new Color(1f, 0.85f, 0.35f);

        // 左：商店
        Text shopTitle = CreateText(root, "商店", 32, new Vector2(-620f, 320f), new Vector2(200f, 44f));
        shopTitle.alignment = TextAnchor.MiddleLeft;
        shopTitle.color = new Color(0.55f, 0.85f, 0.95f);

        BuildGrid(root, shopSlots, ShopColumns, ShopRows, new Vector2(-640f, 240f), true);

        // 右：背包
        Text bagTitle = CreateText(root, "背包", 32, new Vector2(40f, 320f), new Vector2(200f, 44f));
        bagTitle.alignment = TextAnchor.MiddleLeft;
        bagTitle.color = new Color(0.78f, 0.72f, 0.95f);

        BuildGrid(root, bagSlots, BagColumns, BagRows, new Vector2(60f, 240f), false);

        // 底部详情
        Image detailBox = CreateImage(root, "Detail", new Vector2(1480f, 190f), new Vector2(0f, -330f),
            new Color(0.16f, 0.18f, 0.22f, 1f));

        detailIcon = CreateImage(detailBox.transform, "Icon", new Vector2(110f, 110f), new Vector2(-650f, 10f),
            Color.white);
        detailIcon.preserveAspect = true;

        detailName = CreateText(detailBox.transform, "选择一件物品", 34, new Vector2(-330f, 55f),
            new Vector2(560f, 50f));
        detailName.alignment = TextAnchor.MiddleLeft;

        detailDescription = CreateText(detailBox.transform, "", 24, new Vector2(-140f, -10f),
            new Vector2(940f, 80f));
        detailDescription.alignment = TextAnchor.UpperLeft;
        detailDescription.color = new Color(0.75f, 0.78f, 0.82f);

        // 操作按钮
        buyButton = CreateButton(root, "购买", new Vector2(-520f, -190f), new Vector2(200f, 64f),
            new Color(0.25f, 0.62f, 0.7f), () => { if (BuyClicked != null) BuyClicked(); });
        sellButton = CreateButton(root, "出售", new Vector2(-290f, -190f), new Vector2(200f, 64f),
            new Color(0.5f, 0.42f, 0.75f), () => { if (SellClicked != null) SellClicked(); });
        useButton = CreateButton(root, "使用", new Vector2(-60f, -190f), new Vector2(200f, 64f),
            new Color(0.32f, 0.62f, 0.36f), () => { if (UseClicked != null) UseClicked(); });

        // 数量
        minusButton = CreateButton(root, "-", new Vector2(220f, -190f), new Vector2(64f, 64f),
            new Color(0.22f, 0.24f, 0.28f), () => { if (MinusClicked != null) MinusClicked(); });
        quantityLabel = CreateText(root, "1", 34, new Vector2(300f, -190f), new Vector2(90f, 50f));
        plusButton = CreateButton(root, "+", new Vector2(380f, -190f), new Vector2(64f, 64f),
            new Color(0.22f, 0.24f, 0.28f), () => { if (PlusClicked != null) PlusClicked(); });

        // 关闭
        CreateButton(root, "关闭 (I)", new Vector2(600f, -190f), new Vector2(200f, 64f),
            new Color(0.35f, 0.2f, 0.2f), () => { if (CloseClicked != null) CloseClicked(); });

        // 提示条
        Image toastBg = CreateImage(root, "Toast", new Vector2(700f, 60f), new Vector2(0f, 340f),
            new Color(0f, 0f, 0f, 0.85f));
        toastRoot = toastBg.gameObject;
        toastLabel = CreateText(toastBg.transform, "", 28, Vector2.zero, new Vector2(660f, 50f));
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
                Vector2 pos = start + new Vector2(c * 130f, -r * 130f);
                Slot slot = CreateSlot(parent, new Vector2(120f, 120f), pos, isShop);

                slot.button.onClick.AddListener(() => {
                    if (isShop) { if (ShopSlotClicked != null) ShopSlotClicked(index); }
                    else { if (BagSlotClicked != null) BagSlotClicked(index); }
                });

                slots.Add(slot);
            }
        }
    }

    private Slot CreateSlot(Transform parent, Vector2 size, Vector2 pos, bool isShop)
    {
        var slot = new Slot();

        GameObject go = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        slot.background = go.GetComponent<Image>();
        slot.background.color = isShop
            ? new Color(0.16f, 0.22f, 0.26f, 1f)
            : new Color(0.18f, 0.18f, 0.24f, 1f);

        slot.button = go.GetComponent<Button>();
        slot.button.targetGraphic = slot.background;

        slot.icon = CreateImage(go.transform, "Icon", new Vector2(84f, 84f), new Vector2(0f, 6f), Color.white);
        slot.icon.preserveAspect = true;
        slot.icon.enabled = false;

        slot.count = CreateText(go.transform, "", 24, new Vector2(0f, -38f), new Vector2(100f, 30f));
        slot.count.alignment = TextAnchor.MiddleRight;
        slot.count.color = Color.white;

        slot.price = CreateText(go.transform, "", 20, new Vector2(0f, 42f), new Vector2(110f, 24f));
        slot.price.alignment = TextAnchor.MiddleCenter;
        slot.price.color = new Color(1f, 0.85f, 0.4f);

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
        slot.price.text = hasItem ? price + " 金" : "";
        slot.count.text = hasItem ? (stock < 0 ? "∞" : "×" + stock) : "";
        slot.count.color = hasItem && stock == 0 ? new Color(1f, 0.4f, 0.4f) : Color.white;
        slot.background.color = selected
            ? new Color(0.25f, 0.45f, 0.5f, 1f)
            : new Color(0.16f, 0.22f, 0.26f, 1f);
    }

    public void ShowBagSlot(int index, Sprite icon, int count, bool selected)
    {
        if (index < 0 || index >= bagSlots.Count) return;

        Slot slot = bagSlots[index];
        bool hasItem = icon != null;

        slot.icon.enabled = hasItem;
        slot.icon.sprite = icon;
        slot.count.text = hasItem && count > 1 ? "×" + count : "";
        slot.price.text = "";
        slot.background.color = selected
            ? new Color(0.3f, 0.28f, 0.45f, 1f)
            : new Color(0.18f, 0.18f, 0.24f, 1f);
    }

    public void ShowDetail(Sprite icon, string name, string description, string priceText)
    {
        detailIcon.enabled = icon != null;
        detailIcon.sprite = icon;
        detailName.text = string.IsNullOrEmpty(name) ? "选择一件物品" : name;
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

        CreateText(go.transform, label, 28, Vector2.zero, size);
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
