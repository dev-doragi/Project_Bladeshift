using UnityEngine;

[DefaultExecutionOrder(-170)]
public class GameManager : Singleton<GameManager>
{
    [Header("Debug")]
    [SerializeField] private bool _forcePlayingOnBootstrap = false;

    public GameState CurrentState { get; private set; } = GameState.Ready;

    protected override void OnBootstrap()
    {
        Application.targetFrameRate = 60;

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<StageLoadedEvent>(OnStageLoaded);
            EventBus.Instance.Subscribe<StageClearedEvent>(OnStageCleared);
            EventBus.Instance.Subscribe<StageFailedEvent>(OnStageFailed);
            EventBus.Instance.Subscribe<BossHeadDefeatedEvent>(OnBossHeadDefeated);
            EventBus.Instance.Subscribe<GameClearSequenceCompletedEvent>(OnGameClearSequenceCompleted);
        }

        if (_forcePlayingOnBootstrap)
        {
            ChangeState(GameState.Playing);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<StageLoadedEvent>(OnStageLoaded);
            EventBus.Instance.Unsubscribe<StageClearedEvent>(OnStageCleared);
            EventBus.Instance.Unsubscribe<StageFailedEvent>(OnStageFailed);
            EventBus.Instance.Unsubscribe<BossHeadDefeatedEvent>(OnBossHeadDefeated);
            EventBus.Instance.Unsubscribe<GameClearSequenceCompletedEvent>(OnGameClearSequenceCompleted);
        }
    }

    private void OnStageLoaded(StageLoadedEvent evt)
    {
        ChangeState(GameState.Playing);
    }

    private void OnStageCleared(StageClearedEvent evt)
    {
        if (evt.IsFinalStage)
        {
            SoundManager.Instance?.StopBGM();
            ChangeState(GameState.GameClear);
        }
    }

    private void OnStageFailed(StageFailedEvent evt)
    {
        SoundManager.Instance?.StopBGM();
        ChangeState(GameState.GameOver);
    }

    private void OnBossHeadDefeated(BossHeadDefeatedEvent evt)
    {
        SoundManager.Instance?.StopBGM();
    }

    private void OnGameClearSequenceCompleted(GameClearSequenceCompletedEvent evt)
    {
        if (!evt.IsFinalStage)
        {
            return;
        }

        ChangeState(GameState.GameClear);
    }

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState)
        {
            return;
        }

        GameState previousState = CurrentState;
        CurrentState = newState;

        Debug.Log($"[GameManager] State Changed: {previousState} -> {newState}");

        EventBus.Instance?.Publish(new GameStateChangedEvent
        {
            PreviousState = previousState,
            NewState = newState
        });
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
