using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss 专用血条（屏幕底部的大条）：
/// - 玩家靠近 Boss 时淡入，离开时淡出
/// - 两层 Image：红条立刻变化，橙色延迟条慢慢追
/// - Boss 死亡后延迟一会儿再淡出
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    [Header("目标")]
    public CharacterStats target;
    public Transform player;

    [Header("显示条件")]
    [Tooltip("玩家进入这个距离内才显示血条")]
    public float showDistance = 25f;

    [Tooltip("淡入淡出速度")]
    public float fadeSpeed = 2f;

    [Header("显示")]
    public Image fillImage;
    public Image delayedFillImage;
    public Text nameText;
    public Text valueText;

    [Tooltip("Boss 名字；留空则用 target 的物体名")]
    public string bossName = "";

    [Header("表现")]
    [Tooltip("延迟条每秒追赶的比例（0.4 = 每秒补 40%）")]
    public float delayedSpeed = 0.4f;

    [Tooltip("Boss 死亡后多久淡出")]
    public float hideDelayOnDeath = 2.5f;

    private CanvasGroup group;
    private float targetRatio = 1f;
    private float displayed = 1f;
    private bool dead;
    private float deadTimer;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        if (fillImage != null) fillImage.type = Image.Type.Filled;
        if (delayedFillImage != null) delayedFillImage.type = Image.Type.Filled;

        if (target == null)
            Debug.LogWarning("[" + name + "] 没有设置 Boss 的 CharacterStats，血条不会显示", this);
    }

    private void OnEnable()
    {
        if (target == null) return;

        target.OnHealthChanged += Refresh;
        target.OnDead += HandleDead;

        if (nameText != null) nameText.text = string.IsNullOrEmpty(bossName) ? target.name : bossName;

        SyncImmediate(target.CurHealth);
    }

    /// <summary>
    /// 世界场景是叠加加载/卸载的，Boss 会被销毁重建。
    /// 所以每帧检查：目标没了就重新找一个带 BossMarker 的。
    /// </summary>
    private void UpdateTargetIfNeeded()
    {
        // 玩家常驻在 Boot，引用可能没配；没配就找一次
        if (player == null)
        {
            PlayerController pc = FindObjectOfType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        if (target != null) return;

        BossMarker marker = FindObjectOfType<BossMarker>();
        if (marker == null || marker.stats == null) return;

        BindTo(marker.stats, marker.displayName);
    }

    private void BindTo(CharacterStats stats, string displayName)
    {
        if (stats == null) return;

        if (target != null)
        {
            target.OnHealthChanged -= Refresh;
            target.OnDead -= HandleDead;
        }

        target = stats;
        target.OnHealthChanged += Refresh;
        target.OnDead += HandleDead;

        dead = false;
        deadTimer = 0f;

        if (nameText != null)
            nameText.text = !string.IsNullOrEmpty(displayName)
                ? displayName
                : (!string.IsNullOrEmpty(bossName) ? bossName : stats.name);

        SyncImmediate(stats.CurHealth);
    }

    private void OnDisable()
    {
        if (target == null) return;

        target.OnHealthChanged -= Refresh;
        target.OnDead -= HandleDead;
    }

    /// <summary>Start 在所有 Awake 之后，重新同步一次（OnEnable 可能读到 0 血）。</summary>
    private void Start()
    {
        if (target == null) return;

        SyncImmediate(target.CurHealth);

        // 如果玩家一开始就在范围内，直接显示（不用等第一次掉血）
        if (player != null &&
            Vector3.Distance(target.transform.position, player.position) <= showDistance)
        {
            if (group != null) group.alpha = 1f;
        }
    }

    private void Update()
    {
        if (group == null) return;

        // 世界重新加载后 Boss 是新的实例，重绑一次
        UpdateTargetIfNeeded();

        // 死亡后计时淡出
        if (dead)
        {
            deadTimer += Time.deltaTime;
            if (deadTimer >= hideDelayOnDeath)
                group.alpha = Mathf.MoveTowards(group.alpha, 0f, fadeSpeed * Time.deltaTime);
            else
                group.alpha = Mathf.MoveTowards(group.alpha, 1f, fadeSpeed * Time.deltaTime);

            UpdateDelayed();
            return;
        }

        // 靠近才显示
        bool near = target != null && player != null && target.MaxHealth > 0
                    && Vector3.Distance(target.transform.position, player.position) <= showDistance;

        group.alpha = Mathf.MoveTowards(group.alpha, near ? 1f : 0f, fadeSpeed * Time.deltaTime);

        UpdateDelayed();
    }

    private void UpdateDelayed()
    {
        if (delayedFillImage == null) return;

        if (displayed > targetRatio)
        {
            displayed = Mathf.MoveTowards(displayed, targetRatio, delayedSpeed * Time.deltaTime);
            delayedFillImage.fillAmount = displayed;
        }
    }

    private void Refresh(int current)
    {
        if (target == null) return;

        targetRatio = target.MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)current / target.MaxHealth);

        if (fillImage != null) fillImage.fillAmount = targetRatio;

        // 回血：延迟条直接跟上
        if (targetRatio > displayed)
        {
            displayed = targetRatio;
            if (delayedFillImage != null) delayedFillImage.fillAmount = displayed;
        }

        if (valueText != null)
            valueText.text = current + " / " + target.MaxHealth;

        // 第一次挨打时立刻显示
        if (group != null && group.alpha <= 0.01f && !dead)
            group.alpha = 1f;
    }

    private void HandleDead()
    {
        dead = true;
        deadTimer = 0f;

        targetRatio = 0f;
        displayed = 0f;

        if (fillImage != null) fillImage.fillAmount = 0f;
        if (delayedFillImage != null) delayedFillImage.fillAmount = 0f;
        if (valueText != null && target != null) valueText.text = "0 / " + target.MaxHealth;
    }

    private void SyncImmediate(int current)
    {
        if (target == null) return;

        targetRatio = target.MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)current / target.MaxHealth);
        displayed = targetRatio;

        if (fillImage != null) fillImage.fillAmount = targetRatio;
        if (delayedFillImage != null) delayedFillImage.fillAmount = targetRatio;

        if (valueText != null)
            valueText.text = current + " / " + target.MaxHealth;
    }
}
