using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data
{
    /// <summary>
    /// Одна видео-катсцена: файл, условие показа, правила пропуска и субтитры.
    /// Всё, что настраивает геймдиз, лежит здесь — больше трогать ничего не нужно,
    /// кроме кнопки "Пересобрать" в CutsceneLibrarySO.
    /// </summary>
    [CreateAssetMenu(fileName = "Cutscene", menuName = "8floor/TimeManager/Video Cutscene")]
    public class VideoCutsceneSO : ScriptableObject
    {
        /// <summary>Папка с роликами внутри StreamingAssets.</summary>
        public const string VideoFolder = "Cutscenes";

        /// <summary>Префикс ключа сейва. Флаги просмотра лежат в общем списке PassedComics.</summary>
        public const string SaveKeyPrefix = "video:";

        // Рисуется вручную в DrawVideoPicker: нативным попапом Unity, а не селектором Odin —
        // всплывающие окна Odin падают с MissingMethodException на Unity 6000.0.x.
        [HideInInspector]
        [SerializeField]
        private string _videoFile;

        [BoxGroup("Когда показывать")]
        [LabelText("Условие")]
        [SerializeField]
        private VideoCutsceneTrigger _trigger = VideoCutsceneTrigger.Manual;

        [BoxGroup("Когда показывать")]
        [LabelText("Уровень")]
        [ShowIf("@_trigger == VideoCutsceneTrigger.BeforeLevel || _trigger == VideoCutsceneTrigger.AfterLevel")]
        [SerializeField]
        private int _level;

        [BoxGroup("Когда показывать")]
        [LabelText("Показывать один раз")]
        [Tooltip("Включено — ролик играет один раз на профиль. Выключено — каждый раз, когда условие выполнено.")]
        [SerializeField]
        private bool _playOnce = true;

        [BoxGroup("Когда показывать")]
        [LabelText("После ролика показать комикс")]
        [Tooltip("Сразу после видео покажется готовый комикс из ComicsBridge, если он не пройден. " +
                 "Выключено — после ролика сразу продолжается игра.")]
        [SerializeField]
        private bool _showComicsAfter;

        [BoxGroup("Проигрывание")]
        [LabelText("Можно пропустить")]
        [SerializeField]
        private bool _canSkip = true;

        [BoxGroup("Проигрывание")]
        [LabelText("Кнопка пропуска через, сек")]
        [ShowIf(nameof(_canSkip))]
        [MinValue(0f)]
        [SerializeField]
        private float _skipButtonDelay = 2f;

        [BoxGroup("Проигрывание")]
        [LabelText("Глушить музыку меню")]
        [SerializeField]
        private bool _pauseMusic = true;

        [BoxGroup("Проигрывание")]
        [LabelText("Громкость ролика")]
        [Range(0f, 1f)]
        [SerializeField]
        private float _volume = 1f;

        [BoxGroup("Субтитры")]
        [InfoBox("Тексты берутся из локализации по ключу (GameText_*.json), как весь остальной текст. " +
                 "Тайминги расставляются в Tools/Cutscenes/Тайминг субтитров.")]
        [LabelText("Строки")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        [SerializeField]
        private List<SubtitleLine> _subtitles = new();

        public string VideoFile => _videoFile;
        public VideoCutsceneTrigger Trigger => _trigger;
        public int Level => _level;
        public bool PlayOnce => _playOnce;
        public bool ShowComicsAfter => _showComicsAfter;
        public bool CanSkip => _canSkip;
        public float SkipButtonDelay => _skipButtonDelay;
        public bool PauseMusic => _pauseMusic;
        public float Volume => _volume;
        public List<SubtitleLine> Subtitles => _subtitles;

        /// <summary>Идентификатор катсцены = имя ассета.</summary>
        public string Id => name;

        /// <summary>Ключ, под которым просмотр пишется в сейв профиля.</summary>
        public string SaveKey => SaveKeyPrefix + name;

        /// <summary>Абсолютный путь к ролику для VideoPlayer.url.</summary>
        public string GetVideoUrl()
        {
            if (string.IsNullOrEmpty(_videoFile))
            {
                return string.Empty;
            }

            return Path.Combine(Application.streamingAssetsPath, VideoFolder, _videoFile);
        }

        /// <summary>
        /// Проверка настроек. Возвращает список проблем, пустой список — всё хорошо.
        /// Используется кнопкой "Проверить" и окном тайминга.
        /// </summary>
        public List<string> Validate(float videoLength = 0f)
        {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(_videoFile))
            {
                issues.Add("Не выбран файл ролика.");
            }

            for (int i = 0; i < _subtitles.Count; i++)
            {
                var line = _subtitles[i];

                if (line == null)
                {
                    issues.Add($"Строка {i + 1}: пустая.");
                    continue;
                }

                if (string.IsNullOrEmpty(line.LocKey))
                {
                    issues.Add($"Строка {i + 1}: не указан ключ локализации.");
                }

                if (line.End <= line.Start)
                {
                    issues.Add($"Строка {i + 1}: конец ({line.End:0.00}) не позже начала ({line.Start:0.00}).");
                }

                if (videoLength > 0f && line.End > videoLength + 0.01f)
                {
                    issues.Add($"Строка {i + 1}: выходит за длину ролика ({videoLength:0.00} сек).");
                }

                if (i > 0 && _subtitles[i - 1] != null && line.Start < _subtitles[i - 1].End - 0.01f)
                {
                    issues.Add($"Строка {i + 1}: пересекается с предыдущей.");
                }
            }

            return issues;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Выбор файла ролика. Намеренно нативный EditorGUILayout.Popup:
        /// селекторы Odin открывают своё окно, которое падает на Unity 6000.0.x.
        /// Слэши в путях Unity сама разложит в подменю по папкам.
        /// </summary>
        [BoxGroup("Видео")]
        [PropertyOrder(-1)]
        [OnInspectorGUI]
        private void DrawVideoPicker()
        {
            var files = new List<string>(GetVideoFiles());
            var options = new string[files.Count];

            for (int i = 0; i < files.Count; i++)
            {
                options[i] = string.IsNullOrEmpty(files[i]) ? "— не выбрано —" : files[i];
            }

            var index = files.IndexOf(_videoFile ?? string.Empty);
            var missing = index < 0;

            if (missing)
            {
                // Файл прописан, но его нет на диске — не теряем значение молча.
                var extended = new List<string>(options) { $"{_videoFile} (файл не найден)" };
                files.Add(_videoFile);
                options = extended.ToArray();
                index = files.Count - 1;
            }

            UnityEditor.EditorGUI.BeginChangeCheck();

            var newIndex = UnityEditor.EditorGUILayout.Popup("Файл", index, options);

            if (UnityEditor.EditorGUI.EndChangeCheck() && newIndex != index)
            {
                UnityEditor.Undo.RecordObject(this, "Файл катсцены");
                _videoFile = files[newIndex];
                UnityEditor.EditorUtility.SetDirty(this);
            }

            if (missing)
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    $"Файл не найден: StreamingAssets/{VideoFolder}/{_videoFile}",
                    UnityEditor.MessageType.Error);
            }
            else if (files.Count <= 1)
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    $"В StreamingAssets/{VideoFolder} нет ни одного ролика (mp4/webm/mov/m4v).",
                    UnityEditor.MessageType.Warning);
            }
        }

        [BoxGroup("Субтитры")]
        [Button("Проверить", ButtonSizes.Medium)]
        private void ValidateFromInspector()
        {
            var issues = Validate();

            if (issues.Count == 0)
            {
                Debug.Log($"[{name}] Настройки в порядке. Строк субтитров: {_subtitles.Count}.", this);
                return;
            }

            Debug.LogWarning($"[{name}] Найдено проблем: {issues.Count}\n" + string.Join("\n", issues), this);
        }

        [BoxGroup("Субтитры")]
        [Button("Тайминг субтитров", ButtonSizes.Medium)]
        private void OpenTimingWindow()
        {
            UnityEditor.Selection.activeObject = this;
            UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Cutscenes/Тайминг субтитров");
        }

        private static List<string> _videoFilesCache;
        private static double _videoFilesCacheTime;

        /// <summary>
        /// Список роликов в StreamingAssets/Cutscenes для выпадашки.
        /// Результат кэшируется: метод зовётся на каждую перерисовку инспектора,
        /// а рекурсивный обход папки на каждый кадр — заметная лишняя работа.
        /// </summary>
        private static IEnumerable<string> GetVideoFiles()
        {
            var now = UnityEditor.EditorApplication.timeSinceStartup;

            if (_videoFilesCache != null && now - _videoFilesCacheTime < 3d)
            {
                return _videoFilesCache;
            }

            var result = new List<string> { string.Empty };
            var root = Path.Combine(Application.streamingAssetsPath, VideoFolder);

            if (!Directory.Exists(root))
            {
                _videoFilesCache = result;
                _videoFilesCacheTime = now;

                return result;
            }

            foreach (var file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();

                if (extension != ".mp4" && extension != ".webm" && extension != ".mov" && extension != ".m4v")
                {
                    continue;
                }

                result.Add(file.Substring(root.Length + 1).Replace('\\', '/'));
            }

            return result;
        }
#endif
    }
}
