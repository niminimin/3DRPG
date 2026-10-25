using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 石头（RockProjectile）的对象池：Boss 反复投石时不反复 Instantiate/Destroy。
/// 与 DamagePopupPool 同一套写法。
/// </summary>
public class RockPool : MonoBehaviour
{
    public static RockPool instance;

    [Tooltip("石头预制体")]
    public RockProjectile rockPrefab;

    [Tooltip("预热数量")]
    public int defaultCapacity = 2;

    [Tooltip("池内最多保留多少个（超出的在归还时销毁）")]
    public int maxSize = 6;

    private ObjectPool<RockProjectile> pool;

    private void Awake()
    {
        instance = this;

        pool = new ObjectPool<RockProjectile>(
            createFunc: CreateNew,
            actionOnGet: OnGet,
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyed,
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);

        // 预热：一次性借满，再统一归还（借一个还一个只会造出 1 个）
        int prewarm = Mathf.Min(defaultCapacity, maxSize);
        var temp = new List<RockProjectile>(prewarm);

        for (int i = 0; i < prewarm; i++) temp.Add(pool.Get());
        foreach (var rock in temp) pool.Release(rock);
    }

    private RockProjectile CreateNew()
    {
        RockProjectile rock = Instantiate(rockPrefab, transform);
        rock.OnFinished = r => pool.Release(r);   // 石头用完自己回来
        rock.gameObject.SetActive(false);
        return rock;
    }

    private void OnGet(RockProjectile rock)
    {
        rock.gameObject.SetActive(true);
    }

    private void OnRelease(RockProjectile rock)
    {
        rock.transform.SetParent(transform, false);
        rock.gameObject.SetActive(false);
    }

    private void OnDestroyed(RockProjectile rock)
    {
        if (rock != null) Destroy(rock.gameObject);
    }

    /// <summary>借一块石头并摆到出手位置。</summary>
    public RockProjectile Get(Vector3 position)
    {
        if (rockPrefab == null) return null;

        RockProjectile rock = pool.Get();
        rock.transform.position = position;
        rock.transform.rotation = Quaternion.identity;
        return rock;
    }
}
