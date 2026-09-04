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
/// Brings prefab files to the exact state Unity's own Save produces, without losing
/// external references.
///
/// Why: the variant flattening wrote the OLD stripped-object ids into the files so
/// the ~47k existing references kept working. Import honors ids written in a file,
/// but an editor SAVE recomputes them — so the first manual save of such a file
/// regenerated its stripped anchors and orphaned every external reference to them
/// (la8_foodTent: 18 anchors moved, 99 references in 69 files died).
///
/// The fix: do that regeneration ONCE, deliberately — re-save each file through
/// Unity, map old→new stripped ids by (class, source object, prefab instance), and
/// rewrite all references project-wide. After that a manual save writes the very
/// same ids and nothing can break again.
///
/// Files are processed bottom-up in dependency order (a nested/base prefab before
/// everyone who uses it), because canonical ids of an owner depend on the ids of
/// its sources.
/// </summary>
public static class PrefabIdCanonicalizer
{
	private const long MainPrefabAssetFileId = 100100000;

	private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

	private static readonly Regex DocumentRegex = new Regex(
		@"(?m)^--- !u!(\d+) &(-?\d+)( stripped)?\r?$",
		RegexOptions.Compiled);

	private static readonly Regex SourcePrefabRegex = new Regex(
		@"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-fA-F]{32})",
		RegexOptions.Compiled);

	private static readonly Regex ExternalObjectReferenceRegex = new Regex(
		@"\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*\d+\}",
		RegexOptions.Compiled);

	private static readonly HashSet<string> TextAssetExtensions = new HashSet<string>(
		new[]
		{
			".prefab", ".unity", ".asset", ".anim", ".controller", ".overridecontroller",
			".playable", ".mat", ".spriteatlas", ".spriteatlasv2", ".rendertexture", ".mask",
			".guiskin", ".preset", ".signal", ".mixer"
		},
		StringComparer.OrdinalIgnoreCase);

	// ------------------------------------------------------------------ public API

	public sealed class Candidate
	{
		public string Path;
		public string Guid;
		public int NestedInstances;
		public int OrderIndex;
		public string SkipReason;

		public bool Applicable => string.IsNullOrEmpty(SkipReason);
	}

	public sealed class Result
	{
		public int Processed;
		public int AlreadyCanonical;
		public int AnchorsMoved;
		public int ReferencesRewritten;
		public int FilesRewritten;
		public readonly List<string> Log = new List<string>();

		public override string ToString()
		{
			return
				$"Канонизировано префабов: {Processed}\n" +
				$"  из них без переноса ссылок (уже каноничны или не адресуются): {AlreadyCanonical}\n" +
				$"Сдвинуто адресуемых id: {AnchorsMoved}\n" +
				$"Переписано внешних ссылок: {ReferencesRewritten} в {FilesRewritten} файлах\n\n" +
				"Теперь ручное сохранение этих префабов выдаёт те же id — ломаться нечему.\n" +
				(Log.Count > 0 ? "\n" + string.Join("\n", Log.Take(80)) : string.Empty);
		}
	}

	/// <summary>
	/// Prefabs from the given folders that contain nested prefab instances, sorted
	/// bottom-up: sources before the files that use them.
	/// </summary>
	public static List<Candidate> Analyze(IEnumerable<string> pathsOrFolders)
	{
		var all = ConvertPrefabVariantsToOriginals.CollectAllPrefabs(pathsOrFolders);
		var candidates = new List<Candidate>();
		var byGuid = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);
		var sourcesOf = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

		foreach (var path in all)
		{
			var candidate = new Candidate
			{
				Path = path,
				Guid = AssetDatabase.AssetPathToGUID(path)
			};

			string text;
			try
			{
				text = File.ReadAllText(GetAbsoluteAssetPath(path), Encoding.UTF8);
			}
			catch (Exception exception)
			{
				candidate.SkipReason = "не читается: " + exception.Message;
				candidates.Add(candidate);
				continue;
			}

			var sources = SourcePrefabRegex.Matches(text)
				.Cast<Match>()
				.Select(match => match.Groups[1].Value.ToLowerInvariant())
				.Distinct()
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			candidate.NestedInstances = sources.Count == 0
				? 0
				: SourcePrefabRegex.Matches(text).Count;

			if (candidate.NestedInstances == 0)
			{
				candidate.SkipReason = "нет вложенных префабов — id и так стабильны";
				candidates.Add(candidate);
				continue;
			}

			sourcesOf[candidate.Guid] = sources;
			byGuid[candidate.Guid] = candidate;
			candidates.Add(candidate);
		}

		// Bottom-up topological order restricted to the set; cycles fall back to
		// path order and are reported by the runner if they matter.
		var order = new List<string>();
		var visited = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		void Visit(string guid)
		{
			if (visited.TryGetValue(guid, out var state))
			{
				return;
			}

			visited[guid] = 1;
			if (sourcesOf.TryGetValue(guid, out var sources))
			{
				foreach (var source in sources.Where(byGuid.ContainsKey).OrderBy(s => s, StringComparer.Ordinal))
					Visit(source);
			}

			visited[guid] = 2;
			order.Add(guid);
		}

		foreach (var guid in byGuid.Keys.OrderBy(g => byGuid[g].Path, StringComparer.Ordinal))
			Visit(guid);

		for (var index = 0; index < order.Count; index++)
			byGuid[order[index]].OrderIndex = index;

		var sorted = candidates
			.OrderBy(candidate => candidate.Applicable ? 0 : 1)
			.ThenBy(candidate => candidate.OrderIndex)
			.ThenBy(candidate => candidate.Path, StringComparer.Ordinal)
			.ToList();

		DumpToTemp("CanonicalizeAnalysis.txt", builder =>
		{
			var ready = sorted.Where(candidate => candidate.Applicable).ToList();
			builder.AppendLine($"к канонизации: {ready.Count}  пропущено: {sorted.Count - ready.Count}");
			builder.AppendLine("--- порядок обработки (снизу вверх по зависимостям) ---");
			foreach (var candidate in ready)
				builder.AppendLine($"{candidate.OrderIndex,4}  влож.{candidate.NestedInstances,3}  {candidate.Path}");
			builder.AppendLine("--- пропущено ---");
			foreach (var candidate in sorted.Where(candidate => !candidate.Applicable))
				builder.AppendLine($"{candidate.Path} :: {candidate.SkipReason}");
		});

		return sorted;
	}

	private static void DumpToTemp(string fileName, Action<StringBuilder> fill)
	{
		try
		{
			var builder = new StringBuilder();
			fill(builder);
			var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", fileName));
			File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
			Debug.Log("[Canonicalize] полный отчёт: " + path);
		}
		catch (Exception exception)
		{
			Debug.LogWarning("[Canonicalize] не удалось записать отчёт: " + exception.Message);
		}
	}

	/// <summary>
	/// Canonicalizes the applicable candidates one by one, in the analyzed order.
	/// Each file is its own transaction: a failure rolls back that file and its
	/// rewritten owners, keeps everything done before, and stops.
	/// </summary>
	public static Result Run(List<Candidate> candidates)
	{
		var blockers = ConvertPrefabVariantsToOriginals.DescribeEnvironmentBlockers(true);
		if (!string.IsNullOrEmpty(blockers))
			throw new InvalidOperationException(blockers);

		var todo = candidates.Where(candidate => candidate.Applicable).ToList();
		if (todo.Count == 0)
			throw new InvalidOperationException("Нечего канонизировать.");

		// One up-front pass: which inner objects of each candidate does the rest of
		// the project actually address. Only those need protecting, and a file nobody
		// addresses by inner object can be re-saved without any measuring at all.
		var todoGuids = new HashSet<string>(todo.Select(c => c.Guid), StringComparer.OrdinalIgnoreCase);
		var scan = ConvertPrefabVariantsToOriginals.ScanReferences(
			todoGuids, null, "Скан ссылок в набор", -1f);

		var result = new Result();

		ConvertPrefabVariantsToOriginals.BeginInspection();
		try
		{
			for (var index = 0; index < todo.Count; index++)
			{
				var candidate = todo[index];
				EditorUtility.DisplayProgressBar(
					$"Канонизация ({index + 1}/{todo.Count})",
					candidate.Path,
					(float)index / todo.Count);

				scan.ReferencedIdsByGuid.TryGetValue(candidate.Guid, out var referencedIds);
				var protectedIds = new HashSet<long>(referencedIds ?? Enumerable.Empty<long>());
				protectedIds.Remove(MainPrefabAssetFileId);
				protectedIds.Remove(0);

				var stepBackups = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
				try
				{
					CanonicalizeOne(candidate, protectedIds, result, stepBackups);
				}
				catch (Exception exception)
				{
					var rollbackError = Rollback(stepBackups);
					EditorUtility.ClearProgressBar();
					AssetDatabase.Refresh();

					var message =
						$"Остановлено на {candidate.Path} (готово до него: {result.Processed}).\n" +
						"Файл этого шага и его владельцы восстановлены.\n\n" + exception.Message;
					if (!string.IsNullOrEmpty(rollbackError))
						message += "\n\nОШИБКА ОТКАТА:\n" + rollbackError;

					Debug.LogException(exception);
					throw new InvalidOperationException(message, exception);
				}
			}
		}
		finally
		{
			ConvertPrefabVariantsToOriginals.EndInspection();
			EditorUtility.ClearProgressBar();
			AssetDatabase.Refresh();
		}

		Debug.Log("[Canonicalize] " + result);

		DumpToTemp("CanonicalizeRun.txt", builder =>
		{
			builder.AppendLine($"обработано: {result.Processed}  без переноса: {result.AlreadyCanonical}");
			builder.AppendLine($"сдвинуто адресуемых id: {result.AnchorsMoved}");
			builder.AppendLine($"переписано ссылок: {result.ReferencesRewritten} в {result.FilesRewritten} файлах");
			builder.AppendLine("--- подробно ---");
			foreach (var line in result.Log)
				builder.AppendLine(line);
		});

		return result;
	}

	// ------------------------------------------------------------------ core step

	private static void CanonicalizeOne(
		Candidate candidate,
		HashSet<long> protectedIds,
		Result result,
		Dictionary<string, byte[]> stepBackups)
	{
		var absolutePath = GetAbsoluteAssetPath(candidate.Path);
		var bytesBefore = File.ReadAllBytes(absolutePath);
		stepBackups[candidate.Path] = bytesBefore;

		var textBefore = Utf8WithoutBom.GetString(bytesBefore);
		var realBefore = CollectRealAnchors(textBefore);

		// The full object map — every live object of the prefab including VIRTUAL
		// ones of nested instances that have no stripped doc in the file. Anchors
		// alone are not enough: computed ids move on re-save exactly like stripped
		// ones, and the Pack02 level chain addresses such objects.
		var mapBefore = protectedIds.Count > 0
			? ConvertPrefabVariantsToOriginals.BuildStableKeyToFileIdMap(candidate.Path, false)
			: null;

		// Unity itself produces the canonical serialization.
		var root = PrefabUtility.LoadPrefabContents(candidate.Path);
		if (root == null)
			throw new InvalidOperationException("LoadPrefabContents вернул null: " + candidate.Path);

		try
		{
			PrefabUtility.SaveAsPrefabAsset(root, candidate.Path, out var success);
			if (!success)
				throw new InvalidOperationException("SaveAsPrefabAsset failed: " + candidate.Path);
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}

		var textAfter = File.ReadAllText(absolutePath, Encoding.UTF8);
		var realAfter = CollectRealAnchors(textAfter);

		// Real objects must never move: Unity preserves persistent ids on save.
		var lostReal = realBefore.Keys.Except(realAfter.Keys).Take(10).ToList();
		if (lostReal.Count > 0)
		{
			throw new InvalidOperationException(
				$"При пересохранении исчезли НЕ-stripped объекты в {candidate.Path}: " +
				string.Join(", ", lostReal) + " — файл восстановлен, разберись вручную.");
		}

		result.Processed++;

		if (protectedIds.Count == 0)
		{
			// Nobody addresses this prefab's inner objects — nothing can break.
			result.AlreadyCanonical++;
			return;
		}

		var mapAfter = ConvertPrefabVariantsToOriginals.BuildStableKeyToFileIdMap(candidate.Path, false);

		// Diff by hierarchy identity, keep only the ids somebody actually references.
		var idBefore = new Dictionary<long, string>();
		foreach (var entry in mapBefore)
		{
			// Duplicate values cannot be remapped unambiguously; skip them and let
			// the validator surface the reference if it ever mattered.
			if (!idBefore.ContainsKey(entry.Value))
				idBefore[entry.Value] = entry.Key;
			else
				idBefore[entry.Value] = null;
		}

		var map = new Dictionary<long, long>();
		var dead = new List<long>();
		foreach (var oldId in protectedIds)
		{
			if (!idBefore.TryGetValue(oldId, out var key))
			{
				// Referenced id did not correspond to any live object even before the
				// save — the reference was already dead, nothing to preserve.
				dead.Add(oldId);
				continue;
			}

			if (key == null)
				continue;

			if (!mapAfter.TryGetValue(key, out var newId))
			{
				dead.Add(oldId);
				continue;
			}

			if (newId != oldId)
				map[oldId] = newId;
		}

		if (dead.Count > 0)
		{
			result.Log.Add(
				$"{Path.GetFileName(candidate.Path)}: {dead.Count} адресуемых id были мертвы " +
				"ещё до пересохранения (старый мусор), оставлены как есть");
		}

		if (map.Count == 0)
		{
			result.AlreadyCanonical++;
			return;
		}

		result.AnchorsMoved += map.Count;

		var rewrite = RewriteReferencesProjectWide(candidate.Path, candidate.Guid, map, stepBackups);
		result.ReferencesRewritten += rewrite.Item1;
		result.FilesRewritten += rewrite.Item2;
		result.Log.Add(
			$"{Path.GetFileName(candidate.Path)}: сдвинуто {map.Count} адресуемых id, " +
			$"переписано {rewrite.Item1} ссылок в {rewrite.Item2} файлах");
	}

	private static Dictionary<long, int> CollectRealAnchors(string text)
	{
		var result = new Dictionary<long, int>();
		foreach (Match match in DocumentRegex.Matches(text))
		{
			if (!match.Groups[3].Success)
				result[long.Parse(match.Groups[2].Value)] = int.Parse(match.Groups[1].Value);
		}

		return result;
	}

	private static Tuple<int, int> RewriteReferencesProjectWide(
		string canonicalizedPath,
		string guid,
		Dictionary<long, long> map,
		Dictionary<string, byte[]> stepBackups)
	{
		var writes = 0;
		var changedPaths = new List<string>();
		var candidatePaths = AssetDatabase.GetAllAssetPaths()
			.Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
			.Where(path => TextAssetExtensions.Contains(Path.GetExtension(path)))
			.Where(path => !string.Equals(path, canonicalizedPath, StringComparison.OrdinalIgnoreCase))
			.ToArray();

		for (var index = 0; index < candidatePaths.Length; index++)
		{
			var path = candidatePaths[index];
			if (index % 100 == 0)
			{
				EditorUtility.DisplayProgressBar(
					"Переписываю ссылки",
					path,
					(float)index / candidatePaths.Length);
			}

			var absolutePath = GetAbsoluteAssetPath(path);
			string text;
			try
			{
				text = File.ReadAllText(absolutePath, Encoding.UTF8);
			}
			catch (Exception)
			{
				continue;
			}

			if (text.IndexOf(guid, StringComparison.OrdinalIgnoreCase) < 0)
				continue;

			var writesInFile = 0;
			var rewritten = ExternalObjectReferenceRegex.Replace(text, match =>
			{
				if (!string.Equals(match.Groups[2].Value, guid, StringComparison.OrdinalIgnoreCase))
					return match.Value;
				if (!long.TryParse(match.Groups[1].Value, out var currentId) || currentId == MainPrefabAssetFileId)
					return match.Value;
				if (!map.TryGetValue(currentId, out var newId))
					return match.Value;

				writesInFile++;
				var idGroup = match.Groups[1];
				var relativeIndex = idGroup.Index - match.Index;
				return match.Value.Substring(0, relativeIndex) + newId +
				       match.Value.Substring(relativeIndex + idGroup.Length);
			});

			if (writesInFile == 0)
				continue;

			if (!stepBackups.ContainsKey(path))
				stepBackups[path] = File.ReadAllBytes(absolutePath);

			File.WriteAllText(absolutePath, rewritten, Utf8WithoutBom);
			changedPaths.Add(path);
			writes += writesInFile;
		}

		if (changedPaths.Count > 0)
		{
			AssetDatabase.StartAssetEditing();
			try
			{
				foreach (var path in changedPaths)
					AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
			}
		}

		return Tuple.Create(writes, changedPaths.Count);
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

	private static string GetAbsoluteAssetPath(string assetPath)
	{
		var projectRoot = Directory.GetParent(Application.dataPath);
		if (projectRoot == null)
			throw new InvalidOperationException("Cannot resolve Unity project root");
		return Path.GetFullPath(Path.Combine(projectRoot.FullName, assetPath));
	}
}
#endif
