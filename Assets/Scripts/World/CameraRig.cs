using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 相机机位组：放在 Boot 场景里，玩家常驻，所以相机也常驻（但不需要 DontDestroyOnLoad）。
/// 注意不要再设 Persistent：从游戏回主菜单时会卸载 Boot，如果相机跨场景活着，
/// 主菜单场景就会有两台相机 + 两个 AudioListener，渲染出黑屏/错位。
/// 结构：
///   CameraRig (本脚本)
///   ├─ Main Camera (Camera + CinemachineBrain)
///   └─ FreeLook Camera (CinemachineFreeLook)
/// </summary>
public class CameraRig : Singleton<CameraRig>
{
    [Tooltip("留空会自动在子物体里找")]
    public Cinemachine.CinemachineFreeLook freeLook;

    [Tooltip("留空会自动在子物体里找")]
    public Camera mainCamera;

    private void Start()
    {
        BindToPlayer();
    }

    protected override void Awake()
    {
        base.Awake();      // 负责 instance / 重复实例销毁 / DontDestroyOnLoad

        if (freeLook == null) freeLook = GetComponentInChildren<Cinemachine.CinemachineFreeLook>(true);
        if (mainCamera == null) mainCamera = GetComponentInChildren<Camera>(true);

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        base.OnDestroy();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 新场景刚加载完，玩家可能还没 Awake，下一帧再绑一次
        BindToPlayer();
        StartCoroutine(BindNextFrame());
    }

    private System.Collections.IEnumerator BindNextFrame()
    {
        yield return null;
        BindToPlayer();
    }

    /// <summary>把相机目标指向当前场景的玩家。</summary>
    public void BindToPlayer()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player == null) return;

        // 原来相机跟的是玩家身下的 LookAt 枢轴（头部高度），换场景后也要沿用
        Transform target = player.transform;
        Transform pivot = player.transform.Find("LookAt");
        if (pivot != null) target = pivot;

        if (freeLook != null)
        {
            freeLook.Follow = target;
            freeLook.LookAt = target;
        }
    }
}
