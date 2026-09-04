using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PathSplineDebugVisualizer : MonoBehaviour
{
#if UNITY_EDITOR

    public Color color = Color.cyan;
    public float width = 0.1f;
    public int segmentsPerSpline = 32;

    private Transform runtimeRoot;

    public void Draw()
    {
        Clear();

        var authoring = GetComponent<PathSplineAuthoring>();
        if (authoring == null)
        {
            Debug.LogWarning("No PathSplineAuthoring");
            return;
        }

        var splines = authoring.SplineContainer.Splines;
        if (splines.Count == 0)
        {
            Debug.LogWarning("No splines to draw");
            return;
        }

        runtimeRoot = new GameObject("__SplineDebugRuntime").transform;
        runtimeRoot.SetParent(transform, false);

        foreach (Spline spline in splines)
        {
            CreateLine(spline);
        }
    }

    private void CreateLine(Spline spline)
    {
        GameObject go = new GameObject("SplineLine");
        go.transform.SetParent(runtimeRoot, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.useWorldSpace = true;
        lr.loop = false;

        int count = Mathf.Max(segmentsPerSpline, 2);
        lr.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            Vector3 local = spline.EvaluatePosition(t);
            Vector3 world = transform.TransformPoint(local);
            lr.SetPosition(i, world);
        }
    }

    public void Clear()
    {
        if (runtimeRoot != null)
        {
            DestroyImmediate(runtimeRoot.gameObject);
            runtimeRoot = null;
        }
    }

#endif
}
