using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerRespawnTrigger : MonoBehaviour
{
    [SerializeField] private PlayerRespawnPoint _respawnPoint;
    [SerializeField] private bool _disableAfterActivated = true;

    private bool _activated;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (_respawnPoint == null)
            _respawnPoint = GetComponentInChildren<PlayerRespawnPoint>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_activated)
            return;

        if (!other.TryGetComponent(out PlayerController _))
            return;

        if (_respawnPoint == null)
        {
            Debug.LogWarning("[PlayerRespawnTrigger] Respawn point is missing.", this);
            return;
        }

        RespawnManager.Instance?.SetRespawnPoint(_respawnPoint);

        _activated = true;

        if (_disableAfterActivated)
            gameObject.SetActive(false);
    }
}