using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主菜单：标题 + 新游戏 / 继续 / 退出。
/// UI 在代码里生成（和暂停菜单同一套做法）。
/// 有存档时点"新游戏"会弹确认框。
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("显示")]
    public string title = "3D RPG";
    public string subTitle = "M_Studio Style Demo";

    [Header("目标场景")]
    public string bootSceneName = "Boot";

    private GameObject confirmPanel;
    private Text continueButtonLabel;
    private Button continueButton;
    private bool isLoading;

    private void Start()
    {
        BuildUI();
        RefreshContinueButton();
    }

    /// <summary>"继续"按钮：没有存档就灰掉。</summary>
    private void RefreshContinueButton()
    {
        bool hasSave = SaveManager.instance != null && SaveManager.instance.HasSaveFile;
        // SaveManager 在 Boot 里，主菜单场景没有 → 直接看文件
        if (SaveManager.instance == null)
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "save_0.json");
            hasSave = System.IO.File.Exists(path);
        }

        if (continueButton != null) continueButton.interactable = hasSave;

        if (continueButtonLabel != null)
            continueButtonLabel.color = hasSave
                ? Color.white
                : new Color(1f, 1f, 1f, 0.35f);
    }

    // ==================== 按钮 ====================
    private void OnNewGame()
    {
        if (isLoading) return;

        // 有存档 → 弹确认框
        if (HasSaveFile())
        {
            confirmPanel.SetActive(true);
            return;
        }

        StartGame(LaunchContext.LaunchMode.NewGame);
    }

    private void OnConfirmNewGame()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        StartGame(LaunchContext.LaunchMode.NewGame);
    }

    private void OnCancelConfirm()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    private void OnContinue()
    {
        if (isLoading) return;
        if (!HasSaveFile()) return;

        StartGame(LaunchContext.LaunchMode.Continue);
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private bool HasSaveFile()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "save_0.json");
        return System.IO.File.Exists(path);
    }

    private void StartGame(LaunchContext.LaunchMode mode)
    {
        if (isLoading) return;
        isLoading = true;

        LaunchContext.Mode = mode;
        StartCoroutine(LoadBootRoutine());
    }

    private IEnumerator LoadBootRoutine()
    {
        Time.timeScale = 1f;

        if (SceneFader.instance != null)
            yield return SceneFader.instance.FadeOut();

        SceneManager.LoadScene(bootSceneName, LoadSceneMode.Single);
    }

    // ==================== UI ====================
    private void BuildUI()
    {
        EnsureEventSystem();

        GameObject canvasGO = new GameObject("MenuCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 标题
        Text titleText = CreateText(canvasGO.transform, title, 96, new Vector2(0f, 300f), new Vector2(1200f, 140f));
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(1f, 0.87f, 0.2f);

        Text subText = CreateText(canvasGO.transform, subTitle, 28, new Vector2(0f, 215f), new Vector2(900f, 40f));
        subText.color = new Color(1f, 1f, 1f, 0.6f);

        // 按钮
        CreateButton(canvasGO.transform, "新游戏", 40f, OnNewGame, out _);

        continueButton = CreateButton(canvasGO.transform, "继续", -40f, OnContinue, out continueButtonLabel);

        CreateButton(canvasGO.transform, "退出", -120f, OnQuit, out _);

        // 版本号
        Text version = CreateText(canvasGO.transform, "v0.1", 22, new Vector2(0f, -430f), new Vector2(400f, 30f));
        version.color = new Color(1f, 1f, 1f, 0.45f);

        // 确认框
        BuildConfirmPanel(canvasGO.transform);
    }

    private void BuildConfirmPanel(Transform parent)
    {
        GameObject dim = new GameObject("ConfirmDim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(parent, false);
        Image dimImg = dim.GetComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.75f);
        Stretch(dim.GetComponent<RectTransform>());
        confirmPanel = dim;

        GameObject panel = new GameObject("ConfirmPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(dim.transform, false);
        Image panelImg = panel.GetComponent<Image>();
        panelImg.color = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(720f, 300f);

        Text msg = CreateText(panel.transform, "已有存档，开始新游戏会覆盖它。\n确定要重新开始吗？", 32,
            new Vector2(0f, 60f), new Vector2(640f, 120f));

        CreateButton(panel.transform, "确定", -70f, OnConfirmNewGame, out _, new Vector2(260f, 64f), new Vector2(-90f, -70f));
        CreateButton(panel.transform, "取消", -70f, OnCancelConfirm, out _, new Vector2(260f, 64f), new Vector2(90f, -70f));

        confirmPanel.SetActive(false);
    }

    private Button CreateButton(Transform parent, string label, float y,
                                UnityEngine.Events.UnityAction action, out Text labelText,
                                Vector2? size = null, Vector2? offset = null)
    {
        GameObject btnGO = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image img = btnGO.GetComponent<Image>();
        img.color = new Color(0.16f, 0.18f, 0.22f, 0.85f);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size ?? new Vector2(360f, 72f);
        rt.anchoredPosition = offset ?? new Vector2(0f, y);

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        labelText = CreateText(btnGO.transform, label, 34, Vector2.zero, rt.sizeDelta);
        return btn;
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
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
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

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
    }
}
