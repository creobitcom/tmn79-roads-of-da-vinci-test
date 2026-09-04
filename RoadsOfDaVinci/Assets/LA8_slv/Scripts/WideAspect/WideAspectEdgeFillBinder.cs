using UnityEngine;

public sealed class WideAspectEdgeFillBinder : MonoBehaviour
{
    [SerializeField] private Material _blurMaterial;
    [SerializeField] [Range(0f, 0.5f)] private float _blur = 0.14f;
    [SerializeField] private float _scanInterval = 0.5f;

    private Camera _camera;
    private SpriteRenderer _bound;
    private float _nextScanTime;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void Update()
    {
        if (_bound != null && _bound.enabled && _bound.gameObject.activeInHierarchy)
        {
            return;
        }

        if (Time.unscaledTime < _nextScanTime)
        {
            return;
        }

        _nextScanTime = Time.unscaledTime + Mathf.Max(_scanInterval, 0.1f);
        Bind();
    }

    private void Bind()
    {
        if (_blurMaterial == null)
        {
            return;
        }

        SpriteRenderer source = FindBackground();
        _bound = source;

        if (source == null)
        {
            return;
        }

        WideAspectEdgeFill fill = source.GetComponent<WideAspectEdgeFill>();
        if (fill == null)
        {
            fill = source.gameObject.AddComponent<WideAspectEdgeFill>();
        }

        fill.Setup(_blurMaterial, _blur, _camera != null ? _camera : Camera.main);
    }

    private static SpriteRenderer FindBackground()
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        SpriteRenderer visual = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer.name == "FHD")
            {
                return renderer;
            }

            if (visual == null && renderer.name == "Visual" && renderer.transform.parent != null && renderer.transform.parent.name == "Background")
            {
                visual = renderer;
            }
        }

        return visual;
    }
}
