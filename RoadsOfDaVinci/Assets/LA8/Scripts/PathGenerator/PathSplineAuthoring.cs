using UnityEngine;
using UnityEngine.Splines;

[DisallowMultipleComponent]
[ExecuteAlways]
public class PathSplineAuthoring : MonoBehaviour
{
    [SerializeField, HideInInspector]
    private SplineContainer _splineContainer;

    public SplineContainer SplineContainer
    {
        get
        {
            EnsureSplineContainer();
            return _splineContainer;
        }
    }

    private void Awake()
    {
        EnsureSplineContainer();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureSplineContainer();
    }
#endif

    private void EnsureSplineContainer()
    {
        if (_splineContainer != null)
            return;

        _splineContainer = GetComponent<SplineContainer>();
        if (_splineContainer == null)
            _splineContainer = gameObject.AddComponent<SplineContainer>();
    }

    // ===== API =====

    public void AddNewSpline()
    {
        SplineContainer.AddSpline();
    }

    public void RemoveAllSplines()
    {
        var container = SplineContainer;

        while (container.Splines.Count > 0)
        {
            container.RemoveSpline(container.Splines[0]);
        }
    }
}
