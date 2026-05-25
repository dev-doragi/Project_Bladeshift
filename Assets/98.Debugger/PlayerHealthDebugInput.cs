#if UNITY_EDITOR

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealthDebugInput : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PlayerHealth _playerHealth;

    [Header("Debug Keys")]
    [SerializeField] private Key _damageKey = Key.F1;
    [SerializeField] private Key _healKey = Key.F2;
    [SerializeField] private Key _resetKey = Key.F3;
    [SerializeField] private Key _killKey = Key.F4;

    [Header("Values")]
    [SerializeField] private float _damage = 1f;
    [SerializeField] private int _healAmount = 1;

    private void Awake()
    {
        if (_playerHealth == null)
            _playerHealth = GetComponent<PlayerHealth>();

        if (_playerHealth == null)
            _playerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    private void Update()
    {
        if (_playerHealth == null)
            return;

        if (Keyboard.current != null && Keyboard.current[_damageKey].wasPressedThisFrame)
        {
            _playerHealth.TakeDamage(CreateDebugDamageData(_damage));
            return;
        }

        if (Keyboard.current != null && Keyboard.current[_healKey].wasPressedThisFrame)
        {
            _playerHealth.Heal(_healAmount);
            return;
        }

        if (Keyboard.current != null && Keyboard.current[_resetKey].wasPressedThisFrame)
        {
            _playerHealth.ResetHp();
            return;
        }

        if (Keyboard.current != null && Keyboard.current[_killKey].wasPressedThisFrame)
        {
            KillPlayer();
        }
    }

    private void KillPlayer()
    {
        while (!_playerHealth.IsDead && _playerHealth.CurrentHp > 0)
        {
            _playerHealth.TakeDamage(CreateDebugDamageData(_playerHealth.CurrentHp));
        }
    }

    private DamageData CreateDebugDamageData(float damage)
    {
        return new DamageData
        {
            Damage = damage
        };
    }
}

#endif