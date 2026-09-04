#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Converts Prefab Variants to regular Prefabs without breaking references.
/// Persistent YAML fileIDs are preserved; references to virtual nested-prefab
/// IDs are remapped to the IDs generated for the resulting regular Prefab.
/// UI lives in PrefabVariantConverterWindow; this class is the engine.
/// </summary>
public static class ConvertPrefabVariantsToOriginals
{
	public const string DefaultGameObjectsFolder = "Assets/LA8_slv/GameObjects";

	private const long MainPrefabAssetFileId = 100100000;
	private const string MainPrefabAssetFileIdText = "100100000";
	private const int MaxRewritePasses = 8;

	private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

	private static readonly Regex AnchorRegex = new Regex(
		@"(?m)(^--- !u!\d+ &)(-?\d+)(?=(?: stripped)?\r?$)",
		RegexOptions.Compiled);

	private static readonly Regex InlineObjectReferenceRegex = new Regex(
		@"(\{fileID:\s*)(-?\d+)([^}\r\n]*\})",
		RegexOptions.Compiled);

	private static readonly Regex ExternalObjectReferenceRegex = new Regex(
		@"\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*\d+\}",
		RegexOptions.Compiled);

	private static readonly HashSet<string> TextAssetExtensions = new HashSet<string>(
		new[]
		{
			".prefab", ".unity", ".asset", ".anim", ".controller", ".overridecontroller",
			".playable", ".mat", ".spriteatlas", ".spriteatlasv2", ".rendertexture", ".mask",
			".guiskin", ".preset", ".signal", ".mixer", ".brush", ".terrainlayer", ".lighting",
			".shadervariants", ".inputactions"
		},
		StringComparer.OrdinalIgnoreCase);

	// Read-only instantiation happens in a preview scene so the user's open scene
	// is never dirtied. Falls back to the active scene if a preview scene fails.
	private static Scene _inspectionScene;

	// Set once the tool replaced the open scene with an empty one, so the following
	// batches do not trip over the dirty flag the conversion itself produces.
	private static bool _openedOwnScene;

	// ------------------------------------------------------------------ public API

	public sealed class VariantInfo
	{
		public readonly string Path;
		public readonly string Guid;
		public readonly int Depth;
		public readonly string BasePath;

		public VariantInfo(string path, string guid, int depth, string basePath)
		{
			Path = path;
			Guid = guid;
			Depth = depth;
			BasePath = basePath;
		}
	}

	/// <summary>
	/// A GUID + fileID reference that does not resolve to any object of the target
	/// prefab. Unity silently ignores those: they are leftovers of objects deleted
	/// somewhere up the variant chain, so there is nothing to preserve.
	/// </summary>
	public sealed class UnresolvedReference
	{
		public readonly string OwnerPath;
		public readonly string Guid;
		public readonly long FileId;

		public UnresolvedReference(string ownerPath, string guid, long fileId)
		{
			OwnerPath = ownerPath;
			Guid = guid;
			FileId = fileId;
		}

		public override string ToString()
		{
			return $"{OwnerPath} -> GUID {Guid}, fileID {FileId}";
		}
	}

	public sealed class ReferenceScan
	{
		public int TotalReferences;
		public readonly Dictionary<string, int> ReferencesByGuid =
			new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		public readonly Dictionary<string, int> ReferencesByOwner =
			new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		public readonly List<UnresolvedReference> Unresolved = new List<UnresolvedReference>();

		/// <summary>
		/// Every asset that somebody addresses by an inner object rather than by the
		/// asset itself. If such an asset gets rewritten, its own IDs must not move.
		/// </summary>
		public readonly HashSet<string> DeeplyReferencedGuids =
			new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Which inner objects of each scanned GUID are addressed at all.</summary>
		public readonly Dictionary<string, HashSet<long>> ReferencedIdsByGuid =
			new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
	}

	public sealed class ConversionReport
	{
		public bool DryRun;
		public int Requested;
		public int Converted;
		public int ExternalReferencesChecked;
		public int RewrittenReferences;
		public int RewrittenFiles;
		public int AnchorWrites;
		public int LocalReferenceWrites;
		public int VerifiedOwners;
		public int SkippedSceneOwners;
		public int DanglingReferences;
		public int DanglingOwners;
		public int RewritePasses;

		private string DanglingLine
		{
			get
			{
				return DanglingReferences == 0
					? string.Empty
					: $"Висячих ссылок пропущено: {DanglingReferences} в {DanglingOwners} файлах\n" +
					  "  (были сломаны ДО конвертации — объект удалён выше по цепочке; список в Console)\n";
			}
		}

		public override string ToString()
		{
			if (DryRun)
			{
				return
					$"ПРОВЕРКА (dry run) пройдена. Ничего не записано.\n" +
					$"Готовы к конвертации: {Requested}\n" +
					$"Проверено внешних ссылок: {ExternalReferencesChecked}\n" +
					DanglingLine +
					"Живые ссылки разрешаются, набор замкнут по потомкам,\n" +
					"иерархии читаются, дубликатов fileID нет.";
			}

			return
				$"Готово. Преобразовано: {Converted} / {Requested}\n" +
				$"Проверено внешних ссылок: {ExternalReferencesChecked}\n" +
				$"Переназначено внешних ссылок: {RewrittenReferences} в {RewrittenFiles} файлах" +
				(RewritePasses > 1 ? $" за {RewritePasses} прохода" : string.Empty) + "\n" +
				DanglingLine +
				$"Восстановлено YAML anchors: {AnchorWrites}\n" +
				$"Восстановлено локальных ссылок: {LocalReferenceWrites}\n" +
				(VerifiedOwners > 0
					? $"Проверено владельцев (.prefab): {VerifiedOwners}" +
					  (SkippedSceneOwners > 0 ? $", пропущено сцен/ассетов: {SkippedSceneOwners}" : string.Empty) + "\n"
					: string.Empty) +
				"\nСтруктура и все внешние GUID + fileID ссылки проверены после импорта.";
		}
	}

	/// <summary>
	/// Returns a human readable list of reasons why running now is unsafe, or an
	/// empty string. Dirty scenes only block real conversion runs.
	/// </summary>
	public static string DescribeEnvironmentBlockers(bool forConversion)
	{
		var blockers = new List<string>();

		if (EditorApplication.isPlayingOrWillChangePlaymode)
			blockers.Add("Активен Play Mode — останови воспроизведение.");

		if (PrefabStageUtility.GetCurrentPrefabStage() != null)
			blockers.Add("Открыт Prefab Mode — закрой режим редактирования префаба.");

		if (EditorSettings.serializationMode != SerializationMode.ForceText)
			blockers.Add("Asset Serialization ≠ Force Text — тулса работает только с текстовыми ассетами.");

		if (forConversion)
		{
			for (var index = 0; index < SceneManager.sceneCount; index++)
			{
				var scene = SceneManager.GetSceneAt(index);
				if (!scene.isDirty)
					continue;

				// Converting instantiates prefabs into the active scene, so the empty
				// scene the tool opened itself is dirty by design — not a blocker.
				if (_openedOwnScene && string.IsNullOrEmpty(scene.path))
					continue;

				var name = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
				blockers.Add($"Сцена \"{name}\" не сохранена — сохрани (Ctrl+S) или закрой её.");
			}
		}

		return string.Join("\n", blockers);
	}

	/// <summary>
	/// Expands folders, keeps .prefab paths and returns only Prefab Variants,
	/// deepest first. A descendant is flattened while all of its bases still exist.
	/// </summary>
	public static List<VariantInfo> CollectVariants(IEnumerable<string> assetPathsOrFolders)
	{
		var prefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var folders = new List<string>();

		foreach (var path in assetPathsOrFolders)
		{
			if (string.IsNullOrEmpty(path))
				continue;

			if (AssetDatabase.IsValidFolder(path))
				folders.Add(path);
			else if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
				prefabPaths.Add(path);
		}

		if (folders.Count > 0)
		{
			foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders.ToArray()))
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
					prefabPaths.Add(path);
			}
		}

		var result = new List<VariantInfo>();
		foreach (var path in prefabPaths)
		{
			var info = TryDescribeVariant(path);
			if (info != null)
				result.Add(info);
		}

		return SortDeepestFirst(result);
	}

	/// <summary>Every .prefab in the given folders/paths, variant or not.</summary>
	public static List<string> CollectAllPrefabs(IEnumerable<string> assetPathsOrFolders)
	{
		var prefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var folders = new List<string>();

		foreach (var path in assetPathsOrFolders)
		{
			if (string.IsNullOrEmpty(path))
				continue;

			if (AssetDatabase.IsValidFolder(path))
				folders.Add(path);
			else if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
				prefabPaths.Add(path);
		}

		if (folders.Count > 0)
		{
			foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders.ToArray()))
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
					prefabPaths.Add(path);
			}
		}

		return prefabPaths.OrderBy(path => path, StringComparer.Ordinal).ToList();
	}

	/// <summary>
	/// Variants whose base is in the set but which are not in the set themselves.
	/// Their inherited fileIDs are a function of the base IDs, so converting a base
	/// without its descendants silently breaks references into those descendants.
	/// </summary>
	public static List<string> FindMissingVariantDescendants(IEnumerable<string> paths)
	{
		var selected = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
		var missing = new List<string>();
		var allPrefabPaths = AssetDatabase.FindAssets("t:Prefab")
			.Select(AssetDatabase.GUIDToAssetPath)
			.Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
			.Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		// Cheap text pre-filter: only prefabs mentioning a selected GUID can derive
		// from it. Loading 1000+ prefab assets would otherwise dominate the runtime.
		var candidates = new List<string>();
		for (var index = 0; index < allPrefabPaths.Length; index++)
		{
			var path = allPrefabPaths[index];
			if (selected.Contains(path))
				continue;

			if (index % 100 == 0)
			{
				EditorUtility.DisplayProgressBar(
					"Search variant descendants",
					path,
					(float)index / allPrefabPaths.Length);
			}

			string text;
			try
			{
				text = File.ReadAllText(GetAbsoluteAssetPath(path), Encoding.UTF8);
			}
			catch (Exception)
			{
				continue;
			}

			if (text.IndexOf("m_SourcePrefab", StringComparison.Ordinal) >= 0)
				candidates.Add(path);
		}

		EditorUtility.ClearProgressBar();

		// Descendants of descendants must be picked up too, so iterate to closure.
		var grown = true;
		while (grown)
		{
			grown = false;
			for (var index = candidates.Count - 1; index >= 0; index--)
			{
				var path = candidates[index];
				var info = TryDescribeVariant(path);
				if (info == null)
				{
					candidates.RemoveAt(index);
					continue;
				}

				if (string.IsNullOrEmpty(info.BasePath) || !selected.Contains(info.BasePath))
					continue;

				candidates.RemoveAt(index);
				selected.Add(path);
				missing.Add(path);
				grown = true;
			}
		}

		missing.Sort(StringComparer.Ordinal);
		return missing;
	}

	/// <summary>
	/// Walks every text asset and reports GUID + fileID references pointing at the
	/// given GUIDs. When validIdsByGuid is supplied, references to unknown IDs are
	/// collected in InvalidReferences.
	/// </summary>
	public static ReferenceScan ScanReferences(
		HashSet<string> guids,
		Dictionary<string, HashSet<long>> validIdsByGuid,
		string progressTitle,
		float progress,
		IEnumerable<string> restrictTo = null,
		bool collectDeeplyReferenced = false)
	{
		var scan = new ReferenceScan();
		var candidatePaths = restrictTo == null
			? EnumerateTextAssetPaths()
			: restrictTo.OrderBy(path => path, StringComparer.Ordinal).ToArray();

		for (var index = 0; index < candidatePaths.Length; index++)
		{
			var path = candidatePaths[index];
			if (index % 50 == 0 && !string.IsNullOrEmpty(progressTitle))
			{
				EditorUtility.DisplayProgressBar(
					progressTitle,
					path,
					progress < 0f ? (float)index / candidatePaths.Length : progress);
			}

			string text;
			try
			{
				text = File.ReadAllText(GetAbsoluteAssetPath(path), Encoding.UTF8);
			}
			catch (Exception)
			{
				continue;
			}

			foreach (Match match in ExternalObjectReferenceRegex.Matches(text))
			{
				var guid = match.Groups[2].Value;
				if (collectDeeplyReferenced &&
				    !string.Equals(match.Groups[1].Value, MainPrefabAssetFileIdText, StringComparison.Ordinal))
				{
					scan.DeeplyReferencedGuids.Add(guid);
				}

				if (!guids.Contains(guid))
					continue;

				scan.TotalReferences++;
				scan.ReferencesByGuid.TryGetValue(guid, out var guidCount);
				scan.ReferencesByGuid[guid] = guidCount + 1;
				scan.ReferencesByOwner.TryGetValue(path, out var ownerCount);
				scan.ReferencesByOwner[path] = ownerCount + 1;

				long.TryParse(match.Groups[1].Value, out var fileId);
				if (!scan.ReferencedIdsByGuid.TryGetValue(guid, out var referencedIds))
				{
					referencedIds = new HashSet<long>();
					scan.ReferencedIdsByGuid[guid] = referencedIds;
				}

				referencedIds.Add(fileId);

				if (validIdsByGuid == null)
					continue;

				if (!validIdsByGuid[guid].Contains(fileId))
					scan.Unresolved.Add(new UnresolvedReference(path, guid, fileId));
			}
		}

		return scan;
	}

	/// <summary>
	/// Converts the given prefabs. Throws InvalidOperationException on any problem;
	/// everything written so far is rolled back before the exception leaves.
	/// </summary>
	public static ConversionReport Run(
		IEnumerable<string> paths,
		bool dryRun,
		bool verifyOwners,
		bool openEmptyScene)
	{
		var blockers = DescribeEnvironmentBlockers(!dryRun);
		if (!string.IsNullOrEmpty(blockers))
			throw new InvalidOperationException(blockers);

		var variantInfo = CollectVariants(paths);
		if (variantInfo.Count == 0)
			throw new InvalidOperationException("Prefab Variant не найдено.");

		var missingDescendants = FindMissingVariantDescendants(variantInfo.Select(info => info.Path));
		if (missingDescendants.Count > 0)
		{
			throw new InvalidOperationException(
				"В набор не попали Variant-потомки конвертируемых префабов. Их унаследованные " +
				"fileID производны от базы, поэтому конвертация базы без потомков ломает ссылки.\n" +
				"Добавь в набор:\n" + string.Join("\n", missingDescendants.Take(25)) +
				(missingDescendants.Count > 25 ? $"\n... и ещё {missingDescendants.Count - 25}" : string.Empty));
		}

		if (!dryRun && openEmptyScene)
		{
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			_openedOwnScene = true;
		}

		var report = new ConversionReport { DryRun = dryRun, Requested = variantInfo.Count };
		var snapshots = new List<PrefabSnapshot>();
		var mutationStarted = false;
		var externalBackups = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

		try
		{
			OpenInspectionScene();

			// Snapshot every source before the first mutation. These bytes are also
			// the transaction rollback data.
			for (var index = 0; index < variantInfo.Count; index++)
			{
				var info = variantInfo[index];
				EditorUtility.DisplayProgressBar(
					"Analyze Prefab Variants",
					info.Path,
					(float)index / variantInfo.Count * 0.2f);

				var absolutePath = GetAbsoluteAssetPath(info.Path);
				var bytes = File.ReadAllBytes(absolutePath);
				var yaml = Utf8WithoutBom.GetString(bytes);
				if (!yaml.StartsWith("%YAML", StringComparison.Ordinal))
					throw new InvalidOperationException($"Prefab is not text-serialized: {info.Path}");

				var stableIds = BuildStableKeyToFileIdMap(info.Path, true);
				EnsureUniqueIds(stableIds, info.Path, "before conversion");
				snapshots.Add(new PrefabSnapshot(info, bytes, stableIds));
			}

			var targetGuids = new HashSet<string>(
				snapshots.Select(snapshot => snapshot.Info.Guid),
				StringComparer.OrdinalIgnoreCase);
			var targetPaths = new HashSet<string>(
				snapshots.Select(snapshot => snapshot.Info.Path),
				StringComparer.OrdinalIgnoreCase);

			var preScan = ScanReferences(
				targetGuids,
				BuildOriginalValidIdSets(snapshots),
				"Audit external Prefab links",
				0.2f,
				null,
				true);
			report.ExternalReferencesChecked = preScan.TotalReferences;

			// References that do not resolve now are already broken: an object was
			// deleted up the variant chain and the owner kept a stale override.
			// They cannot be preserved, so they are left byte-identical and reported.
			var danglingByGuid = targetGuids.ToDictionary(
				guid => guid,
				guid => new HashSet<long>(),
				StringComparer.OrdinalIgnoreCase);

			foreach (var unresolved in preScan.Unresolved)
				danglingByGuid[unresolved.Guid].Add(unresolved.FileId);

			report.DanglingReferences = preScan.Unresolved.Count;
			report.DanglingOwners = preScan.Unresolved
				.Select(unresolved => unresolved.OwnerPath)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Count();

			if (report.DanglingReferences > 0)
			{
				// A large share means the tool failed to enumerate objects, not that
				// the data is broken. Refuse instead of mangling the project.
				if (report.DanglingReferences * 2 > preScan.TotalReferences)
				{
					throw new InvalidOperationException(
						$"Не разрешается слишком много ссылок ({report.DanglingReferences} из " +
						$"{preScan.TotalReferences}) — это похоже на ошибку инструмента, а не на битые данные.\n" +
						string.Join("\n", preScan.Unresolved.Take(25).Select(unresolved => unresolved.ToString())));
				}

				Debug.LogWarning(
					$"[ConvertVariants] Висячих ссылок: {report.DanglingReferences} в {report.DanglingOwners} файлах. " +
					"Они были сломаны до конвертации (объект удалён выше по цепочке) и оставлены без изменений:\n" +
					string.Join("\n", preScan.Unresolved.Take(60).Select(unresolved => unresolved.ToString())) +
					(report.DanglingReferences > 60 ? $"\n... и ещё {report.DanglingReferences - 60}" : string.Empty));
			}

			// Rewriting an owner can make Unity regenerate the IDs of ITS stripped
			// objects too. That breaks whoever addresses those objects — in this project
			// the Pack02 level chain does exactly that. Such owners are tracked like
			// targets: their moved IDs get remapped in their own referrers.
			var externalOwners = preScan.ReferencesByOwner.Keys
				.Where(path => !targetPaths.Contains(path))
				.ToList();
			var ownerPrefabs = externalOwners
				.Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
				.OrderBy(path => path, StringComparer.Ordinal)
				.ToList();

			report.SkippedSceneOwners = externalOwners.Count - ownerPrefabs.Count;

			var ownerGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var path in ownerPrefabs)
			{
				var ownerGuid = AssetDatabase.AssetPathToGUID(path);
				if (!string.IsNullOrEmpty(ownerGuid))
					ownerGuids[ownerGuid] = path;
			}

			var ownerScan = ScanReferences(
				new HashSet<string>(ownerGuids.Keys, StringComparer.OrdinalIgnoreCase),
				null,
				"Audit links into owners",
				0.22f);

			// The referrers of those owners must be rewritable too.
			var scanSet = new HashSet<string>(preScan.ReferencesByOwner.Keys, StringComparer.OrdinalIgnoreCase);
			scanSet.UnionWith(ownerScan.ReferencesByOwner.Keys);
			scanSet.UnionWith(targetPaths);

			var trackedOwners = new List<TrackedOwner>();
			if (!dryRun)
			{
				var candidates = ownerGuids
					.Where(entry => IsAddressedByInnerObject(ownerScan, entry.Key) || verifyOwners)
					.OrderBy(entry => entry.Value, StringComparer.Ordinal)
					.ToList();

				for (var index = 0; index < candidates.Count; index++)
				{
					var path = candidates[index].Value;
					EditorUtility.DisplayProgressBar(
						"Snapshot owner Prefab IDs",
						path,
						0.24f + (float)index / candidates.Count * 0.04f);

					ownerScan.ReferencedIdsByGuid.TryGetValue(candidates[index].Key, out var referenced);
					var baseline = BuildStableKeyToFileIdMap(path, false);
					var valid = new HashSet<long>(baseline.Values) { MainPrefabAssetFileId };
					valid.UnionWith(CollectSerializedObjectIds(path));

					trackedOwners.Add(new TrackedOwner(
						path,
						candidates[index].Key,
						baseline,
						new HashSet<long>(referenced ?? new HashSet<long>()),
						// References that do not resolve even now are pre-existing rot.
						new HashSet<long>((referenced ?? new HashSet<long>()).Where(id => !valid.Contains(id)))));
				}
			}

			if (dryRun)
				return report;

			for (var index = 0; index < snapshots.Count; index++)
			{
				var snapshot = snapshots[index];
				EditorUtility.DisplayProgressBar(
					"Convert Variants → Originals",
					snapshot.Info.Path,
					0.25f + (float)index / snapshots.Count * 0.7f);

				mutationStarted = true;
				ConvertOne(snapshot.Info.Path);

				var generatedIds = BuildStableKeyToFileIdMap(snapshot.Info.Path, true);
				EnsureUniqueIds(generatedIds, snapshot.Info.Path, "after SaveAsPrefabAsset");
				var newToOld = BuildStrictNewToOldMap(snapshot.StableIds, generatedIds, snapshot.Info.Path);

				var writes = RestoreLocalFileIdsInPrefabFile(snapshot.Info.Path, newToOld);
				report.AnchorWrites += writes.AnchorWrites;
				report.LocalReferenceWrites += writes.LocalReferenceWrites;

				AssetDatabase.ImportAsset(
					snapshot.Info.Path,
					ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

				report.Converted++;
			}

			AssetDatabase.SaveAssets();

			// IDs of nested prefab children are virtual and cannot always be forced by
			// editing YAML anchors. Resolve their final IDs after every base changed,
			// then update GUID + fileID references in the rest of the project.
			var actualByGuid = new Dictionary<string, Dictionary<long, long>>(StringComparer.OrdinalIgnoreCase);
			for (var index = 0; index < snapshots.Count; index++)
			{
				EditorUtility.DisplayProgressBar(
					"Resolve final Prefab IDs",
					snapshots[index].Info.Path,
					0.9f + (float)index / snapshots.Count * 0.04f);
				actualByGuid[snapshots[index].Info.Guid] = BuildOldToActualIdMap(snapshots[index]);
			}

			// Rewriting a converted prefab that nests another converted prefab makes
			// Unity regenerate the stripped IDs of the nesting file on import — which
			// invalidates the references its own owners hold. So rewrite, re-resolve
			// what moved, and repeat until nothing moves any more.
			var remapByGuid = actualByGuid.ToDictionary(
				entry => entry.Key,
				entry => new Dictionary<long, long>(entry.Value),
				StringComparer.OrdinalIgnoreCase);
			var rewrittenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var converged = false;

			// Owners are remapped partially: only the IDs that actually moved are known,
			// everything else in them must be left alone rather than treated as an error.
			var partialGuids = new HashSet<string>(
				trackedOwners.Select(owner => owner.Guid),
				StringComparer.OrdinalIgnoreCase);

			for (var pass = 1; pass <= MaxRewritePasses && !converged; pass++)
			{
				var rewriteResult = RewriteExternalReferences(
					scanSet, targetPaths, remapByGuid, danglingByGuid, partialGuids, externalBackups, pass);
				report.RewrittenReferences += rewriteResult.ReferenceWrites;
				report.RewritePasses = pass;
				rewrittenFiles.UnionWith(rewriteResult.ChangedPaths);

				if (rewriteResult.ReferenceWrites == 0)
				{
					converged = true;
					break;
				}

				// A file can only have regenerated IDs if it was itself just rewritten,
				// so everything else needs no re-measurement.
				var nextRemap = new Dictionary<string, Dictionary<long, long>>(StringComparer.OrdinalIgnoreCase);

				var suspects = snapshots
					.Where(snapshot => rewriteResult.ChangedPaths.Contains(snapshot.Info.Path))
					.ToList();

				for (var index = 0; index < suspects.Count; index++)
				{
					var snapshot = suspects[index];
					EditorUtility.DisplayProgressBar(
						$"Re-resolve Prefab IDs (проход {pass + 1})",
						snapshot.Info.Path,
						0.96f + (float)index / suspects.Count * 0.01f);

					var resolved = BuildOldToActualIdMap(snapshot);
					var previous = actualByGuid[snapshot.Info.Guid];
					if (IdMapsEqual(previous, resolved))
						continue;

					nextRemap[snapshot.Info.Guid] = BuildCurrentToActualMap(previous, resolved, snapshot.Info.Path);
					actualByGuid[snapshot.Info.Guid] = resolved;
				}

				var ownerSuspects = trackedOwners
					.Where(owner => rewriteResult.ChangedPaths.Contains(owner.Path))
					.ToList();

				for (var index = 0; index < ownerSuspects.Count; index++)
				{
					var owner = ownerSuspects[index];
					EditorUtility.DisplayProgressBar(
						$"Re-resolve owner IDs (проход {pass + 1})",
						owner.Path,
						0.97f + (float)index / ownerSuspects.Count * 0.01f);

					var resolved = BuildStableKeyToFileIdMap(owner.Path, false);
					var moved = BuildOwnerRemap(owner, resolved);
					owner.Baseline = resolved;
					if (moved.Count == 0)
						continue;

					// The referrers now hold the new IDs, so track those from here on.
					foreach (var entry in moved)
					{
						owner.ReferencedIds.Remove(entry.Key);
						owner.ReferencedIds.Add(entry.Value);
					}

					if (nextRemap.TryGetValue(owner.Guid, out var existing))
					{
						foreach (var entry in moved)
							AddRemapping(existing, entry.Key, entry.Value, owner.Path);
					}
					else
					{
						nextRemap[owner.Guid] = moved;
					}
				}

				if (nextRemap.Count == 0)
					converged = true;
				else
					remapByGuid = nextRemap;
			}

			if (!converged)
			{
				throw new InvalidOperationException(
					$"fileID не стабилизировались за {MaxRewritePasses} проходов. " +
					"Скорее всего в наборе есть циклическая вложенность префабов.");
			}

			report.RewrittenFiles = rewrittenFiles.Count;

			var postValidIds = BuildActualValidIdSets(actualByGuid);
			foreach (var dangling in danglingByGuid)
				postValidIds[dangling.Key].UnionWith(dangling.Value);

			var postScan = ScanReferences(
				targetGuids,
				postValidIds,
				"Verify external Prefab links",
				0.99f,
				scanSet);
			ThrowOnUnresolvedReferences(postScan);
			report.ExternalReferencesChecked = postScan.TotalReferences;

			// Finally: every inner object of a tracked owner that somebody addresses must
			// still exist under the ID the referrers now hold.
			if (trackedOwners.Count > 0)
			{
				var ownerValidIds = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
				foreach (var owner in trackedOwners)
				{
					var current = rewrittenFiles.Contains(owner.Path)
						? BuildStableKeyToFileIdMap(owner.Path, false)
						: owner.Baseline;

					var valid = new HashSet<long>(current.Values) { MainPrefabAssetFileId };
					valid.UnionWith(CollectSerializedObjectIds(owner.Path));
					valid.UnionWith(owner.DanglingIds);
					ownerValidIds[owner.Guid] = valid;
					report.VerifiedOwners++;
				}

				var ownerVerification = ScanReferences(
					new HashSet<string>(ownerValidIds.Keys, StringComparer.OrdinalIgnoreCase),
					ownerValidIds,
					"Verify links into owners",
					0.995f,
					scanSet);
				ThrowOnUnresolvedReferences(ownerVerification);
			}

			Debug.Log("[ConvertVariants] " + report);
			return report;
		}
		catch (Exception exception)
		{
			if (!mutationStarted)
				throw;

			var rollbackError = Rollback(snapshots, externalBackups);
			var message = "Конвертация остановлена, исходные prefab восстановлены.\n\n" + exception.Message;
			if (!string.IsNullOrEmpty(rollbackError))
				message += "\n\nОШИБКА ОТКАТА:\n" + rollbackError;

			Debug.LogException(exception);
			throw new InvalidOperationException(message, exception);
		}
		finally
		{
			CloseInspectionScene();
			EditorUtility.ClearProgressBar();
			AssetDatabase.Refresh();
		}
	}

	// ------------------------------------------------------------------ internals

	private static VariantInfo TryDescribeVariant(string prefabPath)
	{
		var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
		if (asset == null || PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant)
			return null;

		var baseObject = PrefabUtility.GetCorrespondingObjectFromSource(asset);
		var basePath = baseObject == null ? string.Empty : AssetDatabase.GetAssetPath(baseObject);

		return new VariantInfo(
			prefabPath,
			AssetDatabase.AssetPathToGUID(prefabPath),
			GetVariantDepth(asset),
			basePath);
	}

	/// <summary>
	/// Splits the set into independently committable transactions. Prefabs of one
	/// variant family (base ↔ derived) must convert together, so families are never
	/// split; a family larger than maxPerBatch simply becomes its own batch.
	/// </summary>
	public static List<List<string>> SplitIntoTransactions(List<VariantInfo> variants, int maxPerBatch)
	{
		var familyOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		string FindRoot(string path)
		{
			var current = path;
			while (familyOf.TryGetValue(current, out var parent) &&
			       !string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
				current = parent;
			return current;
		}

		foreach (var variant in variants)
			familyOf[variant.Path] = variant.Path;

		foreach (var variant in variants)
		{
			if (string.IsNullOrEmpty(variant.BasePath) || !familyOf.ContainsKey(variant.BasePath))
				continue;

			var left = FindRoot(variant.Path);
			var right = FindRoot(variant.BasePath);
			if (!string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
				familyOf[left] = right;
		}

		var families = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (var variant in variants)
		{
			var key = FindRoot(variant.Path);
			if (!families.TryGetValue(key, out var members))
			{
				members = new List<string>();
				families[key] = members;
			}

			members.Add(variant.Path);
		}

		var batches = new List<List<string>>();
		var current = new List<string>();
		foreach (var family in families.Values.OrderByDescending(family => family.Count))
		{
			if (current.Count > 0 && current.Count + family.Count > Mathf.Max(1, maxPerBatch))
			{
				batches.Add(current);
				current = new List<string>();
			}

			current.AddRange(family);
		}

		if (current.Count > 0)
			batches.Add(current);

		return batches;
	}

	public static List<VariantInfo> SortDeepestFirst(List<VariantInfo> variants)
	{
		return variants
			.OrderByDescending(info => info.Depth)
			.ThenBy(info => info.Path, StringComparer.Ordinal)
			.ToList();
	}

	private static void ThrowOnUnresolvedReferences(ReferenceScan scan)
	{
		if (scan.Unresolved.Count == 0)
			return;

		throw new InvalidOperationException(
			"После конвертации остались GUID + fileID ссылки, которые не сопоставились с объектами prefab. " +
			"Первые ссылки:\n" +
			string.Join("\n", scan.Unresolved.Take(25).Select(unresolved => unresolved.ToString())));
	}

	private static string[] EnumerateTextAssetPaths()
	{
		return AssetDatabase.GetAllAssetPaths()
			.Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
			.Where(path => TextAssetExtensions.Contains(Path.GetExtension(path)))
			.ToArray();
	}

	private static Dictionary<string, HashSet<long>> BuildOriginalValidIdSets(List<PrefabSnapshot> snapshots)
	{
		return snapshots.ToDictionary(
			snapshot => snapshot.Info.Guid,
			snapshot =>
			{
				var ids = new HashSet<long>(snapshot.StableIds.Values) { MainPrefabAssetFileId };
				ids.UnionWith(CollectSerializedObjectIds(snapshot.Info.Path));
				return ids;
			},
			StringComparer.OrdinalIgnoreCase);
	}

	private static Dictionary<string, HashSet<long>> BuildActualValidIdSets(
		Dictionary<string, Dictionary<long, long>> oldToActualByGuid)
	{
		return oldToActualByGuid.ToDictionary(
			entry => entry.Key,
			entry =>
			{
				var ids = new HashSet<long>(entry.Value.Values) { MainPrefabAssetFileId };
				ids.UnionWith(CollectSerializedObjectIds(AssetDatabase.GUIDToAssetPath(entry.Key)));
				return ids;
			},
			StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Every object Unity itself exposes for the asset. Used next to the hierarchy
	/// walk so an object the walk cannot reach is not mistaken for a dead reference.
	/// </summary>
	private static HashSet<long> CollectSerializedObjectIds(string prefabPath)
	{
		var ids = new HashSet<long>();
		if (string.IsNullOrEmpty(prefabPath))
			return ids;

		var expectedGuid = AssetDatabase.AssetPathToGUID(prefabPath);
		foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(prefabPath))
		{
			if (asset == null)
				continue;

			if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId) || localId == 0)
				continue;

			if (string.Equals(guid, expectedGuid, StringComparison.OrdinalIgnoreCase))
				ids.Add(localId);
		}

		return ids;
	}

	private static ExternalRewriteResult RewriteExternalReferences(
		HashSet<string> scanSet,
		HashSet<string> targetPaths,
		Dictionary<string, Dictionary<long, long>> oldToActualByGuid,
		Dictionary<string, HashSet<long>> danglingByGuid,
		HashSet<string> partialGuids,
		Dictionary<string, byte[]> externalBackups,
		int pass)
	{
		var actualIdsByGuid = BuildActualValidIdSets(oldToActualByGuid);
		var changedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var referenceWrites = 0;
		var candidatePaths = scanSet.OrderBy(path => path, StringComparer.Ordinal).ToArray();

		for (var index = 0; index < candidatePaths.Length; index++)
		{
			var path = candidatePaths[index];
			if (index % 10 == 0)
			{
				EditorUtility.DisplayProgressBar(
					$"Rewrite external Prefab links (проход {pass})",
					path,
					0.94f + (float)index / candidatePaths.Length * 0.02f);
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

			var writesInFile = 0;
			var rewritten = ExternalObjectReferenceRegex.Replace(text, match =>
			{
				var guid = match.Groups[2].Value;
				if (!oldToActualByGuid.TryGetValue(guid, out var oldToActual))
					return match.Value;
				if (!long.TryParse(match.Groups[1].Value, out var currentId))
					return match.Value;
				if (currentId == MainPrefabAssetFileId)
					return match.Value;

				if (!oldToActual.TryGetValue(currentId, out var actualId))
				{
					// The owner may itself have just been regenerated and can already
					// contain the final ID.
					if (actualIdsByGuid[guid].Contains(currentId))
						return match.Value;

					// Already dangling before the conversion: leave it byte-identical.
					if (danglingByGuid.TryGetValue(guid, out var dangling) && dangling.Contains(currentId))
						return match.Value;

					// A partial map only knows the IDs that moved; the rest stay as they are.
					if (partialGuids.Contains(guid))
						return match.Value;

					throw new InvalidOperationException(
						$"Cannot remap {path}: GUID {guid}, fileID {currentId}");
				}

				if (actualId == currentId)
					return match.Value;

				writesInFile++;
				var idGroup = match.Groups[1];
				var relativeIndex = idGroup.Index - match.Index;
				return match.Value.Substring(0, relativeIndex) + actualId +
				       match.Value.Substring(relativeIndex + idGroup.Length);
			});

			if (writesInFile == 0)
				continue;

			if (!targetPaths.Contains(path) && !externalBackups.ContainsKey(path))
				externalBackups[path] = File.ReadAllBytes(absolutePath);

			File.WriteAllText(absolutePath, rewritten, Utf8WithoutBom);
			changedPaths.Add(path);
			referenceWrites += writesInFile;
		}

		// One batched import instead of N synchronous ones: importing a level prefab
		// pulls in its dependencies, so doing it per file was the bulk of the runtime.
		if (changedPaths.Count > 0)
		{
			EditorUtility.DisplayProgressBar(
				$"Import rewritten files (проход {pass})",
				$"{changedPaths.Count} файлов",
				0.96f);

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

		return new ExternalRewriteResult(referenceWrites, changedPaths);
	}

	private static Dictionary<long, long> BuildStrictNewToOldMap(
		Dictionary<string, long> oldIds,
		Dictionary<string, long> newIds,
		string prefabPath)
	{
		var missing = oldIds.Keys.Except(newIds.Keys).Take(20).ToList();
		var added = newIds.Keys.Except(oldIds.Keys).Take(20).ToList();
		if (missing.Count > 0 || added.Count > 0)
		{
			throw new InvalidOperationException(
				$"Hierarchy changed while unpacking {prefabPath}.\n" +
				(missing.Count > 0 ? "Missing keys:\n" + string.Join("\n", missing) + "\n" : string.Empty) +
				(added.Count > 0 ? "New keys:\n" + string.Join("\n", added) : string.Empty));
		}

		var result = new Dictionary<long, long>();
		foreach (var oldEntry in oldIds)
		{
			var newId = newIds[oldEntry.Key];
			if (result.TryGetValue(newId, out var alreadyMapped) && alreadyMapped != oldEntry.Value)
				throw new InvalidOperationException($"Generated fileID collision in {prefabPath}: {newId}");
			result[newId] = oldEntry.Value;
		}

		if (result.Values.Distinct().Count() != result.Count)
			throw new InvalidOperationException($"Old fileID collision in {prefabPath}");

		return result;
	}

	private static FileIdWriteResult RestoreLocalFileIdsInPrefabFile(
		string prefabPath,
		Dictionary<long, long> newToOld)
	{
		var absolutePath = GetAbsoluteAssetPath(prefabPath);
		var text = File.ReadAllText(absolutePath, Encoding.UTF8);
		var anchorWrites = 0;
		var localReferenceWrites = 0;
		var writableIds = new HashSet<long>(
			AnchorRegex.Matches(text)
				.Cast<Match>()
				.Select(match => long.Parse(match.Groups[2].Value)));
		var writableMap = newToOld
			.Where(entry => writableIds.Contains(entry.Key))
			.ToDictionary(entry => entry.Key, entry => entry.Value);

		// Regex.Replace evaluates the original text once, so swaps such as A -> B
		// and B -> A are safe. A sequential replace would corrupt them.
		text = AnchorRegex.Replace(text, match =>
		{
			if (!long.TryParse(match.Groups[2].Value, out var newId) ||
			    !writableMap.TryGetValue(newId, out var oldId) || newId == oldId)
				return match.Value;

			anchorWrites++;
			return match.Groups[1].Value + oldId;
		});

		text = InlineObjectReferenceRegex.Replace(text, match =>
		{
			// A reference containing a GUID addresses another asset. Its fileID is
			// in that other asset's namespace and must never be rewritten here.
			var tail = match.Groups[3].Value;
			if (tail.IndexOf("guid:", StringComparison.Ordinal) >= 0)
				return match.Value;

			if (!long.TryParse(match.Groups[2].Value, out var newId) ||
			    !writableMap.TryGetValue(newId, out var oldId) || newId == oldId)
				return match.Value;

			localReferenceWrites++;
			return match.Groups[1].Value + oldId + tail;
		});

		EnsureUniqueYamlAnchors(text, prefabPath);
		File.WriteAllText(absolutePath, text, Utf8WithoutBom);
		return new FileIdWriteResult(anchorWrites, localReferenceWrites);
	}

	private static void EnsureUniqueYamlAnchors(string yaml, string prefabPath)
	{
		var duplicate = AnchorRegex.Matches(yaml)
			.Cast<Match>()
			.Select(match => match.Groups[2].Value)
			.GroupBy(id => id)
			.FirstOrDefault(group => group.Count() > 1);

		if (duplicate != null)
			throw new InvalidOperationException($"Duplicate YAML anchor {duplicate.Key} in {prefabPath}");
	}

	/// <summary>Preview-scene guard for external users of BuildStableKeyToFileIdMap.</summary>
	public static void BeginInspection()
	{
		OpenInspectionScene();
	}

	public static void EndInspection()
	{
		CloseInspectionScene();
	}

	public static Dictionary<string, long> BuildStableKeyToFileIdMap(string prefabPath, bool strict)
	{
		var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
		if (asset == null)
			throw new InvalidOperationException("Cannot load " + prefabPath);

		var map = new Dictionary<string, long>();
		var instance = InstantiateForInspection(asset);
		if (instance == null)
			throw new InvalidOperationException("InstantiatePrefab failed: " + prefabPath);

		var expectedGuid = AssetDatabase.AssetPathToGUID(prefabPath);
		try
		{
			CollectKeys(instance.transform, instance.transform, prefabPath, expectedGuid, map, strict);
		}
		finally
		{
			UnityEngine.Object.DestroyImmediate(instance);
		}

		return map;
	}

	private static void CollectKeys(
		Transform transform,
		Transform root,
		string prefabPath,
		string expectedGuid,
		Dictionary<string, long> map,
		bool strict)
	{
		var hierarchyPath = BuildTransformPath(transform, root);

		TryAddCorresponding(
			transform.gameObject, hierarchyPath + "|GameObject", prefabPath, expectedGuid, map, strict);

		var typeIndex = new Dictionary<Type, int>();
		foreach (var component in transform.gameObject.GetComponents<Component>())
		{
			if (component == null)
				continue;

			var type = component.GetType();
			typeIndex.TryGetValue(type, out var index);
			typeIndex[type] = index + 1;
			var typeName = type.FullName ?? type.Name;
			TryAddCorresponding(
				component, hierarchyPath + "|" + typeName + "|" + index, prefabPath, expectedGuid, map, strict);
		}

		for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
			CollectKeys(transform.GetChild(childIndex), root, prefabPath, expectedGuid, map, strict);
	}

	private static void TryAddCorresponding(
		UnityEngine.Object instanceObject,
		string key,
		string prefabPath,
		string expectedGuid,
		Dictionary<string, long> map,
		bool strict)
	{
		if (instanceObject == null)
			return;

		// The explicit path is important for inherited Variant objects. The
		// parameterless Source call may walk to the base prefab and return an ID
		// from the wrong GUID namespace.
		var source = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(instanceObject, prefabPath);
		if (source == null)
			source = PrefabUtility.GetCorrespondingObjectFromSource(instanceObject);
		if (source == null)
			source = instanceObject;

		TryAddPersistentAssetObject(source, key, prefabPath, expectedGuid, map, strict);
	}

	private static void TryAddPersistentAssetObject(
		UnityEngine.Object assetObject,
		string key,
		string prefabPath,
		string expectedGuid,
		Dictionary<string, long> map,
		bool strict)
	{
		if (assetObject == null)
			return;

		if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(assetObject, out var guid, out long localId) || localId == 0)
			return;

		if (!string.Equals(guid, expectedGuid, StringComparison.OrdinalIgnoreCase))
		{
			if (!strict)
				return;

			throw new InvalidOperationException(
				$"Resolved {key} in {prefabPath} to GUID {guid} instead of {expectedGuid}");
		}

		map[key] = localId;
	}

	private static void EnsureUniqueIds(Dictionary<string, long> map, string prefabPath, string stage)
	{
		var duplicate = map
			.GroupBy(entry => entry.Value)
			.FirstOrDefault(group => group.Count() > 1);
		if (duplicate == null)
			return;

		throw new InvalidOperationException(
			$"Duplicate fileID {duplicate.Key} in {prefabPath} ({stage}):\n" +
			string.Join("\n", duplicate.Select(entry => entry.Key)));
	}

	private static Dictionary<long, long> BuildOldToActualIdMap(PrefabSnapshot snapshot)
	{
		var asset = AssetDatabase.LoadAssetAtPath<GameObject>(snapshot.Info.Path);
		if (asset == null)
			throw new InvalidOperationException("Cannot reload " + snapshot.Info.Path);
		if (PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant)
			throw new InvalidOperationException("Still a Prefab Variant: " + snapshot.Info.Path);

		var actualIds = BuildStableKeyToFileIdMap(snapshot.Info.Path, true);
		EnsureUniqueIds(actualIds, snapshot.Info.Path, "validation");
		var actualToOld = BuildStrictNewToOldMap(snapshot.StableIds, actualIds, snapshot.Info.Path);
		return actualToOld.ToDictionary(entry => entry.Value, entry => entry.Key);
	}

	private static bool IdMapsEqual(Dictionary<long, long> expected, Dictionary<long, long> actual)
	{
		return expected.Count == actual.Count && expected.All(entry =>
			actual.TryGetValue(entry.Key, out var actualId) && actualId == entry.Value);
	}

	/// <summary>
	/// Maps whatever the project files currently hold to the freshly resolved IDs.
	/// A file either still holds the original ID (never rewritten) or the ID written
	/// by the previous pass, so both are mapped to the new one.
	/// </summary>
	private static Dictionary<long, long> BuildCurrentToActualMap(
		Dictionary<long, long> previous,
		Dictionary<long, long> resolved,
		string prefabPath)
	{
		var map = new Dictionary<long, long>();
		foreach (var entry in previous)
		{
			if (!resolved.TryGetValue(entry.Key, out var actualId))
				throw new InvalidOperationException($"Object disappeared while remapping {prefabPath}");

			AddRemapping(map, entry.Value, actualId, prefabPath);
			AddRemapping(map, entry.Key, actualId, prefabPath);
		}

		return map;
	}

	private static void AddRemapping(Dictionary<long, long> map, long from, long to, string prefabPath)
	{
		if (map.TryGetValue(from, out var existing))
		{
			if (existing == to)
				return;

			throw new InvalidOperationException(
				$"Ambiguous fileID remapping in {prefabPath}: {from} -> {existing} and {to}");
		}

		map[from] = to;
	}

	/// <summary>
	/// Maps the moved IDs of an owner, but only those somebody actually addresses:
	/// a level SO usually points at the prefab root only, and roots never move.
	/// </summary>
	private static Dictionary<long, long> BuildOwnerRemap(TrackedOwner owner, Dictionary<string, long> resolved)
	{
		var moved = new Dictionary<long, long>();
		foreach (var entry in owner.Baseline)
		{
			if (!owner.ReferencedIds.Contains(entry.Value))
				continue;

			if (!resolved.TryGetValue(entry.Key, out var current))
			{
				throw new InvalidOperationException(
					$"Объект исчез из {owner.Path} при переназначении: {entry.Key}");
			}

			if (current != entry.Value)
				AddRemapping(moved, entry.Value, current, owner.Path);
		}

		return moved;
	}

	private static bool IsAddressedByInnerObject(ReferenceScan scan, string guid)
	{
		return scan.ReferencedIdsByGuid.TryGetValue(guid, out var ids) &&
		       ids.Any(id => id != MainPrefabAssetFileId);
	}

	private sealed class TrackedOwner
	{
		public readonly string Path;
		public readonly string Guid;
		public readonly HashSet<long> ReferencedIds;
		public readonly HashSet<long> DanglingIds;
		public Dictionary<string, long> Baseline;

		public TrackedOwner(
			string path,
			string guid,
			Dictionary<string, long> baseline,
			HashSet<long> referencedIds,
			HashSet<long> danglingIds)
		{
			Path = path;
			Guid = guid;
			Baseline = baseline;
			ReferencedIds = referencedIds;
			DanglingIds = danglingIds;
		}
	}

	private static string Rollback(
		List<PrefabSnapshot> snapshots,
		Dictionary<string, byte[]> externalBackups)
	{
		var errors = new List<string>();
		var restoredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// Owners first, converted prefabs second: should a converted prefab have ended
		// up in both collections, its pre-conversion snapshot must win.
		foreach (var backup in externalBackups)
		{
			try
			{
				File.WriteAllBytes(GetAbsoluteAssetPath(backup.Key), backup.Value);
				restoredPaths.Add(backup.Key);
			}
			catch (Exception exception)
			{
				errors.Add(backup.Key + ": " + exception.Message);
			}
		}

		foreach (var snapshot in snapshots)
		{
			try
			{
				File.WriteAllBytes(GetAbsoluteAssetPath(snapshot.Info.Path), snapshot.OriginalBytes);
				restoredPaths.Add(snapshot.Info.Path);
			}
			catch (Exception exception)
			{
				errors.Add(snapshot.Info.Path + ": " + exception.Message);
			}
		}

		// A single Refresh instead of per-file ImportAsset: the files were written
		// behind the AssetDatabase's back, and a targeted ForceUpdate import then
		// floods the console with "Build asset version error" modification-time
		// complaints. Refresh rescans and picks up the restored bytes cleanly.
		if (restoredPaths.Count > 0)
		{
			try
			{
				AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
			}
			catch (Exception exception)
			{
				errors.Add("Refresh: " + exception.Message);
			}
		}

		return string.Join("\n", errors.Take(20));
	}

	private static void ConvertOne(string path)
	{
		var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		if (asset == null)
			throw new InvalidOperationException("Cannot load " + path);
		if (PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant)
			throw new InvalidOperationException("Not a Prefab Variant: " + path);

		var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
		if (instance == null)
			throw new InvalidOperationException("InstantiatePrefab returned null: " + path);

		try
		{
			// Unpacking the outermost root of a Variant instance peels exactly ONE
			// layer: what remains is an instance of the base prefab, and saving that
			// would write a Variant again. Peel until the root is no longer a prefab
			// instance. OutermostRoot never touches nested prefabs deeper in the
			// hierarchy, so their links survive — unlike PrefabUnpackMode.Completely.
			var guard = 0;
			while (PrefabUtility.IsAnyPrefabInstanceRoot(instance))
			{
				if (guard++ > 64)
					throw new InvalidOperationException("Cannot unpack variant chain: " + path);

				PrefabUtility.UnpackPrefabInstance(
					instance,
					PrefabUnpackMode.OutermostRoot,
					InteractionMode.AutomatedAction);
			}

			PrefabUtility.SaveAsPrefabAsset(instance, path, out var success);
			if (!success)
				throw new InvalidOperationException("SaveAsPrefabAsset failed: " + path);
		}
		finally
		{
			UnityEngine.Object.DestroyImmediate(instance);
		}

		// Fail on the very first prefab instead of at the end of a long run.
		var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		if (saved == null || PrefabUtility.GetPrefabAssetType(saved) == PrefabAssetType.Variant)
			throw new InvalidOperationException("Still a Prefab Variant after save: " + path);
	}

	private static void OpenInspectionScene()
	{
		try
		{
			_inspectionScene = EditorSceneManager.NewPreviewScene();
		}
		catch (Exception)
		{
			_inspectionScene = default;
		}
	}

	private static void CloseInspectionScene()
	{
		if (!_inspectionScene.IsValid())
			return;

		try
		{
			EditorSceneManager.ClosePreviewScene(_inspectionScene);
		}
		catch (Exception)
		{
			// Nothing to do: the scene is temporary anyway.
		}

		_inspectionScene = default;
	}

	private static GameObject InstantiateForInspection(GameObject asset)
	{
		if (_inspectionScene.IsValid())
		{
			try
			{
				var previewInstance = PrefabUtility.InstantiatePrefab(asset, _inspectionScene) as GameObject;
				if (previewInstance != null)
					return previewInstance;
			}
			catch (Exception)
			{
				// Fall through to the active scene.
			}
		}

		return PrefabUtility.InstantiatePrefab(asset) as GameObject;
	}

	private static string BuildTransformPath(Transform transform, Transform root)
	{
		if (transform == root)
			return "ROOT";

		var parts = new List<string>();
		var current = transform;
		while (current != null && current != root)
		{
			parts.Add(current.GetSiblingIndex() + ":" + current.name);
			current = current.parent;
		}

		parts.Add("ROOT");
		parts.Reverse();
		return string.Join("/", parts);
	}

	private static int GetVariantDepth(GameObject prefabAsset)
	{
		var depth = 0;
		UnityEngine.Object current = prefabAsset;
		var guard = 0;

		while (current is GameObject gameObject &&
		       PrefabUtility.GetPrefabAssetType(gameObject) == PrefabAssetType.Variant &&
		       guard++ < 64)
		{
			depth++;
			current = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
		}

		return depth;
	}

	private static string GetAbsoluteAssetPath(string assetPath)
	{
		var projectRoot = Directory.GetParent(Application.dataPath);
		if (projectRoot == null)
			throw new InvalidOperationException("Cannot resolve Unity project root");
		return Path.GetFullPath(Path.Combine(projectRoot.FullName, assetPath));
	}

	private sealed class PrefabSnapshot
	{
		public readonly VariantInfo Info;
		public readonly byte[] OriginalBytes;
		public readonly Dictionary<string, long> StableIds;

		public PrefabSnapshot(VariantInfo info, byte[] originalBytes, Dictionary<string, long> stableIds)
		{
			Info = info;
			OriginalBytes = originalBytes;
			StableIds = stableIds;
		}
	}

	private readonly struct FileIdWriteResult
	{
		public readonly int AnchorWrites;
		public readonly int LocalReferenceWrites;

		public FileIdWriteResult(int anchorWrites, int localReferenceWrites)
		{
			AnchorWrites = anchorWrites;
			LocalReferenceWrites = localReferenceWrites;
		}
	}

	private readonly struct ExternalRewriteResult
	{
		public readonly int ReferenceWrites;
		public readonly HashSet<string> ChangedPaths;

		public ExternalRewriteResult(int referenceWrites, HashSet<string> changedPaths)
		{
			ReferenceWrites = referenceWrites;
			ChangedPaths = changedPaths;
		}
	}
}
#endif
