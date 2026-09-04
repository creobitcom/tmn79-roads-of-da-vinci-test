#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PathGenerator : MonoBehaviour
{
    [Header("Настройки Prefab")]
    public GameObject pointPrefab;
    public Transform graphNodesParent;

    [Header("Границы карты")]
    public float mapWidth = 22f;
    public float mapHeight = 11f;
    public float nodeStep = 0.7f;

    [Header("Настройки Скелета (Змейки)")]
    public Vector2 startPoint = new Vector2(-10, 0);
    public bool showInterestPoints = true;
    public List<Vector2> targetPoints = new List<Vector2>();
    
    [Header("Вторичные точки (Одиночные ветки)")]
    public List<Vector2> secondaryTargets = new List<Vector2>();
    
    [Space]
    [Range(0.01f, 0.3f)] public float targetAttraction = 0.08f; 
    [Range(0.5f, 0.99f)] public float snakeSmoothness = 0.93f;

    [Header("Настройки Хаоса")]
    public int chaosWanderersCount = 3;
    public int wandererLifetime = 40;
    [Range(0.1f, 0.5f)] public float chaosRandomness = 0.25f;

    [HideInInspector] public List<GameObject> chaosGroups = new List<GameObject>();
    private List<Vector2> allNodes = new List<Vector2>(); 
    private System.Random rng;
    private GameObject skeletonGroup;
    private GameObject secondaryGroup;

    private class Agent
    {
        public Vector2 position;
        public Vector2 direction;
        public Vector2? finalGoal;
        public int lifeTime;

        public Agent(Vector2 pos, Vector2 dir, Vector2? goal = null, int life = 100)
        {
            position = pos;
            direction = dir.normalized;
            finalGoal = goal;
            lifeTime = life;
        }
    }

    // --- ГЕНЕРАЦИЯ СКЕЛЕТА ---
    public void GenerateSkeleton()
    {
        ClearAll();
        rng = new System.Random(Random.Range(0, 99999));
        allNodes.Clear();
        allNodes.Add(startPoint);

        skeletonGroup = new GameObject("SKELETON_ROOT");
        skeletonGroup.transform.SetParent(ResolveParent());

        // Основные пути
        List<Vector2> mainTargets = new List<Vector2>(targetPoints);
        int safety = 0;
        while (mainTargets.Count > 0 && safety < 100)
        {
            safety++;
            Vector2 bestTarget;
            Vector2 bestSource = FindBestSource(mainTargets, out bestTarget);
            BuildOrganicPath(bestSource, bestTarget, skeletonGroup.transform);
            mainTargets.Remove(bestTarget);
        }
    }

    // --- ГЕНЕРАЦИЯ ВТОРИЧНЫХ ВЕТОК ---
// --- ГЕНЕРАЦИЯ ВТОРИЧНЫХ ВЕТОК ---
    public void GenerateSecondaryBranches()
    {
        if (allNodes.Count < 1) 
        {
            Debug.LogWarning("Сначала сгенерируй основной скелет!");
            return;
        }
        
        rng = new System.Random(Random.Range(0, 99999));

        // --- ИСПРАВЛЕНИЕ: Очистка старых координат из общего списка ---
        if (secondaryGroup != null) 
        {
            foreach (Transform child in secondaryGroup.transform)
            {
                allNodes.Remove(child.position);
            }
            DestroyImmediate(secondaryGroup);
        }

        secondaryGroup = new GameObject("SECONDARY_BRANCHES");
        secondaryGroup.transform.SetParent(ResolveParent());

        foreach (Vector2 target in secondaryTargets)
        {
            Vector2 bestSource = allNodes[0];
            float minDist = float.MaxValue;
            foreach (var s in allNodes)
            {
                float d = Vector2.Distance(s, target);
                if (d < minDist) { minDist = d; bestSource = s; }
            }

            if (Vector2.Distance(bestSource, target) < nodeStep) continue;

            BuildOrganicPath(bestSource, target, secondaryGroup.transform);
            
            // Принудительный финальный нод
            // if (!allNodes.Any(p => Vector2.Distance(p, target) < nodeStep * 0.5f))
            // {
            //     allNodes.Add(target);
            //     Instantiate(pointPrefab, target, Quaternion.identity, secondaryGroup.transform);
            // }
        }
    }

    private void BuildOrganicPath(Vector2 start, Vector2 end, Transform parent)
    {
        Vector2 currentDir = (end - start).normalized;
        Agent z = new Agent(start, currentDir, end, 500);

        int stepSafety = 0;
        while (stepSafety < 500)
        {
            stepSafety++;
            Vector2 noise = new Vector2((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
            Vector2 toGoal = (end - z.position).normalized;

            Vector2 nextDir = Vector3.Slerp(z.direction, noise, 1f - snakeSmoothness);
            nextDir = Vector3.Slerp(nextDir, toGoal, targetAttraction).normalized;

            z.direction = nextDir;
            z.position += z.direction * nodeStep;

            if (!InBounds(z.position)) break;
            if (TryAddToNodes(z.position))
            {
                Instantiate(pointPrefab, z.position, Quaternion.identity, parent);
            }

            if (Vector2.Distance(z.position, end) < nodeStep * 0.8f) break;
        }
    }

    // --- ХАОС ---
    public void AddChaosLayer()
    {
        if (allNodes.Count < 2) return;
        rng = new System.Random(Random.Range(0, 99999));

        GameObject newLayer = new GameObject($"Chaos_Layer_{chaosGroups.Count + 1}");
        newLayer.transform.SetParent(ResolveParent());
        chaosGroups.Add(newLayer);

        for (int i = 0; i < chaosWanderersCount; i++)
        {
            Vector2 root = allNodes[rng.Next(allNodes.Count)];
            Vector2 randDir = new Vector2((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
            Agent a = new Agent(root, randDir, null, wandererLifetime);

            int life = 0;
            while (life < a.lifeTime)
            {
                life++;
                Vector2 noise = new Vector2((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
                a.direction = Vector3.Slerp(a.direction, noise, chaosRandomness).normalized;
                a.position += a.direction * nodeStep;

                if (InBounds(a.position) && TryAddToNodes(a.position))
                {
                    Instantiate(pointPrefab, a.position, Quaternion.identity, newLayer.transform);
                }
                else if (!InBounds(a.position)) break;
            }
        }
    }

    public void RemoveLayer(int index)
    {
        if (index < 0 || index >= chaosGroups.Count) return;
        GameObject layer = chaosGroups[index];
        if (layer)
        {
            foreach (Transform child in layer.transform) allNodes.Remove(child.position);
            DestroyImmediate(layer);
        }
        chaosGroups.RemoveAt(index);
    }

    private bool TryAddToNodes(Vector2 pos)
    {
        foreach (var p in allNodes)
            if (Vector2.Distance(p, pos) < nodeStep * 0.7f) return false;
        allNodes.Add(pos);
        return true;
    }

    private Vector2 FindBestSource(List<Vector2> targets, out Vector2 target)
    {
        target = targets[0];
        Vector2 bestSrc = allNodes[0];
        float minDist = float.MaxValue;
        foreach (var t in targets)
            foreach (var s in allNodes)
            {
                float d = Vector2.Distance(s, t);
                if (d < minDist) { minDist = d; bestSrc = s; target = t; }
            }
        return bestSrc;
    }

    public void ClearAll()
    {
        foreach (var g in chaosGroups) if (g) DestroyImmediate(g);
        chaosGroups.Clear();
        if (skeletonGroup) DestroyImmediate(skeletonGroup);
        if (secondaryGroup) DestroyImmediate(secondaryGroup);
        
        Transform p = ResolveParent();
        for (int i = p.childCount - 1; i >= 0; i--) DestroyImmediate(p.GetChild(i).gameObject);
        allNodes.Clear();
    }

    private bool InBounds(Vector2 p) => Mathf.Abs(p.x) < mapWidth / 2f && Mathf.Abs(p.y) < mapHeight / 2f;

    private Transform ResolveParent()
    {
        if (graphNodesParent) return graphNodesParent;
        var f = transform.Find("GraphNodes");
        if (!f) f = new GameObject("GraphNodes").transform;
        f.SetParent(transform);
        return f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(mapWidth, mapHeight, 0.1f));
        
        if (showInterestPoints)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(startPoint, 0.3f);
            
            Gizmos.color = Color.red;
            foreach (var t in targetPoints)
                Gizmos.DrawSphere(t, 0.3f);
            
            Gizmos.color = Color.yellow;
            foreach (var t in secondaryTargets)
                Gizmos.DrawSphere(t, 0.25f);
        }
    }





    // Добавьте в класс PathGenerator следующие поля и методы
    [Header("Рендер дорог в PNG")]
    public Sprite templateSprite;          // Опциональный фоновый спрайт
    public int pixelsPerUnit = 100;        // Пикселей на единицу мира (например, 100 -> 2200x1100 для карты 22x11)
    public bool saveToPrefabFolder = true; // новая галочка

    public Color lineColor = Color.black;
    [Range(1, 10)] public int lineThickness = 2;

    public void RenderRoadsToPNG()
    {
        // 1. Собираем ВСЕ точки из контейнера (включая хаос и вручную добавленные)
        Transform container = ResolveParent();
        List<Vector2> allPoints = new List<Vector2>();
        CollectAllPointsRecursive(container, allPoints);
        
        if (allPoints.Count < 2)
        {
            Debug.LogWarning($"Недостаточно точек для рисования линий. Найдено {allPoints.Count} точек.");
            return;
        }

        // 2. Строим рёбра на основе геометрической близости
        HashSet<(int, int)> edges = BuildEdges(allPoints);
        
        // 3. Создаём текстуру
        Texture2D texture = CreateTextureFromTemplate();
        if (texture == null) return;

        // Очищаем фон, если нет шаблона
        if (templateSprite == null)
        {
            Color clearColor = new Color(1, 1, 1, 0);
            for (int x = 0; x < texture.width; x++)
                for (int y = 0; y < texture.height; y++)
                    texture.SetPixel(x, y, clearColor);
        }

        // 4. Рисуем линии с выбранным цветом и толщиной
        foreach (var (i, j) in edges)
        {
            Vector2 a = allPoints[i];
            Vector2 b = allPoints[j];
            DrawLineOnTexture(texture, WorldToPixel(a), WorldToPixel(b), lineColor, lineThickness);
        }

        texture.Apply();
        byte[] pngData = texture.EncodeToPNG();
        
        string path = GetAutoSavePath();
        if (!saveToPrefabFolder)
            path = EditorUtility.SaveFilePanel("Сохранить дорожную карту", "Assets", "roadmap.png", "png");
        
        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllBytes(path, pngData);
            Debug.Log($"Карта дорог сохранена: {path}");
            AssetDatabase.Refresh();
        }
    }

    private void CollectAllPointsRecursive(Transform parent, List<Vector2> points)
    {
        foreach (Transform child in parent)
        {
            // Если у объекта НЕТ детей, значит это финальная точка (твой префаб)
            // Если у объекта ЕСТЬ дети, значит это технический родитель (папка)
            if (child.childCount == 0)
            {
                points.Add(child.position);
            }
            
            // В любом случае идем вглубь, чтобы проверить детей
            if (child.childCount > 0)
                CollectAllPointsRecursive(child, points);
        }
    }

    private string GetAutoSavePath()
    {
        string prefabPath = "";
        
        #if UNITY_2018_3_OR_NEWER
            // Новая система префабов (Unity 2018.3+)
            // Сначала пробуем метод для новых версий
            prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        #else
            // Старая система префабов (Unity 2018.2 и ниже)
            UnityEngine.Object prefabParent = PrefabUtility.GetPrefabParent(gameObject);
            if (prefabParent != null)
                prefabPath = AssetDatabase.GetAssetPath(prefabParent);
        #endif

        // Если объект – префаб или часть префаба
        if (!string.IsNullOrEmpty(prefabPath))
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(prefabPath), "roadmap.png");
        
        // Если объект в сцене
        string scenePath = gameObject.scene.path;
        if (!string.IsNullOrEmpty(scenePath))
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(scenePath), "roadmap.png");
        
        // Запасной вариант
        return "Assets/roadmap.png";
    }
    

    // Вспомогательный метод: собирает позиции точек из иерархии Transform
    private void CollectPointsFromTransform(Transform parent, List<Vector2> points)
    {
        foreach (Transform child in parent)
            points.Add(child.position);
    }

    // Восстанавливает рёбра на основе геометрической близости
    private HashSet<(int, int)> BuildEdges(List<Vector2> points)
    {
        float tolerance = nodeStep * 1.2f;
        HashSet<(int, int)> edges = new HashSet<(int, int)>();

        for (int i = 0; i < points.Count; i++)
        {
            // Ищем ближайших соседей (максимум 3, но не больше)
            List<(int idx, float dist)> neighbors = new List<(int, float)>();
            for (int j = 0; j < points.Count; j++)
            {
                if (i == j) continue;
                float d = Vector2.Distance(points[i], points[j]);
                if (d < tolerance)
                    neighbors.Add((j, d));
            }

            // Сортируем по расстоянию и берём двух-трёх ближайших
            neighbors.Sort((a, b) => a.dist.CompareTo(b.dist));
            int maxEdges = (neighbors.Count >= 2) ? 2 : neighbors.Count; // для ветвлений можно и 3, но будет O(n)
            // Для ветвлений (точки с 3+ соседями) разрешим добавить третьего, если разница расстояний мала
            bool isBranch = points.Count(p => Vector2.Distance(points[i], p) < tolerance) >= 3;
            int connections = isBranch ? Mathf.Min(3, neighbors.Count) : maxEdges;

            for (int k = 0; k < connections; k++)
            {
                int j = neighbors[k].idx;
                // Добавляем ребро в каноническом порядке (меньший индекс первым)
                int a = Mathf.Min(i, j);
                int b = Mathf.Max(i, j);
                edges.Add((a, b));
            }
        }
        return edges;
    }

    // Преобразует мировые координаты в пиксельные на текстуре
    private Vector2Int WorldToPixel(Vector2 worldPos)
    {
        float w, h;
        if (templateSprite != null && templateSprite.texture != null)
        {
            // Мир имеет размеры = (размер текстуры в пикселях) / pixelsPerUnit
            w = templateSprite.texture.width / (float)pixelsPerUnit;
            h = templateSprite.texture.height / (float)pixelsPerUnit;
        }
        else
        {
            w = mapWidth;
            h = mapHeight;
        }

        // Преобразуем мировые координаты (от -w/2 до w/2, от -h/2 до h/2)
        float u = (worldPos.x + w / 2f) / w;
        float v = (worldPos.y + h / 2f) / h;

        int texWidth, texHeight;
        if (templateSprite != null && templateSprite.texture != null)
        {
            texWidth = templateSprite.texture.width;
            texHeight = templateSprite.texture.height;
        }
        else
        {
            texWidth = Mathf.RoundToInt(pixelsPerUnit * w);
            texHeight = Mathf.RoundToInt(pixelsPerUnit * h);
        }

        int px = Mathf.RoundToInt(u * texWidth);
        int py = Mathf.RoundToInt(v * texHeight);
        px = Mathf.Clamp(px, 0, texWidth - 1);
        py = Mathf.Clamp(py, 0, texHeight - 1);
        return new Vector2Int(px, py);
    }

    // Создаёт текстуру из шаблона (если есть) или новую нужного размера
    private Texture2D CreateTextureFromTemplate()
    {
        if (templateSprite != null && templateSprite.texture != null)
        {
            Texture2D src = templateSprite.texture;
            Texture2D tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            Graphics.CopyTexture(src, tex); // быстро копирует всю текстуру
            return tex;
        }
        else
        {
            int width = Mathf.RoundToInt(pixelsPerUnit * mapWidth);
            int height = Mathf.RoundToInt(pixelsPerUnit * mapHeight);
            return new Texture2D(width, height);
        }
    }
        // Рисует линию на текстуре с заданной толщиной (алгоритм Брезенхема)
        private void DrawLineOnTexture(Texture2D tex, Vector2Int p0, Vector2Int p1, Color color, int thickness)
        {
            int dx = Mathf.Abs(p1.x - p0.x);
            int dy = Mathf.Abs(p1.y - p0.y);
            int sx = p0.x < p1.x ? 1 : -1;
            int sy = p0.y < p1.y ? 1 : -1;
            int err = dx - dy;

            int x = p0.x, y = p0.y;
            while (true)
            {
                // Рисуем квадрат толщины
                for (int tx = -thickness/2; tx <= thickness/2; tx++)
                    for (int ty = -thickness/2; ty <= thickness/2; ty++)
                    {
                        int nx = x + tx, ny = y + ty;
                        if (nx >= 0 && nx < tex.width && ny >= 0 && ny < tex.height)
                            tex.SetPixel(nx, ny, color);
                    }

                if (x == p1.x && y == p1.y) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }
            }
        }



    }

[CustomEditor(typeof(PathGenerator))]
public class PathGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        PathGenerator script = (PathGenerator)target;


        GUILayout.Space(20);
        GUI.backgroundColor = new Color(0.9f, 0.7f, 0.5f);
        if (GUILayout.Button(script.showInterestPoints ? "HIDE INTEREST POINTS" : "SHOW INTEREST POINTS", GUILayout.Height(30)))
        {
            script.showInterestPoints = !script.showInterestPoints;
            SceneView.RepaintAll();   // принудительно обновляем сцену
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(20);
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("1. GENERATE MAIN SKELETON", GUILayout.Height(40))) script.GenerateSkeleton();

        GUILayout.Space(5);
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("2. GENERATE SECONDARY BRANCHES", GUILayout.Height(30))) script.GenerateSecondaryBranches();

        GUILayout.Space(10);
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("3. ADD CHAOS LAYER", GUILayout.Height(30))) script.AddChaosLayer();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Chaos Layers Management:", EditorStyles.boldLabel);
        for (int i = 0; i < script.chaosGroups.Count; i++)
        {
            if (script.chaosGroups[i] == null) continue;
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField($"Layer {i + 1} ({script.chaosGroups[i].transform.childCount} pts)");
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("REMOVE", GUILayout.Width(70))) script.RemoveLayer(i);
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }

        GUILayout.Space(20);
        GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
        if (GUILayout.Button("CLEAR EVERYTHING", GUILayout.Height(30))) script.ClearAll();

        GUILayout.Space(10);
    GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
    if (GUILayout.Button("Сохранить PNG с линиями дорог", GUILayout.Height(35)))
    {
        script.RenderRoadsToPNG();
    }
    }
}


#endif