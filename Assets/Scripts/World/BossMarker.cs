using UnityEngine;

/// <summary>
/// Boss 标记：挂在 Boss 身上，UI 靠它在整个场景里找 Boss。
/// 场景切换后 HUD 常驻、Boss 是重新加载的，所以不能靠序列化引用。
/// </summary>
public class BossMarker : MonoBehaviour
{
    public CharacterStats stats;

    [Tooltip("显示在血条上的名字")]
    public string displayName = "";

    private void Awake()
    {
        if (stats == null) stats = GetComponent<CharacterStats>();
    }
}
