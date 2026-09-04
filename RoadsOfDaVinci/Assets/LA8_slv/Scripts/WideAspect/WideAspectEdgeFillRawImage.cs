using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class WideAspectEdgeFillRawImage : MonoBehaviour
{
    [SerializeField] private RawImage _source;
    [SerializeField] private RectTransform _area;
    [SerializeField] private Material _blurMaterial;
    [SerializeField] [Range(0f, 0.5f)] private float _blur = 0.14f;
    [SerializeField] [Range(0f, 1f)] private float _darken = 0.55f;
    [SerializeField] [Range(0f, 1f)] private float _desaturate = 0.35f;
    [SerializeField] private Vector2 _maxExtend = Vector2.one;

    private static readonly int SpriteRectId = Shader.PropertyToID("_SpriteRect");
    private static readonly int RegionId = Shader.PropertyToID("_Region");
    private static readonly int MaxExtendId = Shader.PropertyToID("_MaxExtend");
    private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int BlurId = Shader.PropertyToID("_Blur");
    private static readonly int DarkenId = Shader.PropertyToID("_Darken");
    private static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");

    private GameObject _fillObject;
    private RectTransform _fillRect;
    private RawImage _fillImage;
    private Material _runtimeMaterial;
    private RenderTexture _cache;
    private (int, Rect, Vector4, Vector4, float, float, float, float, float, int, int) _cacheKey;

    private void Awake()
    {
        if (_source == null)
        {
            _source = GetComponent<RawImage>();
        }

        if (_area == null && _source != null)
        {
            _area = _source.rectTransform.parent as RectTransform;
        }
    }

    private void OnEnable()
    {
        EnsureFill();
        Fit();
    }

    private void OnDisable()
    {
        DestroyFill();
    }

    private void LateUpdate()
    {
        Fit();
    }

    private void OnValidate()
    {
        Fit();
    }

    private bool CanCreateFill()
    {
#if UNITY_EDITOR
        if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(this))
        {
            return false;
        }
#endif
        return gameObject.scene.IsValid();
    }

    private void EnsureFill()
    {
        if (_source == null)
        {
            _source = GetComponent<RawImage>();
        }

        if (_area == null && _source != null)
        {
            _area = _source.rectTransform.parent as RectTransform;
        }

        if (_source == null || _area == null || _blurMaterial == null || _fillObject != null || !CanCreateFill())
        {
            return;
        }

        _fillObject = new GameObject("WideAspectEdgeFillRawImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage))
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = _source.gameObject.layer
        };

        _fillRect = (RectTransform)_fillObject.transform;
        _fillRect.SetParent(_area, false);

        _runtimeMaterial = new Material(_blurMaterial) { hideFlags = HideFlags.HideAndDontSave };

        _fillImage = _fillObject.GetComponent<RawImage>();
        _fillImage.raycastTarget = false;
        _fillImage.maskable = _source.maskable;
        _fillImage.enabled = false;
    }

    private void Fit()
    {
        if (_fillImage == null || _runtimeMaterial == null)
        {
            return;
        }

        if (_source == null || _area == null || !_source.enabled || _source.texture == null ||
            !_source.gameObject.activeInHierarchy)
        {
            _fillImage.enabled = false;
            return;
        }

        Rect area = _area.rect;
        Rect art = _source.rectTransform.rect;

        if (area.width <= 0.0001f || area.height <= 0.0001f || art.width <= 0.0001f || art.height <= 0.0001f)
        {
            _fillImage.enabled = false;
            return;
        }

        float regionSizeX = Mathf.Clamp01(art.width / area.width);
        float regionSizeY = Mathf.Clamp01(art.height / area.height);
        float regionMinX = (1f - regionSizeX) * 0.5f;
        float regionMinY = (1f - regionSizeY) * 0.5f;
        float falloff = Mathf.Max(regionMinX / regionSizeX, regionMinY / regionSizeY);

        if (falloff <= 0.002f)
        {
            _fillImage.enabled = false;
            return;
        }

        Rect uv = _source.uvRect;

        _fillImage.color = _source.color;
        _fillImage.uvRect = new Rect(0f, 0f, 1f, 1f);

        _runtimeMaterial.SetVector(SpriteRectId, new Vector4(uv.x, uv.y, uv.width, uv.height));
        _runtimeMaterial.SetVector(RegionId, new Vector4(regionMinX, regionMinY, regionSizeX, regionSizeY));
        _runtimeMaterial.SetVector(MaxExtendId, new Vector4(Mathf.Max(_maxExtend.x, 1e-4f), Mathf.Max(_maxExtend.y, 1e-4f), 0f, 0f));
        _runtimeMaterial.SetFloat(FalloffId, falloff);
        _runtimeMaterial.SetFloat(AspectId, art.height / art.width);
        _runtimeMaterial.SetFloat(BlurId, _blur);
        _runtimeMaterial.SetFloat(DarkenId, _darken);
        _runtimeMaterial.SetFloat(DesaturateId, _desaturate);

        var cacheKey = (_source.texture.GetInstanceID(), uv,
            new Vector4(regionMinX, regionMinY, regionSizeX, regionSizeY),
            new Vector4(_maxExtend.x, _maxExtend.y, 0f, 0f),
            falloff, art.height / art.width, _blur, _darken, _desaturate, Screen.width, Screen.height);

        if (_cache == null || !cacheKey.Equals(_cacheKey))
        {
            _cacheKey = cacheKey;
            WideAspectEdgeFillCache.Render(ref _cache, _source.texture, _runtimeMaterial);
        }

        _fillImage.texture = _cache;

        if (_fillRect.parent != _area)
        {
            _fillRect.SetParent(_area, false);
        }

        _fillRect.localRotation = Quaternion.identity;
        _fillRect.localScale = Vector3.one;
        _fillRect.anchorMin = Vector2.zero;
        _fillRect.anchorMax = Vector2.one;
        _fillRect.pivot = new Vector2(0.5f, 0.5f);
        _fillRect.offsetMin = Vector2.zero;
        _fillRect.offsetMax = Vector2.zero;

        int targetIndex = _source.rectTransform.parent == _area
            ? _source.rectTransform.GetSiblingIndex() + 1
            : 0;

        if (_fillRect.GetSiblingIndex() != targetIndex)
        {
            _fillRect.SetSiblingIndex(targetIndex);
        }

        _fillImage.enabled = true;
    }

    private void DestroyFill()
    {
        WideAspectEdgeFillCache.Release(ref _cache);
        _cacheKey = default;
        DestroyObject(_runtimeMaterial);
        DestroyObject(_fillObject);
        _runtimeMaterial = null;
        _fillObject = null;
        _fillRect = null;
        _fillImage = null;
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
