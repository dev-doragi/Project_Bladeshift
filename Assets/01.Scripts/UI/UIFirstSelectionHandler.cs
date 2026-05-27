using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIFirstSelectionHandler : MonoBehaviour
{
    [SerializeField] private Selectable _firstSelectable;
    [SerializeField] private bool _selectOnEnable = true;
    [SerializeField] private bool _onlyGamepadMode = true;

    private Coroutine _selectRoutine;

    private void OnEnable()
    {
        if (!_selectOnEnable)
            return;

        SelectFirst();
    }

    public void SelectFirst()
    {
        if (_selectRoutine != null)
            StopCoroutine(_selectRoutine);

        _selectRoutine = StartCoroutine(SelectFirstRoutine());
    }

    private IEnumerator SelectFirstRoutine()
    {
        yield return null;

        if (_firstSelectable == null)
            yield break;

        if (!_firstSelectable.gameObject.activeInHierarchy || !_firstSelectable.interactable)
            yield break;

        if (_onlyGamepadMode && !IsGamepadMode())
            yield break;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(_firstSelectable.gameObject);
    }

    private bool IsGamepadMode()
    {
        return InputDeviceTracker.IsGamepad;
    }
}
