using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class PathPrefabSpawner : MonoBehaviour
{
    public GameObject prefab;
    public float spacing = 1f;
    public int splineResolution = 100;

    private readonly List<GameObject> spawned = new();

    public void Spawn()
    {
        Clear();

        if (prefab == null)
            return;

        var authoring = GetComponent<PathSplineAuthoring>();
        if (authoring == null)
            return;

        foreach (Spline spline in authoring.SplineContainer.Splines)
        {
            SpawnOnSpline(spline);
        }
    }

    private void SpawnOnSpline(Spline spline)
    {
        float length = spline.GetLength();
        int count = Mathf.FloorToInt(length / spacing);

        for (int i = 0; i <= count; i++)
        {
            float distance = i * spacing;

            float t = SplineDistanceUtility.GetTAtDistance(
                spline,
                distance,
                splineResolution);

            Vector3 local = spline.EvaluatePosition(t);
            Vector3 world = transform.TransformPoint(local);

            GameObject go = Instantiate(prefab, world, Quaternion.identity, transform);
            spawned.Add(go);
        }
    }

    public void Clear()
    {
        foreach (var go in spawned)
        {
            if (go != null)
                DestroyImmediate(go);
        }
        spawned.Clear();
    }
}
