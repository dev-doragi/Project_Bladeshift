using UnityEngine;

public interface ISingletonBootstrap
{
    bool IsBootstrapped { get; }
    void BootstrapIfNeeded();
}

[DefaultExecutionOrder(-100)]
public abstract class Singleton<T> : MonoBehaviour, ISingletonBootstrap where T : MonoBehaviour
{
    [SerializeField] protected bool _isDontDestroyOnLoad = true;

    private static T _instance;
    private static bool _isQuitting;

    public static bool IsExisted => _instance != null && !_isQuitting;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                if (_isQuitting)
                {
                    return null;
                }

                Debug.LogWarning($"[{typeof(T).Name}] Instance is not initialized.");
            }

            return _instance;
        }
    }

    public bool IsBootstrapped { get; private set; }

    protected virtual void Awake()
    {
        _isQuitting = false;

        if (_instance != null && _instance != this as T)
        {
            Debug.LogWarning($"[{typeof(T).Name}] Duplicate instance detected and destroyed: {name}", this);
            Destroy(gameObject);
            return;
        }

        _instance = this as T;

        if (_isDontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    public void BootstrapIfNeeded()
    {
        if (IsBootstrapped)
        {
            return;
        }

        OnBootstrap();
        IsBootstrapped = true;
    }

    protected virtual void OnBootstrap() { }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _isQuitting = true;
    }
}
