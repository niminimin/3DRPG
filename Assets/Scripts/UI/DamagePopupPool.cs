using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 受击飘字的对象池：避免每次伤害都 Instantiate/Destroy。
/// 用法：DamagePopupPool.instance.Get(位置) → Setup(...) → 播完自动归还。
/// </summary>
public class DamagePopupPool : MonoBehaviour
{
    public static DamagePopupPool instance;

    [Tooltip("飘字预制体")]
    public DamagePopup popupPrefab;

    [Tooltip("预热数量")]
    public int defaultCapacity = 8;

    [Tooltip("池内最多保留多少个（超出的在归还时销毁）")]
    public int maxSize = 32;

    private ObjectPool<DamagePopup> pool;

    private void Awake()
    {
        instance = this;

        pool = new ObjectPool<DamagePopup>(
            createFunc: CreateNew,
            actionOnGet: OnGet,
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyed,
            collectionCheck: true,      // 编辑器里检测"同一个对象被归还两次"
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);

        // 预热：先一次性借出 N 个（每次借都会 createFunc 造新的），再统一归还
        // 注意：不能"借一个还一个"，那样池子每次都复用同一个，只会造出 1 个
        int prewarm = Mathf.Min(defaultCapacity, maxSize);
        var temp = new System.Collections.Generic.List<DamagePopup>(prewarm);

        for (int i = 0; i < prewarm; i++) temp.Add(pool.Get());
        foreach (var popup in temp) pool.Release(popup);
    }

    private DamagePopup CreateNew()
    {
        DamagePopup popup = Instantiate(popupPrefab, transform);
        popup.OnFinished = () => pool.Release(popup);   // 播完自动归还
        popup.gameObject.SetActive(false);
        return popup;
    }

    private void OnGet(DamagePopup popup)
    {
        popup.gameObject.SetActive(true);
    }

    private void OnRelease(DamagePopup popup)
    {
        popup.gameObject.SetActive(false);
        popup.transform.SetParent(transform, false);
    }

    private void OnDestroyed(DamagePopup popup)
    {
        if (popup != null) Destroy(popup.gameObject);
    }

    /// <summary>借一个飘字并摆到指定位置。</summary>
    public DamagePopup Get(Vector3 position)
    {
        if (popupPrefab == null) return null;

        DamagePopup popup = pool.Get();
        popup.transform.position = position;
        popup.transform.rotation = Quaternion.identity;
        return popup;
    }
}
