/// <summary>
/// 启动模式：主菜单点了什么，Boot 场景据此决定怎么开局。
/// 纯静态类，跨场景传递（不占用场景物体）。
/// 默认 Default：直接进游戏、不动存档 —— 方便在编辑器里直接从 Boot 开始调试。
/// </summary>
public static class LaunchContext
{
    public enum LaunchMode
    {
        Default,    // 直接进游戏（调试用），不碰存档
        NewGame,    // 新游戏：清空存档后重开
        Continue    // 继续：读取最近存档
    }

    public static LaunchMode Mode = LaunchMode.Default;
}
