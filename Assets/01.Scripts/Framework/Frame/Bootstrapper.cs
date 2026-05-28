using UnityEngine;

/// <summary>
/// 매니저 인스턴스 보장 및 초기화 순서를 중앙에서 제어합니다.
/// </summary>
[DefaultExecutionOrder(-500)]
public class Bootstrapper : MonoBehaviour
{
    [Header("Strict Validation")]
    [SerializeField] private bool _strictMode = true;

    [Header("Required Managers (DDOL)")]
    [SerializeField] private GameManager _gameManagerPrefab;
    [SerializeField] private TimeManager _timeManagerPrefab;
    [SerializeField] private SceneLoader _sceneLoaderPrefab;
    [SerializeField] private InputReader _inputReaderPrefab;
    [SerializeField] private PauseManager _pauseManagerPrefab;
    [SerializeField] private GameFlowManager _gameFlowManagerPrefab;
    [SerializeField] private RespawnManager _respawnManagerPrefab;

    [Header("Optional Managers")]
    [SerializeField] private SoundManager _soundManagerPrefab;
    [SerializeField] private CameraManager _cameraManagerPrefab;
    [SerializeField] private UIManager _uiManagerPrefab;
    [SerializeField] private PoolManager _poolManagerPrefab;

    private void Awake()
    {
        ValidateRequiredPrefabs();

        EnsureInstance(_gameManagerPrefab);
        EnsureInstance(_timeManagerPrefab);
        EnsureInstance(_sceneLoaderPrefab);
        EnsureInstance(_inputReaderPrefab);
        EnsureInstance(_pauseManagerPrefab);
        EnsureInstance(_gameFlowManagerPrefab);

        EnsureInstance(_soundManagerPrefab);
        EnsureInstance(_respawnManagerPrefab);
        EnsureInstance(_cameraManagerPrefab);
        EnsureInstance(_uiManagerPrefab);
        EnsureInstance(_poolManagerPrefab);
    }

    private void Start()
    {
        BootstrapManagers();
    }

    private void BootstrapManagers()
    {
        bool success = true;

        success &= BootstrapRequired(GameManager.Instance, nameof(GameManager));
        success &= BootstrapRequired(TimeManager.Instance, nameof(TimeManager));
        success &= BootstrapRequired(SceneLoader.Instance, nameof(SceneLoader));
        success &= BootstrapRequired(InputReader.Instance, nameof(InputReader));
        success &= BootstrapRequired(PauseManager.Instance, nameof(PauseManager));
        success &= BootstrapRequired(GameFlowManager.Instance, nameof(GameFlowManager));

        success &= BootstrapOptional(SoundManager.Instance, nameof(SoundManager));
        success &= BootstrapOptional(RespawnManager.Instance, nameof(RespawnManager));
        success &= BootstrapOptional(CameraManager.Instance, nameof(CameraManager));
        success &= BootstrapOptional(UIManager.Instance, nameof(UIManager));
        success &= BootstrapOptional(PoolManager.Instance, nameof(PoolManager));

        if (success)
        {
            Debug.Log("<color=green>[Bootstrapper]</color> manager bootstrapping completed.");
        }
        else
        {
            Debug.LogWarning("<color=yellow>[Bootstrapper]</color> manager bootstrapping completed with missing required managers.");
        }
    }

    private bool BootstrapRequired(ISingletonBootstrap manager, string managerName)
    {
        if (manager == null)
        {
            Debug.LogError($"[Bootstrapper] Missing required manager instance: {managerName}", this);
            return false;
        }

        manager.BootstrapIfNeeded();
        return true;
    }

    private bool BootstrapOptional(ISingletonBootstrap manager, string managerName)
    {
        if (manager == null)
        {
            Debug.LogWarning($"[Bootstrapper] Optional manager instance not found: {managerName}", this);
            return true;
        }

        manager.BootstrapIfNeeded();
        return true;
    }

    private void ValidateRequiredPrefabs()
    {
        if (!_strictMode)
        {
            return;
        }

        ValidateRequiredPrefab(_gameManagerPrefab, nameof(_gameManagerPrefab));
        ValidateRequiredPrefab(_timeManagerPrefab, nameof(_timeManagerPrefab));
        ValidateRequiredPrefab(_sceneLoaderPrefab, nameof(_sceneLoaderPrefab));
        ValidateRequiredPrefab(_inputReaderPrefab, nameof(_inputReaderPrefab));
        ValidateRequiredPrefab(_pauseManagerPrefab, nameof(_pauseManagerPrefab));
        ValidateRequiredPrefab(_gameFlowManagerPrefab, nameof(_gameFlowManagerPrefab));
        ValidateRequiredPrefab(_respawnManagerPrefab, nameof(_respawnManagerPrefab));
    }

    private void ValidateRequiredPrefab(Object prefab, string fieldName)
    {
        if (prefab == null)
        {
            Debug.LogError($"[Bootstrapper] Required prefab is missing: {fieldName}", this);
        }
    }

    private void EnsureInstance<T>(T prefab) where T : MonoBehaviour
    {
        if (prefab == null)
        {
            return;
        }

        if (FindAnyObjectByType<T>() != null)
        {
            return;
        }

        Instantiate(prefab);
    }
}
