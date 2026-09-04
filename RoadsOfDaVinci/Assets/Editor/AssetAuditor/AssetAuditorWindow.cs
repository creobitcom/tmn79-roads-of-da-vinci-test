using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Creobit.LA8.EditorTools
{
    /// <summary>
    /// Окно аудита легаси-ассетов: показывает, какие файлы в GG10/LA7 никем из билда не используются,
    /// сколько памяти освободится, и даёт удалить их в один клик с проверкой безопасности.
    /// </summary>
    public class AssetAuditorWindow : EditorWindow
    {
        const string PrefsKey = "Creobit.LA8.AssetAuditor.Settings";

        [MenuItem("Tools/LA8/Аудит легаси-ассетов (очистка) %#l")]
        public static void Open()
        {
            var w = GetWindow<AssetAuditorWindow>("Аудит ассетов");
            w.minSize = new Vector2(760, 520);
            w.Show();
        }

        // -- состояние
        AssetAuditorEngine _engine;
        AuditSettings _settings;
        AuditResult _result;

        enum Tab { Deletable, Protected, Kept, Holders }
        Tab _tab = Tab.Deletable;

        List<HolderEntry> _holders;
        Vector2 _holdersScroll;
        readonly HashSet<string> _holdersExpanded = new HashSet<string>();

        Node _delTree, _keepTree, _protTree;
        readonly HashSet<string> _selected = new HashSet<string>();

        string _search = "";
        bool _hideRisky = false;
        bool _configExpanded = true;
        bool _filterDirty = true;
        bool _selDirty = true;

        Vector2 _treeScroll, _detailScroll;
        AssetEntry _detail;

        // -- стили (лениво)
        GUIStyle _sTag, _sPathBtn, _sHeader, _sSub, _sRowSel;
        bool _stylesReady;

        void OnEnable()
        {
            _engine = new AssetAuditorEngine();
            LoadSettings();
        }

        // ============================================================ GUI

        void OnGUI()
        {
            EnsureStyles();
            DrawHeader();
            DrawConfig();
            DrawActionBar();

            if (_result == null)
            {
                EditorGUILayout.HelpBox(
                    "Задайте целевые папки и модель корней, затем нажмите «Сканировать».\n" +
                    "Инструмент строит guid-граф зависимостей всего проекта и находит ассеты целевых папок, " +
                    "на которые не ведёт ни одна ссылка из билда.",
                    MessageType.Info);
                return;
            }

            DrawSummary();
            DrawTabs();

            if (_tab == Tab.Holders)
            {
                DrawHolders();
                return;
            }

            DrawToolbar();
            DrawTree();
            DrawDetail();
            DrawFooter();
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Аудит легаси-ассетов", _sHeader);
            EditorGUILayout.LabelField(
                "GnomesGarden10 / LA7 → что реально нужно билду LA8, а что можно удалить.", _sSub);
            EditorGUILayout.Space(2);
        }

        void DrawConfig()
        {
            _configExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_configExpanded, "Настройки");
            if (_configExpanded)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("Целевые папки (кандидаты на удаление):", EditorStyles.boldLabel);
                DrawFolderList(_settings.TargetFolders, "target");

                EditorGUILayout.Space(4);
                var mode = (RootMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Модель корней", "Что считать «используется, не удалять»"),
                    _settings.RootMode);
                if ((int)mode != _settings.RootModeInt) { _settings.RootModeInt = (int)mode; SaveSettings(); }

                switch (_settings.RootMode)
                {
                    case RootMode.WholeProjectExceptTargets:
                        EditorGUILayout.HelpBox("Корни: ВЕСЬ проект, кроме целевых папок. " +
                            "Максимально безопасно — оставляется всё, на что ссылается любой контент билда.", MessageType.None);
                        break;
                    case RootMode.ConsumerFoldersOnly:
                        EditorGUILayout.HelpBox("Корни: только папки-потребители (ниже) + сцены билда. " +
                            "Агрессивнее, удалит больше — перепроверьте билд.", MessageType.Warning);
                        DrawFolderList(_settings.RootFolders, "root");
                        break;
                    case RootMode.Custom:
                        EditorGUILayout.LabelField("Папки-корни:", EditorStyles.boldLabel);
                        DrawFolderList(_settings.RootFolders, "root");
                        break;
                }

                EditorGUILayout.BeginHorizontal();
                _settings.IncludeBuildScenes = GUILayout.Toggle(_settings.IncludeBuildScenes, " + сцены из Build Settings", GUILayout.Width(200));
                _settings.IncludeAddressables = GUILayout.Toggle(_settings.IncludeAddressables, " + Addressables", GUILayout.Width(150));
                _settings.KeepFolderContents = GUILayout.Toggle(_settings.KeepFolderContents, " хранить содержимое папок по ссылке", GUILayout.Width(260));
                EditorGUILayout.EndHorizontal();

                _settings.ProtectCode = GUILayout.Toggle(_settings.ProtectCode,
                    new GUIContent(" защищать код / шейдеры / .inputactions (рекомендуется)",
                        "Их зависимости идут через компиляцию и Shader.Find/имена, а не через guid — граф их не видит. При выключении они станут удаляемыми на твой риск."));
                _settings.IncludeProjectSettings = GUILayout.Toggle(_settings.IncludeProjectSettings,
                    new GUIContent(" учитывать ProjectSettings как корни (Graphics/Quality/Preloaded) — обязательно",
                        "ProjectSettings/* лежат вне Assets/ и ссылаются на активные RenderPipeline/Volume/Preloaded ассеты. Без этого их снесёт как «неиспользуемые»."));
                _settings.IncludePackages = GUILayout.Toggle(_settings.IncludePackages,
                    new GUIContent(" учитывать Packages + Library/PackageCache как корни (модули) — обязательно",
                        "Модули (напр. creobit.timemanagement) лежат вне Assets/ и ссылаются на UI-арт проекта по guid. Без этого их снесёт как «неиспользуемые». Скан немного дольше."));

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawFolderList(List<string> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                list[i] = EditorGUILayout.TextField(list[i]);
                if (GUILayout.Button("…", GUILayout.Width(26)))
                {
                    string abs = EditorUtility.OpenFolderPanel("Выберите папку в Assets", Application.dataPath, "");
                    string rel = ToAssetPath(abs);
                    if (rel != null) { list[i] = rel; SaveSettings(); GUI.FocusControl(null); }
                }
                if (GUILayout.Button("✕", GUILayout.Width(24))) { list.RemoveAt(i); SaveSettings(); GUILayout.EndHorizontal(); break; }
                EditorGUILayout.EndHorizontal();
            }

            // зона Drag&Drop + кнопка добавления
            var drop = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            GUI.Box(drop, "＋ добавить папку (или перетащите сюда)", EditorStyles.helpBox);
            var e = Event.current;
            if (drop.Contains(e.mousePosition))
            {
                if (e.type == EventType.DragUpdated) { DragAndDrop.visualMode = DragAndDropVisualMode.Copy; e.Use(); }
                else if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var o in DragAndDrop.objectReferences)
                    {
                        string p = AssetDatabase.GetAssetPath(o);
                        if (!string.IsNullOrEmpty(p) && AssetDatabase.IsValidFolder(p) && !list.Contains(p)) list.Add(p);
                    }
                    SaveSettings(); e.Use();
                }
                else if (e.type == EventType.MouseDown) { list.Add("Assets/"); SaveSettings(); e.Use(); }
            }
        }

        void DrawActionBar()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_settings.TargetFolders.Count == 0))
            {
                if (GUILayout.Button(_engine.IsIndexed ? "Пересканировать проект" : "Сканировать", GUILayout.Height(26)))
                    RunScan(rebuildIndex: true);
                using (new EditorGUI.DisabledScope(!_engine.IsIndexed))
                    if (GUILayout.Button(new GUIContent("Пересчитать", "Быстро: заново обойти граф без перечитывания файлов"), GUILayout.Height(26), GUILayout.Width(120)))
                        RunScan(rebuildIndex: false);
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawSummary()
        {
            EditorGUILayout.Space(2);
            var r = _result;
            var box = new GUIStyle(EditorStyles.helpBox) { richText = true };
            double savePct = r.TargetTotalBytes > 0 ? 100.0 * r.DeletableBytes / r.TargetTotalBytes : 0;
            string msg =
                $"<b>Можно удалить: {r.Deletable.Count} файлов · {AssetAuditorEngine.HumanSize(r.DeletableBytes)}</b>  " +
                $"({savePct:F1}% от {AssetAuditorEngine.HumanSize(r.TargetTotalBytes)} целевых папок)\n" +
                $"Используется: {r.KeptTargets.Count} · " +
                $"Защищено (код/шейдеры/инпуты): {r.Protected.Count} · " +
                $"Рискованных среди удаляемых: {r.RiskyDeletableCount} (Texture/Resources/Addressable/Scene/Data)  ·  " +
                $"скан: {r.WhenLocal:HH:mm:ss}";
            EditorGUILayout.LabelField(msg, box);
        }

        void DrawTabs()
        {
            var names = new[]
            {
                $"Удаляемые ({_result.Deletable.Count})",
                $"Защищённые ({_result.Protected.Count})",
                $"Используется ({_result.KeptTargets.Count})",
                "Кто держит легаси",
            };
            int cur = (int)_tab;
            int sel = GUILayout.Toolbar(cur, names, EditorStyles.toolbarButton);
            if (sel != cur) { _tab = (Tab)sel; _filterDirty = true; _detail = null; }
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var ns = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.MinWidth(160));
            if (ns != _search) { _search = ns; _filterDirty = true; }

            var nh = GUILayout.Toggle(_hideRisky, "скрыть рискованные", EditorStyles.toolbarButton, GUILayout.Width(150));
            if (nh != _hideRisky) { _hideRisky = nh; _filterDirty = true; }

            if (GUILayout.Button("развернуть", EditorStyles.toolbarButton, GUILayout.Width(80))) SetExpandAll(CurrentTree(), true);
            if (GUILayout.Button("свернуть", EditorStyles.toolbarButton, GUILayout.Width(70))) SetExpandAll(CurrentTree(), false);

            GUILayout.FlexibleSpace();

            if (_tab == Tab.Deletable)
            {
                if (GUILayout.Button("выбрать всё", EditorStyles.toolbarButton, GUILayout.Width(90))) SelectVisible(true, safeOnly: false);
                if (GUILayout.Button("только безопасные", EditorStyles.toolbarButton, GUILayout.Width(130))) SelectVisible(true, safeOnly: true);
                if (GUILayout.Button("снять всё", EditorStyles.toolbarButton, GUILayout.Width(80))) { _selected.Clear(); _selDirty = true; }
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawTree()
        {
            var tree = CurrentTree();
            if (_filterDirty) { RecomputeFilter(tree); _filterDirty = false; _selDirty = true; }
            if (_selDirty) { RecomputeSel(tree); _selDirty = false; }

            _treeScroll = EditorGUILayout.BeginScrollView(_treeScroll, GUILayout.ExpandHeight(true));
            if (tree == null || !tree.FVisible)
                EditorGUILayout.LabelField("Ничего не найдено по фильтру.", EditorStyles.centeredGreyMiniLabel);
            else
                foreach (var k in tree.KidsList) DrawNode(k, 0);
            EditorGUILayout.EndScrollView();
        }

        void DrawNode(Node n, int depth)
        {
            if (!n.FVisible) return;
            bool selectable = _tab == Tab.Deletable;

            var row = EditorGUILayout.BeginHorizontal();
            if (_detail != null && n.IsFile && n.Entry == _detail)
                EditorGUI.DrawRect(row, new Color(0.3f, 0.5f, 0.9f, 0.15f));

            GUILayout.Space(depth * 14 + 4);

            if (!n.IsFile)
            {
                n.Expanded = EditorGUILayout.Toggle(n.Expanded, EditorStyles.foldout, GUILayout.Width(14));
            }
            else GUILayout.Space(14);

            if (selectable)
            {
                bool all = n.SelCount > 0 && n.SelCount == n.FCount;
                bool part = n.SelCount > 0 && !all;
                EditorGUI.showMixedValue = part;
                bool nv = EditorGUILayout.Toggle(all, GUILayout.Width(16));
                EditorGUI.showMixedValue = false;
                if (nv != all) ToggleNode(n, nv);
            }

            var icon = AssetDatabase.GetCachedIcon(n.FullPath);
            if (icon != null) GUILayout.Label(icon, GUILayout.Width(16), GUILayout.Height(16));

            string label = n.IsFile ? n.Name : $"{n.Name}";
            if (GUILayout.Button(label, _sPathBtn, GUILayout.ExpandWidth(false)))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(n.FullPath);
                if (obj != null) EditorGUIUtility.PingObject(obj);
                if (n.IsFile) _detail = n.Entry;
            }

            if (!n.IsFile)
                GUILayout.Label($"  {n.FCount} · {AssetAuditorEngine.HumanSize(n.FBytes)}", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            if (n.IsFile && n.Entry.ProtectReason != null)
                DrawTag(n.Entry.ProtectReason, new Color(0.45f, 0.75f, 1f));
            else if (n.IsFile && n.Entry.Risk != RiskFlags.None)
                DrawTag(AssetAuditorEngine.RiskLabel(n.Entry.Risk), new Color(1f, 0.7f, 0.25f));
            if (n.IsFile) GUILayout.Label(AssetAuditorEngine.HumanSize(n.Entry.Size), EditorStyles.miniLabel, GUILayout.Width(72));

            EditorGUILayout.EndHorizontal();

            if (!n.IsFile && n.Expanded)
                foreach (var k in n.KidsList) DrawNode(k, depth + 1);
        }

        void DrawTag(string text, Color color)
        {
            var c = GUI.color;
            GUI.color = color;
            GUILayout.Label(text, _sTag);
            GUI.color = c;
        }

        void DrawDetail()
        {
            if (_detail == null) return;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(_detail.Path, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"размер: {AssetAuditorEngine.HumanSize(_detail.Size)}   guid: {_detail.Guid}" +
                (_detail.ProtectReason != null ? $"   защищено: {_detail.ProtectReason}" : "") +
                (_detail.Risk != RiskFlags.None ? $"   риск: {AssetAuditorEngine.RiskLabel(_detail.Risk)}" : ""),
                EditorStyles.miniLabel);

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.MaxHeight(110));
            if (_tab == Tab.Protected)
            {
                EditorGUILayout.LabelField(
                    "Исключён из удаления: зависимости этого типа идут через компиляцию / Shader.Find / имена, " +
                    "а не через guid — граф не может доказать, что он не нужен. Чистить такое — отдельно (IDE/компилятор).",
                    EditorStyles.wordWrappedMiniLabel);
                var refs = _engine.Referrers(_result, _detail);
                if (refs != null)
                {
                    EditorGUILayout.LabelField($"Есть guid-ссылки от ({refs.Count}):", EditorStyles.miniBoldLabel);
                    foreach (var rp in refs.Take(40)) DrawReferrerRow(rp);
                }
            }
            else if (_tab == Tab.Kept)
            {
                // Цепочка «почему оставлено»: ассет -> родитель -> ... -> корень (последний).
                var chain = _engine.ChainToRoot(_result, _detail.Path);
                string root = chain.Count > 0 ? chain[chain.Count - 1] : _detail.Path;
                bool rootIsTarget = AssetAuditorEngine.IsUnder(root, _settings.TargetFolders);
                EditorGUILayout.LabelField(
                    rootIsTarget
                        ? $"Почему оставлено: цепочка ({chain.Count}) упирается в корень ВНУТРИ целевой папки — {root}"
                        : $"Почему оставлено: цепочка ({chain.Count}) до корня — {root}",
                    EditorStyles.miniBoldLabel);
                for (int i = 0; i < chain.Count; i++)
                {
                    string suffix = i == chain.Count - 1 ? "⟵ КОРЕНЬ" : "";
                    DrawReferrerRow(chain[i], Math.Min(i, 12), suffix);
                }

                var refs = _engine.Referrers(_result, _detail);
                if (refs != null && refs.Count > 1)
                {
                    EditorGUILayout.LabelField($"Все прямые ссылающиеся ({refs.Count}):", EditorStyles.miniBoldLabel);
                    foreach (var rp in refs.Take(40)) DrawReferrerRow(rp);
                }
            }
            else
            {
                if (_result.DeletableReverse.TryGetValue(_detail.Path, out var inner) && inner.Count > 0)
                {
                    EditorGUILayout.LabelField($"Ссылаются только другие удаляемые ({inner.Count}):", EditorStyles.miniBoldLabel);
                    foreach (var rp in inner.Take(60)) DrawReferrerRow(rp);
                }
                else EditorGUILayout.LabelField("Не используется билдом. Ни один оставляемый ассет не ссылается.", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawHolders()
        {
            if (_holders == null)
                _holders = _engine.BuildLegacyHolders(_result, _settings);

            EditorGUILayout.HelpBox(
                "Каждая строка — ассет ВНЕ целевых папок, который первым тянет за собой легаси. " +
                "Отцепи ссылку здесь — и весь его хвост уедет из билда. Сортировка по весу удерживаемого.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Держателей: {_holders.Count}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Экспорт CSV", EditorStyles.toolbarButton, GUILayout.Width(100)))
                ExportHoldersCsv();
            if (GUILayout.Button("Экспорт ВСЕХ файлов", EditorStyles.toolbarButton, GUILayout.Width(150)))
                ExportKeptFilesCsv();
            EditorGUILayout.EndHorizontal();

            _holdersScroll = EditorGUILayout.BeginScrollView(_holdersScroll);
            foreach (var h in _holders)
            {
                EditorGUILayout.BeginHorizontal();
                var expanded = _holdersExpanded.Contains(h.Path);
                if (GUILayout.Button(expanded ? "▼" : "▶", EditorStyles.label, GUILayout.Width(16)))
                {
                    if (expanded) _holdersExpanded.Remove(h.Path);
                    else _holdersExpanded.Add(h.Path);
                }

                GUILayout.Label(AssetAuditorEngine.HumanSize(h.HeldBytes), EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.Label($"{h.HeldCount} шт", EditorStyles.miniLabel, GUILayout.Width(60));

                var icon = AssetDatabase.GetCachedIcon(h.Path);
                if (icon != null) GUILayout.Label(icon, GUILayout.Width(14), GUILayout.Height(14));
                if (GUILayout.Button(h.Path, _sPathBtn, GUILayout.ExpandWidth(false)))
                {
                    var o = AssetDatabase.LoadMainAssetAtPath(h.Path);
                    if (o != null) EditorGUIUtility.PingObject(o);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (!_holdersExpanded.Contains(h.Path)) continue;
                foreach (var sample in h.Samples)
                    DrawReferrerRow(sample.Path, 2, AssetAuditorEngine.HumanSize(sample.Size));
                if (h.HeldCount > h.Samples.Count)
                    EditorGUILayout.LabelField($"        … и ещё {h.HeldCount - h.Samples.Count}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        void ExportKeptFilesCsv()
        {
            var path = EditorUtility.SaveFilePanel("Экспорт всех оставленных файлов", "", "legacy-kept-files.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            if (_holders == null)
                _holders = _engine.BuildLegacyHolders(_result, _settings);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("size_mb;risk;path;holder;chain_root;referrers");

            foreach (var e in _result.KeptTargets)
            {
                _result.HolderByPath.TryGetValue(e.Path, out var holder);

                var root = e.Path;
                var guard = 0;
                while (_result.KeepParent.TryGetValue(root, out var parent) && guard++ < 4096) root = parent;

                var referrers = "";
                if (e.Guid != null && _result.KeptReferrers.TryGetValue(e.Guid, out var set))
                    referrers = string.Join("|", set.Take(6));

                sb.AppendLine($"{e.Size / 1048576.0:F4};{e.Risk};{e.Path};{holder};{root};{referrers}");
            }

            System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));
            EditorUtility.RevealInFinder(path);
        }

        void ExportHoldersCsv()
        {
            var path = EditorUtility.SaveFilePanel("Экспорт держателей легаси", "", "legacy-holders.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("held_mb;held_files;holder_path");
            foreach (var h in _holders)
                sb.AppendLine($"{h.HeldBytes / 1048576.0:F3};{h.HeldCount};{h.Path}");

            System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));
            EditorUtility.RevealInFinder(path);
        }

        void DrawReferrerRow(string path, int indent = 0, string suffix = "")
        {
            EditorGUILayout.BeginHorizontal();
            if (indent > 0) GUILayout.Space(indent * 12);
            var icon = AssetDatabase.GetCachedIcon(path);
            if (icon != null) GUILayout.Label(icon, GUILayout.Width(14), GUILayout.Height(14));
            if (GUILayout.Button(path, _sPathBtn, GUILayout.ExpandWidth(false)))
            {
                var o = AssetDatabase.LoadMainAssetAtPath(path);
                if (o != null) EditorGUIUtility.PingObject(o);
            }
            if (!string.IsNullOrEmpty(suffix)) GUILayout.Label(suffix, EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawFooter()
        {
            long selBytes = 0; int selCount = 0;
            foreach (var p in _selected) { selCount++; }
            // размер выбранного берём из дерева удаляемых
            selBytes = _result.Deletable.Where(e => _selected.Contains(e.Path)).Sum(e => e.Size);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Выбрано: {selCount} · {AssetAuditorEngine.HumanSize(selBytes)}", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Экспорт CSV", EditorStyles.toolbarButton, GUILayout.Width(100))) ExportCsv();

            using (new EditorGUI.DisabledScope(selCount == 0))
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
                if (GUILayout.Button($"Удалить выбранное → Корзина", EditorStyles.toolbarButton, GUILayout.Width(190)))
                    DoDelete();
                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();
        }

        // ============================================================ Действия

        void RunScan(bool rebuildIndex)
        {
            SaveSettings();
            _selected.Clear();
            _detail = null;
            _holders = null;
            _holdersExpanded.Clear();
            try
            {
                Func<float, string, bool> prog = (p, m) => EditorUtility.DisplayCancelableProgressBar("Аудит ассетов", m, p);
                if (rebuildIndex || !_engine.IsIndexed)
                    if (!_engine.BuildIndex(prog)) { return; }
                EditorUtility.DisplayCancelableProgressBar("Аудит ассетов", "Обход графа зависимостей…", 0.5f);
                _result = _engine.Compute(_settings, prog);
                _delTree = BuildTree(_result.Deletable);
                _keepTree = BuildTree(_result.KeptTargets);
                _protTree = BuildTree(_result.Protected);
                _filterDirty = true; _selDirty = true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Аудит ассетов", "Ошибка: " + ex.Message, "OK");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        void DoDelete()
        {
            var sel = new HashSet<string>(_selected);
            var dangling = _engine.FindDanglingAfterDelete(_result, sel);
            int risky = _result.Deletable.Count(e => sel.Contains(e.Path) && e.Risk != RiskFlags.None);
            long bytes = _result.Deletable.Where(e => sel.Contains(e.Path)).Sum(e => e.Size);

            string warn = "";
            if (dangling.Count > 0)
                warn += $"\n\n⚠ {dangling.Count} выбранных ассетов ещё нужны ДРУГИМ (невыбранным) удаляемым — " +
                        "это создаст missing reference. Снимите их или выберите ссылающихся тоже.";
            if (risky > 0)
                warn += $"\n\n⚠ {risky} рискованных (Texture/Resources/Addressable/Scene/Data) — могут грузиться по имени/адресу/пути или через SpriteAtlas, граф их не видит. Проверьте вручную.";

            bool ok = EditorUtility.DisplayDialog("Удаление в Корзину",
                $"Удалить {sel.Count} файлов ({AssetAuditorEngine.HumanSize(bytes)}) в Корзину?\n" +
                "Файлы под контролем git — восстановимы. Через AssetDatabase удаляются вместе с .meta." + warn,
                "Удалить", "Отмена");
            if (!ok) return;

            var res = _engine.DeleteToTrash(sel);
            int emptied = _engine.DeleteEmptyFolders(_settings.TargetFolders.Select(f => f.Replace('\\', '/').TrimEnd('/')));

            EditorUtility.DisplayDialog("Готово",
                $"Удалено: {res.deleted} · освобождено {AssetAuditorEngine.HumanSize(res.bytes)}\n" +
                (res.failed > 0 ? $"Не удалось: {res.failed}\n" : "") +
                (emptied > 0 ? $"Удалено пустых папок: {emptied}" : ""), "OK");

            RunScan(rebuildIndex: true); // индекс изменился — пересобираем
        }

        void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("Экспорт отчёта", "", "asset-audit.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            _engine.ExportCsv(_result, path);
            EditorUtility.RevealInFinder(path);
        }

        // ============================================================ Дерево

        class Node
        {
            public string Name, FullPath;
            public bool IsFile;
            public AssetEntry Entry;
            public Dictionary<string, Node> Kids;
            public List<Node> KidsList;
            public long Bytes; public int Count;
            public bool Expanded;
            public bool FVisible; public long FBytes; public int FCount; public int SelCount;
        }

        Node CurrentTree() => _tab == Tab.Deletable ? _delTree : (_tab == Tab.Protected ? _protTree : _keepTree);

        static Node BuildTree(List<AssetEntry> entries)
        {
            var root = new Node { Name = "", FullPath = "", Kids = new Dictionary<string, Node>() };
            foreach (var e in entries)
            {
                var segs = e.Path.Split('/');
                var cur = root; string acc = "";
                for (int i = 0; i < segs.Length; i++)
                {
                    acc = i == 0 ? segs[0] : acc + "/" + segs[i];
                    bool leaf = i == segs.Length - 1;
                    if (cur.Kids == null) cur.Kids = new Dictionary<string, Node>();
                    if (!cur.Kids.TryGetValue(segs[i], out var nx))
                    {
                        nx = new Node { Name = segs[i], FullPath = acc, IsFile = leaf, Entry = leaf ? e : null, Expanded = false };
                        cur.Kids[segs[i]] = nx;
                    }
                    cur = nx;
                }
            }
            Aggregate(root);
            return root;
        }

        static void Aggregate(Node n)
        {
            if (n.IsFile) { n.Bytes = n.Entry.Size; n.Count = 1; return; }
            n.KidsList = n.Kids.Values
                .OrderBy(k => k.IsFile ? 1 : 0)
                .ThenBy(k => k.Name, StringComparer.OrdinalIgnoreCase).ToList();
            long b = 0; int c = 0;
            foreach (var k in n.KidsList) { Aggregate(k); b += k.Bytes; c += k.Count; }
            n.Bytes = b; n.Count = c;
        }

        bool MatchFile(AssetEntry e)
        {
            if (_hideRisky && e.Risk != RiskFlags.None) return false;
            if (!string.IsNullOrEmpty(_search) && e.Path.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) return false;
            return true;
        }

        void RecomputeFilter(Node n)
        {
            if (n == null) return;
            if (n.IsFile) { n.FVisible = MatchFile(n.Entry); n.FBytes = n.FVisible ? n.Entry.Size : 0; n.FCount = n.FVisible ? 1 : 0; return; }
            long b = 0; int c = 0; bool any = false;
            if (n.KidsList != null)
                foreach (var k in n.KidsList) { RecomputeFilter(k); if (k.FVisible) { any = true; b += k.FBytes; c += k.FCount; } }
            n.FVisible = any; n.FBytes = b; n.FCount = c;
        }

        void RecomputeSel(Node n)
        {
            if (n == null) return;
            if (n.IsFile) { n.SelCount = (n.FVisible && _selected.Contains(n.FullPath)) ? 1 : 0; return; }
            int s = 0;
            if (n.KidsList != null) foreach (var k in n.KidsList) { RecomputeSel(k); s += k.SelCount; }
            n.SelCount = s;
        }

        void ToggleNode(Node n, bool select)
        {
            CollectVisibleFiles(n, f =>
            {
                if (select) _selected.Add(f.FullPath); else _selected.Remove(f.FullPath);
            });
            _selDirty = true;
        }

        void CollectVisibleFiles(Node n, Action<Node> act)
        {
            if (!n.FVisible) return;
            if (n.IsFile) { act(n); return; }
            if (n.KidsList != null) foreach (var k in n.KidsList) CollectVisibleFiles(k, act);
        }

        void SelectVisible(bool select, bool safeOnly)
        {
            var tree = _delTree;
            if (tree == null) return;
            if (_filterDirty) { RecomputeFilter(tree); _filterDirty = false; }
            CollectVisibleFiles(tree, f =>
            {
                if (safeOnly && f.Entry.Risk != RiskFlags.None) return;
                if (select) _selected.Add(f.FullPath); else _selected.Remove(f.FullPath);
            });
            _selDirty = true;
        }

        static void SetExpandAll(Node n, bool v)
        {
            if (n == null || n.IsFile) return;
            n.Expanded = v;
            if (n.KidsList != null) foreach (var k in n.KidsList) SetExpandAll(k, v);
        }

        // ============================================================ Утилиты

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _sHeader = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            _sSub = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            _sTag = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            _sPathBtn = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
            _stylesReady = true;
        }

        static string ToAssetPath(string abs)
        {
            if (string.IsNullOrEmpty(abs)) return null;
            abs = abs.Replace('\\', '/');
            string data = Application.dataPath.Replace('\\', '/');
            if (abs == data) return "Assets";
            if (abs.StartsWith(data + "/")) return "Assets" + abs.Substring(data.Length);
            return null;
        }

        void LoadSettings()
        {
            var json = EditorPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { _settings = JsonUtility.FromJson<AuditSettings>(json); } catch { _settings = null; }
            }
            if (_settings == null) _settings = new AuditSettings();
            if (_settings.TargetFolders == null || _settings.TargetFolders.Count == 0)
                _settings.TargetFolders = new List<string> { "Assets/GnomesGarden10", "Assets/LA7" };
            if (_settings.RootFolders == null || _settings.RootFolders.Count == 0)
                _settings.RootFolders = new List<string> { "Assets/LA8_slv" };
        }

        void SaveSettings() => EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(_settings));

        void OnDisable() => SaveSettings();
    }
}
