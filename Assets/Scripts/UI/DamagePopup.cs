using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 受击飘字：从受伤点冒出数字，先快后慢地往上飘，后段淡出，结束自动销毁。
/// 暴击用橙色大字，普通伤害用白色。
/// 如果由 DamagePopupPool 提供，结束时回调 OnFinished 归还，而不是 Destroy。
/// </summary>
public class DamagePopup : MonoBehaviour
{
    [Header("显示")]
    public Text text;
    public CanvasGroup group;

    [Header("运动")]
    [Tooltip("总时长（秒）")]
    public float life = 0.9f;

    [Tooltip("上升的总高度（米）")]
    public float riseHeight = 1.2f;

    [Tooltip("是否始终朝向相机")]
    public bool faceCamera = true;

    /// <summary>播完时回调（由对象池设置）。为空时退化为 Destroy。</summary>
    [System.NonSerialized] public System.Action OnFinished;

    private Vector3 startPos;
    private float timer;
    private bool finished;
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
        if (group == null) group = GetComponent<CanvasGroup>();
    }

    /// <summary>由生成方调用：设置数字与样式。</summary>
    public void Setup(int amount, bool critical)
    {
        startPos = transform.position;
        timer = 0f;
        finished = false;

        if (group != null) group.alpha = 1f;

        if (text != null)
        {
            text.text = amount.ToString();

            // ★ 注意：这里不能改根节点 transform.localScale
            //   根节点承载着 World Space Canvas 的 0.01 缩放，改了会让数字大 100 倍
            //   暴击的"放大"只作用在文字本身上
            if (critical)
            {
                text.color = new Color(1f, 0.72f, 0.18f);   // 暴击：橙黄
                text.fontSize = 36;
                text.fontStyle = FontStyle.Bold;
                text.transform.localScale = Vector3.one * 1.1f;
            }
            else
            {
                text.color = Color.white;
                text.fontSize = 24;
                text.fontStyle = FontStyle.Normal;
                text.transform.localScale = Vector3.one;
            }
        }
    }

    private void LateUpdate()
    {
        if (!faceCamera || cam == null) return;
        transform.rotation = cam.transform.rotation;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float k = Mathf.Clamp01(timer / life);      // 0 → 1

        // 上升：Sqrt 让"先快后慢"，像弹出来然后停住
        transform.position = startPos + Vector3.up * (riseHeight * Mathf.Sqrt(k));

        // 后 40% 才淡出
        if (group != null)
            group.alpha = 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);

        if (timer >= life && !finished)
        {
            finished = true;                        // 防止重复归还

            if (OnFinished != null) OnFinished();    // 交给对象池回收
            else Destroy(gameObject);                // 没有池子时保持原行为
        }
    }
}
