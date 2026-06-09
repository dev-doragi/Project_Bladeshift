using DG.Tweening;
using UnityEngine;

public class BelialEyeTracker : MonoBehaviour
{
    [SerializeField] private Transform _eyesRoot;
    [SerializeField] private Transform _leftEye;
    [SerializeField] private Transform _rightEye;
    [SerializeField, Min(0f)] private float _maxLocalOffsetX = 0.2f;
    [SerializeField, Min(0f)] private float _maxLocalOffsetY = 0.1f;
    [SerializeField, Min(0.01f)] private float _followTweenDuration = 0.12f;
    [SerializeField] private float _eyeAngle = 30f;
    [SerializeField, Min(0.01f)] private float _eyeFlipDuration = 0.15f;

    private Transform _player;
    private Vector3 _baseEyesLocalPos;
    private Quaternion _baseLeftEyeRot;
    private Quaternion _baseRightEyeRot;
    private Tween _followTween;
    private Tween _leftEyeTween;
    private Tween _rightEyeTween;
    private SpriteRenderer[] _eyeRenderers;

    private void Awake()
    {
        CacheDefaults();
    }

    public void CacheDefaults()
    {
        if (_eyesRoot != null)
            _baseEyesLocalPos = _eyesRoot.localPosition;

        if (_leftEye != null)
            _baseLeftEyeRot = _leftEye.localRotation;

        if (_rightEye != null)
            _baseRightEyeRot = _rightEye.localRotation;

        if (_eyesRoot != null)
            _eyeRenderers = _eyesRoot.GetComponentsInChildren<SpriteRenderer>(true);

        SetGroggyEyes(false);
    }

    private void LateUpdate()
    {
        if (_eyesRoot == null)
            return;

        if (_player == null)
        {
            PlayerController foundPlayer = FindFirstObjectByType<PlayerController>();
            if (foundPlayer != null)
                _player = foundPlayer.transform;
        }

        if (_player == null)
            return;

        Vector3 toPlayer = _player.position - _eyesRoot.position;
        Vector2 normalized = toPlayer.sqrMagnitude > 0.0001f ? ((Vector2)toPlayer).normalized : Vector2.zero;

        Vector3 targetLocal = _baseEyesLocalPos + new Vector3(
            normalized.x * _maxLocalOffsetX,
            normalized.y * _maxLocalOffsetY,
            0f);

        _followTween?.Kill();
        _followTween = _eyesRoot.DOLocalMove(targetLocal, _followTweenDuration).SetEase(Ease.OutSine);
    }

    public void SetGroggyEyes(bool isGroggy)
    {
        if (_leftEye == null || _rightEye == null)
            return;

        float leftZ = isGroggy ? -_eyeAngle : _eyeAngle;
        float rightZ = isGroggy ? _eyeAngle : -_eyeAngle;

        _leftEyeTween?.Kill();
        _rightEyeTween?.Kill();

        _leftEyeTween = _leftEye.DOLocalRotate(new Vector3(0f, 0f, leftZ), _eyeFlipDuration).SetEase(Ease.OutSine);
        _rightEyeTween = _rightEye.DOLocalRotate(new Vector3(0f, 0f, rightZ), _eyeFlipDuration).SetEase(Ease.OutSine);
    }

    public void SetEyesVisible(bool visible)
    {
        if (_eyeRenderers == null || _eyeRenderers.Length == 0)
            return;

        float alpha = visible ? 1f : 0f;
        for (int i = 0; i < _eyeRenderers.Length; i++)
        {
            SpriteRenderer sr = _eyeRenderers[i];
            if (sr == null)
                continue;

            Color c = sr.color;
            c.r = 1f;
            c.g = 1f;
            c.b = 1f;
            c.a = alpha;
            sr.color = c;
        }
    }

    public void ForceEyeColorWhite()
    {
        if (_eyeRenderers == null || _eyeRenderers.Length == 0)
            return;

        for (int i = 0; i < _eyeRenderers.Length; i++)
        {
            SpriteRenderer sr = _eyeRenderers[i];
            if (sr == null)
                continue;

            Color c = sr.color;
            c.r = 1f;
            c.g = 1f;
            c.b = 1f;
            sr.color = c;
        }
    }

    private void OnDisable()
    {
        _followTween?.Kill();
        _leftEyeTween?.Kill();
        _rightEyeTween?.Kill();
    }
}
