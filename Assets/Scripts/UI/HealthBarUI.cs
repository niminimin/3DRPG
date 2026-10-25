using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 血条 UI（带"延迟条"效果）：
/// - fillImage       ：真实血量，立刻变化
/// - delayedFillImage：延迟条，慢慢追上去（看得见掉了多少血）
/// 订阅 CharacterStats 的 OnHealthChanged / OnDead，不用每帧轮询。
///
/// 两种用法：
///   怪物头顶：faceCamera = true，target 留空（自动找父物体上的 CharacterStats）
///   玩家 HUD：faceCamera = false，target 手动拖玩家
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Header("数据源")]
    [Tooltip("留空会自动在父物体上找 CharacterStats")]
    public CharacterStats target;

    [Header("显示")]
    [Tooltip("真实血量（Image Type 必须为 Filled）")]
    public Image fillImage;

    [Tooltip("延迟条（排在 fillImage 下面一层）")]
    public Image delayedFillImage;

    [Tooltip("可选：显示 80 / 100 这种文字")]
    public Text valueText;

    [Header("表现")]
    [Tooltip("世界空间血条：始终朝向相机")]
    public bool faceCamera = true;

    [Tooltip("死亡后隐藏整条血条")]
    public bool hideOnDead = true;

    [Tooltip("延迟条每秒追赶的比例（0.5 = 每秒补 50%）")]
    public float delayedSpeed = 0.5f;

    private Camera cam;

    private float targetRatio = 1f;     // 真实血量比例
    private float displayed = 1f;       // 延迟条当前显示的比例

    private void Awake()
    {
        if (target == null) target = GetComponentInParent<CharacterStats>();

        cam = Camera.main;

        // 保证两个 Image 都是 Filled，否则 fillAmount 不生效
        if (fillImage != null) fillImage.type = Image.Type.Filled;
        if (delayedFillImage != null) delayedFillImage.type = Image.Type.Filled;

        // 世界空间 Canvas 最好指定相机，排序更稳定
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
            canvas.worldCamera = cam;

        if (target == null)
            Debug.LogWarning("[" + name + "] 没有找到 CharacterStats，血条不会动", this);
    }

    private void OnEnable()
    {
        if (target == null) return;

        target.OnHealthChanged += Refresh;
        target.OnDead += HandleDead;

        // 打开时先同步一次（避免从满血开始慢慢掉）
        SyncImmediate(target.CurHealth);
    }

    /// <summary>
    /// Start 在所有 Awake 之后执行。
    /// OnEnable 可能比 CharacterStats.Awake 更早跑（那时 CurHealth 还是 0），
    /// 所以这里必须再同步一次，否则玩家血条会一直显示 0。
    /// </summary>
    private void Start()
    {
        if (target != null) SyncImmediate(target.CurHealth);
    }

    private void OnDisable()
    {
        if (target == null) return;

        target.OnHealthChanged -= Refresh;
        target.OnDead -= HandleDead;
    }

    private void LateUpdate()
    {
        if (!faceCamera || cam == null) return;
        transform.rotation = cam.transform.rotation;
    }

    private void Update()
    {
        if (delayedFillImage == null) return;

        // 延迟条只在"掉血"时慢慢追；回血时立刻跟上（在 Refresh 里处理）
        if (displayed > targetRatio)
        {
            displayed = Mathf.MoveTowards(displayed, targetRatio, delayedSpeed * Time.deltaTime);
            delayedFillImage.fillAmount = displayed;
        }
    }

    // ==================== 事件回调 ====================
    private void Refresh(int current)
    {
        if (target == null) return;

        targetRatio = target.MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)current / target.MaxHealth);

        if (fillImage != null) fillImage.fillAmount = targetRatio;

        // 回血：延迟条直接跟上，不要出现"绿条比特效还长"的怪现象
        if (targetRatio > displayed)
        {
            displayed = targetRatio;
            if (delayedFillImage != null) delayedFillImage.fillAmount = displayed;
        }

        if (valueText != null)
            valueText.text = current + " / " + target.MaxHealth;
    }

    private void HandleDead()
    {
        targetRatio = 0f;
        displayed = 0f;

        if (fillImage != null) fillImage.fillAmount = 0f;
        if (delayedFillImage != null) delayedFillImage.fillAmount = 0f;
        if (valueText != null) valueText.text = "0 / " + target.MaxHealth;

        if (hideOnDead) gameObject.SetActive(false);
    }

    private void SyncImmediate(int current)
    {
        targetRatio = target.MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)current / target.MaxHealth);
        displayed = targetRatio;

        if (fillImage != null) fillImage.fillAmount = targetRatio;
        if (delayedFillImage != null) delayedFillImage.fillAmount = targetRatio;

        if (valueText != null)
            valueText.text = current + " / " + target.MaxHealth;
    }
}
