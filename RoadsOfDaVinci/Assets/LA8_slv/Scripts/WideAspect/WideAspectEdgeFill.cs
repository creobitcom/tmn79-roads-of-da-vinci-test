using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public sealed class WideAspectEdgeFill : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _source;
    [SerializeField] private Material _blurMaterial;
    [SerializeField] private Camera _camera;
    [SerializeField] [Range(0f, 0.5f)] private float _blur = 0.14f;
    [SerializeField] [Range(0f, 1f)] private float _darken = 0.55f;
    [SerializeField] [Range(0f, 1f)] private float _desaturate = 0.35f;
    [SerializeField] [Range(1f, 1.2f)] private float _overscan = 1.02f;
    [SerializeField] private Vector2 _maxExtend = Vector2.one;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int SpriteRectId = Shader.PropertyToID("_SpriteRect");
    private static readonly int RegionId = Shader.PropertyToID("_Region");
    private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int BlurId = Shader.PropertyToID("_Blur");
    private static readonly int DarkenId = Shader.PropertyToID("_Darken");
    private static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");
    private static readonly int MaxExtendId = Shader.PropertyToID("_MaxExtend");

    private GameObject _fillObject;
    private Transform _fillTransform;
    private MeshRenderer _fillRenderer;
    private Material _runtimeMaterial;
    private Material _displayMaterial;
    private Mesh _mesh;
    private RenderTexture _cache;
    private (int, Vector4, Vector4, Vector4, float, float, float, float, float, int, int) _cacheKey;
    private Texture _cacheSource;
    private bool _cacheDirty;

    public void Setup(Material blurMaterial, float blur, Camera targetCamera)
    {
        _blurMaterial = blurMaterial;
        _blur = blur;
        _camera = targetCamera;
        EnsureFill();
        Fit();
    }

    private void Awake()
    {
        if (_source == null)
        {
            _source = GetComponent<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        EnsureFill();
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        Fit();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        DestroyFill();
    }

    private void OnValidate()
    {
        Fit();
    }

    private void LateUpdate()
    {
        if (_runtimeMaterial == null || _displayMaterial == null || _cacheSource == null)
        {
            return;
        }

        if (_cacheDirty)
        {
            _cacheDirty = false;
            WideAspectEdgeFillCache.Render(ref _cache, _cacheSource, _runtimeMaterial);
            _displayMaterial.mainTexture = _cache;
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        Camera target = _camera != null ? _camera : Camera.main;
        if (renderingCamera != target)
        {
            return;
        }

        Fit();
    }

    private void EnsureFill()
    {
        if (_source == null)
        {
            _source = GetComponent<SpriteRenderer>();
        }

        if (_source == null || _blurMaterial == null || _fillObject != null || !CanCreateFill())
        {
            return;
        }

        _mesh = new Mesh
        {
            name = "WideAspectEdgeFillQuad",
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            },
            triangles = new[] { 0, 2, 1, 2, 3, 1 }
        };
        _mesh.RecalculateBounds();

        _fillObject = new GameObject("WideAspectEdgeFill")
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = _source.gameObject.layer
        };

        _fillTransform = _fillObject.transform;
        _fillObject.AddComponent<MeshFilter>().sharedMesh = _mesh;

        _runtimeMaterial = new Material(_blurMaterial) { hideFlags = HideFlags.HideAndDontSave };

        Shader displayShader = Shader.Find("LA8/WideAspectEdgeFillDisplay");
        _displayMaterial = displayShader != null
            ? new Material(displayShader) { hideFlags = HideFlags.HideAndDontSave }
            : null;

        _fillRenderer = _fillObject.AddComponent<MeshRenderer>();
        _fillRenderer.sharedMaterial = _displayMaterial != null ? _displayMaterial : _runtimeMaterial;
        _fillRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _fillRenderer.receiveShadows = false;
        _fillRenderer.lightProbeUsage = LightProbeUsage.Off;
        _fillRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _fillRenderer.allowOcclusionWhenDynamic = false;
        _fillRenderer.enabled = false;
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

    private void Fit()
    {
        if (_fillRenderer == null || _runtimeMaterial == null)
        {
            return;
        }

        if (_source == null || !_source.enabled || _source.sprite == null || !_source.gameObject.activeInHierarchy)
        {
            _fillRenderer.enabled = false;
            return;
        }

        Camera cam = _camera != null ? _camera : Camera.main;
        if (cam == null || !cam.orthographic)
        {
            _fillRenderer.enabled = false;
            return;
        }

        float viewHeight = cam.orthographicSize * 2f * _overscan;
        float viewWidth = viewHeight * cam.aspect;
        if (viewWidth <= 0.0001f || viewHeight <= 0.0001f)
        {
            _fillRenderer.enabled = false;
            return;
        }

        Vector3 camPosition = cam.transform.position;
        Bounds bounds = _source.bounds;
        float viewMinX = camPosition.x - viewWidth * 0.5f;
        float viewMinY = camPosition.y - viewHeight * 0.5f;

        if (bounds.max.x <= viewMinX || bounds.min.x >= viewMinX + viewWidth
            || bounds.max.y <= viewMinY || bounds.min.y >= viewMinY + viewHeight)
        {
            _fillRenderer.enabled = false;
            return;
        }

        float regionMinX = (bounds.min.x - viewMinX) / viewWidth;
        float regionMinY = (bounds.min.y - viewMinY) / viewHeight;
        float regionSizeX = bounds.size.x / viewWidth;
        float regionSizeY = bounds.size.y / viewHeight;

        if (regionSizeX <= 0.0001f || regionSizeY <= 0.0001f)
        {
            _fillRenderer.enabled = false;
            return;
        }

        float gapLeft = regionMinX / regionSizeX;
        float gapRight = (1f - regionMinX - regionSizeX) / regionSizeX;
        float gapBottom = regionMinY / regionSizeY;
        float gapTop = (1f - regionMinY - regionSizeY) / regionSizeY;
        float falloff = Mathf.Max(Mathf.Max(gapLeft, gapRight), Mathf.Max(gapBottom, gapTop));

        if (falloff <= 0.002f)
        {
            _fillRenderer.enabled = false;
            return;
        }

        Sprite sprite = _source.sprite;
        Texture texture = sprite.texture;
        Rect textureRect = sprite.textureRect;
        Vector4 spriteRect = new Vector4(
            textureRect.x / texture.width,
            textureRect.y / texture.height,
            textureRect.width / texture.width,
            textureRect.height / texture.height);

        _runtimeMaterial.SetTexture(MainTexId, texture);
        _runtimeMaterial.SetVector(SpriteRectId, spriteRect);
        _runtimeMaterial.SetVector(RegionId, new Vector4(regionMinX, regionMinY, regionSizeX, regionSizeY));
        _runtimeMaterial.SetFloat(FalloffId, falloff);
        _runtimeMaterial.SetFloat(AspectId, bounds.size.y / bounds.size.x);
        _runtimeMaterial.SetFloat(BlurId, _blur);
        _runtimeMaterial.SetFloat(DarkenId, _darken);
        _runtimeMaterial.SetFloat(DesaturateId, _desaturate);
        _runtimeMaterial.SetVector(MaxExtendId, new Vector4(Mathf.Max(_maxExtend.x, 1e-4f), Mathf.Max(_maxExtend.y, 1e-4f), 0f, 0f));

        if (_displayMaterial != null)
        {
            var cacheKey = (texture.GetInstanceID(), spriteRect,
                new Vector4(regionMinX, regionMinY, regionSizeX, regionSizeY),
                new Vector4(_maxExtend.x, _maxExtend.y, 0f, 0f),
                falloff, bounds.size.y / bounds.size.x, _blur, _darken, _desaturate, Screen.width, Screen.height);

            if (_cache == null || !cacheKey.Equals(_cacheKey))
            {
                _cacheKey = cacheKey;
                _cacheSource = texture;
                _cacheDirty = true;
            }
        }

        _fillTransform.SetPositionAndRotation(
            new Vector3(camPosition.x, camPosition.y, _source.transform.position.z),
            cam.transform.rotation);
        _fillTransform.localScale = new Vector3(viewWidth, viewHeight, 1f);

        _fillRenderer.sortingLayerID = _source.sortingLayerID;
        _fillRenderer.sortingOrder = _source.sortingOrder + 1;
        _fillRenderer.enabled = true;
    }

    private void DestroyFill()
    {
        WideAspectEdgeFillCache.Release(ref _cache);
        _cacheKey = default;
        _cacheSource = null;
        _cacheDirty = false;
        DestroyObject(_runtimeMaterial);
        DestroyObject(_displayMaterial);
        DestroyObject(_mesh);
        DestroyObject(_fillObject);
        _runtimeMaterial = null;
        _displayMaterial = null;
        _mesh = null;
        _fillObject = null;
        _fillTransform = null;
        _fillRenderer = null;
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
            return;
        }

        DestroyImmediate(target);
    }
}
