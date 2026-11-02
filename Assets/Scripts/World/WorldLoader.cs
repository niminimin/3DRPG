using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 跨场景加载器（常驻在 Boot 场景）。
/// 世界场景用 Additive 叠加加载/卸载，玩家、UI、相机永远不销毁。
/// </summary>
public class WorldLoader : Singleton<WorldLoader>
{
    [Header("启动世界")]
    [Tooltip("游戏开始后加载的第一个内容场景")]
    public string defaultWorld = "SampleScene";

    [Tooltip("初始落点 id")]
    public string defaultEntryId = "B";

    /// <summary>正在加载/卸载中（传送门用它防止重复触发）。</summary>
    public bool IsBusy { get; private set; }

    /// <summary>当前加载着的内容场景名。</summary>
    public string CurrentWorld { get; private set; }

    private PlayerController player;

    protected override void Awake()
    {
        base.Awake();
        Debug.Log("[WorldLoader] Awake 完成，instance 是否已设置 = " + (instance != null));
    }

    private void Start()
    {
        player = FindObjectOfType<PlayerController>();
        Debug.Log("[WorldLoader] Start，instance 是否为 null = " + (instance == null));

        if (string.IsNullOrEmpty(defaultWorld))
        {
            Debug.LogWarning("[WorldLoader] 没有配置 defaultWorld", this);
            return;
        }

        StartCoroutine(LoadRoutine(defaultWorld, defaultEntryId, false));
    }

    /// <summary>切换到另一个内容场景（传送门调用）。</summary>
    public void LoadWorld(string sceneName, string entryId)
    {
        if (IsBusy) return;
        if (string.IsNullOrEmpty(sceneName)) return;

        StartCoroutine(LoadRoutine(sceneName, entryId, true));
    }

    private IEnumerator LoadRoutine(string sceneName, string entryId, bool withFade)
    {
        IsBusy = true;

        if (withFade && SceneFader.instance != null)
            yield return SceneFader.instance.FadeOut();

        // 1) 卸载旧世界
        if (!string.IsNullOrEmpty(CurrentWorld))
        {
            AsyncOperation unload = SceneManagerUnload(CurrentWorld);
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

        // 4) 把玩家放到落点
        WarpPlayer(entryId);

        // 5) 淡入
        if (withFade && SceneFader.instance != null)
            yield return SceneFader.instance.FadeIn();
        else if (SceneFader.instance != null)
            SceneFader.instance.Clear();

        IsBusy = false;
    }

    private AsyncOperation SceneManagerUnload(string sceneName)
    {
        return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneName);
    }

    private void WarpPlayer(string entryId)
    {
        if (player == null) player = FindObjectOfType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[WorldLoader] 找不到玩家（玩家应该在 Boot 场景里）", this);
            return;
        }

        PortalEntry entry = PortalEntry.Find(entryId);
        if (entry == null)
        {
            Debug.LogWarning("[WorldLoader] 场景 " + CurrentWorld + " 里没有 id=" + entryId + " 的落点", this);
            return;
        }

        player.TeleportTo(entry.transform.position, entry.transform.rotation);
    }
}
