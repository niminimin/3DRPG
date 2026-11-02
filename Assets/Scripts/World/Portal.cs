using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 传送门：玩家走进范围内 → 淡出 → 加载目标场景 → 把玩家放到对应 PortalEntry。
/// 用"距离检测"而不是 OnTriggerEnter：玩家是 NavMeshAgent + Transform 移动，没有 Rigidbody，
/// Trigger 事件不会触发（Trigger 要求至少一方有刚体）。
/// </summary>
public class Portal : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("目标场景名（要和 Build Settings 里的名字一致）")]
    public string targetScene = "";

    [Tooltip("目标场景里 PlayerEntry 的 id")]
    public string entryId = "A";

    [Header("触发")]
    [Tooltip("玩家进入这个半径内就传送")]
    public float activateRadius = 1.3f;

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
            WorldLoader.instance.LoadWorld(targetScene, entryId);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
        Vector3 p = transform.position; p.y = 0f;
        Gizmos.DrawWireSphere(p, activateRadius);
    }
}
