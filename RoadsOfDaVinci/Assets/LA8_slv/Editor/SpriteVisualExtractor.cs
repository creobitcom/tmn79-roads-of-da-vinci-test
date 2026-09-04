#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Moves the root SpriteRenderer of a prefab onto a new child GameObject.
///
/// The component is not re-created: its YAML document keeps its fileID and only its
/// m_GameObject changes. Everything that referenced the root SpriteRenderer — 396
/// local references inside these prefabs and 109 from level files — therefore keeps
/// pointing at the very same component, now living on the child. The child is added
/// with an identity transform, so the sprite renders exactly as before, and it stays
/// under the root's SortingGroup.
/// </summary>
public static class SpriteVisualExtractor
{
	public const string DefaultChildName = "Visual";

	private const int SpriteRendererClassId = 212;
	private const int GameObjectClassId = 1;
	private const int TransformClassId = 4;
	private const int RectTransformClassId = 224;

	private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

	private static readonly Regex DocumentRegex = new Regex(
		@"(?m)^--- !u!(\d+) &(-?\d+)( stripped)?\r?$",
		RegexOptions.Compiled);

	public sealed class Candidate
	{
		public string Path;
		public string Guid;
		public string RootName;
		public string SpriteName;
		public long RootGameObjectId;
		public long RootTransformId;
		public long SpriteRendererId;
		public int LocalReferences;
		public string SkipReason;

		public bool Applicable => string.IsNullOrEmpty(SkipReason);
	}

	public sealed class Result
	{
		public int Applied;
		public int Skipped;
		public readonly List<string> Log = new List<string>();

		public override string ToString()
		{
			return $"Обработано префабов: {Applied}\n" +
			       (Skipped > 0 ? $"Пропущено: {Skipped}\n" : string.Empty) +
			       "fileID SpriteRenderer сохранён, все ссылки на него ведут на новый дочерний объект.\n" +
			       (Log.Count > 0 ? "\n" + string.Join("\n", Log.Take(60)) : string.Empty);
		}
	}

	// ------------------------------------------------------------------ analysis

	public static List<Candidate> Analyze(IEnumerable<string> pathsOrFolders, string childName)
	{
		var result = new List<Candidate>();
		foreach (var path in ConvertPrefabVariantsToOriginals.CollectAllPrefabs(pathsOrFolders))
			result.Add(Describe(path, childName));

		return result;
	}

	private static Candidate Describe(string prefabPath, string childName)
	{
		var candidate = new Candidate
		{
			Path = prefabPath,
			Guid = AssetDatabase.AssetPathToGUID(prefabPath),
			RootName = Path.GetFileNameWithoutExtension(prefabPath)
		};

		string text;
		try
		{
			text = File.ReadAllText(GetAbsoluteAssetPath(prefabPath), Encoding.UTF8);
		}
		catch (Exception exception)
		{
			candidate.SkipReason = "не читается: " + exception.Message;
			return candidate;
		}

		var docs = ParseDocuments(text);
		var rootTransform = docs.FirstOrDefault(doc =>
			!doc.Stripped &&
			(doc.ClassId == TransformClassId || doc.ClassId == RectTransformClassId) &&
			Regex.IsMatch(doc.Body, @"m_Father: \{fileID: 0\}"));

		if (rootTransform == null)
		{
			candidate.SkipReason = "не найден корневой Transform (префаб — вариант?)";
			return candidate;
		}

		if (rootTransform.ClassId == RectTransformClassId)
		{
			candidate.SkipReason = "корень — RectTransform (UI), не трогаю";
			return candidate;
		}

		candidate.RootTransformId = rootTransform.FileId;
		candidate.RootGameObjectId = ReadFileIdField(rootTransform.Body, "m_GameObject");

		var rootGameObject = docs.FirstOrDefault(doc =>
			doc.ClassId == GameObjectClassId && !doc.Stripped && doc.FileId == candidate.RootGameObjectId);
		if (rootGameObject == null)
		{
			candidate.SkipReason = "не найден корневой GameObject";
			return candidate;
		}

		candidate.RootName = ReadStringField(rootGameObject.Body, "m_Name") ?? candidate.RootName;

		// Checked before anything else: in "keep a disabled copy" mode the root still
		// carries a SpriteRenderer afterwards, so only the child proves it was done.
		if (ChildWithSpriteExists(docs, rootTransform, childName))
		{
			candidate.SkipReason = "уже сделано";
			return candidate;
		}

		var spriteRenderers = docs
			.Where(doc => doc.ClassId == SpriteRendererClassId && !doc.Stripped)
			.Where(doc => ReadFileIdField(doc.Body, "m_GameObject") == candidate.RootGameObjectId)
			.ToList();

		if (spriteRenderers.Count == 0)
		{
			candidate.SkipReason = "на корне нет SpriteRenderer";
			return candidate;
		}

		if (spriteRenderers.Count > 1)
		{
			candidate.SkipReason = $"на корне {spriteRenderers.Count} SpriteRenderer — разбери вручную";
			return candidate;
		}

		candidate.SpriteRendererId = spriteRenderers[0].FileId;
		candidate.SpriteName = ReadSpriteName(spriteRenderers[0].Body);

		// How many places inside this very prefab point at that component.
		candidate.LocalReferences = Regex.Matches(
				text, @"\{fileID: " + candidate.SpriteRendererId + @"\}")
			.Count;

		return candidate;
	}

	private static bool ChildWithSpriteExists(List<YamlDocument> docs, YamlDocument rootTransform, string childName)
	{
		foreach (var childId in ReadFileIdList(rootTransform.Body, "m_Children"))
		{
			var childTransform = docs.FirstOrDefault(doc => doc.FileId == childId && !doc.Stripped);
			if (childTransform == null)
				continue;

			var childGameObjectId = ReadFileIdField(childTransform.Body, "m_GameObject");
			var childGameObject = docs.FirstOrDefault(doc =>
				doc.ClassId == GameObjectClassId && doc.FileId == childGameObjectId && !doc.Stripped);
			if (childGameObject == null)
				continue;

			if (!string.Equals(ReadStringField(childGameObject.Body, "m_Name"), childName, StringComparison.Ordinal))
				continue;

			if (docs.Any(doc => doc.ClassId == SpriteRendererClassId &&
			                    ReadFileIdField(doc.Body, "m_GameObject") == childGameObjectId))
				return true;
		}

		return false;
	}

	// ------------------------------------------------------------------ apply

	/// <summary>
	/// Applies the change to every applicable candidate as one transaction: on any
	/// problem all touched files are restored byte for byte.
	/// </summary>
	public static Result Apply(IEnumerable<Candidate> candidates, string childName, bool keepDisabledCopyOnRoot)
	{
		var blockers = ConvertPrefabVariantsToOriginals.DescribeEnvironmentBlockers(true);
		if (!string.IsNullOrEmpty(blockers))
			throw new InvalidOperationException(blockers);

		if (string.IsNullOrWhiteSpace(childName))
			throw new InvalidOperationException("Имя дочернего объекта не задано.");

		var todo = candidates.Where(candidate => candidate.Applicable).ToList();
		if (todo.Count == 0)
			throw new InvalidOperationException("Нечего делать: подходящих префабов нет.");

		var result = new Result();
		var backups = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

		try
		{
			for (var index = 0; index < todo.Count; index++)
			{
				var candidate = todo[index];
				EditorUtility.DisplayProgressBar(
					"Выношу спрайт в дочерний объект",
					candidate.Path,
					(float)index / todo.Count * 0.9f);

				var absolutePath = GetAbsoluteAssetPath(candidate.Path);
				backups[candidate.Path] = File.ReadAllBytes(absolutePath);

				var created = RewritePrefab(candidate.Path, childName, keepDisabledCopyOnRoot);
				AssetDatabase.ImportAsset(
					candidate.Path,
					ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

				VerifyPrefab(candidate, childName, created, keepDisabledCopyOnRoot);
				result.Applied++;
				result.Log.Add(
					$"{Path.GetFileName(candidate.Path)}: SpriteRenderer {candidate.SpriteRendererId} → {childName}" +
					(keepDisabledCopyOnRoot ? " (на корне выключенная копия)" : string.Empty));
			}

			return result;
		}
		catch (Exception exception)
		{
			var rollbackError = Rollback(backups);
			var message = "Остановлено, файлы восстановлены.\n\n" + exception.Message;
			if (!string.IsNullOrEmpty(rollbackError))
				message += "\n\nОШИБКА ОТКАТА:\n" + rollbackError;

			Debug.LogException(exception);
			throw new InvalidOperationException(message, exception);
		}
		finally
		{
			EditorUtility.ClearProgressBar();
			AssetDatabase.Refresh();
		}
	}

	private readonly struct CreatedIds
	{
		public readonly long GameObjectId;
		public readonly long TransformId;

		public CreatedIds(long gameObjectId, long transformId)
		{
			GameObjectId = gameObjectId;
			TransformId = transformId;
		}
	}

	private static CreatedIds RewritePrefab(string prefabPath, string childName, bool keepDisabledCopyOnRoot)
	{
		var absolutePath = GetAbsoluteAssetPath(prefabPath);
		var text = File.ReadAllText(absolutePath, Encoding.UTF8);
		var newLine = text.Contains("\r\n") ? "\r\n" : "\n";

		// Re-derive everything from the file itself: the analysis may be stale.
		var candidate = Describe(prefabPath, childName);
		if (!candidate.Applicable)
			throw new InvalidOperationException($"{prefabPath}: {candidate.SkipReason}");

		var docs = ParseDocuments(text);
		var rootGameObject = docs.Single(doc =>
			doc.ClassId == GameObjectClassId && !doc.Stripped && doc.FileId == candidate.RootGameObjectId);
		var rootTransform = docs.Single(doc => doc.FileId == candidate.RootTransformId && !doc.Stripped);
		var spriteRenderer = docs.Single(doc => doc.FileId == candidate.SpriteRendererId && !doc.Stripped);

		var used = new HashSet<long>(docs.Select(doc => doc.FileId));
		var newGameObjectId = AllocateId(used, prefabPath + "|" + childName + "|GameObject");
		var newTransformId = AllocateId(used, prefabPath + "|" + childName + "|Transform");
		var rootCopyId = keepDisabledCopyOnRoot
			? AllocateId(used, prefabPath + "|" + childName + "|DisabledCopy")
			: 0L;

		var newGameObjectDoc = BuildChildGameObject(
			rootGameObject.Body, childName, newTransformId, candidate.SpriteRendererId);
		var newTransformDoc = BuildChildTransform(
			rootTransform.Body, newGameObjectId, candidate.RootTransformId);

		// The original component keeps its fileID and moves to the child, so every
		// reference follows it. What stays on the root is a disabled copy, in the very
		// same slot of the component list — flip one, flip the other, compare.
		var rootComponentList = keepDisabledCopyOnRoot
			? ReplaceComponentEntry(rootGameObject.Body, candidate.SpriteRendererId, rootCopyId)
			: RemoveComponentEntry(rootGameObject.Body, candidate.SpriteRendererId);

		// Edits are spliced back to front so the earlier offsets stay valid.
		var edits = new List<Tuple<YamlDocument, string>>
		{
			Tuple.Create(rootGameObject, rootComponentList),
			Tuple.Create(rootTransform, AddChild(rootTransform.Body, newTransformId, newLine)),
			Tuple.Create(spriteRenderer, ReplaceFileIdField(spriteRenderer.Body, "m_GameObject", newGameObjectId))
		};

		var builder = new StringBuilder(text);
		foreach (var edit in edits.OrderByDescending(edit => edit.Item1.BodyStart))
		{
			builder.Remove(edit.Item1.BodyStart, edit.Item1.End - edit.Item1.BodyStart);
			builder.Insert(edit.Item1.BodyStart, edit.Item2);
		}

		if (builder.Length > 0 && builder[builder.Length - 1] != '\n')
			builder.Append(newLine);

		builder.Append($"--- !u!{GameObjectClassId} &{newGameObjectId}").Append(newLine);
		builder.Append(newGameObjectDoc);
		builder.Append($"--- !u!{TransformClassId} &{newTransformId}").Append(newLine);
		builder.Append(newTransformDoc);

		if (keepDisabledCopyOnRoot)
		{
			builder.Append($"--- !u!{SpriteRendererClassId} &{rootCopyId}").Append(newLine);
			builder.Append(BuildDisabledRootCopy(spriteRenderer.Body, candidate.RootGameObjectId));
		}

		File.WriteAllText(absolutePath, builder.ToString(), Utf8WithoutBom);
		return new CreatedIds(newGameObjectId, newTransformId);
	}

	/// <summary>
	/// A byte-for-byte copy of the moved renderer, still on the root but disabled: the
	/// A/B switch for eyeballing that nothing changed visually.
	/// </summary>
	private static string BuildDisabledRootCopy(string spriteRendererBody, long rootGameObjectId)
	{
		var body = ReplaceFileIdField(spriteRendererBody, "m_GameObject", rootGameObjectId);

		// Same trap: a renderer that is already disabled would produce no change.
		if (!Regex.IsMatch(body, @"(?m)^  m_Enabled: "))
			throw new InvalidOperationException("В SpriteRenderer не найдено поле m_Enabled");

		return Regex.Replace(body, @"(?m)^  m_Enabled: [^\r\n]*", "  m_Enabled: 0");
	}

	/// <summary>
	/// The child is built from the root's own documents, so the field set always
	/// matches what this Unity version writes.
	/// </summary>
	private static string BuildChildGameObject(
		string rootBody,
		string childName,
		long transformId,
		long spriteRendererId)
	{
		var body = Regex.Replace(
			rootBody,
			@"(?ms)^(  m_Component:\r?\n)(?:  - component: \{fileID: -?\d+\}\r?\n)+",
			match => match.Groups[1].Value +
			         "  - component: {fileID: " + transformId + "}" + LineEndingOf(match.Value) +
			         "  - component: {fileID: " + spriteRendererId + "}" + LineEndingOf(match.Value));

		// [^\r\n]* rather than .* so the CR of a CRLF line is left in place.
		body = Regex.Replace(body, @"(?m)^  m_Name: [^\r\n]*", "  m_Name: " + childName);
		body = Regex.Replace(body, @"(?m)^  m_TagString: [^\r\n]*", "  m_TagString: Untagged");
		body = Regex.Replace(body, @"(?m)^  m_IsActive: [^\r\n]*", "  m_IsActive: 1");
		return body;
	}

	private static string BuildChildTransform(string rootBody, long gameObjectId, long fatherId)
	{
		var body = ReplaceFileIdField(rootBody, "m_GameObject", gameObjectId);
		body = Regex.Replace(
			body, @"(?m)^  m_LocalRotation: [^\r\n]*", "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}");
		body = Regex.Replace(body, @"(?m)^  m_LocalPosition: [^\r\n]*", "  m_LocalPosition: {x: 0, y: 0, z: 0}");
		body = Regex.Replace(body, @"(?m)^  m_LocalScale: [^\r\n]*", "  m_LocalScale: {x: 1, y: 1, z: 1}");
		body = Regex.Replace(
			body, @"(?m)^  m_LocalEulerAnglesHint: [^\r\n]*", "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}");
		body = Regex.Replace(
			body,
			@"(?ms)^  m_Children:(?: \[\]\r?\n|\r?\n(?:  - \{fileID: -?\d+\}\r?\n)*)",
			match => "  m_Children: []" + LineEndingOf(match.Value));
		body = ReplaceFileIdField(body, "m_Father", fatherId);
		return body;
	}

	private static string ReplaceComponentEntry(string body, long componentId, long replacementId)
	{
		// Every prefab here uses CRLF, so the line cannot be closed with a bare $:
		// there is a \r between the brace and the line break.
		var pattern = @"(?m)^(  - component: \{fileID: )" + componentId + @"(\})(?=\r?$)";
		var updated = Regex.Replace(body, pattern, match =>
			match.Groups[1].Value + replacementId + match.Groups[2].Value);

		if (updated == body)
			throw new InvalidOperationException($"В корневом GameObject нет компонента {componentId}");

		return updated;
	}

	private static string RemoveComponentEntry(string body, long componentId)
	{
		var pattern = @"(?m)^  - component: \{fileID: " + componentId + @"\}\r?\n";
		var updated = Regex.Replace(body, pattern, string.Empty);
		if (updated == body)
			throw new InvalidOperationException($"В корневом GameObject нет компонента {componentId}");

		return updated;
	}

	private static string AddChild(string body, long childTransformId, string newLine)
	{
		var entry = "  - {fileID: " + childTransformId + "}";

		var empty = Regex.Match(body, @"(?m)^  m_Children: \[\]\r?$");
		if (empty.Success)
		{
			return body.Substring(0, empty.Index) +
			       "  m_Children:" + newLine + entry +
			       body.Substring(empty.Index + empty.Length);
		}

		var list = Regex.Match(body, @"(?ms)^  m_Children:\r?\n(?:  - \{fileID: -?\d+\}\r?\n)*");
		if (!list.Success)
			throw new InvalidOperationException("Не найден m_Children корневого Transform");

		var insertAt = list.Index + list.Length;
		return body.Substring(0, insertAt) + entry + newLine + body.Substring(insertAt);
	}

	// ------------------------------------------------------------------ verify

	private static void VerifyPrefab(
		Candidate candidate,
		string childName,
		CreatedIds created,
		bool keepDisabledCopyOnRoot)
	{
		var asset = AssetDatabase.LoadAssetAtPath<GameObject>(candidate.Path);
		if (asset == null)
			throw new InvalidOperationException("Префаб не загружается после правки: " + candidate.Path);

		var rootRenderer = asset.GetComponent<SpriteRenderer>();
		if (!keepDisabledCopyOnRoot)
		{
			if (rootRenderer != null)
				throw new InvalidOperationException("SpriteRenderer всё ещё на корне: " + candidate.Path);
		}
		else
		{
			if (rootRenderer == null)
				throw new InvalidOperationException("На корне нет выключенной копии: " + candidate.Path);

			if (rootRenderer.enabled)
				throw new InvalidOperationException("Копия на корне не выключена: " + candidate.Path);
		}

		var child = asset.transform.Find(childName);
		if (child == null)
			throw new InvalidOperationException($"Дочерний \"{childName}\" не появился: {candidate.Path}");

		var renderer = child.GetComponent<SpriteRenderer>();
		if (renderer == null)
			throw new InvalidOperationException($"На \"{childName}\" нет SpriteRenderer: {candidate.Path}");

		if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer, out var guid, out long localId) ||
		    localId != candidate.SpriteRendererId ||
		    !string.Equals(guid, candidate.Guid, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException(
				$"fileID SpriteRenderer не сохранился в {candidate.Path}: было " +
				$"{candidate.SpriteRendererId}, стало {localId} — ссылки бы сломались");
		}

		if (child.localPosition != Vector3.zero ||
		    child.localScale != Vector3.one ||
		    child.localRotation != Quaternion.identity)
		{
			throw new InvalidOperationException(
				$"Трансформ дочернего не единичный в {candidate.Path} — спрайт сместился бы");
		}

		if (keepDisabledCopyOnRoot && rootRenderer.sprite != renderer.sprite)
		{
			throw new InvalidOperationException(
				$"Копия на корне и дочерний рисуют разные спрайты в {candidate.Path}");
		}

		if (created.GameObjectId == 0 || created.TransformId == 0)
			throw new InvalidOperationException("Не выделены id для новых объектов: " + candidate.Path);
	}

	private static string Rollback(Dictionary<string, byte[]> backups)
	{
		var errors = new List<string>();
		foreach (var backup in backups)
		{
			try
			{
				File.WriteAllBytes(GetAbsoluteAssetPath(backup.Key), backup.Value);
			}
			catch (Exception exception)
			{
				errors.Add(backup.Key + ": " + exception.Message);
			}
		}

		try
		{
			AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
		}
		catch (Exception exception)
		{
			errors.Add("Refresh: " + exception.Message);
		}

		return string.Join("\n", errors.Take(20));
	}

	// ------------------------------------------------------------------ yaml helpers

	private sealed class YamlDocument
	{
		public int ClassId;
		public long FileId;
		public bool Stripped;
		public int BodyStart;
		public int End;
		public string Body;
	}

	private static List<YamlDocument> ParseDocuments(string text)
	{
		var matches = DocumentRegex.Matches(text).Cast<Match>().ToList();
		var docs = new List<YamlDocument>();

		for (var index = 0; index < matches.Count; index++)
		{
			var match = matches[index];
			var bodyStart = match.Index + match.Length;
			while (bodyStart < text.Length && (text[bodyStart] == '\r' || text[bodyStart] == '\n'))
				bodyStart++;

			var end = index + 1 < matches.Count ? matches[index + 1].Index : text.Length;
			docs.Add(new YamlDocument
			{
				ClassId = int.Parse(match.Groups[1].Value),
				FileId = long.Parse(match.Groups[2].Value),
				Stripped = match.Groups[3].Success,
				BodyStart = bodyStart,
				End = end,
				Body = text.Substring(bodyStart, end - bodyStart)
			});
		}

		return docs;
	}

	private static long ReadFileIdField(string body, string field)
	{
		var match = Regex.Match(body, @"(?m)^\s*" + field + @": \{fileID: (-?\d+)\}");
		return match.Success ? long.Parse(match.Groups[1].Value) : 0L;
	}

	private static string ReplaceFileIdField(string body, string field, long value)
	{
		// Presence is checked separately: writing the value it already holds is a
		// legitimate no-op (the disabled copy keeps pointing at the root).
		var pattern = @"(?m)^(\s*" + field + @": \{fileID: )-?\d+(\})";
		if (!Regex.IsMatch(body, pattern))
			throw new InvalidOperationException("Не найдено поле " + field);

		return Regex.Replace(
			body, pattern, match => match.Groups[1].Value + value + match.Groups[2].Value);
	}

	private static string ReadSpriteName(string body)
	{
		var match = Regex.Match(body, @"m_Sprite: \{fileID: -?\d+, guid: ([0-9a-fA-F]{32})");
		if (!match.Success)
			return "нет спрайта";

		var path = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
		return string.IsNullOrEmpty(path) ? "спрайт не найден" : Path.GetFileNameWithoutExtension(path);
	}

	private static string ReadStringField(string body, string field)
	{
		var match = Regex.Match(body, @"(?m)^\s*" + field + @": ([^\r\n]*)");
		return match.Success ? match.Groups[1].Value.Trim() : null;
	}

	private static IEnumerable<long> ReadFileIdList(string body, string field)
	{
		var block = Regex.Match(body, @"(?ms)^\s*" + field + @":\r?\n((?:\s*- \{fileID: -?\d+\}\r?\n)*)");
		if (!block.Success)
			return Enumerable.Empty<long>();

		return Regex.Matches(block.Groups[1].Value, @"\{fileID: (-?\d+)\}")
			.Cast<Match>()
			.Select(match => long.Parse(match.Groups[1].Value));
	}

	private static string LineEndingOf(string text)
	{
		return text.Contains("\r\n") ? "\r\n" : "\n";
	}

	/// <summary>
	/// Deterministic so a repeated run on the same prefab picks the same IDs, and
	/// bumped until it does not collide with anything already in the file.
	/// </summary>
	private static long AllocateId(HashSet<long> used, string seed)
	{
		unchecked
		{
			long hash = 1469598103934665603L;
			foreach (var character in seed)
			{
				hash ^= character;
				hash *= 1099511628211L;
			}

			var id = hash & 0x0FFFFFFFFFFFFFFFL;
			if (id < 1000000L)
				id += 1000000L;

			while (used.Contains(id))
				id++;

			used.Add(id);
			return id;
		}
	}

	private static string GetAbsoluteAssetPath(string assetPath)
	{
		var projectRoot = Directory.GetParent(Application.dataPath);
		if (projectRoot == null)
			throw new InvalidOperationException("Cannot resolve Unity project root");
		return Path.GetFullPath(Path.Combine(projectRoot.FullName, assetPath));
	}
}
#endif
