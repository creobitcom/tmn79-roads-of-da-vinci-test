using System;
using System.Collections.Generic;
using System.Reflection;
using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CreobitEditor
{
    public class PathValidatorWindow : EditorWindow
    {
        private const string SettingsKey = "Creobit.PathValidatorWindow.Settings";

        [Serializable]
        private class Settings
        {
            public float nodeRadius = 0.12f;
            public float lineThickness = 5f;
            public bool drawNodes = true;
            public bool drawLabels = true;
            public bool autoFillRoot = true;
            public int labelFontSize = 10;
            public bool showLeak = true;
            public bool showBypass = true;
            public bool showOk = true;
            public bool showInsideAabb = true;
            public bool showGaps = true;
            public bool showOther = true;
            public bool hoverOnly;
            public float hoverRadius = 1.5f;
        }

        private GameObject _levelRoot;
        private float _nodeRadius = 0.12f;
        private float _lineThickness = 5f;
        private bool _drawNodes = true;
        private bool _drawLabels = true;
        private bool _autoFillRoot = true;
        private int _labelFontSize = 10;

        private bool _showLeak = true;
        private bool _showBypass = true;
        private bool _showOk = true;
        private bool _showInsideAabb = true;
        private bool _showGaps = true;
        private bool _showOther = true;
        private bool _hoverOnly = false;
        private float _hoverRadius = 1.5f;

        private readonly List<(Vector3 a, Vector3 b, float dist)> _gaps = new();
        private int[] _component;
        private float _maxDistCached;

        private readonly List<NodeInfo> _nodes = new();
        private readonly List<EdgeInfo> _edges = new();
        private readonly List<ObstacleInfo> _obstacles = new();
        private readonly List<Issue> _issues = new();
        private bool _hasScan;

        private struct NodeInfo
        {
            public Vector3 pos;
            public bool insideObstacle;
            public string obstacleName;
        }

        private struct EdgeInfo
        {
            public int ia;
            public int ib;
            public Vector3 a;
            public Vector3 b;
            public bool blocked;
            public bool bypass;
            public bool leakThrough;
            public string obstacleName;
        }

        private class ObstacleInfo
        {
            public string name;
            public Bounds bounds;
            public Vector3 pos;
            public bool useInteractionGraphNode;
            public bool useNearestInteractionGraphNode;
            public Vector3 interactionPosition;
            public int blockedNodeIndex = -1;
        }

        private struct Issue
        {
            public Vector3 pos;
            public string text;
            public Color color;
        }

        [MenuItem("Tools/Path Validator")]
        public static void Open()
        {
            GetWindow<PathValidatorWindow>("Path Validator");
        }

        private void OnEnable()
        {
            LoadSettings();
            SceneView.duringSceneGui += OnSceneGUI;
            TryAutoPick();
        }

        private void OnDisable()
        {
            SaveSettings();
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void LoadSettings()
        {
            if (!EditorPrefs.HasKey(SettingsKey))
                return;

            var settings = JsonUtility.FromJson<Settings>(EditorPrefs.GetString(SettingsKey));
            if (settings == null)
                return;

            _nodeRadius = settings.nodeRadius;
            _lineThickness = settings.lineThickness;
            _drawNodes = settings.drawNodes;
            _drawLabels = settings.drawLabels;
            _autoFillRoot = settings.autoFillRoot;
            _labelFontSize = settings.labelFontSize;
            _showLeak = settings.showLeak;
            _showBypass = settings.showBypass;
            _showOk = settings.showOk;
            _showInsideAabb = settings.showInsideAabb;
            _showGaps = settings.showGaps;
            _showOther = settings.showOther;
            _hoverOnly = settings.hoverOnly;
            _hoverRadius = settings.hoverRadius;
        }

        private void SaveSettings()
        {
            var settings = new Settings
            {
                nodeRadius = _nodeRadius,
                lineThickness = _lineThickness,
                drawNodes = _drawNodes,
                drawLabels = _drawLabels,
                autoFillRoot = _autoFillRoot,
                labelFontSize = _labelFontSize,
                showLeak = _showLeak,
                showBypass = _showBypass,
                showOk = _showOk,
                showInsideAabb = _showInsideAabb,
                showGaps = _showGaps,
                showOther = _showOther,
                hoverOnly = _hoverOnly,
                hoverRadius = _hoverRadius
            };

            EditorPrefs.SetString(SettingsKey, JsonUtility.ToJson(settings));
        }

        private void TryAutoPick()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) _levelRoot = stage.prefabContentsRoot;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Валидатор путей уровня", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Зелёные — свободный путь.\n" +
                "Красные — рантайм блокирует (узел препятствия на ребре).\n" +
                "Фиолетовые — путь геометрически идёт сквозь препятствие, но рантайм его НЕ блокирует (юнит пройдёт сквозь).\n" +
                "Жёлтый AABB — препятствие обходимо альтернативным маршрутом.\n" +
                "Большая красная сфера — узел, который реально блокирует A* в рантайме.",
                MessageType.Info);

            _levelRoot = (GameObject)EditorGUILayout.ObjectField(
                "Корень префаба", _levelRoot, typeof(GameObject), true);

            if (GUILayout.Button("Взять из открытого префаба"))
                TryAutoPick();

            EditorGUI.BeginChangeCheck();
            _autoFillRoot = EditorGUILayout.Toggle("Авто-заполнять Root (1-й child)", _autoFillRoot);
            _drawNodes = EditorGUILayout.Toggle("Рисовать узлы", _drawNodes);
            _drawLabels = EditorGUILayout.Toggle("Метки проблем", _drawLabels);
            _lineThickness = EditorGUILayout.Slider("Толщина линий", _lineThickness, 1f, 12f);
            _nodeRadius = EditorGUILayout.Slider("Радиус узла", _nodeRadius, 0.02f, 0.5f);
            _labelFontSize = EditorGUILayout.IntSlider("Размер шрифта", _labelFontSize, 10, 28);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Фильтры меток", EditorStyles.boldLabel);
            _hoverOnly = EditorGUILayout.Toggle("Только под курсором", _hoverOnly);
            if (_hoverOnly)
                _hoverRadius = EditorGUILayout.Slider("Радиус наведения", _hoverRadius, 0.3f, 5f);
            _showLeak = EditorGUILayout.Toggle("ДЫРА (сквозной проход)", _showLeak);
            _showBypass = EditorGUILayout.Toggle("ОБХОД", _showBypass);
            _showOk = EditorGUILayout.Toggle("ОК (заблокировано)", _showOk);
            _showInsideAabb = EditorGUILayout.Toggle("Узлы внутри AABB", _showInsideAabb);
            _showGaps = EditorGUILayout.Toggle("Разрывы пути", _showGaps);
            _showOther = EditorGUILayout.Toggle("Прочие сообщения", _showOther);

            if (EditorGUI.EndChangeCheck())
            {
                SaveSettings();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            GUI.enabled = _levelRoot != null;
            if (GUILayout.Button("СКАНИРОВАТЬ", GUILayout.Height(32)))
                Scan();
            GUI.enabled = true;

            if (GUILayout.Button("Очистить"))
            {
                _nodes.Clear(); _edges.Clear(); _obstacles.Clear(); _issues.Clear();
                _hasScan = false;
                SceneView.RepaintAll();
            }

            if (_hasScan)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Отчёт", EditorStyles.boldLabel);
                int blocked = 0, leak = 0;
                var bypassObs = new HashSet<string>();
                var leakObs = new HashSet<string>();
                foreach (var e in _edges)
                {
                    if (e.blocked) blocked++;
                    if (e.leakThrough) { leak++; leakObs.Add(e.obstacleName); }
                    if (e.bypass) bypassObs.Add(e.obstacleName);
                }
                EditorGUILayout.LabelField($"Узлов: {_nodes.Count}");
                EditorGUILayout.LabelField($"Рёбер: {_edges.Count} | заблокировано: {blocked} | сквозных: {leak}");
                EditorGUILayout.LabelField($"Препятствий: {_obstacles.Count} | обходимо: {bypassObs.Count} | дырявых: {leakObs.Count}");
                EditorGUILayout.LabelField($"Разрывов пути: {_gaps.Count}");
                EditorGUILayout.LabelField($"Проблем: {_issues.Count}",
                    _issues.Count > 0 ? EditorStyles.boldLabel : EditorStyles.label);

                foreach (var i in _issues)
                    EditorGUILayout.LabelField("• " + i.text);
            }
        }

        private void Scan()
        {
            _nodes.Clear(); _edges.Clear(); _obstacles.Clear(); _issues.Clear(); _gaps.Clear();
            _component = null;
            _hasScan = false;

            if (_levelRoot == null) return;

            var pathfinder = _levelRoot.GetComponentInChildren<AstarPath>(true);
            if (pathfinder == null)
            {
                _issues.Add(new Issue { pos = _levelRoot.transform.position, text = "Не найден компонент Pathfinder (AstarPath)", color = Color.red });
                _hasScan = true; SceneView.RepaintAll(); return;
            }

            if (pathfinder.data == null)
            {
                _issues.Add(new Issue { pos = pathfinder.transform.position, text = "AstarPath.data == null", color = Color.red });
                _hasScan = true; SceneView.RepaintAll(); return;
            }

            if (pathfinder.data.graphs == null || pathfinder.data.graphs.Length == 0)
            {
                try { pathfinder.data.DeserializeGraphs(); }
                catch (System.Exception ex)
                {
                    _issues.Add(new Issue { pos = pathfinder.transform.position, text = $"DeserializeGraphs упал: {ex.Message}", color = Color.red });
                }
            }

            PointGraph pg = null;
            if (pathfinder.data.graphs != null)
                foreach (var g in pathfinder.data.graphs) if (g is PointGraph p) { pg = p; break; }

            if (pg == null)
            {
                _issues.Add(new Issue { pos = pathfinder.transform.position, text = "Не найден PointGraph в Pathfinder (даже после десериализации)", color = Color.red });
                _hasScan = true; SceneView.RepaintAll(); return;
            }

            // Тег-режим (Root == null): ноды берутся по тегу searchTag где угодно — НЕ авто-заполняем Root,
            // иначе затрём намеренную настройку. Root-режим — как раньше (дети Root).
            bool tagMode = pg.root == null &&
                           !string.IsNullOrEmpty(pg.searchTag) && pg.searchTag != "Untagged";

            if (pg.root == null && !tagMode && _autoFillRoot && pathfinder.transform.childCount > 0)
            {
                pg.root = pathfinder.transform.GetChild(0);
                EditorUtility.SetDirty(pathfinder);
                _issues.Add(new Issue { pos = pg.root.position, text = $"Root был пуст. Авто-заполнен: {pg.root.name}", color = Color.yellow });
            }

            if (pg.root == null && !tagMode)
            {
                _issues.Add(new Issue { pos = pathfinder.transform.position, text = "PointGraph.Root не задан и тег searchTag пуст", color = Color.red });
                _hasScan = true; SceneView.RepaintAll(); return;
            }

            float maxDist = pg.maxDistance;
            if (maxDist <= 0f) maxDist = 1f;
            Vector3 axisLimit = (Vector3)pg.limits;
            Vector3 rootPos = pg.root != null ? pg.root.position : _levelRoot.transform.position;

            var nodePositions = new List<Vector3>();
            if (tagMode)
            {
                CollectNodesByTag(_levelRoot.transform, pg.searchTag, nodePositions);
                if (nodePositions.Count == 0)
                    _issues.Add(new Issue { pos = rootPos, text = $"Тег-режим: объектов с тегом '{pg.searchTag}' под корнем не найдено", color = Color.yellow });
            }
            else
            {
                CollectNodes(pg.root, pg.recursive, nodePositions);
            }

            CollectObstacles(_levelRoot.transform);

            for (int i = 0; i < nodePositions.Count; i++)
            {
                var ni = new NodeInfo { pos = nodePositions[i] };
                foreach (var ob in _obstacles)
                {
                    if (ob.bounds.Contains(new Vector3(ni.pos.x, ni.pos.y, ob.bounds.center.z)))
                    {
                        ni.insideObstacle = true;
                        ni.obstacleName = ob.name;
                        _issues.Add(new Issue
                        {
                            pos = ni.pos,
                            text = $"Узел внутри AABB препятствия «{ob.name}»",
                            color = new Color(1f, 0.5f, 0f)
                        });
                        break;
                    }
                }
                _nodes.Add(ni);
            }

            foreach (var ob in _obstacles)
            {
                if (!ob.useInteractionGraphNode) continue;
                int best = -1;
                float bestSq = float.MaxValue;
                for (int i = 0; i < nodePositions.Count; i++)
                {
                    float sq = (nodePositions[i] - ob.interactionPosition).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = i; }
                }
                ob.blockedNodeIndex = best;
            }

            for (int i = 0; i < nodePositions.Count; i++)
            {
                for (int j = i + 1; j < nodePositions.Count; j++)
                {
                    var a = nodePositions[i];
                    var b = nodePositions[j];
                    if (!WithinDistance(a, b, maxDist, axisLimit)) continue;

                    var edge = new EdgeInfo { ia = i, ib = j, a = a, b = b };

                    foreach (var ob in _obstacles)
                    {
                        if (ob.blockedNodeIndex == i || ob.blockedNodeIndex == j)
                        {
                            edge.blocked = true;
                            edge.obstacleName = ob.name;
                            break;
                        }
                    }

                    if (!edge.blocked)
                    {
                        foreach (var ob in _obstacles)
                        {
                            if (SegmentIntersectsBounds2D(a, b, ob.bounds))
                            {
                                edge.leakThrough = true;
                                edge.obstacleName = ob.name;
                                break;
                            }
                        }
                    }

                    _edges.Add(edge);
                }
            }

            var leakingObstacles = new HashSet<string>();
            foreach (var e in _edges) if (e.leakThrough) leakingObstacles.Add(e.obstacleName);
            leakingObstaclesCache = leakingObstacles;
            foreach (var ob in _obstacles)
            {
                if (leakingObstacles.Contains(ob.name))
                {
                    string reason = !ob.useInteractionGraphNode
                        ? "useInteractionGraphNode=false → в рантайме НЕ блокирует узлы"
                        : "блокируется только 1 ближайший узел, остальные edges проходят сквозь AABB";
                    _issues.Add(new Issue
                    {
                        pos = ob.bounds.center,
                        text = $"Сквозной проход через «{ob.name}» — {reason}",
                        color = new Color(1f, 0f, 1f)
                    });
                }
            }

            DetectBypass(nodePositions.Count);
            DetectGaps(nodePositions, maxDist);
            _maxDistCached = maxDist;

            if (_nodes.Count == 0)
                _issues.Add(new Issue { pos = rootPos, text = "Не найдено ни одной точки графа (Root/тег)", color = Color.red });

            if (_obstacles.Count == 0)
                _issues.Add(new Issue { pos = _levelRoot.transform.position, text = "Не найдено ни одного препятствия с BlocksPath", color = Color.yellow });

            _hasScan = true;
            SceneView.RepaintAll();
        }

        private void DetectGaps(List<Vector3> positions, float maxDist)
        {
            int n = positions.Count;
            _component = new int[n];
            for (int i = 0; i < n; i++) _component[i] = -1;

            var isBlockedNode = new bool[n];
            foreach (var ob in _obstacles)
                if (ob.blockedNodeIndex >= 0 && ob.blockedNodeIndex < n)
                    isBlockedNode[ob.blockedNodeIndex] = true;

            var adj = new List<int>[n];
            for (int k = 0; k < n; k++) adj[k] = new List<int>();
            foreach (var e in _edges)
            {
                if (e.blocked) continue;
                if (isBlockedNode[e.ia] || isBlockedNode[e.ib]) continue;
                adj[e.ia].Add(e.ib);
                adj[e.ib].Add(e.ia);
            }

            int comp = 0;
            var q = new Queue<int>();
            for (int s = 0; s < n; s++)
            {
                if (_component[s] != -1) continue;
                if (isBlockedNode[s]) { _component[s] = -2; continue; }
                _component[s] = comp;
                q.Clear(); q.Enqueue(s);
                while (q.Count > 0)
                {
                    int cur = q.Dequeue();
                    foreach (var nb in adj[cur])
                    {
                        if (_component[nb] != -1) continue;
                        _component[nb] = comp;
                        q.Enqueue(nb);
                    }
                }
                comp++;
            }

            if (comp <= 1) return;

            var best = new Dictionary<(int, int), (int ia, int ib, float d)>();
            float searchLimit = maxDist * 1.5f;
            for (int i = 0; i < n; i++)
            {
                if (_component[i] < 0) continue;
                for (int j = i + 1; j < n; j++)
                {
                    if (_component[j] < 0) continue;
                    if (_component[i] == _component[j]) continue;
                    float d = Vector3.Distance(positions[i], positions[j]);
                    if (d <= maxDist) continue;
                    if (d > searchLimit) continue;
                    var key = _component[i] < _component[j]
                        ? (_component[i], _component[j])
                        : (_component[j], _component[i]);
                    if (!best.TryGetValue(key, out var prev) || d < prev.d)
                        best[key] = (i, j, d);
                }
            }

            foreach (var kv in best.Values)
                _gaps.Add((positions[kv.ia], positions[kv.ib], kv.d));

            if (_gaps.Count > 0)
            {
                _issues.Add(new Issue
                {
                    pos = positions[0],
                    text = $"Разрывов пути: {_gaps.Count}. Компонентов связности: {comp} (норма — 1)",
                    color = new Color(1f, 0.3f, 0.3f)
                });
            }
        }

        private void DetectBypass(int nodeCount)
        {
            var adjAll = new List<int>[nodeCount];
            for (int k = 0; k < nodeCount; k++) adjAll[k] = new List<int>();
            foreach (var e in _edges)
            {
                adjAll[e.ia].Add(e.ib);
                adjAll[e.ib].Add(e.ia);
            }

            var visited = new int[nodeCount];
            int mark = 0;
            var queue = new Queue<int>();

            foreach (var ob in _obstacles)
            {
                int bn = ob.blockedNodeIndex;
                if (bn < 0) continue;

                var neighbors = adjAll[bn];
                if (neighbors.Count < 2) continue;

                bool foundBypass = false;
                int start = neighbors[0];
                mark++;
                queue.Clear();
                queue.Enqueue(start);
                visited[start] = mark;
                visited[bn] = mark;

                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    foreach (var nb in adjAll[cur])
                    {
                        if (nb == bn) continue;
                        if (visited[nb] == mark) continue;
                        visited[nb] = mark;
                        queue.Enqueue(nb);
                    }
                }

                for (int i = 1; i < neighbors.Count; i++)
                {
                    if (visited[neighbors[i]] == mark) { foundBypass = true; break; }
                }

                if (foundBypass)
                {
                    for (int idx = 0; idx < _edges.Count; idx++)
                    {
                        var e = _edges[idx];
                        if (e.obstacleName == ob.name && (e.blocked || e.leakThrough))
                        {
                            e.bypass = true;
                            _edges[idx] = e;
                        }
                    }
                    _issues.Add(new Issue
                    {
                        pos = ob.bounds.center,
                        text = $"Препятствие обходимо: «{ob.name}» — есть альтернативный путь",
                        color = Color.yellow
                    });
                }
            }
        }

        private static void CollectNodes(Transform root, bool recursive, List<Vector3> result)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                result.Add(c.position);
                if (recursive) CollectNodes(c, true, result);
            }
        }

        // Тег-режим: рекурсивно собирает позиции всех объектов под root с заданным тегом
        // (аналог рантайма PointGraph при Root==null, но работает и в Prefab Stage).
        private static void CollectNodesByTag(Transform root, string tag, List<Vector3> result)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.CompareTag(tag)) result.Add(t.position);
        }

        private static bool WithinDistance(Vector3 a, Vector3 b, float maxDist, Vector3 axisLimit)
        {
            Vector3 d = a - b;
            if (axisLimit.x > 0 && Mathf.Abs(d.x) > axisLimit.x) return false;
            if (axisLimit.y > 0 && Mathf.Abs(d.y) > axisLimit.y) return false;
            if (axisLimit.z > 0 && Mathf.Abs(d.z) > axisLimit.z) return false;
            return d.sqrMagnitude <= maxDist * maxDist;
        }

        private void CollectObstacles(Transform root)
        {
            var staticViews = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in staticViews)
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if (t.Name != "StaticObjectView") continue;

                var dataField = t.GetField("_dataSO", BindingFlags.Instance | BindingFlags.NonPublic);
                if (dataField == null) continue;
                var data = dataField.GetValue(mb) as ScriptableObject;
                if (data == null) continue;

                var blocksProp = data.GetType().GetProperty("BlocksPath",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (blocksProp == null) continue;
                var val = blocksProp.GetValue(data);
                if (val is not bool b || !b) continue;

                Bounds bounds = ComputeBounds(mb.gameObject);

                bool useIGN = GetFieldBool(mb, "_useInteractionGraphNode", true);
                bool useNearest = GetFieldBool(mb, "_useNearestInteractionGraphNode", true);
                Vector3 interactionPos = mb.transform.position;
                if (useIGN && !useNearest)
                {
                    var ignTransform = t.GetField("_interactionGraphNodeTransform",
                        BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(mb) as Transform;
                    if (ignTransform != null) interactionPos = ignTransform.position;
                }

                _obstacles.Add(new ObstacleInfo
                {
                    name = mb.gameObject.name,
                    bounds = bounds,
                    pos = mb.transform.position,
                    useInteractionGraphNode = useIGN,
                    useNearestInteractionGraphNode = useNearest,
                    interactionPosition = interactionPos
                });
            }
        }

        private static bool GetFieldBool(object obj, string fieldName, bool defaultValue)
        {
            var f = obj.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) return defaultValue;
            var v = f.GetValue(obj);
            return v is bool b ? b : defaultValue;
        }

        private static Bounds ComputeBounds(GameObject go)
        {
            var col2d = go.GetComponent<Collider2D>();
            if (col2d != null) return col2d.bounds;
            var col = go.GetComponent<Collider>();
            if (col != null) return col.bounds;
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null) return rend.bounds;
            return new Bounds(go.transform.position, Vector3.one * 0.5f);
        }

        private static bool SegmentIntersectsBounds2D(Vector3 p1, Vector3 p2, Bounds b)
        {
            float minX = b.min.x, maxX = b.max.x, minY = b.min.y, maxY = b.max.y;
            if (p1.x >= minX && p1.x <= maxX && p1.y >= minY && p1.y <= maxY) return true;
            if (p2.x >= minX && p2.x <= maxX && p2.y >= minY && p2.y <= maxY) return true;

            float dx = p2.x - p1.x, dy = p2.y - p1.y;
            float tMin = 0f, tMax = 1f;
            if (!ClipAxis(p1.x, dx, minX, maxX, ref tMin, ref tMax)) return false;
            if (!ClipAxis(p1.y, dy, minY, maxY, ref tMin, ref tMax)) return false;
            return tMax >= tMin;
        }

        private static bool ClipAxis(float p, float d, float lo, float hi, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(d) < 1e-6f) return p >= lo && p <= hi;
            float t1 = (lo - p) / d;
            float t2 = (hi - p) / d;
            if (t1 > t2) (t1, t2) = (t2, t1);
            if (t1 > tMin) tMin = t1;
            if (t2 < tMax) tMax = t2;
            return tMax >= tMin;
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (!_hasScan) return;

            var bypassed = new HashSet<string>();
            foreach (var e in _edges) if (e.bypass) bypassed.Add(e.obstacleName);

            foreach (var ob in _obstacles)
            {
                Handles.color = bypassed.Contains(ob.name)
                    ? new Color(1f, 1f, 0f, 0.95f)
                    : new Color(1f, 0.3f, 0.1f, 0.9f);
                var c = ob.bounds.center; var e = ob.bounds.extents;
                Vector3 p1 = new(c.x - e.x, c.y - e.y, c.z);
                Vector3 p2 = new(c.x + e.x, c.y - e.y, c.z);
                Vector3 p3 = new(c.x + e.x, c.y + e.y, c.z);
                Vector3 p4 = new(c.x - e.x, c.y + e.y, c.z);
                Handles.DrawAAPolyLine(3f, p1, p2, p3, p4, p1);
            }

            var colFree = new Color(0.15f, 0.95f, 0.25f, 0.95f);
            var colBlocked = new Color(1f, 0.15f, 0.15f, 0.95f);
            var colLeak = new Color(1f, 0f, 1f, 0.95f);
            foreach (var e in _edges)
            {
                Handles.color = e.blocked ? colBlocked : (e.leakThrough ? colLeak : colFree);
                Handles.DrawAAPolyLine(_lineThickness, e.a, e.b);
            }

            Handles.color = new Color(1f, 0.1f, 0.1f, 1f);
            foreach (var gap in _gaps)
                Handles.DrawDottedLine(gap.a, gap.b, 4f);

            var blockedNodes = new HashSet<int>();
            foreach (var ob in _obstacles)
                if (ob.blockedNodeIndex >= 0) blockedNodes.Add(ob.blockedNodeIndex);
            foreach (var idx in blockedNodes)
            {
                if (idx < 0 || idx >= _nodes.Count) continue;
                Handles.color = new Color(1f, 0f, 0f, 1f);
                Handles.SphereHandleCap(0, _nodes[idx].pos, Quaternion.identity, _nodeRadius * 1.8f, EventType.Repaint);
            }

            if (_drawNodes)
            {
                foreach (var n in _nodes)
                {
                    Handles.color = n.insideObstacle
                        ? new Color(1f, 0.6f, 0f, 1f)
                        : new Color(0.2f, 0.7f, 1f, 1f);
                    Handles.SphereHandleCap(0, n.pos, Quaternion.identity, _nodeRadius, EventType.Repaint);
                }
            }

            if (_drawLabels)
            {
                Vector3? mouseWorld = null;
                var ev = Event.current;
                if (_hoverOnly && ev != null)
                {
                    var ray = HandleUtility.GUIPointToWorldRay(ev.mousePosition);
                    var plane = new Plane(Vector3.forward, Vector3.zero);
                    if (plane.Raycast(ray, out float enter))
                        mouseWorld = ray.GetPoint(enter);
                    if (ev.type == EventType.MouseMove) sv.Repaint();
                }

                var pending = new List<(Vector3 pos, string text, Color color)>();

                foreach (var ob in _obstacles)
                {
                    string tag = null; Color c = Color.white; bool include = false;
                    if (leakingObstaclesCache != null && leakingObstaclesCache.Contains(ob.name))
                    { tag = "ДЫРА"; c = colLeak; include = _showLeak; }
                    else if (bypassed.Contains(ob.name))
                    { tag = "ОБХОД"; c = Color.yellow; include = _showBypass; }
                    else if (HasBlockedEdge(ob.name))
                    { tag = "ОК"; c = colBlocked; include = _showOk; }
                    if (!include || tag == null) continue;
                    var lpos = ob.bounds.center + new Vector3(0, ob.bounds.extents.y + 0.2f, 0);
                    if (mouseWorld.HasValue && !ob.bounds.Contains((Vector3)mouseWorld) &&
                        Vector3.Distance((Vector3)mouseWorld, ob.bounds.center) > _hoverRadius) continue;
                    pending.Add((lpos, $"{tag}: {ob.name}", c));
                }

                if (_showGaps)
                {
                    foreach (var gap in _gaps)
                    {
                        var mid = (gap.a + gap.b) * 0.5f;
                        if (mouseWorld.HasValue && Vector3.Distance((Vector3)mouseWorld, mid) > _hoverRadius) continue;
                        pending.Add((mid, $"РАЗРЫВ {gap.dist:F2} > {_maxDistCached:F2}", new Color(1f, 0.2f, 0.2f)));
                    }
                }

                foreach (var i in _issues)
                {
                    bool include;
                    if (i.text.StartsWith("Узел внутри AABB")) include = _showInsideAabb;
                    else if (i.text.StartsWith("Сквозной проход")) include = _showLeak;
                    else if (i.text.StartsWith("Препятствие обходимо")) include = _showBypass;
                    else if (i.text.StartsWith("Разрывов пути")) include = _showGaps;
                    else include = _showOther;
                    if (!include) continue;
                    if (mouseWorld.HasValue && Vector3.Distance((Vector3)mouseWorld, i.pos) > _hoverRadius) continue;
                    pending.Add((i.pos + new Vector3(0.1f, 0.1f, 0f), i.text, i.color));
                }

                DrawLabelsResolved(pending);
            }
        }

        private void DrawLabelsResolved(List<(Vector3 worldPos, string text, Color color)> items)
        {
            if (items.Count == 0) return;

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = _labelFontSize,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                padding = new RectOffset(6, 6, 3, 3)
            };

            var rects = new Rect[items.Count];
            var contents = new GUIContent[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                contents[i] = new GUIContent(items[i].text);
                var size = style.CalcSize(contents[i]);
                var gp = HandleUtility.WorldToGUIPoint(items[i].worldPos);
                rects[i] = new Rect(gp.x - size.x * 0.5f, gp.y - size.y * 0.5f, size.x, size.y);
            }

            const float pad = 2f;
            for (int pass = 0; pass < 8; pass++)
            {
                bool moved = false;
                for (int i = 0; i < rects.Length; i++)
                {
                    for (int j = i + 1; j < rects.Length; j++)
                    {
                        var ri = rects[i]; var rj = rects[j];
                        if (!ri.Overlaps(rj)) continue;

                        float overlapY = Mathf.Min(ri.yMax, rj.yMax) - Mathf.Max(ri.y, rj.y) + pad;
                        if (ri.center.y <= rj.center.y)
                        {
                            ri.y -= overlapY * 0.5f;
                            rj.y += overlapY * 0.5f;
                        }
                        else
                        {
                            ri.y += overlapY * 0.5f;
                            rj.y -= overlapY * 0.5f;
                        }
                        rects[i] = ri; rects[j] = rj;
                        moved = true;
                    }
                }
                if (!moved) break;
            }

            Handles.BeginGUI();
            for (int i = 0; i < rects.Length; i++)
            {
                var rect = rects[i];
                var color = items[i].color;
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.85f));
                EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, 1), color);
                EditorGUI.DrawRect(new Rect(rect.x - 1, rect.yMax, rect.width + 2, 1), color);
                EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y - 1, 1, rect.height + 2), color);
                EditorGUI.DrawRect(new Rect(rect.xMax, rect.y - 1, 1, rect.height + 2), color);
                style.normal.textColor = color;
                GUI.Label(rect, contents[i], style);
            }
            Handles.EndGUI();
        }

        private HashSet<string> leakingObstaclesCache;

        private bool HasBlockedEdge(string name)
        {
            foreach (var e in _edges) if (e.blocked && e.obstacleName == name) return true;
            return false;
        }

        private void DrawLabelBox(Vector3 worldPos, string text, Color color)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = _labelFontSize,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                padding = new RectOffset(6, 6, 3, 3)
            };
            style.normal.textColor = Color.white;

            var content = new GUIContent(text);
            Vector2 size = style.CalcSize(content);

            Handles.BeginGUI();
            var guiPoint = HandleUtility.WorldToGUIPoint(worldPos);
            var rect = new Rect(guiPoint.x - size.x * 0.5f, guiPoint.y - size.y * 0.5f, size.x, size.y);
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.85f));
            var border = new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2);
            EditorGUI.DrawRect(new Rect(border.x, border.y, border.width, 1), color);
            EditorGUI.DrawRect(new Rect(border.x, border.yMax - 1, border.width, 1), color);
            EditorGUI.DrawRect(new Rect(border.x, border.y, 1, border.height), color);
            EditorGUI.DrawRect(new Rect(border.xMax - 1, border.y, 1, border.height), color);
            style.normal.textColor = color;
            GUI.Label(rect, content, style);
            Handles.EndGUI();
        }
    }
}
