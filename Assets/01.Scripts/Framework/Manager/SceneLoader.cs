using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-180)]
public class SceneLoader : Singleton<SceneLoader>
{
    [Header("Scene Settings")]
    [SerializeField] private string _lobbySceneName = "01.LobbyScene";
    [SerializeField] private string _tutorialSceneName = "02.TutorialScene";
    [SerializeField] private string _stageSelectSceneName = "03.StageSelectScene";
    [SerializeField] private string _inGameSceneName = "04.InGameScene";

    private bool _isLoading;
    private bool _hasPendingPostLoadState;
    private GameState _pendingPostLoadState = GameState.Ready;

    protected override void OnBootstrap()
    {
        EventBus.Instance?.Subscribe<SceneLoadRequestedEvent>(OnSceneLoadRequested);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<SceneLoadRequestedEvent>(OnSceneLoadRequested);
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void GoToLobby()
    {
        RequestLoad(_lobbySceneName, GameState.Ready);
    }

    public void GoToStageSelect()
    {
        PreserveBGMForNextSceneLoad();
        RequestLoad(_stageSelectSceneName, GameState.Ready);
    }

    public void EnterTutorial()
    {
        PreserveBGMForNextSceneLoad();
        RequestLoad(_tutorialSceneName, GameState.Ready);
    }

    public void EnterInGameFromTutorial(int stageIndex)
    {
        PreserveBGMForNextSceneLoad();
        RequestLoad(_inGameSceneName, GameState.Playing);
    }

    public void EnterInGame(int stageIndex)
    {
        RequestLoad(_inGameSceneName, GameState.Playing);
    }

    public void ReloadCurrentScene()
    {
        RequestLoad(SceneManager.GetActiveScene().name, GameState.Playing);
    }

    public void Quit()
    {
        Application.Quit();
    }

    public void RequestLoad(string sceneName)
    {
        EventBus.Instance?.Publish(new SceneLoadRequestedEvent
        {
            SceneName = sceneName,
            HasPostLoadState = false,
            PostLoadState = GameState.Ready
        });
    }

    public void RequestLoad(string sceneName, GameState postLoadState)
    {
        EventBus.Instance?.Publish(new SceneLoadRequestedEvent
        {
            SceneName = sceneName,
            HasPostLoadState = true,
            PostLoadState = postLoadState
        });
    }

    private void OnSceneLoadRequested(SceneLoadRequestedEvent evt)
    {
        if (string.IsNullOrWhiteSpace(evt.SceneName))
        {
            Debug.LogError("[SceneLoader] Scene name is null or empty.", this);
            return;
        }

        if (_isLoading)
        {
            Debug.LogWarning($"[SceneLoader] Already loading scene. Request ignored: {evt.SceneName}", this);
            return;
        }

        _hasPendingPostLoadState = evt.HasPostLoadState;
        _pendingPostLoadState = evt.PostLoadState;

        StartCoroutine(LoadSceneAsyncRoutine(evt.SceneName));
    }

    private IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        _isLoading = true;

        TimeManager.Instance?.ResetTime();
        InputReader.Instance?.SetInputBlocked(false);
        GameManager.Instance?.ChangeState(GameState.Loading);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[SceneLoader] Failed to start loading scene: {sceneName}", this);
            _isLoading = false;
            yield break;
        }

        while (!op.isDone)
        {
            yield return null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TimeManager.Instance?.ResetTime();

        if (!Mathf.Approximately(Time.timeScale, 1f))
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        _isLoading = false;
        EventBus.Instance?.Publish(new SceneLoadedEvent { SceneName = scene.name });

        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideAllPanels();
        }

        if (_hasPendingPostLoadState && GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(_pendingPostLoadState);
        }

        _hasPendingPostLoadState = false;
        _pendingPostLoadState = GameState.Ready;
    }

    private void PreserveBGMForNextSceneLoad()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.RequestSkipNextSceneLoadedBGMStop();
        }
    }
}
