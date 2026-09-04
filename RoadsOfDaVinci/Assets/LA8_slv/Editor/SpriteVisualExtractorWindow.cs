#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Drop folders or prefabs, analyze, then move the root SpriteRenderer onto a child —
/// one prefab at a time first, the whole batch once it looks right.
/// </summary>
public sealed class SpriteVisualExtractorWindow : EditorWindow
{
	private enum PendingAction
	{
		None,
		Analyze,
		ApplyOne,
		ApplyAll
	}

	private readonly List<string> _roots = new List<string>();

	private PendingAction _pending;
	private string _pendingPath;
	private List<SpriteVisualExtractor.Candidate> _candidates;
	private string _childName = SpriteVisualExtractor.DefaultChildName;
	private bool _keepDisabledCopyOnRoot = true;
	private string _report;
	private string _error;
	private Vector2 _scroll;

	[MenuItem("Window/Tools/Sprite → дочерний Visual", false, 1001)]
	public static void Open()
	{
		var window = GetWindow<SpriteVisualExtractorWindow>("Sprite → Visual");
		window.minSize = new Vector2(640f, 460f);
		window.Show();
	}

	private void Update()
	{
		if (_pending == PendingAction.None)
			return;

		var action = _pending;
		var path = _pendingPath;
		_pending = PendingAction.None;
		_pendingPath = null;

		switch (action)
		{
			case PendingAction.Analyze:
				Analyze();
				break;
			case PendingAction.ApplyOne:
				Apply(_candidates?.Where(candidate => candidate.Path == path).ToList());
				break;
			case PendingAction.ApplyAll:
				Apply(_candidates);
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
		DrawSettings();
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
			_roots.Count == 0 ? "Перетащи сюда папку или .prefab" : $"В наборе: {_roots.Count}",
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

	private void DrawSettings()
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

		EditorGUILayout.Space(4f);
		_childName = EditorGUILayout.TextField("Имя дочернего объекта", _childName);

		_keepDisabledCopyOnRoot = EditorGUILayout.ToggleLeft(
			"Оставить на корне выключенную копию SpriteRenderer (для сравнения)",
			_keepDisabledCopyOnRoot);
		EditorGUILayout.LabelField(
			_keepDisabledCopyOnRoot
				? "Оригинал компонента (и все ссылки на него) уезжает на дочерний, на корне — его выключенный дубль."
				: "SpriteRenderer уезжает на дочерний, на корне не остаётся ничего.",
			EditorStyles.miniLabel);

		EditorGUILayout.BeginHorizontal();
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
				"SpriteRenderer с корня переезжает на новый дочерний объект вместе со своим fileID — " +
				"поэтому все ссылки на него (и внутри префаба, и из уровней) продолжают вести на него же, " +
				"просто он теперь на дочернем. Трансформ дочернего единичный, спрайт выглядит и " +
				"масштабируется как раньше, SortingGroup корня продолжает на него действовать.",
				MessageType.Info);
			return;
		}

		var ready = _candidates.Where(candidate => candidate.Applicable).ToList();
		var skipped = _candidates.Count - ready.Count;

		EditorGUILayout.Space(8f);
		EditorGUILayout.LabelField(
			$"Префабов: {_candidates.Count}   готовы: {ready.Count}   пропущены: {skipped}",
			EditorStyles.boldLabel);

		foreach (var candidate in _candidates.OrderBy(candidate => candidate.Applicable ? 0 : 1)
			         .ThenBy(candidate => candidate.Path, StringComparer.Ordinal))
		{
			EditorGUILayout.BeginHorizontal();

			if (candidate.Applicable)
			{
				GUILayout.Label($"{candidate.LocalReferences} ссыл.", EditorStyles.miniLabel, GUILayout.Width(70f));
				EditorGUILayout.LabelField(
					new GUIContent(Path.GetFileName(candidate.Path), candidate.Path));
				GUILayout.Label(candidate.SpriteName ?? "—", EditorStyles.miniLabel, GUILayout.Width(150f));

				if (GUILayout.Button("сделать", GUILayout.Width(70f)))
				{
					_pendingPath = candidate.Path;
					_pending = PendingAction.ApplyOne;
				}
			}
			else
			{
				GUILayout.Label("пропуск", EditorStyles.miniLabel, GUILayout.Width(70f));
				EditorGUILayout.LabelField(
					new GUIContent(Path.GetFileName(candidate.Path), candidate.Path));
				GUILayout.Label(candidate.SkipReason, EditorStyles.miniLabel, GUILayout.Width(220f));
			}

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

		EditorGUILayout.Space(8f);
		using (new EditorGUI.DisabledScope(ready.Count == 0))
		{
			var color = GUI.backgroundColor;
			GUI.backgroundColor = new Color(1f, 0.75f, 0.4f);
			if (GUILayout.Button($"Применить ко всем ({ready.Count})", GUILayout.Height(28f)))
				_pending = PendingAction.ApplyAll;
			GUI.backgroundColor = color;
		}
	}

	private void DrawResult()
	{
		if (!string.IsNullOrEmpty(_error))
		{
			EditorGUILayout.Space(8f);
			EditorGUILayout.HelpBox(_error, MessageType.Error);
		}

		if (string.IsNullOrEmpty(_report))
			return;

		EditorGUILayout.Space(8f);
		EditorGUILayout.LabelField("Отчёт", EditorStyles.boldLabel);
		EditorGUILayout.SelectableLabel(_report, EditorStyles.textArea, GUILayout.Height(120f));
	}

	private void Analyze()
	{
		_report = null;
		_error = null;

		try
		{
			_candidates = SpriteVisualExtractor.Analyze(_roots, _childName);
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

	private void Apply(List<SpriteVisualExtractor.Candidate> candidates)
	{
		_report = null;
		_error = null;

		if (candidates == null || candidates.Count == 0)
		{
			_error = "Нечего применять.";
			return;
		}

		var ready = candidates.Count(candidate => candidate.Applicable);
		if (ready > 1 && !EditorUtility.DisplayDialog(
			    "Sprite → дочерний Visual",
			    $"Будет изменено префабов: {ready}\n\n" +
			    $"• SpriteRenderer переедет на дочерний \"{_childName}\"\n" +
			    "• fileID компонента сохранится, ссылки не порвутся\n" +
			    (_keepDisabledCopyOnRoot
				    ? "• на корне останется его ВЫКЛЮЧЕННАЯ копия\n"
				    : "• на корне SpriteRenderer'а не останется\n") +
			    "• при любой осечке всё откатится\n\n" +
			    "Сделай commit перед запуском. Продолжить?",
			    "Применить",
			    "Отмена"))
		{
			return;
		}

		try
		{
			_report = SpriteVisualExtractor.Apply(candidates, _childName, _keepDisabledCopyOnRoot).ToString();
			Debug.Log("[SpriteVisual] " + _report);
			_candidates = SpriteVisualExtractor.Analyze(_roots, _childName);
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
