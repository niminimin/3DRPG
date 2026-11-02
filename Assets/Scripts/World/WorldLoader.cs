using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 跨场景加载器（常驻在 Boot 场景）。
/// 世界场景用 Additive 叠加加载/卸载，玩家、UI、相机永远不销毁。
///
/// 两条路径刻意分开：
///   LoadInitial() —— 开局/读档：加载世界 + 放到出生点，不走"找门"
///   LoadWorld()   —— 走传送门：加载世界 + 放到目标门前
///
/// 找门不每帧遍历：场景加载后建一次 id → Portal 的索引表，之后 O(1) 查询。
/// </summary>
public class WorldLoader : Singleton<WorldLoader>
{
    [Header("启动世界")]
    [Tooltip("游戏开始后加载的第一个内容场景")]
    public string defaultWorld = "SampleScene";

    [Header("出生点（只用于开局）")]
    public Vector3 startPosition = Vector3.zero;
    public float startYaw = 0f;

    /// <summary>正在加载/卸载中（传送门用它防止重复触发）。</summary>
    public bool IsBusy { get; private set; }

    /// <summary>当前加载着的内容场景名。</summary>
    public string CurrentWorld { get; private set; }

    private PlayerController player;

    // 当前世界场景的门索引：id → Portal
    private readonly Dictionary<string, Portal> doors = new Dictionary<string, Portal>();

    private void Start()
    {
        player = FindObjectOfType<PlayerController>();

        if (string.IsNullOrEmpty(defaultWorld))
        {
            Debug.LogWarning("[WorldLoader] 没有配置 defaultWorld", this);
            return;
        }

        StartCoroutine(LoadInitialRoutine(defaultWorld));
    }

    // ==================== 路径 1：开局 ====================
    /// <summary>开局加载：不找门，直接放到出生点。</summary>
    private IEnumerator LoadInitialRoutine(string sceneName)
    {
        IsBusy = true;

        yield return LoadWorldScene(sceneName);
        if (string.IsNullOrEmpty(CurrentWorld))
        {
            IsBusy = false;
            yield break;
        }

        Vector3 spawn = SnapToNavMesh(startPosition);
        WarpPlayer(spawn, startYaw);

        if (SceneFader.instance != null) SceneFader.instance.Clear();

        IsBusy = false;
    }

    // ==================== 路径 2：传送 ====================
    /// <summary>走传送门：加载目标世界，并站到目标门前。</summary>
    public void LoadWorld(string sceneName, string targetDoorId)
    {
        if (IsBusy) return;
        if (string.IsNullOrEmpty(sceneName)) return;

        StartCoroutine(LoadWorldRoutine(sceneName, targetDoorId));
    }

    private IEnumerator LoadWorldRoutine(string sceneName, string targetDoorId)
    {
        IsBusy = true;

        if (SceneFader.instance != null)
            yield return SceneFader.instance.FadeOut();

        yield return LoadWorldScene(sceneName);
        if (string.IsNullOrEmpty(CurrentWorld))
        {
            if (SceneFader.instance != null) yield return SceneFader.instance.FadeIn();
            IsBusy = false;
            yield break;
        }

        PlaceBeforeDoor(targetDoorId);

        if (SceneFader.instance != null)
            yield return SceneFader.instance.FadeIn();

        IsBusy = false;
    }

    // ==================== 公共：加载场景 + 建门索引 ====================
    private IEnumerator LoadWorldScene(string sceneName)
    {
        // 1) 卸载旧世界
        if (!string.IsNullOrEmpty(CurrentWorld))
        {
            doors.Clear();

            AsyncOperation unload = SceneManager.UnloadSceneAsync(CurrentWorld);
            while (unload != null && !unload.isDone) yield return null;

            CurrentWorld = null;
        }

        // 2) 加载新世界（叠加，不动 Boot）
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError("[WorldLoader] 场景加载失败：" + sceneName + "（检查 Build Settings）", this);
            yield break;
        }

        while (!op.isDone) yield return null;
        CurrentWorld = sceneName;

        // 3) 等新场景的 Awake/Start 跑完
        yield return null;

        // 4) 只扫这一场景的门，建索引
        BuildDoorIndex(SceneManager.GetSceneByName(sceneName));
    }

    /// <summary>遍历新场景里的门，建立 id → Portal 的索引（一次 O(n)，之后 O(1)）。</summary>
    private void BuildDoorIndex(Scene scene)
    {
        doors.Clear();

        if (!scene.IsValid()) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // 只在这个场景的根物体下找，不会扫到 Boot
            Portal[] found = root.GetComponentsInChildren<Portal>(true);

            foreach (Portal portal in found)
            {
                if (string.IsNullOrEmpty(portal.id))
                {
                    Debug.LogWarning("[WorldLoader] 场景 " + scene.name + " 里有门没填 id：" + portal.name, portal);
                    continue;
                }

                if (doors.ContainsKey(portal.id))
                {
                    Debug.LogError("[WorldLoader] 场景 " + scene.name + " 里门 id 重复：" + portal.id, portal);
                    continue;
                }

                doors.Add(portal.id, portal);
            }
        }

        Debug.Log("[WorldLoader] 场景 " + scene.name + " 的门索引建立完成，共 " + doors.Count + " 扇");
    }

    // ==================== 落点计算 ====================
    private void PlaceBeforeDoor(string doorId)
    {
        Portal door = FindDoor(doorId);

        if (door == null)
        {
            Debug.LogError("[WorldLoader] 场景 " + CurrentWorld + " 里找不到 id=" + doorId + " 的门，玩家留在原地", this);
            return;
        }

        Vector3 position = SnapToNavMesh(door.GetArrivalPosition());
        Quaternion rotation = door.GetArrivalRotation();

        if (player == null) player = FindObjectOfType<PlayerController>();
        if (player == null) return;

        player.TeleportTo(position, rotation);
    }

    private Portal FindDoor(string doorId)
    {
        if (string.IsNullOrEmpty(doorId)) return null;
        doors.TryGetValue(doorId, out Portal door);
        return door;
    }

    /// <summary>把点投到导航网格上，避免落在半空/墙里。</summary>
    private Vector3 SnapToNavMesh(Vector3 position)
    {
        if (UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;

        return position;
    }

    private void WarpPlayer(Vector3 position, float yaw)
    {
        if (player == null) player = FindObjectOfType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[WorldLoader] 找不到玩家（玩家应该在 Boot 场景里）", this);
            return;
        }

        player.TeleportTo(position, Quaternion.Euler(0f, yaw, 0f));
    }
}
