using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全屏黑幕：只负责淡入淡出（常驻在 Boot 场景）。
/// 真正的加载流程在 WorldLoader 里。
/// </summary>
public class SceneFader : Singleton<SceneFader>
{
    [Header("淡入淡出")]
    public float fadeOutTime = 0.35f;
    public float fadeInTime = 0.35f;

    [Header("颜色")]
    public Color fadeColor = Color.black;

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

    /// <summary>淡到全黑。</summary>
    public IEnumerator FadeOut()
    {
        yield return FadeTo(1f, fadeOutTime);
    }

    /// <summary>从全黑淡回来。</summary>
    public IEnumerator FadeIn()
    {
        yield return FadeTo(0f, fadeInTime);
    }

    /// <summary>直接透明（首帧用）。</summary>
    public void Clear()
    {
        SetAlpha(0f);
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
