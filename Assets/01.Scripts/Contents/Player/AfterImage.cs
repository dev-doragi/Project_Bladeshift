using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class AfterImage : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _bodySpriteRenderer;
    [SerializeField] private float _spawnInterval = 0.03f;
    [SerializeField] private float _lifeTime = 0.15f;
    [SerializeField] private Color _afterImageColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private int _sortingOrderOffset = -1;

    private PlayerController _controller;
    private float _spawnTimer;

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();

        if (_bodySpriteRenderer == null)
            _bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (_controller == null || _bodySpriteRenderer == null)
            return;

        if (!_controller.IsDashing)
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
        if (_bodySpriteRenderer.sprite == null)
            return;

        GameObject afterImageObject = new GameObject("AfterImage");
        Transform afterImageTransform = afterImageObject.transform;

        afterImageTransform.position = _bodySpriteRenderer.transform.position;
        afterImageTransform.rotation = _bodySpriteRenderer.transform.rotation;
        afterImageTransform.localScale = _bodySpriteRenderer.transform.lossyScale;

        SpriteRenderer afterImageRenderer = afterImageObject.AddComponent<SpriteRenderer>();
        afterImageRenderer.sprite = _bodySpriteRenderer.sprite;
        afterImageRenderer.flipX = _bodySpriteRenderer.flipX;
        afterImageRenderer.sortingLayerID = _bodySpriteRenderer.sortingLayerID;
        afterImageRenderer.sortingOrder = _bodySpriteRenderer.sortingOrder + _sortingOrderOffset;
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