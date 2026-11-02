using UnityEngine;

/// <summary>
/// 传送门：玩家走进范围内就传送到目标场景的指定位置。
///
/// 设计要点（参考 2DAdventure 的 TeleportPoint）：
///   "落到哪里"由**传送门自己**携带（positionToGo / yawToGo），
///   加载器只负责加载场景 + 把玩家搬到这个坐标，不去场景里找标记。
///
/// 用距离检测而不是 OnTriggerEnter：玩家是 NavMeshAgent + Transform 移动、没有 Rigidbody，
/// Trigger 事件不会触发。
/// </summary>
public class Portal : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("目标场景名（要和 Build Settings 里的名字一致）")]
    public string targetScene = "";

    [Tooltip("到达目标场景后的位置（目标场景的世界坐标）")]
    public Vector3 positionToGo = Vector3.zero;

    [Tooltip("到达后的朝向（Y 轴角度，180 = 面向 -Z）")]
    public float yawToGo = 180f;

    [Header("触发")]
    [Tooltip("玩家进入这个半径内就传送")]
    public float activateRadius = 2.5f;

    [Tooltip("触发后的冷却，防止站门口连续传送")]
    public float cooldown = 2f;

    [Header("显示")]
    public string portalName = "传送门";

    private float readyTime;
    private Transform player;

    private void Start()
    {
        // 开局先给一倍冷却，避免刚落地就被传送走
        readyTime = Time.time + cooldown;
    }

    private void Update()
    {
        if (WorldLoader.instance == null || WorldLoader.instance.IsBusy) return;
        if (Time.time < readyTime) return;
        if (string.IsNullOrEmpty(targetScene)) return;

        if (player == null)
        {
            PlayerController pc = FindObjectOfType<PlayerController>();
            if (pc == null) return;
            player = pc.transform;
        }

        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = player.position; b.y = 0f;

        if (Vector3.Distance(a, b) <= activateRadius)
        {
            readyTime = Time.time + cooldown;
            WorldLoader.instance.LoadWorld(targetScene, positionToGo, yawToGo);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
        Vector3 p = transform.position; p.y = 0f;
        Gizmos.DrawWireSphere(p, activateRadius);

        // 到达点
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(positionToGo + Vector3.up * 0.5f, 0.5f);
        Gizmos.DrawRay(positionToGo + Vector3.up * 0.5f, Quaternion.Euler(0f, yawToGo, 0f) * Vector3.forward * 2f);
    }
}
