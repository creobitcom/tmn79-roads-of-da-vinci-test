using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SetImageAlpha : MonoBehaviour
{
    private Image _image;

    private void Awake()
    {
        _image ??= GetComponent<Image>();

        if (_image == null)
        {
            DestroyImmediate(this);
        }
    }

    public void SetAlpha(float alpha)
    {
        if (_image == null)
        {
            _image = GetComponent<Image>();
        }
        var color = _image.color;
        
        _image.color = new Color(color.r, color.g, color.b, alpha);
    }
}
