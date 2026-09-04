#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Drop folders, analyze, canonicalize. See PrefabIdCanonicalizer for what and why.
/// </summary>
public sealed class PrefabIdCanonicalizerWindow : EditorWindow
{
	private enum PendingAction
	{
		None,
		Analyze,
		Run
	}

	private readonly List<string> _roots = new List<string>();

	private PendingAction _pending;
	private List<PrefabIdCanonicalizer.Candidate> _candidates;
	private string _report;
	private string _error;
	private bool _showSkipped;
	private Vector2 _scroll;

	[MenuItem("Window/Tools/Канонизация fileID префабов", false, 1002)]
	public static void Open()
	{
		var window = GetWindow<PrefabIdCanonicalizerWindow>("Канонизация ID");
		window.minSize = new Vector2(640f, 440f);
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
			case PendingAction.Run:
				Run();
				break;
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
		DrawCandidates();
		DrawResult();

		EditorGUILayout.EndScrollView();
	}

	private void DrawDropArea()
	{
		EditorGUILayout.Space(4f);

		var rect = GUILayoutUtility.GetRect(0f, 52f, GUILayout.ExpandWidth(true));
		GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
		GUI.Label(
			rect,
			_roots.Count == 0 ? "Перетащи сюда папки или .prefab" : $"В наборе: {_roots.Count}",
			new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });

		var evt = Event.current;
		if (!rect.Contains(evt.mousePosition) ||
		    (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform))
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

		if (!AssetDatabase.IsValidFolder(path) && !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
			return;

		if (_roots.Any(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
			return;

		_roots.Add(path);
		_candidates = null;
		_report = null;
		_error = null;
	}

	private void DrawRoots()
	{
		if (_roots.Count > 0)
		{
			EditorGUILayout.Space(4f);
			for (var index = _roots.Count - 1; index >= 0; index--)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(_roots[index]);
				if (GUILayout.Button("×", GUILayout.Width(26f)))
				{
					_roots.RemoveAt(index);
					_candidates = null;
				}

				EditorGUILayout.EndHorizontal();
			}
		}

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("GameObjects + Levels (рекомендуется)"))
		{
			AddRoot("Assets/LA8_slv/GameObjects");
			AddRoot("Assets/LA8_slv/Levels");
		}

		if (GUILayout.Button("Добавить выделенное"))
		{
			foreach (var selected in Selection.objects)
				AddRoot(AssetDatabase.GetAssetPath(selected));
		}

		using (new EditorGUI.DisabledScope(_roots.Count == 0))
		{
			if (GUILayout.Button("Очистить"))
			{
				_roots.Clear();
				_candidates = null;
				_report = null;
				_error = null;
			}

			if (GUILayout.Button("Анализировать"))
				_pending = PendingAction.Analyze;
		}

		EditorGUILayout.EndHorizontal();
	}

	private void DrawCandidates()
	{
		if (_candidates == null)
		{
			EditorGUILayout.Space(6f);
			EditorGUILayout.HelpBox(
				"Зачем: после расплющивания вариантов ручное сохранение префаба-обёртки " +
				"(la8_foodTent и подобных) перегенерирует id вложенных объектов и ломает ссылки " +
				"из уровней. Эта тулса делает ту же перегенерацию один раз, контролируемо: " +
				"пересохраняет файл силами самой Unity, вычисляет карту старый→новый id и " +
				"переписывает все ссылки в проекте. После неё сохраняй что угодно как угодно.\n\n" +
				"Порядок обработки — снизу вверх по зависимостям (вложенные раньше обёрток), " +
				"каждый файл — отдельная транзакция с откатом.",
				MessageType.Info);
			return;
		}

		var ready = _candidates.Where(candidate => candidate.Applicable).ToList();
		var skipped = _candidates.Count - ready.Count;

		EditorGUILayout.Space(8f);
		EditorGUILayout.LabelField(
			$"К канонизации: {ready.Count}   пропущено (без вложенных): {skipped}",
			EditorStyles.boldLabel);

		foreach (var candidate in ready)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(candidate.OrderIndex.ToString(), EditorStyles.miniLabel, GUILayout.Width(34f));
			GUILayout.Label($"{candidate.NestedInstances} влож.", EditorStyles.miniLabel, GUILayout.Width(60f));
			EditorGUILayout.LabelField(new GUIContent(Path.GetFileName(candidate.Path), candidate.Path));
			if (GUILayout.Button("→", GUILayout.Width(26f)))
			{
				var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(candidate.Path);
				if (asset != null)
				{
					Selection.activeObject = asset;
					EditorGUIUtility.PingObject(asset);
				}
			}

			EditorGUILayout.EndHorizontal();
		}

		if (skipped > 0)
		{
			_showSkipped = EditorGUILayout.Foldout(_showSkipped, $"Пропущенные ({skipped})", true);
			if (_showSkipped)
			{
				EditorGUI.indentLevel++;
				foreach (var candidate in _candidates.Where(c => !c.Applicable))
					EditorGUILayout.LabelField(Path.GetFileName(candidate.Path), candidate.SkipReason);
				EditorGUI.indentLevel--;
			}
		}

		EditorGUILayout.Space(8f);
		using (new EditorGUI.DisabledScope(ready.Count == 0))
		{
			var color = GUI.backgroundColor;
			GUI.backgroundColor = new Color(1f, 0.75f, 0.4f);
			if (GUILayout.Button($"Канонизировать ({ready.Count})", GUILayout.Height(28f)))
				_pending = PendingAction.Run;
			GUI.backgroundColor = color;
		}
	}

	private void DrawResult()
	{
		if (!string.IsNullOrEmpty(_error))
		{
			EditorGUILayout.Space(8f);
			EditorGUILayout.HelpBox(_error, MessageType.Error);
			if (GUILayout.Button("Скопировать текст ошибки"))
				EditorGUIUtility.systemCopyBuffer = _error;
		}

		if (string.IsNullOrEmpty(_report))
			return;

		EditorGUILayout.Space(8f);
		EditorGUILayout.LabelField("Отчёт", EditorStyles.boldLabel);
		EditorGUILayout.SelectableLabel(_report, EditorStyles.textArea, GUILayout.Height(140f));
		if (GUILayout.Button("Скопировать отчёт"))
			EditorGUIUtility.systemCopyBuffer = _report;
	}

	private void Analyze()
	{
		_report = null;
		_error = null;

		try
		{
			_candidates = PrefabIdCanonicalizer.Analyze(_roots);
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

	private void Run()
	{
		if (_candidates == null)
			return;

		var ready = _candidates.Count(candidate => candidate.Applicable);
		if (!EditorUtility.DisplayDialog(
			    "Канонизация fileID",
			    $"Будет пересохранено префабов: {ready}\n\n" +
			    "• каждый файл пересохраняет сама Unity — id становятся каноническими\n" +
			    "• все ссылки в проекте переписываются по карте старый→новый\n" +
			    "• порядок: вложенные раньше обёрток\n" +
			    "• каждый файл — своя транзакция; ошибка откатит только текущий шаг\n\n" +
			    "Рабочая копия должна быть чистой (git commit). Продолжить?",
			    "Канонизировать",
			    "Отмена"))
		{
			return;
		}

		_report = null;
		_error = null;

		try
		{
			_report = PrefabIdCanonicalizer.Run(_candidates).ToString();
			_candidates = null;
		}
		catch (Exception exception)
		{
			_error = exception.Message;
		}
		finally
		{
			EditorUtility.ClearProgressBar();
			Repaint();
		}
	}
}
#endif
