using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ESC 暂停菜单（常驻 Boot）：继续 / 保存 / 读取 / 新游戏 / 退出。
/// UI 在代码里生成，不需要手动摆。
/// </summary>
public class PauseMenu : Singleton<PauseMenu>
{
    private CanvasGroup group;
    private Text titleText;
    private bool isOpen;

    private void Start()
    {
        BuildUI();
        SetOpen(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isOpen) Resume();
            else Open();
        }
    }

    // ==================== 开关 ====================
    public void Open()
    {
        SetOpen(true);

        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 清掉游戏里设置的自定义光标（否则暂停后还显示"移动/攻击"图标）
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        if (titleText != null) titleText.text = "暂停";
    }

    public void Resume()
    {
        SetOpen(false);
        Time.timeScale = 1f;
        Cursor.visible = true;

        // 恢复后交给 MouseManager 下一帧重新设置光标
    }

    private void SetOpen(bool open)
    {
        isOpen = open;

        if (group == null) return;
        group.alpha = open ? 1f : 0f;
        group.blocksRaycasts = open;
        group.interactable = open;
    }

    // ==================== 按钮行为 ====================
    private void OnSave()
    {
        if (SaveManager.instance == null) return;

        bool ok = SaveManager.instance.Save();
        if (titleText != null) titleText.text = ok ? "已保存" : "保存失败";
    }

    private void OnLoad()
    {
        if (SaveManager.instance == null) return;

        if (!SaveManager.instance.HasSaveFile)
        {
            if (titleText != null) titleText.text = "没有存档";
            return;
        }

        Time.timeScale = 1f;
        SetOpen(false);

        if (titleText != null) titleText.text = "暂停";
        SaveManager.instance.Load();
    }

    private void OnNewGame()
    {
        if (SaveManager.instance == null) return;

        Time.timeScale = 1f;
        SetOpen(false);

        if (titleText != null) titleText.text = "暂停";
        SaveManager.instance.NewGame();
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>回主菜单：先把时间恢复，再淡出切场景。</summary>
    private void OnMainMenu()
    {
        Time.timeScale = 1f;
        SetOpen(false);
        StartCoroutine(BackToMenuRoutine());
    }

    private System.Collections.IEnumerator BackToMenuRoutine()
    {
        if (SceneFader.instance != null)
            yield return SceneFader.instance.FadeOut();

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            "MainMenu", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    // ==================== UI 生成 ====================
    private void BuildUI()
    {
        EnsureEventSystem();

        // 根 Canvas
        // 注意：必须有 GraphicRaycaster，否则按钮收不到鼠标事件（点不动）
        GameObject canvasGO = new GameObject("PauseCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;

        group = canvasGO.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // 半透明黑底
        GameObject dimGO = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimGO.transform.SetParent(canvasGO.transform, false);
        Image dim = dimGO.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.7f);
        dim.raycastTarget = true;
        Stretch(dimGO.GetComponent<RectTransform>());

        // 面板
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGO.transform, false);
        Image panelImg = panel.GetComponent<Image>();
        panelImg.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(420f, 540f);
        panelRT.anchoredPosition = Vector2.zero;

        // 标题
        titleText = CreateText(panel.transform, "暂停", 34, new Vector2(0f, 210f), new Vector2(360f, 50f));
        titleText.fontStyle = FontStyle.Bold;

        // 按钮
        CreateButton(panel.transform, "继续", 130f, Resume);
        CreateButton(panel.transform, "保存", 62f, OnSave);
        CreateButton(panel.transform, "读取", -6f, OnLoad);
        CreateButton(panel.transform, "新游戏", -74f, OnNewGame);
        CreateButton(panel.transform, "回主菜单", -142f, OnMainMenu);
        CreateButton(panel.transform, "退出游戏", -210f, OnQuit);
    }

    private void CreateButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image img = btnGO.GetComponent<Image>();
        img.color = new Color(0.25f, 0.45f, 0.85f, 1f);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(300f, 52f);
        rt.anchoredPosition = new Vector2(0f, y);

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        Text t = CreateText(btnGO.transform, label, 24, Vector2.zero, new Vector2(300f, 52f));
        t.alignment = TextAnchor.MiddleCenter;
    }

    private Text CreateText(Transform parent, string content, int size, Vector2 pos, Vector2 sizeDelta)
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
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = pos;

        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>按钮需要 EventSystem，没有就建一个（Boot 常驻，不需要 DontDestroyOnLoad）。</summary>
    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
