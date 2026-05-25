using UnityEngine;
using UnityEngine.SceneManagement;

public class PausePanelPresenter : MonoBehaviour
{
    [Header("Quit")]
    [SerializeField] private bool _quitToDesktop = true;

    public void ContinueGame()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
        {
            EventBus.Instance?.Publish(new PauseRequestedEvent { Pause = false });
        }
    }

    public void RetryGame()
    {
        if (SceneLoader.Instance == null)
        {
            Debug.LogError("[PausePanelPresenter] SceneLoader instance is missing.", this);
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        SceneLoader.Instance.RequestLoad(sceneName, GameState.Playing);
    }

    public void ExitGame()
    {
        if (_quitToDesktop)
        {
            Application.Quit();
            return;
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.GoToLobby();
        }
    }
}
