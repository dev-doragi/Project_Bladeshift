using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-95)]
public class RespawnManager : Singleton<RespawnManager>
{
    private Vector3 _savedRespawnPosition;
    private bool _hasSavedRespawnPosition;

    protected override void Awake()
    {
        base.Awake();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnDestroy();
    }

    public void SetRespawnPoint(PlayerRespawnPoint point)
    {
        if (point == null)
            return;

        _savedRespawnPosition = point.transform.position;
        _hasSavedRespawnPosition = true;

        Debug.Log($"[RespawnManager] Respawn point saved: {_savedRespawnPosition}", this);
    }

    public void RestartCurrentSceneFromRespawnPoint()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_hasSavedRespawnPosition)
            return;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[RespawnManager] Player not found after scene loaded.", this);
            return;
        }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = _savedRespawnPosition;
        }

        player.transform.position = _savedRespawnPosition;

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null)
            health.ReviveForRespawn();

        WeaponController weapon = FindFirstObjectByType<WeaponController>();
        if (weapon != null)
            weapon.ForceDockForRespawn();

        if (CameraManager.Instance != null)
            CameraManager.Instance.SnapToTargetForRespawn(player.transform);
    }

    public void ClearRespawnPoint()
    {
        _hasSavedRespawnPosition = false;
        _savedRespawnPosition = default;
    }
}