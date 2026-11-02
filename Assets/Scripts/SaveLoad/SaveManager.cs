using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 存档管理器（常驻 Boot 场景）。
///
/// 两层语义（和 2DAdventure 一致）：
///   内存层：敌人一死就立刻记录 → 换场景回来，它还死着
///   磁盘层：只有显式"保存存档"才写文件
/// 所以：打死敌人 → 没存档就退出 → 读档 → 它还活着（未保存的进度该丢）
/// </summary>
public class SaveManager : Singleton<SaveManager>
{
    [Header("存档文件")]
    [Tooltip("存档文件名（放在 Application.persistentDataPath 下）")]
    public string fileName = "save_0.json";

    /// <summary>内存里的当前存档数据。</summary>
    public SaveData Data { get; private set; } = new SaveData();

    public string SavePath
    {
        get { return Path.Combine(Application.persistentDataPath, fileName); }
    }

    public bool HasSaveFile
    {
        get { return File.Exists(SavePath); }
    }

    // ==================== 内存层：敌人状态 ====================
    /// <summary>立刻记录一个敌人死亡（只写内存，不落盘）。</summary>
    public void MarkEnemyDead(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        Data.MarkEnemyDead(id);
    }

    public bool IsEnemyDead(string id)
    {
        return Data.IsEnemyDead(id);
    }

    /// <summary>
    /// 场景加载完之后调用：把该死掉的敌人清掉。
    /// 只影响本场景的实例，不动 Boot。
    /// </summary>
    public void ApplyWorldState(Scene scene)
    {
        if (!scene.IsValid()) return;

        int killed = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            SaveIdentity[] ids = root.GetComponentsInChildren<SaveIdentity>(true);

            foreach (SaveIdentity identity in ids)
            {
                if (!identity.HasId) continue;
                if (!IsEnemyDead(identity.id)) continue;

                // 只处理"有血量的角色"，以后其他可存对象按需扩展
                if (identity.GetComponent<CharacterStats>() == null) continue;

                UnityEngine.Object.Destroy(identity.gameObject);
                killed++;
            }
        }

        if (killed > 0)
            Debug.Log("[SaveManager] 场景 " + scene.name + " 里清掉了 " + killed + " 个已死亡的敌人");
    }

    // ==================== 玩家 ====================
    /// <summary>把玩家当前状态收集进内存存档。</summary>
    public void CapturePlayer()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        PlayerProgress progress = PlayerProgress.instance;

        if (player != null)
        {
            Data.playerPosition = player.transform.position;
            Data.playerYaw = player.transform.eulerAngles.y;
        }

        if (progress != null)
        {
            Data.level = progress.Level;
            Data.exp = progress.CurrentExp;
        }

        CharacterStats stats = progress != null ? progress.stats : null;
        if (stats != null) Data.curHealth = stats.CurHealth;

        if (WorldLoader.instance != null && !string.IsNullOrEmpty(WorldLoader.instance.CurrentWorld))
            Data.sceneName = WorldLoader.instance.CurrentWorld;

        Data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>把存档里的数值套回玩家（位置由 WorldLoader 负责）。</summary>
    public void ApplyPlayerStats()
    {
        PlayerProgress progress = PlayerProgress.instance;
        if (progress == null) return;

        progress.RestoreState(Data.level, Data.exp);

        CharacterStats stats = progress.stats;
        if (stats != null) stats.SetCurHealth(Data.curHealth);
    }

    // ==================== 磁盘层 ====================
    /// <summary>保存到磁盘（先收集玩家状态）。</summary>
    public bool Save()
    {
        CapturePlayer();

        try
        {
            string dir = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
            Debug.Log("[SaveManager] 存档已保存: " + SavePath
                      + "（死亡敌人 " + Data.deadEnemyIds.Count + " 个）");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveManager] 保存失败: " + e.Message, this);
            return false;
        }
    }

    /// <summary>从磁盘读取，并让 WorldLoader 加载存档里的场景。返回是否成功。</summary>
    public bool Load()
    {
        if (!HasSaveFile)
        {
            Debug.LogWarning("[SaveManager] 没有存档文件: " + SavePath, this);
            return false;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            Data = JsonUtility.FromJson<SaveData>(json);

            if (Data == null) Data = new SaveData();
            if (Data.deadEnemyIds == null) Data.deadEnemyIds = new List<string>();
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveManager] 读取失败（按新游戏处理）: " + e.Message, this);
            Data = new SaveData();
            return false;
        }

        Debug.Log("[SaveManager] 存档已读取，场景 = " + Data.sceneName
                  + "，死亡敌人 " + Data.deadEnemyIds.Count + " 个");

        if (WorldLoader.instance != null)
            WorldLoader.instance.LoadFromSave(Data);
        else
            Debug.LogError("[SaveManager] 找不到 WorldLoader，无法加载场景", this);

        return true;
    }

    /// <summary>新游戏：清空内存 + 删除磁盘存档 + 重新加载默认世界。</summary>
    public void NewGame()
    {
        Data = new SaveData();
        DeleteSaveFile();

        if (WorldLoader.instance != null)
            WorldLoader.instance.RestartGame();
    }

    private void DeleteSaveFile()
    {
        if (!File.Exists(SavePath)) return;

        try
        {
            File.Delete(SavePath);
            Debug.Log("[SaveManager] 已删除存档文件");
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveManager] 删除存档失败: " + e.Message, this);
        }
    }
}
