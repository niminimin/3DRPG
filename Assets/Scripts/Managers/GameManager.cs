using UnityEngine;

/// <summary>
/// 全局游戏状态管理器。
/// 只负责"广播事实"，不直接指挥敌人：谁想响应就自己订阅 OnPlayerDied。
/// </summary>
public class GameManager : Singleton<GameManager>
{
    /// <summary>玩家死亡广播：敌人（庆祝）、UI（结算面板）等自行订阅。</summary>
    public event System.Action OnPlayerDied;

    /// <summary>游戏是否已结束（防止重复广播，UI 晚创建时也能直接查）。</summary>
    public bool IsGameOver { get; private set; }

    private CharacterStats playerStats;

    private void Start()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null && player.Stats != null)
        {
            playerStats = player.Stats;
            playerStats.OnDead += HandlePlayerDead;
        }
        else
        {
            Debug.LogWarning("[GameManager] 场景里没找到玩家，玩家死亡广播不会生效", this);
        }
    }

    private void HandlePlayerDead()
    {
        if (IsGameOver) return;         // 只广播一次
        IsGameOver = true;

        Debug.Log("[GameManager] 玩家死亡，广播 OnPlayerDied");
        if (OnPlayerDied != null) OnPlayerDied();
    }

    /// <summary>读档 / 新游戏时清掉"游戏结束"标记，否则之后的死亡不会再广播。</summary>
    public void ResetGameOver()
    {
        IsGameOver = false;
    }

    protected override void OnDestroy()
    {
        if (playerStats != null) playerStats.OnDead -= HandlePlayerDead;
        base.OnDestroy();
    }
}
