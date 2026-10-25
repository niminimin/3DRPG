using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 跨场景传送 + 黑屏淡入淡出。
/// 挂在场景里任意物体上（建议单独一个 SceneFader 物体），会 DontDestroyOnLoad 跟着走。
/// </summary>
public class SceneFader : Singleton<SceneFader>
{
    protected override bool Persistent { get { return true; } }

    [Header("淡入淡出")]
    public float fadeOutTime = 0.35f;
    public float fadeInTime = 0.35f;

    [Header("颜色")]
    public Color fadeColor = Color.black;

    /// <summary>正在传送中（传送门用它防止重复触发）。</summary>
    public bool IsBusy { get; private set; }

    private CanvasGroup group;

    protected override void Awake()
    {
        base.Awake();
        BuildUI();
        SetAlpha(0f);
    }

    /// <summary>代码里自动建一个全屏遮罩，不用手动摆 UI。</summary>
    private void BuildUI()
    {
        if (group != null) return;

        GameObject canvasGO = new GameObject("FadeCanvas", typeof(RectTransform), typeof(Canvas));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;          // 盖在所有 UI 上面

        CanvasGroup cg = canvasGO.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        group = cg;

        GameObject imgGO = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        imgGO.transform.SetParent(canvasGO.transform, false);

        Image img = imgGO.GetComponent<Image>();
        img.color = fadeColor;
        img.raycastTarget = false;

        RectTransform rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void SetAlpha(float a)
    {
        if (group != null) group.alpha = a;
    }

    /// <summary>传送到目标场景，并落到 entryId 对应的落点。</summary>
    public void Teleport(string sceneName, string entryId)
    {
        if (IsBusy) return;
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SceneFader] 没有配置目标场景", this);
            return;
        }

        StartCoroutine(TeleportRoutine(sceneName, entryId));
    }

    private IEnumerator TeleportRoutine(string sceneName, string entryId)
    {
        IsBusy = true;

        yield return FadeTo(1f, fadeOutTime);

        // 保存玩家进度（等级、经验、属性加成）
        PlayerSession.Capture();

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError("[SceneFader] 场景加载失败：" + sceneName + "（检查 Build Settings）", this);
            yield return FadeTo(0f, fadeInTime);
            IsBusy = false;
            yield break;
        }

        op.allowSceneActivation = false;

        while (op.progress < 0.9f) yield return null;

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // 等新场景的 Awake/Start 跑完
        yield return null;

        PlacePlayer(entryId);
        PlayerSession.Restore();

        yield return FadeTo(0f, fadeInTime);

        IsBusy = false;
    }

    /// <summary>把新场景里的玩家放到落点上。</summary>
    private void PlacePlayer(string entryId)
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[SceneFader] 新场景里没有 Player", this);
            return;
        }

        PortalEntry entry = PortalEntry.Find(entryId);

        if (entry == null)
        {
            Debug.LogWarning("[SceneFader] 新场景里没有 id=" + entryId + " 的 PortalEntry，玩家留在原地", this);
            return;
        }

        Vector3 pos = entry.transform.position;
        Quaternion rot = entry.faceForward ? entry.transform.rotation : player.transform.rotation;

        player.TeleportTo(pos, rot);
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        float start = group != null ? group.alpha : 0f;
        float t = 0f;

        if (duration <= 0f)
        {
            SetAlpha(target);
            yield break;
        }

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(start, target, t / duration));
            yield return null;
        }

        SetAlpha(target);
    }
}
