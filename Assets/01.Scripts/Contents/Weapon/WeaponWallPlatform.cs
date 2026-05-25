using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponWallPlatform : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponStateMachine _stateMachine;
    [SerializeField] private GameObject _platformObject;

    [Header("Safety")]
    [SerializeField] private float _releaseClearance = 0.25f;
    [SerializeField] private float _releaseDelay = 0.08f;

    private readonly List<IgnoredCollisionPair> _ignoredPairs = new();

    private Collider2D[] _platformColliders;
    private Collider2D[] _playerColliders;
    private PlayerController _playerController;
    private GameObject _playerObject;

    private bool _isIgnoringPlayerCollision;
    private float _clearTimer;

    private struct IgnoredCollisionPair
    {
        public Collider2D PlatformCollider;
        public Collider2D PlayerCollider;

        public IgnoredCollisionPair(Collider2D platformCollider, Collider2D playerCollider)
        {
            PlatformCollider = platformCollider;
            PlayerCollider = playerCollider;
        }
    }

    private void Awake()
    {
        if (_stateMachine == null)
            _stateMachine = GetComponent<WeaponStateMachine>();

        CachePlatformColliders();
        SetPlatformActive(false);
        SetPlatformCollisionEnabled(false);
    }

    private void OnEnable()
    {
        if (_stateMachine != null)
            _stateMachine.StateChanged += OnWeaponStateChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (_stateMachine != null)
            _stateMachine.StateChanged -= OnWeaponStateChanged;

        DeactivatePlatform();
    }

    private void Update()
    {
        if (_stateMachine == null || !_stateMachine.IsPinnedToWall)
        {
            DeactivatePlatform();
            return;
        }

        if (!_isIgnoringPlayerCollision) return;

        EnsurePlayerColliders();
        ApplyIgnoredCollisionPairs();

        if (!IsPlayerSafelyClear())
        {
            _clearTimer = 0f;
            return;
        }

        _clearTimer += Time.deltaTime;

        if (_clearTimer < _releaseDelay) return;

        ReleaseIgnoredCollisionPairs();
        _isIgnoringPlayerCollision = false;
        _clearTimer = 0f;
    }

    private void OnWeaponStateChanged(WeaponState previousState, WeaponState newState)
    {
        Refresh();
    }

    private void Refresh()
    {
        bool isPinnedToWall = _stateMachine != null && _stateMachine.IsPinnedToWall;

        if (!isPinnedToWall)
        {
            DeactivatePlatform();
            return;
        }

        ActivatePlatformWithPlayerCollisionIgnored();
    }

    private void ActivatePlatformWithPlayerCollisionIgnored()
    {
        SetPlatformActive(true);
        CachePlatformColliders();
        SetPlatformCollisionEnabled(true);

        EnsurePlayerColliders();

        _isIgnoringPlayerCollision = true;
        _clearTimer = 0f;

        ApplyIgnoredCollisionPairs();
    }

    private void DeactivatePlatform()
    {
        ReleaseIgnoredCollisionPairs();

        _isIgnoringPlayerCollision = false;
        _clearTimer = 0f;

        SetPlatformCollisionEnabled(false);
        SetPlatformActive(false);
    }

    private void SetPlatformActive(bool active)
    {
        if (_platformObject != null)
            _platformObject.SetActive(active);
    }

    private void SetPlatformCollisionEnabled(bool enabled)
    {
        if (_platformColliders == null) return;

        for (int i = 0; i < _platformColliders.Length; i++)
        {
            Collider2D col = _platformColliders[i];
            if (col == null) continue;

            col.enabled = enabled;
        }
    }

    private void CachePlatformColliders()
    {
        if (_platformObject == null)
        {
            _platformColliders = System.Array.Empty<Collider2D>();
            return;
        }

        _platformColliders = _platformObject.GetComponentsInChildren<Collider2D>(true);
    }

    private void EnsurePlayerColliders()
    {
        if (_playerColliders != null && _playerColliders.Length > 0)
            return;

        if (_playerController == null)
            _playerController = FindFirstObjectByType<PlayerController>();

        if (_playerController != null)
        {
            _playerObject = _playerController.gameObject;
        }
        else if (_playerObject == null)
        {
            _playerObject = GameObject.FindWithTag("Player");
        }

        if (_playerObject == null)
        {
            _playerColliders = System.Array.Empty<Collider2D>();
            return;
        }

        _playerColliders = _playerObject.GetComponentsInChildren<Collider2D>(true);
    }

    private void ApplyIgnoredCollisionPairs()
    {
        if (_platformColliders == null || _platformColliders.Length == 0) return;
        if (_playerColliders == null || _playerColliders.Length == 0) return;

        for (int i = 0; i < _platformColliders.Length; i++)
        {
            Collider2D platformCollider = _platformColliders[i];
            if (platformCollider == null) continue;
            if (!platformCollider.enabled) continue;

            for (int j = 0; j < _playerColliders.Length; j++)
            {
                Collider2D playerCollider = _playerColliders[j];
                if (playerCollider == null) continue;
                if (!playerCollider.enabled) continue;

                if (HasIgnoredPair(platformCollider, playerCollider)) continue;

                Physics2D.IgnoreCollision(platformCollider, playerCollider, true);
                _ignoredPairs.Add(new IgnoredCollisionPair(platformCollider, playerCollider));
            }
        }
    }

    private bool HasIgnoredPair(Collider2D platformCollider, Collider2D playerCollider)
    {
        for (int i = 0; i < _ignoredPairs.Count; i++)
        {
            IgnoredCollisionPair pair = _ignoredPairs[i];

            if (pair.PlatformCollider == platformCollider && pair.PlayerCollider == playerCollider)
                return true;
        }

        return false;
    }

    private void ReleaseIgnoredCollisionPairs()
    {
        for (int i = 0; i < _ignoredPairs.Count; i++)
        {
            IgnoredCollisionPair pair = _ignoredPairs[i];

            if (pair.PlatformCollider == null) continue;
            if (pair.PlayerCollider == null) continue;

            Physics2D.IgnoreCollision(pair.PlatformCollider, pair.PlayerCollider, false);
        }

        _ignoredPairs.Clear();
    }

    private bool IsPlayerSafelyClear()
    {
        if (_platformColliders == null || _platformColliders.Length == 0)
            return true;

        if (_playerColliders == null || _playerColliders.Length == 0)
            return true;

        for (int i = 0; i < _platformColliders.Length; i++)
        {
            Collider2D platformCollider = _platformColliders[i];
            if (platformCollider == null) continue;
            if (!platformCollider.enabled) continue;

            for (int j = 0; j < _playerColliders.Length; j++)
            {
                Collider2D playerCollider = _playerColliders[j];
                if (playerCollider == null) continue;
                if (!playerCollider.enabled) continue;

                ColliderDistance2D distance = platformCollider.Distance(playerCollider);

                if (distance.isOverlapped)
                    return false;

                if (distance.distance <= _releaseClearance)
                    return false;
            }
        }

        return true;
    }
}