#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LineCreator))]
public class LineCreatorEditor : Editor

{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LineCreator tool = (LineCreator)target;

        GUILayout.Space(10);

        if (GUILayout.Button("CREATE LINE"))
        {
            CreateLine(tool);
        }
    }

    Vector2 Bezier(Vector2 a, Vector2 b, float t, float sagAmount)
    {
        Vector2 mid = (a + b) / 2f;

        if (sagAmount > 0f)
        {
            float distance = Vector2.Distance(a, b);

            // максимум провисания вниз
            float sag = distance * 0.5f * sagAmount;

            // вниз по Y
            mid.y -= sag;
        }

        return Mathf.Pow(1 - t, 2) * a +
            2 * (1 - t) * t * mid +
            Mathf.Pow(t, 2) * b;
    }

    void CreateLine(LineCreator tool)
    {
        RectTransform canvasRect = tool.canvas.GetComponent<RectTransform>();

        Vector2 a;
        Vector2 b;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(null, tool.pointA.position),
            null,
            out a
        );

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(null, tool.pointB.position),
            null,
            out b
        );

        // 👉 СОЗДАЁМ ROOT ОБЪЕКТ
        GameObject root = new GameObject("String");
        RectTransform rootRect = root.AddComponent<RectTransform>();
        root.transform.SetParent(tool.canvas, false);
        rootRect.anchoredPosition = Vector2.zero;

        int segments = 25;
        Vector2 prev = a;

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;

            Vector2 current = Bezier(a, b, t, tool.sagAmount);

            GameObject seg = Instantiate(tool.linePrefab);
            seg.transform.SetParent(root.transform, false);

            RectTransform r = seg.GetComponent<RectTransform>();

            Vector2 dir = current - prev;

            r.anchoredPosition = (prev + current) / 2f;
            r.sizeDelta = new Vector2(dir.magnitude, tool.ropeThickness);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            r.rotation = Quaternion.Euler(0, 0, angle);
            prev = current;
        }
    }
}
#endif