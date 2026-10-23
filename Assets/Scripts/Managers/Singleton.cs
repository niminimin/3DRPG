using UnityEngine;

/// <summary>
/// 泛型单例基类：T 填具体的管理器类型。
///
/// 用法：
///   public class Foo : Singleton<Foo>
///   {
///       protected override void Awake()
///       {
///           base.Awake();     // ★ 必须调用，负责赋值 instance 和处理重复实例
///           // ...自己的初始化
///       }
///   }
///
/// 注意：子类如果重写 Awake，一定要先调用 base.Awake()，否则 instance 永远为 null。
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    /// <summary>全局唯一实例；不存在时返回 null（调用方自行判空）。</summary>
    public static T instance
    {
        get { return _instance; }
    }

    /// <summary>是否需要跨场景保留（默认不保留；单场景项目用不到）。</summary>
    protected virtual bool Persistent
    {
        get { return false; }
    }

    protected virtual void Awake()
    {
        // 场景里出现第二个同类管理器：保留先到的，销毁后来的
        if (_instance != null && _instance != this as T)
        {
            Debug.LogWarning("[Singleton] 场景里已存在 " + typeof(T).Name + "，销毁重复的 " + name, this);
            Destroy(gameObject);
            return;
        }

        _instance = this as T;

        if (Persistent)
            DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnDestroy()
    {
        // 实例被销毁时清空引用，避免下次进场景时拿到已销毁的引用
        if (_instance == this as T)
            _instance = null;
    }
}
