using System.Collections;
using UnityEngine;

/// <summary>
/// 跨场景加载器（常驻在 Boot 场景）。
/// 世界场景用 Additive 叠加加载/卸载，玩家、UI、相机永远不销毁。
///
/// 设计要点（参考 2DAdventure 的 SceneLoad）：
///   加载器不认识"落点"，它只负责"加载场景 + 把玩家搬到 (position, yaw)"。
///   具体落到哪里，由**调用方**（传送门）在请求里带过来。
///   只有第一次进游戏用默认出生点 startPosition。
/// </summary>
public class WorldLoader : Singleton<WorldLoader>
{
    [Header("启动世界")]
    [Tooltip("游戏开始后加载的第一个内容场景")]
    public string defaultWorld = "SampleScene";

    [Header("默认出生点（只用于第一次进游戏）")]
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

        if (string.IsNullOrEmpty(defaultWorld))
        {
            Debug.LogWarning("[WorldLoader] 没有配置 defaultWorld", this);
            return;
        }

        // 第一次进游戏：用默认出生点，不淡入淡出
        StartCoroutine(LoadRoutine(defaultWorld, startPosition, startYaw, false));
    }

    /// <summary>
    /// 切换内容场景。
    /// 落点由调用方决定（传送门把自己配的 positionToGo 传进来）。
    /// </summary>
    public void LoadWorld(string sceneName, Vector3 position, float yaw)
    {
        if (IsBusy) return;
        if (string.IsNullOrEmpty(sceneName)) return;

        StartCoroutine(LoadRoutine(sceneName, position, yaw, true));
    }

    private IEnumerator LoadRoutine(string sceneName, Vector3 position, float yaw, bool withFade)
    {
        IsBusy = true;

        if (withFade && SceneFader.instance != null)
            yield return SceneFader.instance.FadeOut();

        // 1) 卸载旧世界
        if (!string.IsNullOrEmpty(CurrentWorld))
        {
            AsyncOperation unload = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(CurrentWorld);
            while (unload != null && !unload.isDone) yield return null;
            CurrentWorld = null;
        }

        // 2) 加载新世界（叠加，不动 Boot）
        AsyncOperation load = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
            sceneName, UnityEngine.SceneManagement.LoadSceneMode.Additive);

        if (load == null)
        {
            Debug.LogError("[WorldLoader] 场景加载失败：" + sceneName + "（检查 Build Settings）", this);

            if (withFade && SceneFader.instance != null)
                yield return SceneFader.instance.FadeIn();

            IsBusy = false;
            yield break;
        }

        while (!load.isDone) yield return null;
        CurrentWorld = sceneName;

        // 3) 等新场景的 Awake/Start 跑完
        yield return null;

        // 4) 把玩家搬到调用方指定的位置
        WarpPlayer(position, yaw);

        // 5) 淡入
        if (withFade && SceneFader.instance != null)
            yield return SceneFader.instance.FadeIn();
        else if (SceneFader.instance != null)
            SceneFader.instance.Clear();

        IsBusy = false;
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
