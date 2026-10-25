using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 传送落点：玩家从别的场景传送过来时，生成在这个物体所在的位置。
/// id 要和传送门上的 entryId 对应。
/// </summary>
public class PortalEntry : MonoBehaviour
{
    [Tooltip("落点 id，传送门的 entryId 要和它一致")]
    public string id = "A";

    [Tooltip("到达后玩家面朝的方向（沿这个物体的 forward）")]
    public bool faceForward = true;

    /// <summary>在场景里找指定 id 的落点。</summary>
    public static PortalEntry Find(string id)
    {
        PortalEntry[] all = FindObjectsOfType<PortalEntry>();

        foreach (var e in all)
            if (e.id == id) return e;

        // 没找到就先返回第一个，别让玩家掉在原地
        return all.Length > 0 ? all[0] : null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * 2f);
    }
}
