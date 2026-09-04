using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PathSplineAuthoring))]
public class PathToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        var authoring = (PathSplineAuthoring)target;
        var spawner = authoring.GetComponent<PathPrefabSpawner>();
        var visualizer = authoring.GetComponent<PathSplineDebugVisualizer>();

        if (GUILayout.Button("Add New Spline"))
            authoring.AddNewSpline();

        if (GUILayout.Button("Remove All Splines"))
            authoring.RemoveAllSplines();

        GUILayout.Space(10);

        if (spawner != null)
        {
            if (GUILayout.Button("Spawn Prefabs"))
                spawner.Spawn();

            if (GUILayout.Button("Clear Spawned Prefabs"))
                spawner.Clear();
        }

        GUILayout.Space(10);

        if (visualizer != null)
        {
            if (GUILayout.Button("Draw Debug Paths"))
                visualizer.Draw();

            if (GUILayout.Button("Clear Debug Paths"))
                visualizer.Clear();
        }
    }
}
