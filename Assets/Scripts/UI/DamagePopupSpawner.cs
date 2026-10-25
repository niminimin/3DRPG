using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在角色上：订阅自己的 CharacterStats.OnDamaged，受伤时在头顶生成一个飘字。
/// 样式（暴击/普通）由 DamagePopup 根据 DamageResult 决定。
/// </summary>
public class DamagePopupSpawner : MonoBehaviour
{
    [Tooltip("飘字预制体")]
    public GameObject popupPrefab;

    [Tooltip("生成高度（相对角色根节点）")]
    public float headHeight = 1.8f;

    [Tooltip("随机水平偏移，避免数字叠在一起")]
    public float spread = 0.25f;

    private CharacterStats stats;

    private void Awake()
    {
        stats = GetComponent<CharacterStats>();

        if (popupPrefab == null)
            Debug.LogWarning("[" + name + "] 没有配置飘字预制体", this);
    }

    private void OnEnable()
    {
        if (stats != null) stats.OnDamaged += Spawn;
    }

    private void OnDisable()
    {
        if (stats != null) stats.OnDamaged -= Spawn;
    }

    private void Spawn(DamageResult result)
    {
        if (popupPrefab == null) return;

        Vector3 pos = transform.position
                      + Vector3.up * headHeight
                      + new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-spread, spread));

        DamagePopup popup = null;

        // 优先从对象池借；场景里没有池子时退化为生成/销毁
        if (DamagePopupPool.instance != null)
        {
            popup = DamagePopupPool.instance.Get(pos);
        }
        else
        {
            GameObject go = Instantiate(popupPrefab, pos, Quaternion.identity);
            popup = go.GetComponent<DamagePopup>();
        }

        if (popup != null) popup.Setup(result.amount, result.isCritical);
    }
}
