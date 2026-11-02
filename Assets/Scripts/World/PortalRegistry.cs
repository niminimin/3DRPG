using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 传送门注册表：门在 Awake 时自己登记，销毁时自己注销。
/// 这样加载场景后不需要遍历整棵场景树去找门。
///
/// 注意：这是纯静态表，跨场景有效；场景卸载时门会被销毁，OnDestroy 会自动注销。
/// 如果担心漏注销，WorldLoader 卸载世界时也会 Clear() 兜底。
/// </summary>
public static class PortalRegistry
{
    private static readonly Dictionary<string, Portal> map = new Dictionary<string, Portal>();

    /// <summary>门自己调用（Awake）。</summary>
    public static void Register(Portal portal)
    {
        if (portal == null || string.IsNullOrEmpty(portal.id)) return;

        if (map.TryGetValue(portal.id, out Portal existing) && existing != null && existing != portal)
        {
            Debug.LogError("[PortalRegistry] 门 id 重复：" + portal.id
                           + "\n  已有：" + existing.gameObject.scene.name + "/" + existing.name
                           + "\n  新的：" + portal.gameObject.scene.name + "/" + portal.name, portal);
            return;
        }

        map[portal.id] = portal;
    }

    /// <summary>门自己调用（OnDestroy）。</summary>
    public static void Unregister(Portal portal)
    {
        if (portal == null || string.IsNullOrEmpty(portal.id)) return;

        // 只有当表里存的就是这一扇时才删（防止后来的同 id 门被前一扇的销毁带走）
        if (map.TryGetValue(portal.id, out Portal existing) && existing == portal)
            map.Remove(portal.id);
    }

    /// <summary>按 id 查门；查不到返回 null。</summary>
    public static Portal Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (map.TryGetValue(id, out Portal portal))
        {
            // Unity 的"伪 null"：场景卸载后对象被销毁但引用还在，这里顺手清掉
            if (portal == null)
            {
                map.Remove(id);
                return null;
            }
            return portal;
        }

        return null;
    }

    /// <summary>清空（切换世界时兜底）。</summary>
    public static void Clear()
    {
        map.Clear();
    }

    /// <summary>当前已注册的门数量（调试用）。</summary>
    public static int Count
    {
        get { return map.Count; }
    }
}
