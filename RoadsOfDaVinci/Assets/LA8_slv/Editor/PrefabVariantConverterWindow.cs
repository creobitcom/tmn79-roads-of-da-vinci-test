#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Drop folders or prefabs, analyze what will happen, then convert.
/// All work is done by ConvertPrefabVariantsToOriginals.
/// </summary>
public sealed class PrefabVariantConverterWindow : EditorWindow
{
	private sealed class Analysis
	{
		public List<ConvertPrefabVariantsToOriginals.VariantInfo> Variants =
			new List<ConvertPrefabVariantsToOriginals.VariantInfo>();
		public List<string> AddedDescendants = new List<string>();
		public List<string> Owners = new List<string>();
		public ConvertPrefabVariantsToOriginals.ReferenceScan Scan;
		public int PrefabsInRoots;
		public string Blockers = string.Empty;
	}

	private enum PendingAction
	{
		None,
		Analyze,
		DryRun,
		Convert,
		ValidateSet,
		ValidateProject
	}

	private readonly List<string> _roots = new List<string>();

	// Analysis and conversion must not run inside OnGUI: they show progress bars,
	// modal dialogs and can take minutes, which breaks the IMGUI layout stack.
	private PendingAction _pending;
	private Analysis _analysis;
	private string _report;
	private string _error;
	private bool _verifyOwners;
	private bool _openEmptyScene = true;
	private bool _useBatches = true;
	private int _batchSize = 25;
	private bool _showVariants = true;
	private bool _showOwners;
	private Vector2 _scroll;

	[MenuItem("Window/Tools/Prefab Variants → Originals", false, 1000)]
	public static void Open()
	{
		var window = GetWindow<PrefabVariantConverterWindow>("Variants → Originals");
		window.minSize = new Vector2(620f, 460f);
		window.Show();
	}

	private void Update()
	{
		if (_pending == PendingAction.None)
			return;

		var action = _pending;
		_pending = PendingAction.None;

		switch (action)
		{
			case PendingAction.Analyze:
				Analyze();
				break;
			case PendingAction.DryRun:
				RunConversion(true);
				break;
			case PendingAction.Convert:
				RunConversion(false);
				break;
			case PendingAction.ValidateSet:
				Validate(false);
				break;
			case PendingAction.ValidateProject:
				Validate(true);
				break;
		}
	}

	private void Validate(bool wholeProject)
	{
		_error = null;
		try
		{
			var targets = wholeProject
				? null
				: ConvertPrefabVariantsToOriginals.CollectAllPrefabs(_roots);

			if (!wholeProject && (targets == null || targets.Count == 0))
			{
				_error = "В наборе нет префабов — перетащи папку или префаб.";
				return;
			}

			_report = PrefabReferenceValidator.Validate(targets).ToString();
			Debug.Log("[ValidateRefs]\n" + _report);
		}
		catch (Exception exception)
		{
			_error = exception.Message;
			Debug.LogException(exception);
		}
		finally
		{
			EditorUtility.ClearProgressBar();
			Repaint();
		}
	}

	private void OnGUI()
	{
		if (_pending != PendingAction.None)
		{
			EditorGUILayout.Space(20f);
			EditorGUILayout.LabelField("Работаю...", EditorStyles.boldLabel);
			return;
		}

		_scroll = EditorGUILayout.BeginScrollView(_scroll);

		DrawDropArea();
		DrawRoots();
		DrawActions();
		DrawAnalysis();
		DrawResult();

		EditorGUILayout.EndScrollView();
	}

	// ------------------------------------------------------------------ drop area

	private void DrawDropArea()
	{
		EditorGUILayout.Space(4f);

		var rect = GUILayoutUtility.GetRect(0f, 56f, GUILayout.ExpandWidth(true));
		var hover = rect.Contains(Event.current.mousePosition);
		GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
		var label = _roots.Count == 0
			? "Перетащи сюда папки или .prefab"
			: $"Перетащи сюда ещё папки или .prefab ({_roots.Count} в наборе)";
		GUI.Label(rect, label, new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });

		var evt = Event.current;
		if (!hover || (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform))
			return;

		DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
		if (evt.type != EventType.DragPerform)
			return;

		var paths = DragAndDrop.paths != null && DragAndDrop.paths.Length > 0
			? DragAndDrop.paths.ToList()
			: DragAndDrop.objectReferences.Select(AssetDatabase.GetAssetPath).ToList();

		foreach (var path in paths)
			AddRoot(path);

		DragAndDrop.AcceptDrag();
		evt.Use();
		Repaint();
	}

	private void AddRoot(string path)
	{
		if (string.IsNullOrEmpty(path))
			return;

		path = path.Replace('\\', '/');
		if (!path.StartsWith("Assets/", StringComparison.Ordinal))
			return;

		var isFolder = AssetDatabase.IsValidFolder(path);
		if (!isFolder && !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
			return;

		if (_roots.Any(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
			return;

		_roots.Add(path);
		_roots.Sort(StringComparer.Ordinal);
		_analysis = null;
		_report = null;
		_error = null;
	}

	private void DrawRoots()
	{
		if (_roots.Count == 0)
			return;

		EditorGUILayout.Space(4f);
		EditorGUILayout.LabelField("Набор", EditorStyles.boldLabel);

		for (var index = _roots.Count - 1; index >= 0; index--)
		{
			EditorGUILayout.BeginHorizontal();

			var isFolder = AssetDatabase.IsValidFolder(_roots[index]);
			GUILayout.Label(isFolder ? "папка" : "префаб", EditorStyles.miniLabel, GUILayout.Width(48f));
			EditorGUILayout.LabelField(_roots[index]);

			if (GUILayout.Button("→", GUILayout.Width(26f)))
				Reveal(_roots[index]);

			if (GUILayout.Button("×", GUILayout.Width(26f)))
			{
				_roots.RemoveAt(index);
				_analysis = null;
			}

			EditorGUILayout.EndHorizontal();
		}
	}

	// ------------------------------------------------------------------ actions

	private void DrawActions()
	{
		EditorGUILayout.Space(6f);
		EditorGUILayout.BeginHorizontal();

		if (GUILayout.Button("Добавить выделенное"))
		{
			foreach (var selected in Selection.objects)
				AddRoot(AssetDatabase.GetAssetPath(selected));
		}

		if (GUILayout.Button("LA8/GameObjects"))
			AddRoot(ConvertPrefabVariantsToOriginals.DefaultGameObjectsFolder);

		using (new EditorGUI.DisabledScope(_roots.Count == 0))
		{
			if (GUILayout.Button("Очистить"))
			{
				_roots.Clear();
				_analysis = null;
				_report = null;
				_error = null;
			}
		}

		EditorGUILayout.EndHorizontal();

		using (new EditorGUI.DisabledScope(_roots.Count == 0))
		{
			if (GUILayout.Button("Анализировать", GUILayout.Height(26f)))
				_pending = PendingAction.Analyze;
		}

		EditorGUILayout.Space(2f);
		EditorGUILayout.BeginHorizontal();
		using (new EditorGUI.DisabledScope(_roots.Count == 0))
		{
			if (GUILayout.Button("Проверить ссылки в набор"))
				_pending = PendingAction.ValidateSet;
		}

		if (GUILayout.Button("Проверить весь проект"))
			_pending = PendingAction.ValidateProject;

		EditorGUILayout.EndHorizontal();
		EditorGUILayout.LabelField(
			"Проверка спрашивает Unity про каждый объект — видит и вычисляемые id вложенных префабов.",
			EditorStyles.miniLabel);
	}

	private void DrawAnalysis()
	{
		if (_analysis == null)
		{
			EditorGUILayout.Space(6f);
			EditorGUILayout.HelpBox(
				"Перетащи папку (например Assets/LA8_slv/Levels/Pack02) или отдельные префабы и нажми " +
				"«Анализировать». Ничего не изменится, пока не нажмёшь «Конвертировать».",
				MessageType.Info);
			return;
		}

		EditorGUILayout.Space(8f);
		EditorGUILayout.LabelField("Анализ", EditorStyles.boldLabel);

		if (_analysis.Variants.Count == 0)
		{
			EditorGUILayout.HelpBox(
				$"Prefab Variant не найдено. Префабов в наборе: {_analysis.PrefabsInRoots} — все уже обычные.",
				MessageType.Info);
			return;
		}

		if (_analysis.Scan == null)
		{
			EditorGUILayout.HelpBox("Анализ не завершён — см. ошибку ниже.", MessageType.Warning);
			return;
		}

		var skipped = Mathf.Max(0, _analysis.PrefabsInRoots - _analysis.Variants.Count);
		EditorGUILayout.LabelField($"Variant'ов к конвертации: {_analysis.Variants.Count}" +
		                           (skipped > 0 ? $"   (обычных префабов пропущено: {skipped})" : string.Empty));
		EditorGUILayout.LabelField($"Ссылок из проекта внутрь этих префабов: {_analysis.Scan.TotalReferences}");
		EditorGUILayout.LabelField($"Файлов-владельцев, которые могут быть перезаписаны: {_analysis.Owners.Count}");

		if (_analysis.AddedDescendants.Count > 0)
		{
			EditorGUILayout.HelpBox(
				$"Автоматически добавлено Variant-потомков: {_analysis.AddedDescendants.Count}.\n" +
				"Их fileID производны от базы, поэтому конвертировать базу без потомков нельзя — " +
				"иначе ссылки на потомков молча сломаются.\n" +
				string.Join("\n", _analysis.AddedDescendants.Take(10)) +
				(_analysis.AddedDescendants.Count > 10
					? $"\n... и ещё {_analysis.AddedDescendants.Count - 10}"
					: string.Empty),
				MessageType.Warning);
		}

		if (!string.IsNullOrEmpty(_analysis.Blockers))
			EditorGUILayout.HelpBox("Нельзя конвертировать сейчас:\n" + _analysis.Blockers, MessageType.Error);

		_showVariants = EditorGUILayout.Foldout(
			_showVariants,
			$"Порядок конвертации ({_analysis.Variants.Count}) — от самых глубоких вариантов",
			true);
		if (_showVariants)
		{
			EditorGUI.indentLevel++;
			foreach (var variant in _analysis.Variants)
			{
				_analysis.Scan.ReferencesByGuid.TryGetValue(variant.Guid, out var refs);

				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("глуб. " + variant.Depth, EditorStyles.miniLabel, GUILayout.Width(58f));
				GUILayout.Label(refs + " ссыл.", EditorStyles.miniLabel, GUILayout.Width(72f));
				EditorGUILayout.LabelField(
					new GUIContent(Path.GetFileName(variant.Path), variant.Path + "\nбаза: " + variant.BasePath));
				GUILayout.Label(
					string.IsNullOrEmpty(variant.BasePath) ? "?" : Path.GetFileName(variant.BasePath),
					EditorStyles.miniLabel,
					GUILayout.Width(180f));
				if (GUILayout.Button("→", GUILayout.Width(26f)))
					Reveal(variant.Path);
				EditorGUILayout.EndHorizontal();
			}

			EditorGUI.indentLevel--;
		}

		_showOwners = EditorGUILayout.Foldout(_showOwners, $"Кто на них ссылается ({_analysis.Owners.Count})", true);
		if (_showOwners)
		{
			EditorGUI.indentLevel++;
			foreach (var owner in _analysis.Owners.Take(200))
				EditorGUILayout.LabelField(owner);
			if (_analysis.Owners.Count > 200)
				EditorGUILayout.LabelField($"... и ещё {_analysis.Owners.Count - 200}");
			EditorGUI.indentLevel--;
		}

		EditorGUILayout.Space(8f);
		_openEmptyScene = EditorGUILayout.ToggleLeft(
			"Открыть пустую сцену перед конвертацией (рекомендуется)", _openEmptyScene);
		_verifyOwners = EditorGUILayout.ToggleLeft(
			$"Проверить fileID во ВСЕХ файлах-владельцах ({_analysis.Owners.Count(IsPrefabOwner)} .prefab, медленно) — " +
			"те, внутрь которых кто-то ссылается, проверяются всегда",
			_verifyOwners);

		_useBatches = EditorGUILayout.ToggleLeft(
			"Пачками — каждая пачка своя транзакция, упавшая не отменяет предыдущие", _useBatches);
		if (_useBatches)
		{
			EditorGUI.indentLevel++;
			_batchSize = EditorGUILayout.IntSlider("Префабов в пачке", _batchSize, 5, 100);
			var batches = ConvertPrefabVariantsToOriginals.SplitIntoTransactions(_analysis.Variants, _batchSize);
			EditorGUILayout.LabelField(
				$"Пачек: {batches.Count} (семейства вариантов не разрезаются, " +
				$"крупнейшая: {batches.Max(batch => batch.Count)})",
				EditorStyles.miniLabel);
			EditorGUI.indentLevel--;
		}

		EditorGUILayout.Space(4f);
		EditorGUILayout.BeginHorizontal();

		if (GUILayout.Button("Проверить без записи (dry run)", GUILayout.Height(28f)))
			_pending = PendingAction.DryRun;

		using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(_analysis.Blockers)))
		{
			var color = GUI.backgroundColor;
			GUI.backgroundColor = new Color(1f, 0.75f, 0.4f);
			if (GUILayout.Button($"Конвертировать ({_analysis.Variants.Count})", GUILayout.Height(28f)))
				_pending = PendingAction.Convert;
			GUI.backgroundColor = color;
		}

		EditorGUILayout.EndHorizontal();
	}

	private void DrawResult()
	{
		if (!string.IsNullOrEmpty(_error))
		{
			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Остановлено", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(_error, MessageType.Error);
			if (GUILayout.Button("Скопировать текст ошибки"))
				EditorGUIUtility.systemCopyBuffer = _error;
		}

		if (string.IsNullOrEmpty(_report))
			return;

		EditorGUILayout.Space(8f);
		EditorGUILayout.LabelField("Отчёт", EditorStyles.boldLabel);
		EditorGUILayout.SelectableLabel(_report, EditorStyles.textArea, GUILayout.Height(120f));
		if (GUILayout.Button("Скопировать отчёт"))
			EditorGUIUtility.systemCopyBuffer = _report;
	}

	// ------------------------------------------------------------------ work

	private void Analyze()
	{
		_report = null;
		_error = null;

		var analysis = new Analysis();
		try
		{
			analysis.PrefabsInRoots = CountPrefabsInRoots();

			var variants = ConvertPrefabVariantsToOriginals.CollectVariants(_roots);
			if (variants.Count > 0)
			{
				var missing = ConvertPrefabVariantsToOriginals.FindMissingVariantDescendants(
					variants.Select(variant => variant.Path).ToList());

				if (missing.Count > 0)
				{
					var expanded = variants.Select(variant => variant.Path).ToList();
					expanded.AddRange(missing);
					variants = ConvertPrefabVariantsToOriginals.CollectVariants(expanded);
					analysis.AddedDescendants = missing;
				}
			}

			analysis.Variants = variants;

			var guids = new HashSet<string>(
				variants.Select(variant => variant.Guid),
				StringComparer.OrdinalIgnoreCase);
			analysis.Scan = ConvertPrefabVariantsToOriginals.ScanReferences(
				guids, null, "Scan project references", -1f);

			var targets = new HashSet<string>(
				variants.Select(variant => variant.Path),
				StringComparer.OrdinalIgnoreCase);
			analysis.Owners = analysis.Scan.ReferencesByOwner
				.Where(entry => !targets.Contains(entry.Key))
				.OrderByDescending(entry => entry.Value)
				.ThenBy(entry => entry.Key, StringComparer.Ordinal)
				.Select(entry => $"{entry.Value,6}  {entry.Key}")
				.ToList();

			analysis.Blockers = ConvertPrefabVariantsToOriginals.DescribeEnvironmentBlockers(true);
		}
		catch (Exception exception)
		{
			_error = exception.Message;
			Debug.LogException(exception);
		}
		finally
		{
			EditorUtility.ClearProgressBar();
		}

		_analysis = analysis;
		Repaint();
	}

	private void RunConversion(bool dryRun)
	{
		if (_analysis == null || _analysis.Variants.Count == 0)
			return;

		var paths = _analysis.Variants.Select(variant => variant.Path).ToList();

		if (!dryRun)
		{
			var batchNote = _useBatches
				? $"• пачками по {_batchSize}: упавшая пачка откатится, предыдущие останутся\n"
				: "• одной транзакцией: любая осечка отменит ВСЁ\n";
			var confirmation =
				$"Будет преобразовано Variant → Original: {paths.Count}\n\n" +
				batchNote +
				"• GUID каждого .prefab останется прежним\n" +
				"• сохраняемые fileID останутся прежними\n" +
				"• виртуальные fileID вложенных prefab будут переназначены в ссылках\n" +
				$"• может быть перезаписано файлов-владельцев: до {_analysis.Owners.Count}\n" +
				"• при первом несовпадении вся операция будет отменена\n\n" +
				"Сделай commit/backup перед запуском. Продолжить?";

			if (!EditorUtility.DisplayDialog("Convert Prefab Variants → Originals", confirmation, "Convert", "Cancel"))
				return;
		}

		_report = null;
		_error = null;

		var batches = dryRun || !_useBatches
			? new List<List<string>> { paths }
			: ConvertPrefabVariantsToOriginals.SplitIntoTransactions(_analysis.Variants, _batchSize);

		var lines = new List<string>();
		var done = 0;

		try
		{
			for (var index = 0; index < batches.Count; index++)
			{
				var openScene = _openEmptyScene && index == 0;
				try
				{
					var report = ConvertPrefabVariantsToOriginals.Run(
						batches[index], dryRun, _verifyOwners, openScene);

					done += batches[index].Count;
					lines.Add(batches.Count > 1
						? $"--- пачка {index + 1}/{batches.Count} ---\n{report}"
						: report.ToString());
				}
				catch (Exception exception)
				{
					_error = batches.Count > 1
						? $"Пачка {index + 1} из {batches.Count} отменена и откачена. " +
						  $"Предыдущие пачки конвертированы и сохранены ({done} префабов).\n\n" + exception.Message
						: exception.Message;
					break;
				}
			}
		}
		finally
		{
			EditorUtility.ClearProgressBar();
		}

		_report = lines.Count > 0 ? string.Join("\n\n", lines) : null;
		if (!dryRun && done > 0)
			_analysis = null;

		Repaint();
	}

	private int CountPrefabsInRoots()
	{
		var prefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var folders = _roots.Where(AssetDatabase.IsValidFolder).ToArray();

		foreach (var root in _roots)
		{
			if (!AssetDatabase.IsValidFolder(root))
				prefabs.Add(root);
		}

		if (folders.Length > 0)
		{
			foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders))
				prefabs.Add(AssetDatabase.GUIDToAssetPath(guid));
		}

		return prefabs.Count;
	}

	private static bool IsPrefabOwner(string ownerLine)
	{
		return ownerLine.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
	}

	private static void Reveal(string assetPath)
	{
		var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
		if (asset == null)
			return;

		Selection.activeObject = asset;
		EditorGUIUtility.PingObject(asset);
	}
}
#endif
