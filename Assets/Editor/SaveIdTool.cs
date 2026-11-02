using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 给当前打开的（内容）场景里的 SaveIdentity 分配唯一 ID。
/// 菜单：Tools/存档/给当前场景分配 ID
/// </summary>
public static class SaveIdTool
{
    [MenuItem("Tools/存档/给当前场景分配 ID")]
    public static void AssignIdsInOpenScenes()
    {
        int total = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded) continue;

            int count = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SaveIdentity[] ids = root.GetComponentsInChildren<SaveIdentity>(true);

                foreach (SaveIdentity identity in ids)
                {
                    if (identity.HasId) continue;

                    identity.id = System.Guid.NewGuid().ToString();
                    EditorUtility.SetDirty(identity);
                    count++;
                }
            }

            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[SaveIdTool] 场景 " + scene.name + "：分配了 " + count + " 个 ID");
            }
            else
            {
                Debug.Log("[SaveIdTool] 场景 " + scene.name + "：没有需要分配的 ID");
            }

            total += count;
        }

        if (total > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[SaveIdTool] 完成，共 " + total + " 个。记得保存场景（Ctrl+S）");
        }
    }

    [MenuItem("Tools/存档/检查重复 ID")]
    public static void CheckDuplicates()
    {
        System.Collections.Generic.Dictionary<string, string> seen =
            new System.Collections.Generic.Dictionary<string, string>();

        int dup = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SaveIdentity identity in root.GetComponentsInChildren<SaveIdentity>(true))
                {
                    if (!identity.HasId) continue;

                    string path = scene.name + "/" + identity.name;

                    if (seen.ContainsKey(identity.id))
                    {
                        Debug.LogError("[SaveIdTool] ID 重复：" + identity.id
                                       + "\n  " + seen[identity.id] + "\n  " + path, identity);
                        dup++;
                    }
                    else
                    {
                        seen[identity.id] = path;
                    }
                }
            }
        }

        Debug.Log(dup == 0
            ? "[SaveIdTool] 没有重复 ID"
            : "[SaveIdTool] 发现 " + dup + " 处重复");
    }

    [MenuItem("Tools/存档/打开存档目录")]
    public static void OpenSaveFolder()
    {
        string path = Application.persistentDataPath;
        if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
        EditorUtility.RevealInFinder(path);
    }

    [MenuItem("Tools/存档/删除存档文件")]
    public static void DeleteSave()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "save_0.json");
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
            Debug.Log("[SaveIdTool] 已删除存档：" + path);
        }
        else
        {
            Debug.Log("[SaveIdTool] 没有存档文件");
        }
    }
}
