using UnityEngine;

/// <summary>
/// 传送门：玩家走进范围就传送到目标场景的某扇门前。
///
/// 设计（参考 2DAdventure 的 TeleportPoint，但落点不存坐标）：
///   - 门只记录"去哪 + 找哪扇门"，不记录坐标
///   - 落点 = 目标门的位置 - 目标门的朝向 × standDistance（由 WorldLoader 现算）
///   - 一个场景可以有很多门，靠唯一的 id 区分
///
/// 用距离检测而不是 OnTriggerEnter：玩家是 NavMeshAgent + Transform 移动、没有 Rigidbody。
/// </summary>
public class Portal : MonoBehaviour
{
    [Header("身份")]
    [Tooltip("这扇门在本场景里的唯一名字，别的门靠它找到我")]
    public string id = "";

    [Header("目标")]
    [Tooltip("目标场景名（要和 Build Settings 里一致）")]
    public string targetScene = "";

    [Tooltip("目标场景里那扇门的 id")]
    public string targetDoorId = "";

    [Tooltip("到达后站在目标门前多远（米）")]
    public float standDistance = 2.5f;

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
            WorldLoader.instance.LoadWorld(targetScene, targetDoorId);
        }
    }

    /// <summary>到达点：门前一 standDistance 米处。</summary>
    public Vector3 GetArrivalPosition()
    {
        return transform.position - transform.forward * standDistance;
    }

    /// <summary>到达朝向：背对门（像刚从门里走出来）。</summary>
    public Quaternion GetArrivalRotation()
    {
        return Quaternion.LookRotation(-transform.forward, Vector3.up);
    }

    private void OnDrawGizmosSelected()
    {
        // 触发范围
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.4f);
        Vector3 p = transform.position; p.y = 0f;
        Gizmos.DrawWireSphere(p, activateRadius);

        // 到达点（本门作为目标门时，玩家会出现在这里）
        Vector3 arrive = GetArrivalPosition();
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(arrive + Vector3.up * 0.5f, 0.4f);
        Gizmos.DrawRay(arrive + Vector3.up * 0.5f, -transform.forward * 1.5f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f, "id: " + id);
#endif
    }
}
