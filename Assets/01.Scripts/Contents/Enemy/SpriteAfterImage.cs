using System;
using System.Collections;
using UnityEngine;

public class SpriteAfterImage : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _sourceRenderer;
    [SerializeField, Min(0.01f)] private float _spawnInterval = 0.03f;
    [SerializeField, Min(0.01f)] private float _lifeTime = 0.15f;
    [SerializeField] private Color _afterImageColor = new(1f, 1f, 1f, 0.5f);
    [SerializeField] private int _sortingOrderOffset = -1;

    private Func<bool> _isActive;
    private float _spawnTimer;

    public void Initialize(Func<bool> isActive, SpriteRenderer sourceRenderer = null)
    {
        _isActive = isActive;

        if (sourceRenderer != null)
            _sourceRenderer = sourceRenderer;
    }

    public void SetAfterImageColor(Color color)
    {
        _afterImageColor = color;
    }

    private void Awake()
    {
        if (_sourceRenderer == null)
            _sourceRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (_sourceRenderer == null || _isActive == null || !_isActive())
        {
            _spawnTimer = 0f;
            return;
        }

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer < _spawnInterval)
            return;

        _spawnTimer = 0f;
        SpawnAfterImage();
    }

    private void SpawnAfterImage()
    {
        if (_sourceRenderer.sprite == null)
            return;

        GameObject afterImageObject = new("AfterImage");
        Transform afterImageTransform = afterImageObject.transform;

        afterImageTransform.position = _sourceRenderer.transform.position;
        afterImageTransform.rotation = _sourceRenderer.transform.rotation;
        afterImageTransform.localScale = _sourceRenderer.transform.lossyScale;

        SpriteRenderer afterImageRenderer = afterImageObject.AddComponent<SpriteRenderer>();
        afterImageRenderer.sprite = _sourceRenderer.sprite;
        afterImageRenderer.flipX = _sourceRenderer.flipX;
        afterImageRenderer.sortingLayerID = _sourceRenderer.sortingLayerID;
        afterImageRenderer.sortingOrder = _sourceRenderer.sortingOrder + _sortingOrderOffset;
        afterImageRenderer.color = _afterImageColor;

        StartCoroutine(FadeAndDestroy(afterImageRenderer, afterImageObject));
    }

    private IEnumerator FadeAndDestroy(SpriteRenderer renderer, GameObject targetObject)
    {
        float elapsedTime = 0f;
        Color startColor = renderer.color;

        while (elapsedTime < _lifeTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / _lifeTime;
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            renderer.color = color;
            yield return null;
        }

        Destroy(targetObject);
    }
}
