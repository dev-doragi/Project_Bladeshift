using UnityEngine;

public class BelialEyeTracker : MonoBehaviour
{
    [SerializeField] private Transform _eyesRoot;
    [SerializeField] private Transform _player;
    [SerializeField] private Vector2 _maxLocalOffset = new Vector2(0.18f, 0.08f);
    [SerializeField] private float _deadZone = 0.15f;
    [SerializeField] private float _followSpeed = 8f;

    private Vector3 _originLocalPosition;

    private void Awake()
    {
        if (_eyesRoot != null)
            _originLocalPosition = _eyesRoot.localPosition;
    }

    private void Start()
    {
        TryFindPlayer();
    }

    private void LateUpdate()
    {
        if (_eyesRoot == null)
            return;

        if (_player == null)
            TryFindPlayer();

        if (_player == null)
            return;

        Vector2 direction = _player.position - _eyesRoot.position;

        if (direction.magnitude < _deadZone)
        {
            MoveEyes(Vector2.zero);
            return;
        }

        direction.Normalize();

        Vector2 targetOffset = new Vector2(
            direction.x * _maxLocalOffset.x,
            direction.y * _maxLocalOffset.y
        );

        MoveEyes(targetOffset);
    }

    private void MoveEyes(Vector2 targetOffset)
    {
        Vector3 targetLocalPosition = _originLocalPosition + new Vector3(targetOffset.x, targetOffset.y, 0f);

        _eyesRoot.localPosition = Vector3.Lerp(
            _eyesRoot.localPosition,
            targetLocalPosition,
            Time.deltaTime * _followSpeed
        );
    }

    private void TryFindPlayer()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();

        if (player != null)
            _player = player.transform;
    }
}