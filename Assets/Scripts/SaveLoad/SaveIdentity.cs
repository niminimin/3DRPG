using UnityEngine;

/// <summary>
/// 存档身份：给场景里的对象一个稳定的唯一 ID，存档只记这个 ID。
/// ID 由菜单 Tools/分配存档 ID（当前场景） 自动生成，不要手改。
///
/// 注意：不要指望"在预制体上填 ID"——那样同一预制体的所有实例会共用一个 ID。
///      预制体上留空，实例上再分配。
/// </summary>
public class SaveIdentity : MonoBehaviour
{
    [Tooltip("由 Tools/分配存档 ID（当前场景） 生成")]
    public string id = "";

    public bool HasId
    {
        get { return !string.IsNullOrEmpty(id); }
    }
}
