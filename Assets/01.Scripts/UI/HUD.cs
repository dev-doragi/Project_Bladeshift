using UnityEngine;

public class HUD : MonoBehaviour
{
    [SerializeField] private UI_HpBar _hpBar;

    private PlayerController _playerController;
    private PlayerHealth _playerHealth;

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<SceneLoadedEvent>(OnSceneLoaded);
        TryAutoBind();
    }

    private void Start()
    {
        TryAutoBind();
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
        Unbind();
    }

    public void Bind(PlayerController playerController)
    {
        Unbind();

        _playerController = playerController;

        if (_playerController != null)
            _playerHealth = _playerController.GetComponent<PlayerHealth>();

        BindPlayerStatsUI();
        Refresh();
    }

    public void Unbind()
    {
        UnbindPlayerStatsUI();

        _playerController = null;
        _playerHealth = null;
    }

    private void TryAutoBind()
    {
        if (_playerController != null && _playerHealth != null)
            return;

        PlayerController foundPlayer = FindFirstObjectByType<PlayerController>();
        if (foundPlayer == null)
            return;

        Bind(foundPlayer);
    }

    private void OnSceneLoaded(SceneLoadedEvent evt)
    {
        TryAutoBind();
        Refresh();
    }

    private void BindPlayerStatsUI()
    {
        if (_hpBar != null)
            _hpBar.Bind(_playerHealth);
    }

    private void UnbindPlayerStatsUI()
    {
        if (_hpBar != null)
            _hpBar.Unbind();
    }

    public void Refresh()
    {
        if (_hpBar != null)
            _hpBar.Refresh();
    }
}

