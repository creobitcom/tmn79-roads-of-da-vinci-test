using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ChangeCanvasVisibilityButton : MonoBehaviour
{
    [Header("Paramaters")]
    [SerializeField]
    [Range(5, 15)]
    private int _neededClicksAmountToShow = 10;
    [Range(1, 5)]
    [SerializeField]
    private float _secondsToResetClicksAmount = 2;
    [Range(1, 5)]
    [SerializeField]
    private float _cooldownSecondsToHide = 1;

    [Header("Componets")]
    [SerializeField]
    private Button _button;
    [SerializeField]
    private CanvasGroup _canvasGroup;
    // This button lives inside _canvasGroup, so it needs its own group with
    // IgnoreParentGroups enabled to stay clickable while the canvas is hidden.
    [SerializeField]
    private CanvasGroup _buttonCanvasGroup;

    private int _clicksAmount;

    private TimeSpan? _lastShowTime;

    private TimeSpan? _lastClickTime;

    private void OnValidate()
    {
        _button ??= GetComponent<Button>();
        _buttonCanvasGroup ??= GetComponent<CanvasGroup>();
    }

    private void OnDestroy()
    {
        _button.onClick.RemoveListener(OnButtonClicked);
    }

    private void Awake()
    {
        _clicksAmount = 0;

        _button.onClick.AddListener(OnButtonClicked);

        SetCanvasVisible(_canvasGroup.alpha > 0);
    }

    private void OnButtonClicked()
    {
        bool canvasIsVisible = _canvasGroup.alpha > 0;

        if (canvasIsVisible)
        {
            TryHideCanvas();
            return;
        }

        TryShowCanvas();
    }

    private void TryHideCanvas()
    {
        if (!CanvasCanBeHidden())
        {
            return;
        }

        SetCanvasVisible(false);
    }

    private void TryShowCanvas()
    {
        if (NeedResetClicksAmount())
        {
            _clicksAmount = 0;
        }

        _clicksAmount += 1;

        _lastClickTime = DateTime.UtcNow.TimeOfDay;

        if (_clicksAmount == _neededClicksAmountToShow)
        {
            SetCanvasVisible(true);

            _clicksAmount = 0;

            _lastShowTime = DateTime.UtcNow.TimeOfDay;
        }
    }

    private void SetCanvasVisible(bool isVisible)
    {
        _canvasGroup.alpha = isVisible ? 1 : 0;
        _canvasGroup.interactable = isVisible;
        _canvasGroup.blocksRaycasts = isVisible;

        _buttonCanvasGroup.alpha = isVisible ? 1 : 0;
    }

    private bool NeedResetClicksAmount()
    {
        return !_lastClickTime.HasValue || (DateTime.UtcNow.TimeOfDay - _lastClickTime.Value).TotalSeconds > _secondsToResetClicksAmount;
    }

    private bool CanvasCanBeHidden()
    {
        return !_lastShowTime.HasValue || (DateTime.UtcNow.TimeOfDay - _lastShowTime.Value).TotalSeconds > _cooldownSecondsToHide;
    }
}
