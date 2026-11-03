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

    private void Start()
    {
        player = FindObjectOfType<PlayerController>();

        // 主菜单点了"继续"：直接读档（读档流程会自己加载场景并放人）
        if (LaunchContext.Mode == LaunchContext.LaunchMode.Continue
            && SaveManager.instance != null && SaveManager.instance.HasSaveFile)
        {
            Debug.Log("[WorldLoader] 从主菜单继续游戏 → 读档");
            SaveManager.instance.Load();
            return;
        }

        // 主菜单点了"新游戏"：清内存 + 删存档，再正常开局
        if (LaunchContext.Mode == LaunchContext.LaunchMode.NewGame && SaveManager.instance != null)
        {
            Debug.Log("[WorldLoader] 从主菜单开始新游戏 → 清空存档");
            SaveManager.instance.ResetData();
        }

        // 消费掉启动意图，避免重进 Boot 时再次触发
        LaunchContext.Mode = LaunchContext.LaunchMode.Default;

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

        // 从主菜单进来时黑幕已经是全黑 → 淡入；直接在 Boot 里启动 → 直接透明
        if (SceneFader.instance != null)
        {
            if (SceneFader.instance.Alpha > 0.01f)
                yield return SceneFader.instance.FadeIn();
            else
                SceneFader.instance.Clear();
        }

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

    // ==================== 路径 3：读档 ====================
    /// <summary>读档：加载存档里的场景，并把玩家放回存档位置。</summary>
    public void LoadFromSave(SaveData data)
    {
        if (IsBusy) return;
        if (data == null) return;

        StartCoroutine(LoadFromSaveRoutine(data));
    }

    private IEnumerator LoadFromSaveRoutine(SaveData data)
    {
        IsBusy = true;

        if (SceneFader.instance != null)
            yield return SceneFader.instance.FadeOut();

        string scene = string.IsNullOrEmpty(data.sceneName) ? defaultWorld : data.sceneName;
        yield return LoadWorldScene(scene);

        if (!string.IsNullOrEmpty(CurrentWorld))
        {
            if (player == null) player = FindObjectOfType<PlayerController>();

            if (player != null)
            {
                Vector3 pos = SnapToNavMesh(data.playerPosition);
                player.TeleportTo(pos, Quaternion.Euler(0f, data.playerYaw, 0f));
            }

            if (SaveManager.instance != null) SaveManager.instance.ApplyPlayerStats();
        }

        if (SceneFader.instance != null)
            yield return SceneFader.instance.FadeIn();

        IsBusy = false;
    }

    // ==================== 路径 4：重开一局 ====================
    /// <summary>新游戏：重新加载默认世界并回到出生点。</summary>
    public void RestartGame()
    {
        if (IsBusy) return;
        StartCoroutine(LoadInitialRoutine(defaultWorld));
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

    // ==================== 公共：加载场景 ====================
    private IEnumerator LoadWorldScene(string sceneName)
    {
        // 1) 卸载旧世界
        if (!string.IsNullOrEmpty(CurrentWorld))
        {
            // 兜底：正常情况上门被销毁时会在 OnDestroy 里自己注销
            PortalRegistry.Clear();

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

        // 3) 等新场景的 Awake/Start 跑完（门会在这期间自己注册进 PortalRegistry）
        yield return null;

        Scene loaded = SceneManager.GetSceneByName(sceneName);
        Debug.Log("[WorldLoader] 场景 " + sceneName + " 已加载，注册的门 " + PortalRegistry.Count + " 扇");

        // 3.5) 把内容场景设为"激活场景"：
        //      天空盒、环境光、雾这些 RenderSettings 是跟着激活场景走的，
        //      不设的话游戏里一直用 Boot 的灯光设置，内容场景调好的光照全被忽略。
        if (loaded.IsValid())
            SceneManager.SetActiveScene(loaded);

        // 4) 清掉已经死过的敌人（存档里的状态）
        if (SaveManager.instance != null)
            SaveManager.instance.ApplyWorldState(loaded);
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
        // 门在 Awake 时已经把自己登记进 PortalRegistry，这里 O(1) 查表
        return PortalRegistry.Find(doorId);
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
