using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class RealtimeLevelShadows : MonoBehaviour
{
    public enum ShadowProjectionMode
    {
        GroundedShear = 0,
        SimpleOffset = 1
    }

    public enum ShadowPreset
    {
        Custom = 0,
        BottomLeft_Natural = 1,
        BottomRight_Standard = 2,
        Left_WarmSunset = 3,
        Right_Morning = 4,
        Overhead_ShortNoon = 5
    }

    private sealed class CasterProxy
    {
        public int SourceId;
        public Renderer Source;
        public Renderer Shadow;
        public List<Renderer> ExtraShadows;
        public SkinnedMeshRenderer SkinnedSource;
        public MeshFilter BakedFilter;
        public Mesh BakedMesh;
        public bool IsUnit;
        public bool IsFlyingUnit;
        public SortingGroup SourceSortingGroup;
    }

    [Header("Feature Toggles")]
    public bool enableUnitShadows = true;
    public bool enableFlyingUnitShadows = true;
    public bool enableAssetShadows = false;

    [Header("Sorting")]
    public bool unitShadowAboveAssets = true;
    public int unitShadowSortingOffset = -1;

    [Header("Presets & Auto-Sync")]
    public ShadowPreset preset = ShadowPreset.Custom;
    public bool syncWithDirectionalLight;

    [Header("Main Settings (Assets / Buildings)")]
    [Range(0f, 360f)] public float angle = 235f;
    [Range(0f, 2f)] public float length = 0.595f;
    [Range(-2f, 2f)] public float flatten = -1.5f;
    [Range(-2f, 2f)] public float skew = 0.06f;
    [Range(-1f, 1f)] public float taper = -0.426f;
    [Range(0.2f, 3f)] public float scaleX = 0.96f;
    [Range(0f, 1f)] public float softness = 0.678f;
    [Range(0f, 1f)] public float tipSoftness = 1f;
    [Range(0f, 1f)] public float tipFade = 0.997f;
    [Range(0f, 1f)] public float alpha = 0.297f;
    public Color shadowColor = new Color(0.04f, 0.04f, 0.08f, 1f);
    public Vector2 manualOffset = new Vector2(-0.09f, 0f);
    public ShadowProjectionMode projectionMode = ShadowProjectionMode.GroundedShear;

    [Header("Unit Specific Settings")]
    public bool customUnitSettings = true;
    [Range(0f, 360f)] public float unitAngle = 255f;
    [Range(0f, 2f)] public float unitLength = 0.388f;
    [Range(-2f, 2f)] public float unitFlatten = -1.49f;
    [Range(-2f, 2f)] public float unitSkew = -0.16f;
    [Range(-1f, 1f)] public float unitTaper = -0.047f;
    [Range(0.2f, 3f)] public float unitScaleX = 0.83f;
    [Range(0f, 1f)] public float unitSoftness = 0.994f;
    [Range(0f, 1f)] public float unitTipSoftness = 0.56f;
    [Range(0f, 1f)] public float unitTipFade = 0.825f;
    [Range(0f, 1f)] public float unitAlpha = 0.201f;
    public Vector2 unitOffset = Vector2.zero;

    [Header("Flying Unit / Drone Settings")]
    public bool customFlyingUnitSettings = true;
    [Range(0f, 360f)] public float flyingUnitAngle = 235f;
    [Range(0f, 2f)] public float flyingUnitOffsetDistance = 0.486f;
    [Range(0.2f, 3f)] public float flyingUnitScale = 0.93f;
    [Range(0.2f, 2f)] public float flyingUnitFlatten = 0.895f;
    [Range(0f, 1f)] public float flyingUnitSoftness = 0.883f;
    [Range(0f, 1f)] public float flyingUnitAlpha = 0.031f;
    public Vector2 flyingUnitManualOffset = Vector2.zero;

    private Material _assetShadowMaterial;
    private Material _unitShadowMaterial;
    private Material _flyingUnitShadowMaterial0;
    private Material _flyingUnitShadowMaterial1;
    private Material _flyingUnitShadowMaterial2;
    private Transform _proxyRoot;
    private readonly List<CasterProxy> _proxies = new List<CasterProxy>();
    private readonly HashSet<int> _proxiedIds = new HashSet<int>();
    private int _scanDelay;
    private ShadowPreset _lastPreset;
    private Light _directionalLight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<RealtimeLevelShadows>() != null)
        {
            return;
        }

        var go = new GameObject(nameof(RealtimeLevelShadows));
        DontDestroyOnLoad(go);
        go.AddComponent<RealtimeLevelShadows>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (_proxyRoot != null)
        {
            _proxyRoot.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_proxyRoot != null)
        {
            _proxyRoot.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        ClearAllProxies();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearAllProxies();
    }

    private void ClearAllProxies()
    {
        for (var i = 0; i < _proxies.Count; i++)
        {
            var proxy = _proxies[i];
            if (proxy.Shadow != null)
            {
                Destroy(proxy.Shadow.gameObject);
            }

            if (proxy.ExtraShadows != null)
            {
                for (var j = 0; j < proxy.ExtraShadows.Count; j++)
                {
                    var extra = proxy.ExtraShadows[j];
                    if (extra != null)
                    {
                        Destroy(extra.gameObject);
                    }
                }
            }

            if (proxy.BakedMesh != null)
            {
                Destroy(proxy.BakedMesh);
            }
        }

        _proxies.Clear();
        _proxiedIds.Clear();

        if (_proxyRoot != null)
        {
            Destroy(_proxyRoot.gameObject);
            _proxyRoot = null;
        }
    }

    private void LateUpdate()
    {
        if (!EnsureMaterials())
        {
            return;
        }

        HandlePresetChange();
        HandleLightSync();
        ApplyShadowSettings();

        if (_scanDelay <= 0)
        {
            ScanCasters();
            _scanDelay = 15;
        }
        else
        {
            _scanDelay--;
        }

        SyncProxies();
    }

    private void HandlePresetChange()
    {
        if (preset == _lastPreset)
        {
            return;
        }

        _lastPreset = preset;
        switch (preset)
        {
            case ShadowPreset.BottomLeft_Natural:
                angle = 225f;
                length = 0.35f;
                flatten = -0.35f;
                skew = 0f;
                taper = 0f;
                softness = 0.35f;
                tipSoftness = 0.25f;
                tipFade = 0.2f;
                alpha = 0.35f;
                break;
            case ShadowPreset.BottomRight_Standard:
                angle = 315f;
                length = 0.35f;
                flatten = -0.35f;
                skew = 0f;
                taper = 0f;
                softness = 0.35f;
                tipSoftness = 0.25f;
                tipFade = 0.2f;
                alpha = 0.35f;
                break;
            case ShadowPreset.Left_WarmSunset:
                angle = 205f;
                length = 0.55f;
                flatten = -0.25f;
                skew = -0.2f;
                taper = 0.1f;
                softness = 0.45f;
                tipSoftness = 0.35f;
                tipFade = 0.3f;
                alpha = 0.38f;
                break;
            case ShadowPreset.Right_Morning:
                angle = 335f;
                length = 0.55f;
                flatten = -0.25f;
                skew = 0.2f;
                taper = 0.1f;
                softness = 0.45f;
                tipSoftness = 0.35f;
                tipFade = 0.3f;
                alpha = 0.38f;
                break;
            case ShadowPreset.Overhead_ShortNoon:
                angle = 270f;
                length = 0.18f;
                flatten = -0.45f;
                skew = 0f;
                taper = -0.2f;
                softness = 0.25f;
                tipSoftness = 0.15f;
                tipFade = 0.1f;
                alpha = 0.3f;
                break;
        }

        if (customUnitSettings && preset != ShadowPreset.Custom)
        {
            unitAngle = angle;
            unitLength = length * 0.85f;
            unitFlatten = flatten;
            unitSkew = skew;
            unitTaper = taper;
            unitSoftness = softness;
            unitTipSoftness = tipSoftness;
            unitTipFade = tipFade;
            unitAlpha = alpha * 0.9f;
        }

        if (customFlyingUnitSettings && preset != ShadowPreset.Custom)
        {
            flyingUnitAngle = angle;
            flyingUnitAlpha = alpha * 0.9f;
            flyingUnitSoftness = Mathf.Clamp01(softness + 0.15f);
        }
    }

    private void HandleLightSync()
    {
        if (!syncWithDirectionalLight)
        {
            return;
        }

        var sceneLight = FindDirectionalLight();
        if (sceneLight == null)
        {
            return;
        }

        var forward = sceneLight.transform.forward;
        var xy = new Vector2(forward.x, forward.y);
        if (xy.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var rad = Mathf.Atan2(xy.y, xy.x);
        var deg = rad * Mathf.Rad2Deg;
        if (deg < 0f)
        {
            deg += 360f;
        }

        angle = deg;
        if (customUnitSettings)
        {
            unitAngle = deg;
        }

        if (customFlyingUnitSettings)
        {
            flyingUnitAngle = deg;
        }
    }

    private Light FindDirectionalLight()
    {
        if (_directionalLight != null)
        {
            return _directionalLight;
        }

        var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < lights.Length; i++)
        {
            if (lights[i].type != LightType.Directional)
            {
                continue;
            }

            _directionalLight = lights[i];
            return _directionalLight;
        }

        return null;
    }

    private bool EnsureMaterials()
    {
        if (_assetShadowMaterial != null && _unitShadowMaterial != null && _flyingUnitShadowMaterial0 != null && _flyingUnitShadowMaterial1 != null && _flyingUnitShadowMaterial2 != null)
        {
            return true;
        }

        var shader = Shader.Find("TMN/PlanarShadow");
        if (shader == null)
        {
            return false;
        }

        _assetShadowMaterial = new Material(shader);
        _assetShadowMaterial.SetFloat("_Cutoff", 0.2f);

        _unitShadowMaterial = new Material(shader);
        _unitShadowMaterial.SetFloat("_Cutoff", 0.01f);
        _unitShadowMaterial.mainTexture = Texture2D.whiteTexture;

        _flyingUnitShadowMaterial0 = new Material(shader);
        _flyingUnitShadowMaterial0.SetFloat("_Cutoff", 0.01f);
        _flyingUnitShadowMaterial0.mainTexture = Texture2D.whiteTexture;

        _flyingUnitShadowMaterial1 = new Material(shader);
        _flyingUnitShadowMaterial1.SetFloat("_Cutoff", 0.01f);
        _flyingUnitShadowMaterial1.mainTexture = Texture2D.whiteTexture;

        _flyingUnitShadowMaterial2 = new Material(shader);
        _flyingUnitShadowMaterial2.SetFloat("_Cutoff", 0.01f);
        _flyingUnitShadowMaterial2.mainTexture = Texture2D.whiteTexture;
        return true;
    }

    private void ApplyShadowSettings()
    {
        var assetRad = angle * Mathf.Deg2Rad;
        var assetDir = new Vector2(Mathf.Cos(assetRad), Mathf.Sin(assetRad));
        var assetShear = new Vector4(assetDir.x * length + skew, assetDir.y * length * flatten, 0f, 0f);
        var assetOffsetVec = new Vector4(manualOffset.x + assetDir.x * length * 0.1f, manualOffset.y + assetDir.y * length * 0.1f, 0f, 0f);
        var assetCol = new Color(shadowColor.r, shadowColor.g, shadowColor.b, alpha);

        _assetShadowMaterial.SetColor("_Color", assetCol);
        _assetShadowMaterial.SetFloat("_Softness", softness);
        _assetShadowMaterial.SetFloat("_TipSoftness", tipSoftness);
        _assetShadowMaterial.SetFloat("_TipFade", tipFade);
        _assetShadowMaterial.SetFloat("_Taper", taper);
        _assetShadowMaterial.SetFloat("_ScaleX", scaleX);
        _assetShadowMaterial.SetFloat("_ProjectionMode", (float)projectionMode);
        _assetShadowMaterial.SetVector("_ShadowOffset", assetOffsetVec);
        _assetShadowMaterial.SetVector("_ShadowShear", assetShear);

        var uAngle = customUnitSettings ? unitAngle : angle;
        var uLength = customUnitSettings ? unitLength : length;
        var uFlatten = customUnitSettings ? unitFlatten : flatten;
        var uSkew = customUnitSettings ? unitSkew : skew;
        var uTaper = customUnitSettings ? unitTaper : taper;
        var uScaleX = customUnitSettings ? unitScaleX : scaleX;
        var uSoftness = customUnitSettings ? unitSoftness : softness;
        var uTipSoftness = customUnitSettings ? unitTipSoftness : tipSoftness;
        var uTipFade = customUnitSettings ? unitTipFade : tipFade;
        var uAlpha = customUnitSettings ? unitAlpha : alpha;
        var uOff = customUnitSettings ? unitOffset : manualOffset;

        var unitRad = uAngle * Mathf.Deg2Rad;
        var unitDir = new Vector2(Mathf.Cos(unitRad), Mathf.Sin(unitRad));
        var unitShear = new Vector4(unitDir.x * uLength + uSkew, unitDir.y * uLength * uFlatten, 0f, 0f);
        var unitOffsetVec = new Vector4(uOff.x + unitDir.x * uLength * 0.1f, uOff.y + unitDir.y * uLength * 0.1f, 0f, 0f);
        var unitCol = new Color(shadowColor.r, shadowColor.g, shadowColor.b, uAlpha);

        _unitShadowMaterial.SetColor("_Color", unitCol);
        _unitShadowMaterial.SetFloat("_Softness", uSoftness);
        _unitShadowMaterial.SetFloat("_TipSoftness", uTipSoftness);
        _unitShadowMaterial.SetFloat("_TipFade", uTipFade);
        _unitShadowMaterial.SetFloat("_Taper", uTaper);
        _unitShadowMaterial.SetFloat("_ScaleX", uScaleX);
        _unitShadowMaterial.SetFloat("_ProjectionMode", (float)projectionMode);
        _unitShadowMaterial.SetVector("_ShadowOffset", unitOffsetVec);
        _unitShadowMaterial.SetVector("_ShadowShear", unitShear);

        var fAngle = customFlyingUnitSettings ? flyingUnitAngle : angle;
        var fDist = customFlyingUnitSettings ? flyingUnitOffsetDistance : (length * 0.8f);
        var fScale = customFlyingUnitSettings ? flyingUnitScale : 1.0f;
        var fFlatten = customFlyingUnitSettings ? flyingUnitFlatten : 0.85f;
        var fSoftness = customFlyingUnitSettings ? flyingUnitSoftness : softness;
        var fAlpha = customFlyingUnitSettings ? flyingUnitAlpha : alpha;
        var fOff = customFlyingUnitSettings ? flyingUnitManualOffset : manualOffset;

        var fRad = fAngle * Mathf.Deg2Rad;
        var fDir = new Vector2(Mathf.Cos(fRad), Mathf.Sin(fRad));
        var fBaseOffset = new Vector2(fOff.x + fDir.x * fDist, fOff.y + fDir.y * fDist);

        var coreAlpha = fAlpha * Mathf.Lerp(1.0f, 0.45f, fSoftness);
        var midAlpha = fAlpha * Mathf.Lerp(0.0f, 0.35f, fSoftness);
        var outerAlpha = fAlpha * Mathf.Lerp(0.0f, 0.20f, fSoftness);

        var coreScale = fScale;
        var midScale = fScale * (1.0f + fSoftness * 0.25f);
        var outerScale = fScale * (1.0f + fSoftness * 0.55f);

        var midOffset = fBaseOffset + fDir * (fSoftness * 0.04f);
        var outerOffset = fBaseOffset + fDir * (fSoftness * 0.08f);

        ConfigureFlyingMaterial(_flyingUnitShadowMaterial0, coreScale, fFlatten, coreAlpha, fBaseOffset);
        ConfigureFlyingMaterial(_flyingUnitShadowMaterial1, midScale, fFlatten, midAlpha, midOffset);
        ConfigureFlyingMaterial(_flyingUnitShadowMaterial2, outerScale, fFlatten, outerAlpha, outerOffset);

        Shader.SetGlobalFloat("_PlanarShadowZBias", 0.04f);
    }

    private void ConfigureFlyingMaterial(Material mat, float scale, float flatten, float alphaVal, Vector2 offset)
    {
        if (mat == null)
        {
            return;
        }

        var col = new Color(shadowColor.r, shadowColor.g, shadowColor.b, alphaVal);
        var offsetVec = new Vector4(offset.x, offset.y, 0f, 0f);
        var shearVec = new Vector4(0f, 0f, scale * flatten, 0f);

        mat.SetColor("_Color", col);
        mat.SetFloat("_Softness", 0f);
        mat.SetFloat("_TipSoftness", 0f);
        mat.SetFloat("_TipFade", 0f);
        mat.SetFloat("_Taper", 0f);
        mat.SetFloat("_ScaleX", scale);
        mat.SetFloat("_ProjectionMode", 1f);
        mat.SetVector("_ShadowOffset", offsetVec);
        mat.SetVector("_ShadowShear", shearVec);
    }

    private void EnsureProxyRoot()
    {
        if (_proxyRoot != null)
        {
            return;
        }

        var go = new GameObject("PlanarShadowProxies");
        DontDestroyOnLoad(go);
        _proxyRoot = go.transform;
    }

    private void ScanCasters()
    {
        var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < renderers.Length; i++)
        {
            TryCreateProxy(renderers[i]);
        }
    }

    private void TryCreateProxy(Renderer source)
    {
        if (source == null || !source.enabled || !source.gameObject.activeInHierarchy)
        {
            return;
        }

        var sourceObject = source.gameObject;
        var name = sourceObject.name;

        if (name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (name == "Shadow" || name == "ShadowMesh" || name == "shadow")
            {
                sourceObject.SetActive(false);
            }
            return;
        }

        if (name == "FHD" || name.EndsWith("_PlanarShadow") || name.Contains("Background") || name.Contains("Fog") || name.Contains("Frame") || name.Contains("WaterSurfaceDecal") || name.Contains("UpgradeMark"))
        {
            return;
        }

        if (name.IndexOf("Glow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Highlight", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Outline", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Particle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Tooltip", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Bubble", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Progress", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Indicator", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Speech", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Dialog", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Upgrade", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return;
        }

        if (source.GetComponentInParent<Canvas>() != null)
        {
            return;
        }

        if (source is SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer.sortingLayerName == "UI" || spriteRenderer.sortingLayerName == "Effects")
            {
                return;
            }

            if (spriteRenderer.sprite == null || spriteRenderer.color.a < 0.01f)
            {
                return;
            }

            if (spriteRenderer.bounds.size.x > 8f || spriteRenderer.bounds.size.y > 8f)
            {
                return;
            }
        }

        var id = source.GetInstanceID();
        if (_proxiedIds.Contains(id))
        {
            return;
        }

        var isUnit = IsUnitCaster(source);
        var isFlyingUnit = isUnit && IsFlyingUnitCaster(source);

        if (isFlyingUnit && !enableFlyingUnitShadows)
        {
            return;
        }

        if (isUnit && !isFlyingUnit && !enableUnitShadows)
        {
            return;
        }

        if (!isUnit && !enableAssetShadows)
        {
            return;
        }

        if (!isUnit && !enableAssetShadows)
        {
            return;
        }

        EnsureProxyRoot();
        CasterProxy proxy = null;
        if (source is SkinnedMeshRenderer skinned)
        {
            proxy = CreateSkinnedProxy(skinned, isUnit, isFlyingUnit);
        }
        else if (source is SpriteRenderer sprite)
        {
            var shadow = CreateSpriteProxy(sprite, isUnit, isFlyingUnit);
            if (shadow != null)
            {
                proxy = new CasterProxy { SourceId = id, Source = source, Shadow = shadow, IsUnit = isUnit, IsFlyingUnit = isFlyingUnit };
            }
        }
        else if (source is MeshRenderer meshRenderer)
        {
            var shadow = CreateMeshProxy(meshRenderer, isUnit, isFlyingUnit, out var extras);
            if (shadow != null)
            {
                proxy = new CasterProxy { SourceId = id, Source = source, Shadow = shadow, ExtraShadows = extras, IsUnit = isUnit, IsFlyingUnit = isFlyingUnit };
            }
        }

        if (proxy == null || proxy.Shadow == null)
        {
            return;
        }

        proxy.SourceSortingGroup = source.GetComponentInParent<SortingGroup>();
        ApplyProxySorting(proxy);

        _proxiedIds.Add(id);
        _proxies.Add(proxy);
    }

    private static bool IsUnitCaster(Renderer source)
    {
        var current = source.transform;
        while (current != null)
        {
            if (current.CompareTag("Unit"))
            {
                return true;
            }

            current = current.parent;
        }

        return source is SkinnedMeshRenderer;
    }

    private static bool IsFlyingUnitCaster(Renderer source)
    {
        if (source is SkinnedMeshRenderer)
        {
            return false;
        }

        var current = source.transform;
        while (current != null)
        {
            var name = current.name;
            if (name.IndexOf("Drone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Fly", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Air", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return true;
    }

    private Material ShadowMaterial(bool isUnit, bool isFlyingUnit)
    {
        if (isFlyingUnit)
        {
            return _flyingUnitShadowMaterial0;
        }

        return isUnit ? _unitShadowMaterial : _assetShadowMaterial;
    }

    private CasterProxy CreateSkinnedProxy(SkinnedMeshRenderer source, bool isUnit, bool isFlyingUnit)
    {
        if (source.sharedMesh == null)
        {
            return null;
        }

        var bakedMesh = new Mesh { name = source.sharedMesh.name + "_BakedShadow" };
        source.BakeMesh(bakedMesh);

        var go = new GameObject(source.gameObject.name + "_PlanarShadow");
        go.transform.SetParent(_proxyRoot, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = bakedMesh;
        var proxy = go.AddComponent<MeshRenderer>();
        proxy.sharedMaterial = ShadowMaterial(isUnit, isFlyingUnit);
        proxy.shadowCastingMode = ShadowCastingMode.Off;
        proxy.receiveShadows = false;
        CopyWorldTransform(source.transform, go.transform);
        return new CasterProxy
        {
            SourceId = source.GetInstanceID(),
            Source = source,
            Shadow = proxy,
            SkinnedSource = source,
            BakedFilter = filter,
            BakedMesh = bakedMesh,
            IsUnit = isUnit,
            IsFlyingUnit = isFlyingUnit
        };
    }

    private MeshRenderer CreateSpriteProxy(SpriteRenderer source, bool isUnit, bool isFlyingUnit)
    {
        if (source.sprite == null)
        {
            return null;
        }

        var mesh = CreateSpriteMesh(source.sprite);
        if (mesh == null)
        {
            return null;
        }

        var go = new GameObject(source.gameObject.name + "_PlanarShadow");
        go.transform.SetParent(_proxyRoot, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var proxy = go.AddComponent<MeshRenderer>();
        proxy.sharedMaterial = ShadowMaterial(isUnit, isFlyingUnit);
        proxy.shadowCastingMode = ShadowCastingMode.Off;
        proxy.receiveShadows = false;
        var block = new MaterialPropertyBlock();
        block.SetTexture("_MainTex", source.sprite.texture);
        proxy.SetPropertyBlock(block);
        CopyWorldTransform(source.transform, go.transform);
        return proxy;
    }

    private static Mesh CreateSpriteMesh(Sprite sprite)
    {
        if (sprite == null)
        {
            return null;
        }

        var vertices2 = sprite.vertices;
        var vertices = new Vector3[vertices2.Length];
        var colors = new Color[vertices2.Length];
        for (var i = 0; i < vertices2.Length; i++)
        {
            vertices[i] = vertices2[i];
            colors[i] = Color.white;
        }

        var triangles16 = sprite.triangles;
        var triangles = new int[triangles16.Length];
        for (var i = 0; i < triangles16.Length; i++)
        {
            triangles[i] = triangles16[i];
        }

        var mesh = new Mesh { name = "SpriteShadowMesh" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, sprite.uv);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        var normals = new Vector3[vertices.Length];
        for (var i = 0; i < normals.Length; i++)
        {
            normals[i] = Vector3.back;
        }

        mesh.SetNormals(normals);
        mesh.RecalculateBounds();
        return mesh;
    }

    private MeshRenderer CreateMeshProxy(MeshRenderer source, bool isUnit, bool isFlyingUnit, out List<Renderer> extras)
    {
        extras = null;
        if (!source.TryGetComponent<MeshFilter>(out var sourceFilter) || sourceFilter.sharedMesh == null)
        {
            return null;
        }

        if (source.gameObject.name == "ShadowMesh")
        {
            return null;
        }

        var go = new GameObject(source.gameObject.name + "_PlanarShadow");
        go.transform.SetParent(_proxyRoot, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = sourceFilter.sharedMesh;
        var proxy = go.AddComponent<MeshRenderer>();
        proxy.sharedMaterial = ShadowMaterial(isUnit, isFlyingUnit);
        proxy.shadowCastingMode = ShadowCastingMode.Off;
        proxy.receiveShadows = false;
        CopyWorldTransform(source.transform, go.transform);

        if (isFlyingUnit)
        {
            extras = new List<Renderer>(2);

            var go1 = new GameObject(source.gameObject.name + "_PlanarShadow_Mid");
            go1.transform.SetParent(_proxyRoot, false);
            var filter1 = go1.AddComponent<MeshFilter>();
            filter1.sharedMesh = sourceFilter.sharedMesh;
            var proxy1 = go1.AddComponent<MeshRenderer>();
            proxy1.sharedMaterial = _flyingUnitShadowMaterial1;
            proxy1.shadowCastingMode = ShadowCastingMode.Off;
            proxy1.receiveShadows = false;
            CopyWorldTransform(source.transform, go1.transform);
            extras.Add(proxy1);

            var go2 = new GameObject(source.gameObject.name + "_PlanarShadow_Outer");
            go2.transform.SetParent(_proxyRoot, false);
            var filter2 = go2.AddComponent<MeshFilter>();
            filter2.sharedMesh = sourceFilter.sharedMesh;
            var proxy2 = go2.AddComponent<MeshRenderer>();
            proxy2.sharedMaterial = _flyingUnitShadowMaterial2;
            proxy2.shadowCastingMode = ShadowCastingMode.Off;
            proxy2.receiveShadows = false;
            CopyWorldTransform(source.transform, go2.transform);
            extras.Add(proxy2);
        }

        return proxy;
    }

    private void ApplyProxySorting(CasterProxy proxy)
    {
        if (proxy == null || proxy.Shadow == null)
        {
            return;
        }

        var layerId = 0;
        var order = 1;

        if (proxy.IsUnit && unitShadowAboveAssets)
        {
            if (proxy.SourceSortingGroup == null && proxy.Source != null)
            {
                proxy.SourceSortingGroup = proxy.Source.GetComponentInParent<SortingGroup>();
            }

            if (proxy.SourceSortingGroup != null)
            {
                layerId = proxy.SourceSortingGroup.sortingLayerID;
                order = proxy.SourceSortingGroup.sortingOrder + unitShadowSortingOffset;
            }
            else if (proxy.Source != null)
            {
                layerId = proxy.Source.sortingLayerID;
                order = proxy.Source.sortingOrder + unitShadowSortingOffset;
            }
        }

        proxy.Shadow.sortingLayerID = layerId;
        proxy.Shadow.sortingOrder = order;

        if (proxy.ExtraShadows != null)
        {
            for (var i = 0; i < proxy.ExtraShadows.Count; i++)
            {
                var extra = proxy.ExtraShadows[i];
                if (extra != null)
                {
                    extra.sortingLayerID = layerId;
                    extra.sortingOrder = order - 1 - i;
                }
            }
        }
    }

    private void SyncProxies()
    {
        for (var i = _proxies.Count - 1; i >= 0; i--)
        {
            var proxy = _proxies[i];
            if (proxy.Source == null || proxy.Shadow == null)
            {
                if (proxy.Shadow != null)
                {
                    proxy.Shadow.gameObject.SetActive(false);
                    proxy.Shadow.enabled = false;
                    Destroy(proxy.Shadow.gameObject);
                }

                if (proxy.ExtraShadows != null)
                {
                    for (var j = 0; j < proxy.ExtraShadows.Count; j++)
                    {
                        var extra = proxy.ExtraShadows[j];
                        if (extra != null)
                        {
                            extra.gameObject.SetActive(false);
                            extra.enabled = false;
                            Destroy(extra.gameObject);
                        }
                    }
                }

                if (proxy.BakedMesh != null)
                {
                    Destroy(proxy.BakedMesh);
                }

                _proxiedIds.Remove(proxy.SourceId);
                _proxies.RemoveAt(i);
                continue;
            }

            var isVisible = proxy.Source.gameObject.activeInHierarchy && proxy.Source.enabled;
            if (proxy.Source.transform.lossyScale.sqrMagnitude < 0.0001f)
            {
                isVisible = false;
            }

            if (proxy.IsFlyingUnit && !enableFlyingUnitShadows)
            {
                isVisible = false;
            }
            else if (proxy.IsUnit && !proxy.IsFlyingUnit && !enableUnitShadows)
            {
                isVisible = false;
            }
            else if (!proxy.IsUnit && !enableAssetShadows)
            {
                isVisible = false;
            }

            if (proxy.Source is SpriteRenderer sr)
            {
                isVisible = isVisible && sr.sprite != null && sr.color.a > 0.01f;
            }
            else if (proxy.Source is MeshRenderer mr)
            {
                isVisible = isVisible && mr.sharedMaterial != null;
            }
            else if (proxy.Source is SkinnedMeshRenderer smr)
            {
                isVisible = isVisible && smr.sharedMesh != null;
            }

            if (proxy.Shadow.gameObject.activeSelf != isVisible)
            {
                proxy.Shadow.gameObject.SetActive(isVisible);
            }

            if (proxy.Shadow.enabled != isVisible)
            {
                proxy.Shadow.enabled = isVisible;
            }

            if (proxy.ExtraShadows != null)
            {
                for (var j = 0; j < proxy.ExtraShadows.Count; j++)
                {
                    var extra = proxy.ExtraShadows[j];
                    if (extra != null)
                    {
                        if (extra.gameObject.activeSelf != isVisible)
                        {
                            extra.gameObject.SetActive(isVisible);
                        }
                        if (extra.enabled != isVisible)
                        {
                            extra.enabled = isVisible;
                        }
                    }
                }
            }

            if (!isVisible)
            {
                continue;
            }

            if (proxy.SkinnedSource != null && proxy.BakedFilter != null && proxy.BakedMesh != null)
            {
                proxy.SkinnedSource.BakeMesh(proxy.BakedMesh);
                proxy.BakedFilter.sharedMesh = proxy.BakedMesh;
            }

            CopyWorldTransform(proxy.Source.transform, proxy.Shadow.transform);

            if (proxy.ExtraShadows != null)
            {
                for (var j = 0; j < proxy.ExtraShadows.Count; j++)
                {
                    var extra = proxy.ExtraShadows[j];
                    if (extra != null)
                    {
                        CopyWorldTransform(proxy.Source.transform, extra.transform);
                    }
                }
            }

            ApplyProxySorting(proxy);
        }
    }

    private static void CopyWorldTransform(Transform from, Transform to)
    {
        to.SetPositionAndRotation(from.position, from.rotation);
        to.localScale = from.lossyScale;
    }
}
