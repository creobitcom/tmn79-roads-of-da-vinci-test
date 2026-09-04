using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MovableObjectTaskView : MonoBehaviour
{

    [SerializeField]
    private Image _progressBar;

    [SerializeField]
    private Image _icon;

    [SerializeField]
    private Image _progressCircle;

    [SerializeField]
    private Image _progressCircleBackground;

    [SerializeField]
    private Sprite _circleBackgroundSprite;

    [SerializeField]
    private Sprite _circleFillSprite;

    [SerializeField]
    private float _circleSizeRatio = 1f;

    [SerializeField]
    private float _iconSizeRatio = 0.62f;

    [SerializeField]
    private Color _circleFillColor = new Color32(10, 149, 225, 255);

    [SerializeField]
    private Color _circleBackgroundColor = new Color32(143, 143, 141, 255);

    private static Sprite _generatedCircleSprite;

    private readonly List<GameObject> _hiddenRingParts = new();

    private bool _iconShowsProgress;

    public void Report(float value, bool enableView = true)
    {
        if (enableView)
        {
            gameObject.SetActive(true);
        }

        _progressBar.fillAmount = value;

        if (_iconShowsProgress && _progressCircle != null)
        {
            _progressCircle.fillAmount = value;
        }
    }

    public void SetIcon(Sprite sprite)
    {
        if (sprite == null)
        {
            ClearIcon();

            return;
        }

        var background = _progressCircleBackground != null
            ? _progressCircleBackground
            : CreateRuntimeImage("TaskProgressCircleBack", _circleSizeRatio);

        var circle = _progressCircle != null
            ? _progressCircle
            : CreateRuntimeImage("TaskProgressCircleFill", _circleSizeRatio);

        var icon = _icon != null ? _icon : CreateRuntimeImage("TaskIcon", _iconSizeRatio);

        if (icon == null)
        {
            return;
        }

        _progressCircleBackground = background;
        _progressCircle = circle;
        _icon = icon;

        if (background != null)
        {
            background.sprite = _circleBackgroundSprite != null
                ? _circleBackgroundSprite
                : GetGeneratedCircleSprite();

            background.type = Image.Type.Simple;
            background.color = _circleBackgroundSprite != null ? Color.white : _circleBackgroundColor;
            background.transform.SetAsLastSibling();
            background.gameObject.SetActive(true);
        }

        if (circle != null)
        {
            circle.sprite = _circleFillSprite != null ? _circleFillSprite : GetGeneratedCircleSprite();
            circle.color = _circleFillSprite != null ? Color.white : _circleFillColor;
            circle.type = Image.Type.Filled;
            circle.fillMethod = Image.FillMethod.Radial360;
            circle.fillOrigin = (int)Image.Origin360.Top;
            circle.fillClockwise = true;
            circle.fillAmount = _progressBar != null ? _progressBar.fillAmount : 0f;
            circle.transform.SetAsLastSibling();
            circle.gameObject.SetActive(true);
        }

        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.color = Color.white;
        icon.type = Image.Type.Simple;
        icon.transform.SetAsLastSibling();
        icon.gameObject.SetActive(true);

        _iconShowsProgress = true;

        HideRing();
    }

    public void ClearIcon()
    {
        _iconShowsProgress = false;

        if (_icon != null)
        {
            _icon.gameObject.SetActive(false);
        }

        if (_progressCircle != null)
        {
            _progressCircle.gameObject.SetActive(false);
        }

        if (_progressCircleBackground != null)
        {
            _progressCircleBackground.gameObject.SetActive(false);
        }

        ShowRing();
    }

    private Image CreateRuntimeImage(string objectName, float sizeRatio)
    {
        var rootRect = transform as RectTransform;

        if (rootRect == null)
        {
            rootRect = _progressBar != null ? _progressBar.rectTransform : null;
        }

        if (rootRect == null)
        {
            return null;
        }

        var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)imageObject.transform;
        var side = Mathf.Min(rootRect.rect.width, rootRect.rect.height) * sizeRatio;

        rect.SetParent(rootRect, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(side, side);
        rect.SetAsLastSibling();

        var image = imageObject.GetComponent<Image>();

        image.raycastTarget = false;

        return image;
    }

    private static Sprite GetGeneratedCircleSprite()
    {
        if (_generatedCircleSprite != null)
        {
            return _generatedCircleSprite;
        }

        const int size = 256;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        var center = new Vector2(size * 0.5f, size * 0.5f);
        var radius = size * 0.5f - 1f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                var alpha = Mathf.Clamp01(radius - distance);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        _generatedCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));

        return _generatedCircleSprite;
    }

    private void HideRing()
    {
        if (_hiddenRingParts.Count > 0)
        {
            return;
        }

        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            if (image == null || image == _icon || image == _progressCircle
                || image == _progressCircleBackground || image.gameObject == gameObject
                || !image.gameObject.activeSelf)
            {
                continue;
            }

            _hiddenRingParts.Add(image.gameObject);
            image.gameObject.SetActive(false);
        }
    }

    private void ShowRing()
    {
        foreach (var part in _hiddenRingParts)
        {
            if (part != null)
            {
                part.SetActive(true);
            }
        }

        _hiddenRingParts.Clear();
    }
}
