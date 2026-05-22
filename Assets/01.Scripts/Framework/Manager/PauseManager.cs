using UnityEngine;

[DefaultExecutionOrder(-140)]
public class PauseManager : Singleton<PauseManager>
{
    private GameState _pauseRestoreState = GameState.Playing;

    protected override void OnBootstrap()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<PausePressedEvent>(OnPausePressed);
            EventBus.Instance.Subscribe<PauseRequestedEvent>(OnPauseRequested);
            EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<PausePressedEvent>(OnPausePressed);
            EventBus.Instance.Unsubscribe<PauseRequestedEvent>(OnPauseRequested);
            EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }
    }

    private void OnPausePressed(PausePressedEvent evt)
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        GameState current = GameManager.Instance.CurrentState;

        if (current == GameState.GameOver || current == GameState.GameClear)
        {
            return;
        }

        if (current == GameState.Playing)
        {
            EventBus.Instance?.Publish(new PauseRequestedEvent { Pause = true });
        }
        else if (current == GameState.Paused)
        {
            EventBus.Instance?.Publish(new PauseRequestedEvent { Pause = false });
        }
    }

    private void OnPauseRequested(PauseRequestedEvent evt)
    {
        TogglePause(evt.Pause);
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        if (evt.NewState == GameState.Paused)
        {
            _pauseRestoreState = evt.PreviousState;
            return;
        }
    }

    public void TogglePause(bool pause)
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        GameState current = GameManager.Instance.CurrentState;

        if (current == GameState.GameOver || current == GameState.GameClear)
        {
            return;
        }

        if (pause)
        {
            if (current == GameState.Playing)
            {
                GameManager.Instance.ChangeState(GameState.Paused);
            }

            return;
        }

        if (current == GameState.Paused && _pauseRestoreState == GameState.Playing)
        {
            GameManager.Instance.ChangeState(GameState.Playing);
        }
    }
}
