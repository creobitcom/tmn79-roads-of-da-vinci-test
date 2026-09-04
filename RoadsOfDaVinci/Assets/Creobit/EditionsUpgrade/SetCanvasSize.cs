using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetCanvasSize : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    private RectTransform _canvasRect;
    private float _cachedWidth;
    private float _cachedHeight;
    private void Awake()
    {
        UpdateSize();
    }

    public void UpdateSize()
    {
        _canvasRect ??= _canvas.GetComponent<RectTransform>();
        _canvasRect.sizeDelta = new Vector2(Screen.width, Screen.height);
        _cachedWidth = Screen.width;
        _cachedHeight = Screen.height;
    }

    private void Update()
    {
        if (_cachedHeight!=Screen.height || _cachedWidth!=Screen.width)
        {
            UpdateSize();
        }
    }
}
