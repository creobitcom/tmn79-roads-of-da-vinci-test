using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Creobit.LA8.EditorTools
{
    public enum RootMode
    {
        WholeProjectExceptTargets = 0, // безопасно: корни — весь проект, кроме целевых папок
        ConsumerFoldersOnly       = 1, // агрессивно: корни — только заданные папки-потребители (напр. LA8_slv)
        Custom                    = 2  // корни — произвольный список папок
    }

    [Flags]
    public enum RiskFlags
    {
        None        = 0,
        InResources = 1, // лежит в папке Resources -> может грузиться по имени через Resources.Load
        Addressable = 2, // помечен Addressable -> грузится по адресу, вне статического графа
        Scene       = 4, // .unity -> может грузиться по имени через SceneManager.LoadScene
        DataFile    = 8, // json/xml/txt/csv/bytes -> часто читается по пути (File/TextAsset), вне графа
        Texture     = 16 // png/jpg/tga/psd/atlas -> часто пакуется в SpriteAtlas и грузится по имени, вне графа
    }

    /// <summary>Одна запись ассета (кандидат на удаление / оставленный / защищённый).</summary>
    public class AssetEntry
    {
        public string Path;
        public string Guid;
        public long Size; // байты ассета + .meta
        public RiskFlags Risk;
        public string ProtectReason; // != null -> исключён из удаления (зависимости не видны guid-графу)
    }

    /// <summary>Результат прохода аудита.</summary>
    public class HolderEntry
    {
        public string Path;
        public long HeldBytes;
        public int HeldCount;
        public List<AssetEntry> Samples = new List<AssetEntry>();
    }

    public class AuditResult
    {
        public List<AssetEntry> Deletable = new List<AssetEntry>();
        public List<AssetEntry> KeptTargets = new List<AssetEntry>();
        public List<AssetEntry> Protected = new List<AssetEntry>();
        public long DeletableBytes;
        public long ProtectedBytes;
        public long TargetTotalBytes;
        public int TargetFileCount;
        public int RiskyDeletableCount;

        // guid оставленного ассета -> кто на него ссылается (только среди оставленных)
        public Dictionary<string, HashSet<string>> KeptReferrers = new Dictionary<string, HashSet<string>>();
        // путь оставленного ассета -> его «родитель» по дереву BFS (шаг ближе к корню). Корни отсутствуют в словаре.
        public Dictionary<string, string> KeepParent = new Dictionary<string, string>();
        // путь оставленного ассета -> вычисленный держатель (заполняется BuildLegacyHolders)
        public Dictionary<string, string> HolderByPath = new Dictionary<string, string>();
        // путь удаляемого ассета -> кто из ДРУГИХ удаляемых на него ссылается (для проверки частичного выбора)
        public Dictionary<string, HashSet<string>> DeletableReverse = new Dictionary<string, HashSet<string>>();

        public DateTime WhenLocal;
    }

    [Serializable]
    public class AuditSettings
    {
        public List<string> TargetFolders = new List<string> { "Assets/GnomesGarden10", "Assets/LA7" };
        public int RootModeInt = (int)RootMode.WholeProjectExceptTargets;
        public List<string> RootFolders = new List<string> { "Assets/LA8_slv" };
        public bool IncludeBuildScenes = true;
        public bool IncludeAddressables = true;
        public bool IncludeProjectSettings = true; // Graphics/Quality/Preloaded ссылаются на ассеты извне Assets/
        public bool IncludePackages = true;        // Packages/ и Library/PackageCache/ (модули) ссылаются на арт проекта
        public bool KeepFolderContents = true; // если оставленный ассет ссылается на папку — оставить всё её содержимое
        public bool ProtectCode = true;        // не удалять код/шейдеры/инпуты — их зависимости не видны guid-графу

        public RootMode RootMode => (RootMode)RootModeInt;
    }

    /// <summary>
    /// Строит guid-граф зависимостей проекта (проект в ForceText), считает достижимость от «корней»
    /// и определяет, какие ассеты целевых папок никем из билда не используются.
    /// Индекс метаданных кэшируется, поэтому смена режима корней — это только повторный BFS (быстро).
    /// </summary>
    public class AssetAuditorEngine
    {
        static readonly Regex GuidRe = new Regex("(?:guid|m_GUID): ([0-9a-f]{32})", RegexOptions.Compiled);
        static readonly Regex SelfGuidRe = new Regex("^guid: ([0-9a-f]{32})", RegexOptions.Compiled | RegexOptions.Multiline);

        // Типы, чьи реальные зависимости НЕ видны guid-графу -> никогда не удаляем автоматически.
        // Код: зависит через using/имена классов (компиляция), а не через guid.
        static readonly HashSet<string> CodeExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",".js",".boo",".dll",".so",".dylib",".a",".asmdef",".asmref",".rsp",".jslib",
            ".cpp",".cc",".c",".h",".hpp",".mm",".m",".java",".kt",".swift",".metal",".winmd"
        };
        // Шейдеры/compute: часто ищутся по имени через Shader.Find("...").
        static readonly HashSet<string> ShaderExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".shader",".shadergraph",".shadersubgraph",".compute",".hlsl",".cginc",".glslinc",".hlslinc",".raytrace"
        };
        // Input System: из .inputactions генерируется C#-обёртка, на которую ссылается код.
        static readonly HashSet<string> InputExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".inputactions" };

        // Data-файлы: часто читаются по пути (File.ReadAllText / TextAsset), а не через guid -> помечаем риском.
        static readonly HashSet<string> DataExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".json",".xml",".txt",".csv",".tsv",".yaml",".yml",".ini",".cfg",".bytes"
        };

        // Текстуры/спрайты: часто пакуются в SpriteAtlas и грузятся ПО ИМЕНИ из кода/атласа -> граф не видит.
        static readonly HashSet<string> TextureExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png",".jpg",".jpeg",".tga",".psd",".psb",".exr",".gif",".bmp",".tif",".tiff",".webp",".spriteatlas",".spriteatlasv2"
        };

        // Типы, несущие ссылки (для скана внешних корней вне Assets/: ProjectSettings, Packages, PackageCache).
        static readonly HashSet<string> RefBearingExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".asset",".prefab",".unity",".mat",".controller",".anim",".overridecontroller",".spriteatlas",".spriteatlasv2",
            ".playable",".mask",".preset",".guiskin",".physicmaterial",".physicsmaterial2d",".terrainlayer",".lighting",
            ".rendertexture",".mixer",".signal",".shadervariants"
        };

        // Бинарные/медиа-типы: их тело не содержит ссылок, читаем только .meta (экономия гигабайтов чтения).
        static readonly HashSet<string> BinaryMediaExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png",".jpg",".jpeg",".tga",".psd",".psb",".exr",".hdr",".gif",".bmp",".tif",".tiff",".webp",".svg",
            ".wav",".mp3",".ogg",".aif",".aiff",".flac",".m4a",".aac",
            ".fbx",".obj",".blend",".dae",".3ds",".max",".ma",".mb",".stl",".ply",
            ".ttf",".otf",".ttc",
            ".mp4",".mov",".webm",".avi",".mkv",
            ".dll",".so",".a",".dylib",".pdb",".mdb",
            ".bytes",".pdf",".zip",".rar",".7z",".gz",".unitypackage",".aar",".jar",".bank",".cubemap",".exe",".bin",".dat",
            ".spineatlas",".atlas",".skel"
        };

        string _projectRoot;
        Dictionary<string, string> _guidToPath;   // self guid -> asset path
        Dictionary<string, string> _pathToGuid;   // asset path -> self guid
        Dictionary<string, string[]> _metaOut;    // asset path -> guids из .meta (без self)
        Dictionary<string, string[]> _bodyCache;  // asset path -> guids из тела (лениво)
        HashSet<string> _allFiles;                // все НЕ-папочные ассеты под Assets/
        List<string> _allFilesSorted;             // отсортированный список для поиска детей папки по префиксу
        HashSet<string> _folders;                 // папки-ассеты
        HashSet<string> _addressableGuids;        // guid'ы, помеченные Addressable

        public bool IsIndexed { get; private set; }
        public int IndexedCount => _allFiles?.Count ?? 0;

        // ---------------------------------------------------------------- Индекс

        /// <summary>Читает .meta всех ассетов и строит карты guid<->path. Тела читаются лениво в BFS.</summary>
        public bool BuildIndex(Func<float, string, bool> progress)
        {
            _projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var all = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)).ToArray();

            _guidToPath = new Dictionary<string, string>(all.Length);
            _pathToGuid = new Dictionary<string, string>(all.Length);
            _metaOut = new Dictionary<string, string[]>(all.Length);
            _bodyCache = new Dictionary<string, string[]>();
            _allFiles = new HashSet<string>();
            _folders = new HashSet<string>();

            for (int i = 0; i < all.Length; i++)
            {
                var p = all[i];
                if ((i & 511) == 0 && progress != null &&
                    progress(i / (float)all.Length, $"Индексация метаданных… {i}/{all.Length}"))
                {
                    IsIndexed = false;
                    return false;
                }

                bool isFolder = AssetDatabase.IsValidFolder(p);
                if (isFolder) _folders.Add(p); else _allFiles.Add(p);

                string metaAbs = AbsPath(p) + ".meta";
                string self = null;
                string[] outg = Array.Empty<string>();
                if (File.Exists(metaAbs))
                {
                    string mt;
                    try { mt = File.ReadAllText(metaAbs); }
                    catch { mt = null; }
                    if (mt != null)
                    {
                        var sm = SelfGuidRe.Match(mt);
                        if (sm.Success) self = sm.Groups[1].Value;
                        var outs = new HashSet<string>();
                        foreach (Match m in GuidRe.Matches(mt))
                        {
                            var g = m.Groups[1].Value;
                            if (g != self) outs.Add(g);
                        }
                        outg = outs.Count > 0 ? outs.ToArray() : Array.Empty<string>();
                    }
                }

                if (self != null)
                {
                    _guidToPath[self] = p;
                    _pathToGuid[p] = self;
                }
                _metaOut[p] = outg;
            }

            _allFilesSorted = _allFiles.ToList();
            _allFilesSorted.Sort(StringComparer.Ordinal);
            _addressableGuids = CollectAddressableGuids();

            IsIndexed = true;
            return true;
        }

        // ---------------------------------------------------------------- Вычисление

        public AuditResult Compute(AuditSettings s, Func<float, string, bool> progress)
        {
            if (!IsIndexed) throw new InvalidOperationException("Index not built");

            var targets = NormalizeFolders(s.TargetFolders);
            Func<string, bool> isTarget = p =>
            {
                foreach (var t in targets)
                    if (p == t || p.StartsWith(t + "/", StringComparison.Ordinal)) return true;
                return false;
            };

            // --- Корни
            var roots = new HashSet<string>();
            switch (s.RootMode)
            {
                case RootMode.WholeProjectExceptTargets:
                    foreach (var p in _allFiles) if (!isTarget(p)) roots.Add(p);
                    break;
                default: // ConsumerFoldersOnly / Custom — оба работают от списка RootFolders
                    var rf = NormalizeFolders(s.RootFolders);
                    foreach (var p in _allFiles)
                        if (!isTarget(p) && rf.Any(t => p == t || p.StartsWith(t + "/", StringComparison.Ordinal)))
                            roots.Add(p);
                    if (s.IncludeBuildScenes)
                        foreach (var sc in EnabledBuildScenes()) if (!isTarget(sc)) roots.Add(sc);
                    break;
            }

            if (s.IncludeAddressables && _addressableGuids != null)
                foreach (var g in _addressableGuids)
                    if (_guidToPath.TryGetValue(g, out var ap) && !isTarget(ap)) roots.Add(ap);

            // Контент ВНЕ Assets/ (ProjectSettings + Packages + PackageCache) ссылается на реальные ассеты
            // (активный URP RenderPipeline Asset; UI-арт, на который смотрят модули типа creobit.timemanagement).
            // Держим их корнями всегда — даже если ассет внутри целевой папки (иначе снесём нужное).
            if (s.IncludeProjectSettings || s.IncludePackages)
                foreach (var g in ExternalRootGuids(s))
                    if (_guidToPath.TryGetValue(g, out var pp)) roots.Add(pp);

            // --- BFS достижимости
            var keep = new HashSet<string>();
            var keptRef = new Dictionary<string, HashSet<string>>();
            var parent = new Dictionary<string, string>();
            var queue = new Queue<string>();
            foreach (var r in roots) if (keep.Add(r)) queue.Enqueue(r); // корни без родителя -> цепочка на них обрывается

            int processed = 0;
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                processed++;
                if ((processed & 1023) == 0 && progress != null)
                    progress(0.5f, $"Обход графа зависимостей… оставлено {keep.Count}");

                if (s.KeepFolderContents && _folders.Contains(p))
                    EnqueueFolderChildren(p, keep, queue, parent);

                foreach (var g in OutGuids(p))
                {
                    if (!_guidToPath.TryGetValue(g, out var q)) continue;
                    if (!keptRef.TryGetValue(g, out var set)) { set = new HashSet<string>(); keptRef[g] = set; }
                    set.Add(p);
                    if (keep.Add(q)) { parent[q] = p; queue.Enqueue(q); }
                }
            }

            // --- Разбор целевых папок
            var result = new AuditResult { KeptReferrers = keptRef, KeepParent = parent, WhenLocal = DateTime.Now };
            var targetFiles = _allFiles.Where(isTarget).ToList();
            result.TargetFileCount = targetFiles.Count;

            var deletablePaths = new List<string>();
            foreach (var p in targetFiles)
            {
                long size = FileSize(p);
                result.TargetTotalBytes += size;
                var entry = new AssetEntry
                {
                    Path = p,
                    Guid = _pathToGuid.TryGetValue(p, out var g) ? g : null,
                    Size = size,
                    Risk = RiskOf(p),
                    ProtectReason = ProtectReasonOf(p, s)
                };
                if (entry.ProtectReason != null)
                {
                    result.Protected.Add(entry);
                    result.ProtectedBytes += size;
                }
                else if (keep.Contains(p))
                {
                    result.KeptTargets.Add(entry);
                }
                else
                {
                    result.Deletable.Add(entry);
                    result.DeletableBytes += size;
                    if (entry.Risk != RiskFlags.None) result.RiskyDeletableCount++;
                    deletablePaths.Add(p);
                }
            }

            // --- Обратные рёбра среди удаляемых (для проверки частичного выбора)
            var delSet = new HashSet<string>(deletablePaths);
            foreach (var p in deletablePaths)
                foreach (var g in OutGuids(p))
                    if (_guidToPath.TryGetValue(g, out var q) && q != p && delSet.Contains(q))
                    {
                        if (!result.DeletableReverse.TryGetValue(q, out var set)) { set = new HashSet<string>(); result.DeletableReverse[q] = set; }
                        set.Add(p);
                    }

            result.Deletable.Sort((a, b) => b.Size.CompareTo(a.Size));
            result.KeptTargets.Sort((a, b) => b.Size.CompareTo(a.Size));
            result.Protected.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return result;
        }

        // ---------------------------------------------------------------- Кто держит легаси

        public const string OnlyAddressableHolder = "◆ ТОЛЬКО запись в Addressables — из проекта не ссылается никто";

        static bool IsAddressableGroupAsset(string path)
        {
            return path.StartsWith("Assets/Settings/AddressableAssetsData/", StringComparison.Ordinal)
                   || path.StartsWith("Assets/AddressableAssetsData/", StringComparison.Ordinal);
        }

        public List<HolderEntry> BuildLegacyHolders(AuditResult r, AuditSettings s)
        {
            var targets = NormalizeFolders(s.TargetFolders);
            Func<string, bool> isTarget = p =>
            {
                foreach (var t in targets)
                    if (p == t || p.StartsWith(t + "/", StringComparison.Ordinal)) return true;
                return false;
            };

            var byHolder = new Dictionary<string, HolderEntry>();

            foreach (var entry in r.KeptTargets)
            {
                var realReferrers = new List<string>();
                if (entry.Guid != null && r.KeptReferrers.TryGetValue(entry.Guid, out var referrers))
                    realReferrers = referrers.Where(x => !IsAddressableGroupAsset(x)).ToList();

                string holder;
                if (realReferrers.Count == 0)
                {
                    holder = OnlyAddressableHolder;
                }
                else
                {
                    var outside = realReferrers.FirstOrDefault(x => !isTarget(x));
                    if (outside != null)
                    {
                        holder = outside;
                    }
                    else
                    {
                        var current = entry.Path;
                        holder = null;
                        var guard = 0;
                        while (r.KeepParent.TryGetValue(current, out var parent) && guard++ < 4096)
                        {
                            if (!isTarget(parent) && !IsAddressableGroupAsset(parent))
                            {
                                holder = parent;
                                break;
                            }

                            current = parent;
                        }
                    }
                }

                var key = holder ?? "(корень внутри целевой папки)";
                r.HolderByPath[entry.Path] = key;
                if (!byHolder.TryGetValue(key, out var acc))
                {
                    acc = new HolderEntry { Path = key };
                    byHolder[key] = acc;
                }

                acc.HeldBytes += entry.Size;
                acc.HeldCount++;
                if (acc.Samples.Count < 12) acc.Samples.Add(entry);
            }

            var list = byHolder.Values.ToList();
            list.Sort((a, b) => b.HeldBytes.CompareTo(a.HeldBytes));
            foreach (var h in list) h.Samples.Sort((a, b) => b.Size.CompareTo(a.Size));

            return list;
        }

        // ---------------------------------------------------------------- Удаление / проверка

        /// <summary>
        /// Для выбранного набора возвращает записи, которые ещё на что-то нужны СНАРУЖИ выбора
        /// (ссылаются оставшиеся, не выбранные к удалению ассеты) — т.е. создадут missing reference.
        /// </summary>
        public List<KeyValuePair<string, List<string>>> FindDanglingAfterDelete(AuditResult r, HashSet<string> selected)
        {
            var res = new List<KeyValuePair<string, List<string>>>();
            foreach (var sel in selected)
            {
                if (!r.DeletableReverse.TryGetValue(sel, out var referrers)) continue;
                var survivors = referrers.Where(x => !selected.Contains(x)).ToList();
                if (survivors.Count > 0) res.Add(new KeyValuePair<string, List<string>>(sel, survivors));
            }
            return res;
        }

        public (int deleted, int failed, long bytes, List<string> fails) DeleteToTrash(IEnumerable<string> paths)
        {
            var list = paths.ToList();
            long bytes = 0;
            foreach (var p in list) bytes += FileSize(p); // размер считаем ДО удаления (после — файлов уже нет)

            var failList = new List<string>();
            // Батч: одна операция вместо тысяч вызовов -> в Корзину (восстановимо), кратно быстрее.
            AssetDatabase.MoveAssetsToTrash(list.ToArray(), failList);
            AssetDatabase.Refresh();

            foreach (var fp in failList) bytes -= FileSize(fp); // неудавшиеся ещё на диске -> вычесть их размер
            return (list.Count - failList.Count, failList.Count, bytes, failList);
        }

        /// <summary>Удаляет ставшие пустыми папки внутри целевых директорий (снизу вверх).</summary>
        public int DeleteEmptyFolders(IEnumerable<string> targetFolders)
        {
            int removed = 0;
            var folders = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(p))
                .Where(p => targetFolders.Any(t => p == t || p.StartsWith(t + "/", StringComparison.Ordinal)))
                .OrderByDescending(p => p.Length) // сначала самые глубокие
                .ToList();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var f in folders)
                {
                    string abs = AbsPath(f);
                    if (!Directory.Exists(abs)) continue;
                    bool empty = !Directory.EnumerateFileSystemEntries(abs)
                        .Any(e => !e.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
                    if (empty && AssetDatabase.MoveAssetToTrash(f)) removed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); AssetDatabase.Refresh(); }
            return removed;
        }

        public void ExportCsv(AuditResult r, string absFile)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("status;path;folder;size_bytes;size_mb;risk;guid");
            void Row(AssetEntry e, string status)
            {
                string folder = e.Path.Contains('/') ? e.Path.Substring(0, e.Path.LastIndexOf('/')) : "";
                sb.Append(status).Append(';')
                  .Append(Csv(e.Path)).Append(';')
                  .Append(Csv(folder)).Append(';')
                  .Append(e.Size).Append(';')
                  .Append((e.Size / 1048576.0).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(';')
                  .Append(Csv(e.ProtectReason ?? RiskLabel(e.Risk))).Append(';')
                  .Append(e.Guid).AppendLine();
            }
            foreach (var e in r.Deletable) Row(e, "DELETE");
            foreach (var e in r.Protected) Row(e, "PROTECT");
            foreach (var e in r.KeptTargets) Row(e, "KEEP");
            File.WriteAllText(absFile, sb.ToString(), new System.Text.UTF8Encoding(true));
        }

        public HashSet<string> Referrers(AuditResult r, AssetEntry e)
        {
            if (e.Guid != null && r.KeptReferrers.TryGetValue(e.Guid, out var s)) return s;
            return null;
        }

        /// <summary>Путь от ассета к корню по дереву BFS: [ассет, родитель, …, корень]. Последний элемент — корень.</summary>
        public List<string> ChainToRoot(AuditResult r, string path)
        {
            var chain = new List<string>();
            var seen = new HashSet<string>();
            var cur = path;
            while (cur != null && seen.Add(cur))
            {
                chain.Add(cur);
                if (!r.KeepParent.TryGetValue(cur, out cur)) break; // корень (нет родителя)
            }
            return chain;
        }

        /// <summary>Лежит ли путь внутри одной из указанных папок (или совпадает с ней).</summary>
        public static bool IsUnder(string path, IEnumerable<string> folders)
        {
            foreach (var f in folders)
            {
                var t = (f ?? "").Replace('\\', '/').TrimEnd('/');
                if (t.Length == 0) continue;
                if (path == t || path.StartsWith(t + "/", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- Внутреннее

        string[] OutGuids(string p)
        {
            var meta = _metaOut.TryGetValue(p, out var mo) ? mo : Array.Empty<string>();
            var body = BodyGuids(p);
            if (body.Length == 0) return meta;
            if (meta.Length == 0) return body;
            var set = new HashSet<string>(meta);
            set.UnionWith(body);
            return set.ToArray();
        }

        string[] BodyGuids(string p)
        {
            if (_bodyCache.TryGetValue(p, out var c)) return c;
            string[] res = Array.Empty<string>();
            string ext = Path.GetExtension(p);
            if (!BinaryMediaExt.Contains(ext))
            {
                string abs = AbsPath(p);
                try
                {
                    var fi = new FileInfo(abs);
                    if (fi.Exists && fi.Length < 96L * 1024 * 1024)
                    {
                        string txt = File.ReadAllText(abs);
                        string self = _pathToGuid.TryGetValue(p, out var sg) ? sg : null;
                        var outs = new HashSet<string>();
                        foreach (Match m in GuidRe.Matches(txt))
                        {
                            var g = m.Groups[1].Value;
                            if (g != self) outs.Add(g);
                        }
                        if (outs.Count > 0) res = outs.ToArray();
                    }
                }
                catch { /* нечитаемый файл — считаем без исходящих ссылок */ }
            }
            _bodyCache[p] = res;
            return res;
        }

        void EnqueueFolderChildren(string folder, HashSet<string> keep, Queue<string> queue, Dictionary<string, string> parent)
        {
            string prefix = folder + "/";
            int lo = LowerBound(_allFilesSorted, prefix);
            for (int i = lo; i < _allFilesSorted.Count; i++)
            {
                var f = _allFilesSorted[i];
                if (!f.StartsWith(prefix, StringComparison.Ordinal)) break;
                if (keep.Add(f)) { parent[f] = folder; queue.Enqueue(f); }
            }
        }

        static int LowerBound(List<string> sorted, string key)
        {
            int lo = 0, hi = sorted.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (string.CompareOrdinal(sorted[mid], key) < 0) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        HashSet<string> CollectAddressableGuids()
        {
            var set = new HashSet<string>();
            var dirs = new[] { "Assets/AddressableAssetsData", "Assets/Settings/AddressableAssetsData" };
            var re = new Regex("(?:guid|m_GUID): ([0-9a-f]{32})", RegexOptions.Compiled);
            foreach (var dir in dirs)
            {
                string abs = AbsPath(dir);
                if (!Directory.Exists(abs)) continue;
                foreach (var f in Directory.EnumerateFiles(abs, "*.asset", SearchOption.AllDirectories))
                {
                    try
                    {
                        foreach (Match m in re.Matches(File.ReadAllText(f))) set.Add(m.Groups[1].Value);
                    }
                    catch { }
                }
            }
            return set;
        }

        /// <summary>
        /// Guid'ы, на которые ссылается контент ВНЕ Assets/: ProjectSettings (Graphics/Quality/Preloaded),
        /// а также Packages/ и Library/PackageCache/ (модули типа creobit.timemanagement ссылаются на арт проекта).
        /// Читаем только ref-несущие типы, чтобы не молотить весь кэш пакетов.
        /// </summary>
        IEnumerable<string> ExternalRootGuids(AuditSettings s)
        {
            var dirs = new List<string>();
            if (s.IncludeProjectSettings) dirs.Add(Path.Combine(_projectRoot, "ProjectSettings"));
            if (s.IncludePackages)
            {
                dirs.Add(Path.Combine(_projectRoot, "Packages"));
                dirs.Add(Path.Combine(_projectRoot, "Library", "PackageCache"));
            }

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories); }
                catch { continue; }
                foreach (var f in files)
                {
                    if (!RefBearingExt.Contains(Path.GetExtension(f))) continue;
                    string txt;
                    try
                    {
                        if (new FileInfo(f).Length > 64L * 1024 * 1024) continue;
                        txt = File.ReadAllText(f);
                    }
                    catch { continue; }
                    foreach (Match m in GuidRe.Matches(txt)) yield return m.Groups[1].Value;
                }
            }
        }

        static IEnumerable<string> EnabledBuildScenes()
        {
            foreach (var sc in EditorBuildSettings.scenes)
                if (sc.enabled && !string.IsNullOrEmpty(sc.path)) yield return sc.path;
        }

        static string ProtectReasonOf(string p, AuditSettings s)
        {
            if (!s.ProtectCode) return null;
            var ext = Path.GetExtension(p);
            if (CodeExt.Contains(ext)) return "код";
            if (ShaderExt.Contains(ext)) return "шейдер (Shader.Find по имени)";
            if (InputExt.Contains(ext)) return "Input Actions (генерирует код)";
            return null;
        }

        RiskFlags RiskOf(string p)
        {
            RiskFlags r = RiskFlags.None;
            foreach (var seg in p.Split('/'))
                if (seg.Equals("resources", StringComparison.OrdinalIgnoreCase)) { r |= RiskFlags.InResources; break; }
            if (_addressableGuids != null && _pathToGuid.TryGetValue(p, out var g) && _addressableGuids.Contains(g))
                r |= RiskFlags.Addressable;
            var ext2 = Path.GetExtension(p);
            if (ext2.Equals(".unity", StringComparison.OrdinalIgnoreCase))
                r |= RiskFlags.Scene;
            if (DataExt.Contains(ext2))
                r |= RiskFlags.DataFile;
            if (TextureExt.Contains(ext2))
                r |= RiskFlags.Texture;
            return r;
        }

        long FileSize(string p)
        {
            long s = 0;
            string a = AbsPath(p);
            try
            {
                var fi = new FileInfo(a); if (fi.Exists) s += fi.Length;
                var fm = new FileInfo(a + ".meta"); if (fm.Exists) s += fm.Length;
            }
            catch { }
            return s;
        }

        string AbsPath(string assetPath) =>
            Path.Combine(_projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));

        static List<string> NormalizeFolders(List<string> folders)
        {
            var res = new List<string>();
            if (folders == null) return res;
            foreach (var f in folders)
            {
                if (string.IsNullOrWhiteSpace(f)) continue;
                var t = f.Replace('\\', '/').TrimEnd('/');
                if (t.Length > 0 && !res.Contains(t)) res.Add(t);
            }
            return res;
        }

        static string Csv(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            if (v.Contains(';') || v.Contains('"') || v.Contains('\n'))
                return '"' + v.Replace("\"", "\"\"") + '"';
            return v;
        }

        public static string RiskLabel(RiskFlags r)
        {
            if (r == RiskFlags.None) return "";
            var parts = new List<string>();
            if ((r & RiskFlags.InResources) != 0) parts.Add("Resources");
            if ((r & RiskFlags.Addressable) != 0) parts.Add("Addressable");
            if ((r & RiskFlags.Scene) != 0) parts.Add("Scene");
            if ((r & RiskFlags.DataFile) != 0) parts.Add("Data");
            if ((r & RiskFlags.Texture) != 0) parts.Add("Texture");
            return string.Join("+", parts);
        }

        public static string HumanSize(long bytes)
        {
            double b = bytes;
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
            return b.ToString(i == 0 ? "F0" : "F2", System.Globalization.CultureInfo.InvariantCulture) + " " + u[i];
        }
    }
}
