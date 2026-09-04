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
/// Asks Unity to resolve every GUID + fileID reference in the project. Unlike a text
/// check this also sees the computed IDs of objects inside nested prefab instances,
/// which never appear as YAML anchors — so this is the authoritative answer to
/// "is anything dangling?".
/// </summary>
public static class PrefabReferenceValidator
{
	private const long MainPrefabAssetFileId = 100100000;

	private static readonly Regex ExternalObjectReferenceRegex = new Regex(
		@"\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*\d+\}",
		RegexOptions.Compiled);

	private static readonly HashSet<string> TextAssetExtensions = new HashSet<string>(
		new[] { ".prefab", ".unity", ".asset", ".playable", ".controller", ".overridecontroller", ".mat" },
		StringComparer.OrdinalIgnoreCase);

	public sealed class Broken
	{
		public readonly string TargetPath;
		public readonly long FileId;
		public readonly List<string> Referrers = new List<string>();

		public Broken(string targetPath, long fileId)
		{
			TargetPath = targetPath;
			FileId = fileId;
		}
	}

	public sealed class Result
	{
		public int FilesScanned;
		public int ReferencesFound;
		public int UniquePairsChecked;
		public readonly List<Broken> BrokenReferences = new List<Broken>();

		public override string ToString()
		{
			var text =
				$"Проверено файлов: {FilesScanned}\n" +
				$"Ссылок с GUID + fileID: {ReferencesFound}\n" +
				$"Уникальных объектов проверено через Unity: {UniquePairsChecked}\n" +
				$"НЕ РАЗРЕШАЕТСЯ: {BrokenReferences.Sum(broken => broken.Referrers.Count)} ссылок " +
				$"на {BrokenReferences.Count} объектов\n";

			if (BrokenReferences.Count == 0)
				return text + "\nВсе ссылки живые.";

			var lines = BrokenReferences
				.OrderByDescending(broken => broken.Referrers.Count)
				.Take(40)
				.Select(broken =>
					$"  {broken.TargetPath} : fileID {broken.FileId}\n" +
					"    <- " + string.Join("\n    <- ", broken.Referrers.Take(5)) +
					(broken.Referrers.Count > 5 ? $"\n    ... и ещё {broken.Referrers.Count - 5}" : string.Empty));

			return text + "\n" + string.Join("\n", lines);
		}
	}

	/// <summary>
	/// Checks references pointing at the given asset paths, or at every asset when
	/// targetPaths is null.
	/// </summary>
	public static Result Validate(IEnumerable<string> targetPaths)
	{
		var result = new Result();
		var wanted = targetPaths == null
			? null
			: new HashSet<string>(
				targetPaths.Select(AssetDatabase.AssetPathToGUID).Where(guid => !string.IsNullOrEmpty(guid)),
				StringComparer.OrdinalIgnoreCase);

		var referrersByPair = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		var candidatePaths = AssetDatabase.GetAllAssetPaths()
			.Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
			.Where(path => TextAssetExtensions.Contains(Path.GetExtension(path)))
			.ToArray();

		try
		{
			for (var index = 0; index < candidatePaths.Length; index++)
			{
				var path = candidatePaths[index];
				if (index % 25 == 0)
				{
					EditorUtility.DisplayProgressBar(
						"Собираю ссылки",
						path,
						(float)index / candidatePaths.Length * 0.4f);
				}

				string text;
				try
				{
					text = File.ReadAllText(path, Encoding.UTF8);
				}
				catch (Exception)
				{
					continue;
				}

				result.FilesScanned++;
				foreach (Match match in ExternalObjectReferenceRegex.Matches(text))
				{
					var guid = match.Groups[2].Value;
					if (wanted != null && !wanted.Contains(guid))
						continue;

					if (!long.TryParse(match.Groups[1].Value, out var fileId) ||
					    fileId == 0 || fileId == MainPrefabAssetFileId)
						continue;

					result.ReferencesFound++;
					var key = guid + ":" + fileId;
					if (!referrersByPair.TryGetValue(key, out var referrers))
					{
						referrers = new List<string>();
						referrersByPair[key] = referrers;
					}

					if (!referrers.Contains(path))
						referrers.Add(path);
				}
			}

			var pairs = referrersByPair.Keys.ToArray();
			result.UniquePairsChecked = pairs.Length;

			for (var index = 0; index < pairs.Length; index++)
			{
				if (index % 100 == 0)
				{
					EditorUtility.DisplayProgressBar(
						"Спрашиваю Unity про каждый объект",
						$"{index} / {pairs.Length}",
						0.4f + (float)index / pairs.Length * 0.6f);
				}

				var separator = pairs[index].IndexOf(':');
				var guid = pairs[index].Substring(0, separator);
				var fileId = long.Parse(pairs[index].Substring(separator + 1));

				if (Resolves(guid, fileId))
					continue;

				var broken = new Broken(AssetDatabase.GUIDToAssetPath(guid), fileId);
				broken.Referrers.AddRange(referrersByPair[pairs[index]]);
				result.BrokenReferences.Add(broken);
			}
		}
		finally
		{
			EditorUtility.ClearProgressBar();
		}

		DumpFullReport(result);
		return result;
	}

	/// <summary>
	/// The window truncates long lists, so the complete result also goes to
	/// Temp/PrefabRefValidation.txt in the project root.
	/// </summary>
	private static void DumpFullReport(Result result)
	{
		try
		{
			var builder = new StringBuilder();
			builder.AppendLine($"files={result.FilesScanned} refs={result.ReferencesFound} " +
			                   $"unique={result.UniquePairsChecked} broken={result.BrokenReferences.Count}");
			foreach (var broken in result.BrokenReferences
				         .OrderByDescending(item => item.Referrers.Count)
				         .ThenBy(item => item.TargetPath, StringComparer.Ordinal))
			{
				builder.AppendLine($"{broken.TargetPath} :: {broken.FileId}");
				foreach (var referrer in broken.Referrers)
					builder.AppendLine("    <- " + referrer);
			}

			var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "PrefabRefValidation.txt"));
			File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
			Debug.Log("[ValidateRefs] полный отчёт: " + path);
		}
		catch (Exception exception)
		{
			Debug.LogWarning("[ValidateRefs] не удалось записать полный отчёт: " + exception.Message);
		}
	}

	private static readonly Dictionary<string, HashSet<long>> AnchorCache =
		new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);

	private static readonly Regex AnchorRegex = new Regex(
		@"(?m)^--- !u!\d+ &(-?\d+)( stripped)?\r?$",
		RegexOptions.Compiled);

	private static bool Resolves(string guid, long fileId)
	{
		var path = AssetDatabase.GUIDToAssetPath(guid);

		// Builtin resources, editor resources and package assets are outside the
		// project and untouched by any of our tooling — not verifiable, not ours.
		if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
			return true;

		// Cheapest and decisive for text-serialized assets: the anchor is the object.
		if (!AnchorCache.TryGetValue(path, out var anchors))
		{
			anchors = null;
			try
			{
				var head = File.ReadAllText(path, Encoding.UTF8);
				if (head.StartsWith("%YAML", StringComparison.Ordinal))
				{
					anchors = new HashSet<long>(
						AnchorRegex.Matches(head).Cast<Match>()
							.Select(match => long.Parse(match.Groups[1].Value)));
				}
			}
			catch (Exception)
			{
				// Binary or unreadable: fall through to GlobalObjectId.
			}

			AnchorCache[path] = anchors;
		}

		if (anchors != null && anchors.Contains(fileId))
			return true;

		// The identifier prints the object ID unsigned, so negative fileIDs have to be
		// reinterpreted rather than formatted with a minus sign. Type 1 covers
		// imported assets (FBX, textures), type 3 native source assets; computed ids
		// of nested prefab objects resolve through either depending on context, so
		// both are tried before declaring the reference dead.
		foreach (var identifierType in new[] { 1, 3 })
		{
			var text = $"GlobalObjectId_V1-{identifierType}-{guid}-{unchecked((ulong)fileId)}-0";
			if (GlobalObjectId.TryParse(text, out var globalId) &&
			    GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) != null)
				return true;
		}

		return false;
	}
}
#endif
