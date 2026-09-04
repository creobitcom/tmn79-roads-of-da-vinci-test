#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace _8floor.TimeManagement.Core.Scripts.Editor.VideoCutscenes
{
    /// <summary>
    /// Монтажка субтитров: превью ролика на всю высоту, транспорт с паузой и покадровым шагом,
    /// таймлайн с блоками реплик, горячие клавиши, выбор ключа поиском по фразам
    /// и правка текстов локализации прямо в окне.
    /// </summary>
    public class VideoCutsceneTimingWindow : EditorWindow
    {
        private const string LocalizationGame = "RoadsOfDaVinci";
        private const string DefaultLanguage = "ru-RU";
        private const string PreviewHeightPref = "VideoCutscene.PreviewHeight";
        private const string PreviewObjectName = "VideoCutscenePreview";
        private const string PreviewVolumePref = "VideoCutscene.PreviewVolume";
        private const string PreviewMutePref = "VideoCutscene.PreviewMute";

        private static readonly Color TimelineBackground = new(0.16f, 0.16f, 0.16f);
        private static readonly Color LineColor = new(0.25f, 0.5f, 0.85f);
        private static readonly Color SelectedLineColor = new(0.95f, 0.7f, 0.2f);
        private static readonly Color PlayheadColor = new(1f, 1f, 1f, 0.9f);
        private static readonly Color SplitterColor = new(0.1f, 0.1f, 0.1f);

        private VideoCutsceneSO _target;
        private string _keyPrefix = string.Empty;
        private Vector2 _scroll;
        private int _selected = -1;
        private bool _scrubbing;

        private const float HandleWidth = 7f;

        private enum DragMode
        {
            None,
            Start,
            End,
            Move
        }

        private int _dragIndex = -1;
        private DragMode _dragMode = DragMode.None;
        private double _dragTimeOffset;

        private float _previewHeight = 320f;
        private bool _resizingPreview;

        private GUIStyle _overlayLabel;
        private CutsceneLibrarySO _library;
        private readonly SubtitleStyle _fallbackStyle = new();
        private bool _styleFoldout = true;

        private enum PlateDrag
        {
            None,
            Move,
            Height
        }

        private PlateDrag _plateDrag = PlateDrag.None;

        private bool _primed;
        private int _primeFrames;
        private int _pendingFrames;

        // Громкость предпросмотра: живёт только в редакторе, в ассет и в игру не попадает.
        private float _previewVolume = 0.7f;
        private bool _previewMuted;

        private GameObject _previewObject;
        private VideoPlayer _preview;
        private RenderTexture _previewTexture;
        private string _preparedUrl;
        private double _prepareStartedAt;
        private string _previewError;

        private string _language = DefaultLanguage;
        private readonly List<string> _languages = new();
        private readonly Dictionary<string, Dictionary<string, string>> _byLanguage = new();
        private readonly Dictionary<string, string> _editedTexts = new();

        [MenuItem("Tools/Cutscenes/Тайминг субтитров")]
        private static void Open()
        {
            var window = GetWindow<VideoCutsceneTimingWindow>("Тайминг субтитров");
            window.minSize = new Vector2(680f, 560f);
            window.TryTakeSelection();
        }

        private void OnEnable()
        {
            _previewHeight = EditorPrefs.GetFloat(PreviewHeightPref, 320f);
            _previewVolume = EditorPrefs.GetFloat(PreviewVolumePref, 0.7f);
            _previewMuted = EditorPrefs.GetBool(PreviewMutePref, false);
            // Объект превью помечен DontSave и не переживает перезагрузку домена
            // осмысленно: ссылка теряется, а сам объект остаётся висеть в сцене.
            CleanupLeakedPreviews();

            EditorApplication.update += EditorUpdate;
            Undo.undoRedoPerformed += UndoRedoHandler;
            TryTakeSelection();

            // Словари не переживают перезагрузку домена, а _target переживает —
            // без этого после перекомпиляции локализация оказывалась пустой.
            LoadTexts();
        }

        private void OnDisable()
        {
            EditorPrefs.SetFloat(PreviewHeightPref, _previewHeight);
            EditorPrefs.SetFloat(PreviewVolumePref, _previewVolume);
            EditorPrefs.SetBool(PreviewMutePref, _previewMuted);
            EditorApplication.update -= EditorUpdate;
            Undo.undoRedoPerformed -= UndoRedoHandler;
            DestroyPreview();
        }

        private void OnSelectionChange()
        {
            TryTakeSelection();
            Repaint();
        }

        private void TryTakeSelection()
        {
            if (Selection.activeObject is VideoCutsceneSO cutscene && cutscene != _target)
            {
                SetTarget(cutscene);
            }
        }

        private void SetTarget(VideoCutsceneSO cutscene)
        {
            _target = cutscene;
            _keyPrefix = MakeDefaultPrefix(cutscene);
            _selected = -1;
            _editedTexts.Clear();
            DestroyPreview();
            LoadTexts();
        }

        /// <summary>
        /// В edit mode движок не крутит игровой цикл сам, поэтому VideoPlayer никогда не
        /// дойдёт до isPrepared и не проиграет ни кадра. Пинаем цикл руками каждый апдейт.
        /// </summary>
        private void EditorUpdate()
        {
            if (_preview == null)
            {
                return;
            }

            // Крутим игровой цикл только когда есть что показывать: постоянный
            // QueuePlayerLoopUpdate + Repaint держал редактор под нагрузкой впустую.
            var busy = !_preview.isPrepared || _preview.isPlaying || _pendingFrames > 0;

            if (busy)
            {
                EditorApplication.QueuePlayerLoopUpdate();

                if (_pendingFrames > 0)
                {
                    _pendingFrames--;
                }
            }

            PrimePreview();

            if (!_preview.isPrepared && string.IsNullOrEmpty(_previewError)
                                     && EditorApplication.timeSinceStartup - _prepareStartedAt > 30d)
            {
                _previewError = "Ролик не подготовился за 30 секунд. Проверь кодек (H.264) и путь к файлу.";
                Repaint();
            }

            if (busy)
            {
                Repaint();
            }
        }

        /// <summary>
        /// VideoPlayer не отдаёт кадр, пока хоть раз не проигрался: без этого первый
        /// клик по таймлайну не перематывал картинку. Проигрываем пару кадров в тишине и встаём на паузу.
        /// </summary>
        private void PrimePreview()
        {
            if (_primed || !_preview.isPrepared)
            {
                return;
            }

            if (_primeFrames == 0)
            {
                ApplyPreviewAudio(true);
                _preview.Play();
                _primeFrames = 3;
                return;
            }

            if (--_primeFrames > 0)
            {
                return;
            }

            _preview.Pause();
            _preview.time = 0d;
            _primed = true;

            ApplyPreviewAudio();
        }

        private void ApplyPreviewAudio(bool forceMute = false)
        {
            if (_preview == null || _preview.audioTrackCount == 0)
            {
                return;
            }

            _preview.SetDirectAudioMute(0, forceMute || _previewMuted);
            _preview.SetDirectAudioVolume(0, _previewVolume);
        }

        private void OnGUI()
        {
            HandleHotkeys();
            DrawToolbar();

            if (_target == null)
            {
                EditorGUILayout.HelpBox("Выбери ассет катсцены в проекте или перетащи сюда.", MessageType.Info);
                return;
            }

            if (string.IsNullOrEmpty(_target.VideoFile))
            {
                EditorGUILayout.HelpBox("В катсцене не выбран файл ролика.", MessageType.Warning);
                return;
            }

            EnsurePreview();

            DrawPreview();
            DrawSplitter();
            DrawTransport();
            DrawTimeline();
            DrawLineButtons();
            DrawLines();
            DrawStyleEditor();
            DrawTools();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var picked = (VideoCutsceneSO)EditorGUILayout.ObjectField(_target, typeof(VideoCutsceneSO), false);

                if (picked != _target)
                {
                    SetTarget(picked);
                }

                if (GUILayout.Button("Перезапустить превью", EditorStyles.toolbarButton, GUILayout.Width(160f)))
                {
                    DestroyPreview();
                }
            }
        }

        // --- превью и транспорт -------------------------------------------------

        private void DrawPreview()
        {
            // Превью занимает всё, что не забрали список и панели: ручка ниже двигает границу.
            var maxHeight = Mathf.Max(120f, position.height - 320f);
            _previewHeight = Mathf.Clamp(_previewHeight, 120f, maxHeight);

            var rect = GUILayoutUtility.GetRect(0f, _previewHeight, GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(rect, Color.black);

            if (!string.IsNullOrEmpty(_previewError))
            {
                EditorGUI.HelpBox(rect, _previewError, MessageType.Error);
                return;
            }

            if (_previewTexture == null || _preview == null || !_preview.isPrepared)
            {
                EditorGUI.LabelField(rect, "готовим ролик…", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            GUI.DrawTexture(rect, _previewTexture, ScaleMode.ScaleToFit);

            // Субтитр раскладываем по кадру, а не по всей чёрной области:
            // в игре канвас совпадает с картинкой, и только так превью честное.
            DrawSubtitleOverlay(VideoRect(rect));
        }

        /// <summary>
        /// Текущая реплика поверх кадра ровно там и таким размером, как будет в игре:
        /// геометрия считается из того же SubtitleStyle, что применяет рантайм.
        /// Шрифт редакторский — начертание смотри в игре.
        /// </summary>
        private void DrawSubtitleOverlay(Rect previewRect)
        {
            var line = FindLineAt((float)CurrentTime);

            if (line == null || string.IsNullOrEmpty(line.LocKey))
            {
                return;
            }

            var style = Style;
            var scale = previewRect.height / 1080f;

            var plate = new Rect(
                previewRect.x + style.SideMargin * scale,
                previewRect.yMax - (style.BottomOffset + style.PlateHeight) * scale,
                previewRect.width - 2f * style.SideMargin * scale,
                style.PlateHeight * scale);

            EditorGUI.DrawRect(plate, style.PlateColor);

            var label = _overlayLabel ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            label.fontSize = Mathf.Max(6, Mathf.RoundToInt(style.FontSizeMax * scale));
            label.normal.textColor = style.TextColor;

            // У TMP-ассета обычно сохранён исходный ttf — тогда превью показывает настоящее начертание.
            if (style.Font != null && style.Font.sourceFontFile != null)
            {
                label.font = style.Font.sourceFontFile;
            }

            GUI.Label(plate, GetText(line.LocKey) ?? line.LocKey, label);

            HandlePlateDrag(plate, scale, style);
        }

        /// <summary>Плашку можно двигать прямо на кадре: за середину — вверх-вниз, за верхний край — высота.</summary>
        private void HandlePlateDrag(Rect plate, float scale, SubtitleStyle style)
        {
            if (Library == null || scale <= 0f)
            {
                return;
            }

            var topEdge = new Rect(plate.x, plate.y - 4f, plate.width, 9f);

            EditorGUIUtility.AddCursorRect(plate, MouseCursor.MoveArrow);
            EditorGUIUtility.AddCursorRect(topEdge, MouseCursor.ResizeVertical);

            var e = Event.current;

            if (e.type == EventType.MouseDown)
            {
                if (topEdge.Contains(e.mousePosition))
                {
                    _plateDrag = PlateDrag.Height;
                    e.Use();
                }
                else if (plate.Contains(e.mousePosition))
                {
                    _plateDrag = PlateDrag.Move;
                    e.Use();
                }
            }

            if (_plateDrag != PlateDrag.None && e.type == EventType.MouseDrag)
            {
                var delta = e.delta.y / scale;

                if (_plateDrag == PlateDrag.Move)
                {
                    style.BottomOffset = Mathf.Round(
                        Mathf.Clamp(style.BottomOffset - delta, 0f, 1080f - style.PlateHeight));
                }
                else
                {
                    style.PlateHeight = Mathf.Round(
                        Mathf.Clamp(style.PlateHeight - delta, 20f, 1080f - style.BottomOffset));
                }

                EditorUtility.SetDirty(Library);
                e.Use();
                Repaint();
            }

            if (e.type == EventType.MouseUp)
            {
                _plateDrag = PlateDrag.None;
            }
        }

        /// <summary>Область реального кадра внутри превью — с учётом чёрных полей.</summary>
        private Rect VideoRect(Rect area)
        {
            if (_preview == null || _preview.width == 0 || _preview.height == 0)
            {
                return area;
            }

            var aspect = (float)_preview.width / _preview.height;

            if (area.width / area.height > aspect)
            {
                var width = area.height * aspect;

                return new Rect(area.center.x - width * 0.5f, area.y, width, area.height);
            }

            var height = area.width / aspect;

            return new Rect(area.x, area.center.y - height * 0.5f, area.width, height);
        }

        private SubtitleStyle Style => Library != null ? Library.SubtitleStyle : _fallbackStyle;

        private CutsceneLibrarySO Library
        {
            get
            {
                if (_library != null)
                {
                    return _library;
                }

                foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(CutsceneLibrarySO)}"))
                {
                    _library = AssetDatabase.LoadAssetAtPath<CutsceneLibrarySO>(
                        AssetDatabase.GUIDToAssetPath(guid));

                    if (_library != null)
                    {
                        return _library;
                    }
                }

                return null;
            }
        }

        private void DrawStyleEditor()
        {
            _styleFoldout = EditorGUILayout.Foldout(_styleFoldout, "Вид субтитров (общий для всех катсцен)", true);

            if (!_styleFoldout)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _library = (CutsceneLibrarySO)EditorGUILayout.ObjectField("Библиотека", Library,
                    typeof(CutsceneLibrarySO), false);

                if (_library == null)
                {
                    EditorGUILayout.HelpBox("Не найден ассет CutsceneLibrary — настройки некуда сохранять.",
                        MessageType.Warning);
                    return;
                }

                var style = _library.SubtitleStyle;

                EditorGUI.BeginChangeCheck();

                var labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 130f;

                style.Font = (TMPro.TMP_FontAsset)EditorGUILayout.ObjectField("Шрифт", style.Font,
                    typeof(TMPro.TMP_FontAsset), false);

                using (new EditorGUILayout.HorizontalScope())
                {
                    style.AutoSize = EditorGUILayout.ToggleLeft("Авто-размер", style.AutoSize, GUILayout.Width(110f));

                    EditorGUIUtility.labelWidth = 40f;

                    if (style.AutoSize)
                    {
                        style.FontSizeMin = EditorGUILayout.FloatField("мин", style.FontSizeMin, GUILayout.Width(90f));
                    }

                    style.FontSizeMax = EditorGUILayout.FloatField(
                        style.AutoSize ? "макс" : "кегль", style.FontSizeMax, GUILayout.Width(90f));

                    EditorGUIUtility.labelWidth = 130f;
                }

                style.TextColor = EditorGUILayout.ColorField("Цвет текста", style.TextColor);
                style.PlateColor = EditorGUILayout.ColorField("Цвет плашки", style.PlateColor);

                style.PlateHeight = EditorGUILayout.FloatField("Высота плашки", style.PlateHeight);
                style.SideMargin = EditorGUILayout.FloatField("Отступ по бокам", style.SideMargin);
                style.BottomOffset = EditorGUILayout.FloatField("Отступ снизу", style.BottomOffset);

                EditorGUIUtility.labelWidth = labelWidth;

                EditorGUILayout.LabelField(
                    "Размеры — в пикселях макета 1920×1080. Плашку можно двигать прямо на кадре: " +
                    "за середину — вверх-вниз, за верхний край — высота.",
                    EditorStyles.miniLabel);

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(_library);
                    Repaint();
                }

                if (GUILayout.Button("Сохранить вид"))
                {
                    AssetDatabase.SaveAssetIfDirty(_library);
                }
            }
        }

        private void DrawSplitter()
        {
            var rect = GUILayoutUtility.GetRect(0f, 6f, GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(rect, SplitterColor);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);

            var e = Event.current;

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                _resizingPreview = true;
                e.Use();
            }

            if (_resizingPreview && e.type == EventType.MouseDrag)
            {
                _previewHeight += e.delta.y;
                e.Use();
                Repaint();
            }

            if (e.type == EventType.MouseUp)
            {
                _resizingPreview = false;
            }
        }

        private void DrawTransport()
        {
            var ready = _preview != null && _preview.isPrepared;

            using (new EditorGUI.DisabledScope(!ready))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("⏮", GUILayout.Width(34f), GUILayout.Height(24f)))
                {
                    Seek(0d);
                }

                if (GUILayout.Button("−1с", GUILayout.Width(46f), GUILayout.Height(24f)))
                {
                    Seek(CurrentTime - 1d);
                }

                if (GUILayout.Button("−кадр", GUILayout.Width(56f), GUILayout.Height(24f)))
                {
                    StepFrames(-1);
                }

                var playing = ready && _preview.isPlaying;

                if (GUILayout.Button(playing ? "⏸  Пауза  (Space)" : "▶  Играть  (Space)", GUILayout.Height(24f)))
                {
                    TogglePlay();
                }

                if (GUILayout.Button("+кадр", GUILayout.Width(56f), GUILayout.Height(24f)))
                {
                    StepFrames(1);
                }

                if (GUILayout.Button("+1с", GUILayout.Width(46f), GUILayout.Height(24f)))
                {
                    Seek(CurrentTime + 1d);
                }

                GUILayout.Label($"{CurrentTime:0.00} / {Length:0.00} сек", GUILayout.Width(120f));

                DrawVolumeControls();
            }
        }

        /// <summary>Громкость предпросмотра — только для этого окна, в ассет не пишется.</summary>
        private void DrawVolumeControls()
        {
            EditorGUI.BeginChangeCheck();

            _previewMuted = GUILayout.Toggle(_previewMuted,
                new GUIContent(_previewMuted ? "🔇" : "🔊", "Звук предпросмотра. На игру не влияет."),
                EditorStyles.miniButton, GUILayout.Width(30f), GUILayout.Height(24f));

            _previewVolume = GUILayout.HorizontalSlider(_previewVolume, 0f, 1f, GUILayout.Width(90f));

            if (EditorGUI.EndChangeCheck())
            {
                ApplyPreviewAudio();
                EditorPrefs.SetFloat(PreviewVolumePref, _previewVolume);
                EditorPrefs.SetBool(PreviewMutePref, _previewMuted);
            }
        }

        private void DrawTimeline()
        {
            var rect = GUILayoutUtility.GetRect(0f, 58f, GUILayout.ExpandWidth(true));
            var length = (float)Length;

            EditorGUI.DrawRect(rect, TimelineBackground);

            if (length <= 0f)
            {
                EditorGUI.LabelField(rect, "  таймлайн появится, когда ролик подготовится", EditorStyles.miniLabel);
                return;
            }

            var lines = _target.Subtitles;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (line == null || line.End <= line.Start)
                {
                    continue;
                }

                var block = BlockRect(rect, length, line);

                EditorGUI.DrawRect(block, i == _selected ? SelectedLineColor : LineColor);

                // Тёмные полоски по краям — за них тянется тайминг.
                if (block.width > 3f * HandleWidth)
                {
                    var edge = new Color(0f, 0f, 0f, 0.35f);

                    EditorGUI.DrawRect(new Rect(block.x, block.y, HandleWidth, block.height), edge);
                    EditorGUI.DrawRect(new Rect(block.xMax - HandleWidth, block.y, HandleWidth, block.height), edge);

                    EditorGUIUtility.AddCursorRect(
                        new Rect(block.x, block.y, HandleWidth, block.height), MouseCursor.ResizeHorizontal);
                    EditorGUIUtility.AddCursorRect(
                        new Rect(block.xMax - HandleWidth, block.y, HandleWidth, block.height),
                        MouseCursor.ResizeHorizontal);
                    EditorGUIUtility.AddCursorRect(
                        new Rect(block.x + HandleWidth, block.y, block.width - 2f * HandleWidth, block.height),
                        MouseCursor.SlideArrow);
                }

                if (block.width > 18f)
                {
                    GUI.Label(block, " " + (i + 1), EditorStyles.miniLabel);
                }
            }

            var headX = rect.x + rect.width * Mathf.Clamp01((float)CurrentTime / length);
            EditorGUI.DrawRect(new Rect(headX - 1f, rect.y, 2f, rect.height), PlayheadColor);

            if (_dragIndex >= 0 && _dragIndex < lines.Count)
            {
                var line = lines[_dragIndex];

                GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, 300f, 16f),
                    $"#{_dragIndex + 1}   {line.Start:0.00} → {line.End:0.00}   ({line.Duration:0.00} сек)",
                    EditorStyles.whiteMiniLabel);
            }
            else
            {
                GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, 420f, 16f),
                    "края блока — тайминг, середина — сдвиг целиком, пустое место — перемотка",
                    EditorStyles.miniLabel);
            }

            HandleTimelineMouse(rect, length);
        }

        private static Rect BlockRect(Rect timeline, float length, SubtitleLine line)
        {
            var x = timeline.x + timeline.width * Mathf.Clamp01(line.Start / length);
            var w = Mathf.Max(4f, timeline.width * Mathf.Clamp01(line.Duration / length));

            return new Rect(x, timeline.y + 20f, w, timeline.height - 26f);
        }

        private void HandleTimelineMouse(Rect rect, float length)
        {
            var e = Event.current;
            var lines = _target.Subtitles;

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    var line = lines[i];

                    if (line == null || line.End <= line.Start)
                    {
                        continue;
                    }

                    var block = BlockRect(rect, length, line);

                    if (!block.Contains(e.mousePosition))
                    {
                        continue;
                    }

                    _selected = i;
                    _dragIndex = i;
                    _dragTimeOffset = PositionToTime(rect, length, e.mousePosition.x) - line.Start;

                    if (block.width > 3f * HandleWidth && e.mousePosition.x <= block.x + HandleWidth)
                    {
                        _dragMode = DragMode.Start;
                    }
                    else if (block.width > 3f * HandleWidth && e.mousePosition.x >= block.xMax - HandleWidth)
                    {
                        _dragMode = DragMode.End;
                    }
                    else
                    {
                        _dragMode = DragMode.Move;
                    }

                    Record("Тайминг субтитра");
                    e.Use();
                    return;
                }

                // Мимо блоков — обычная перемотка.
                _scrubbing = true;
            }

            if (_dragIndex >= 0 && e.type == EventType.MouseDrag)
            {
                DragLine(lines[_dragIndex], PositionToTime(rect, length, e.mousePosition.x), length);
                e.Use();
                Repaint();
            }

            if (_scrubbing && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                Seek(PositionToTime(rect, length, e.mousePosition.x));
                e.Use();
            }

            if (e.type == EventType.MouseUp)
            {
                _scrubbing = false;
                _dragIndex = -1;
                _dragMode = DragMode.None;
            }
        }

        private void DragLine(SubtitleLine line, double time, float length)
        {
            const float minDuration = 0.1f;

            switch (_dragMode)
            {
                case DragMode.Start:
                    line.Start = Round(Mathf.Clamp((float)time, 0f, line.End - minDuration));
                    break;

                case DragMode.End:
                    line.End = Round(Mathf.Clamp((float)time, line.Start + minDuration, length));
                    break;

                case DragMode.Move:
                    var duration = line.Duration;
                    var start = Mathf.Clamp((float)(time - _dragTimeOffset), 0f, length - duration);

                    line.Start = Round(start);
                    line.End = Round(start + duration);
                    break;
            }
        }

        private static float Round(float seconds) => Mathf.Round(seconds * 100f) / 100f;

        private static double PositionToTime(Rect rect, float length, float x)
        {
            return Mathf.Clamp01((x - rect.x) / rect.width) * length;
        }

        private void SelectLineAt(double time)
        {
            var lines = _target.Subtitles;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] != null && lines[i].Contains((float)time))
                {
                    _selected = i;
                    return;
                }
            }
        }

        private SubtitleLine FindLineAt(float time)
        {
            foreach (var line in _target.Subtitles)
            {
                if (line != null && line.Contains(time))
                {
                    return line;
                }
            }

            return null;
        }

        // --- список реплик ------------------------------------------------------

        private void DrawLineButtons()
        {
            var lines = _target.Subtitles;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ строка с текущего времени  (N)", GUILayout.Height(24f)))
                {
                    AddLineAtCurrentTime();
                }

                using (new EditorGUI.DisabledScope(_selected < 0 || _selected >= lines.Count))
                {
                    if (GUILayout.Button("⏱ старт  (Q)", GUILayout.Height(24f), GUILayout.Width(110f)))
                    {
                        SetSelectedStart();
                    }

                    if (GUILayout.Button("⏱ конец  (W)", GUILayout.Height(24f), GUILayout.Width(110f)))
                    {
                        SetSelectedEnd();
                    }
                }

                if (GUILayout.Button("Отсортировать", GUILayout.Height(24f), GUILayout.Width(120f)))
                {
                    Record("Сортировка субтитров");
                    lines.Sort((a, b) => a.Start.CompareTo(b.Start));
                    _selected = -1;
                }
            }
        }

        private void DrawLines()
        {
            var lines = _target.Subtitles;

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            if (lines.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Строк пока нет. Играй ролик (Space), в начале реплики жми N, в конце — W.",
                    MessageType.Info);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (line == null)
                {
                    continue;
                }

                var active = CurrentTime >= line.Start && CurrentTime < line.End;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button($"{i + 1}", EditorStyles.miniButton, GUILayout.Width(26f)))
                        {
                            _selected = i;
                            Seek(line.Start);
                        }

                        GUILayout.Label(active ? "●" : " ", GUILayout.Width(14f));

                        var start = EditorGUILayout.DelayedFloatField(Round(line.Start), GUILayout.Width(58f));
                        GUILayout.Label("→", GUILayout.Width(14f));
                        var end = EditorGUILayout.DelayedFloatField(Round(line.End), GUILayout.Width(58f));

                        if (!Mathf.Approximately(start, Round(line.Start))
                            || !Mathf.Approximately(end, Round(line.End)))
                        {
                            Record("Тайминг субтитра");
                            line.Start = Round(start);
                            line.End = Round(end);
                        }

                        var key = EditorGUILayout.TextField(line.LocKey);

                        if (key != line.LocKey)
                        {
                            Record("Ключ субтитра");
                            line.LocKey = key;
                        }

                        var pickRect = GUILayoutUtility.GetRect(new GUIContent("🔍"), EditorStyles.miniButton,
                            GUILayout.Width(28f));

                        if (GUI.Button(pickRect, new GUIContent("🔍", "Выбрать ключ поиском по фразам"),
                                EditorStyles.miniButton))
                        {
                            var index = i;
                            PopupWindow.Show(pickRect, new KeyPickerPopup(this, picked =>
                            {
                                Record("Ключ субтитра");
                                lines[index].LocKey = picked;
                            }));
                        }

                        if (GUILayout.Button("▶", GUILayout.Width(26f)))
                        {
                            _selected = i;
                            Seek(line.Start);
                            Play();
                        }

                        if (GUILayout.Button("✕", GUILayout.Width(24f)))
                        {
                            Record("Удаление субтитра");
                            lines.RemoveAt(i);
                            _selected = -1;
                            GUIUtility.ExitGUI();
                        }
                    }

                    DrawTextField(line.LocKey);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTextField(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var current = GetText(key);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(_language, EditorStyles.miniLabel, GUILayout.Width(44f));

                var edited = EditorGUILayout.TextField(current ?? string.Empty);

                if (edited != (current ?? string.Empty))
                {
                    _editedTexts[key] = edited;
                    _languageInfoCache.Remove(key);
                }

                GUILayout.Label(DescribeLanguages(key), EditorStyles.miniLabel, GUILayout.Width(220f));
            }
        }

        /// <summary>Показывает, в каких языковых файлах ключ уже есть, а где его не хватает.</summary>
        private readonly Dictionary<string, string> _languageInfoCache = new();

        private string DescribeLanguages(string key)
        {
            EnsureTextsLoaded();

            if (_languageInfoCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var info = BuildLanguageInfo(key);

            _languageInfoCache[key] = info;

            return info;
        }

        private string BuildLanguageInfo(string key)
        {

            if (_editedTexts.ContainsKey(key))
            {
                return "не сохранено";
            }

            var present = new List<string>();
            var missing = new List<string>();

            foreach (var language in _languages)
            {
                var target = _byLanguage[language].ContainsKey(key) ? present : missing;
                target.Add(language.Split('-')[0]);
            }

            if (missing.Count == 0)
            {
                return present.Count == 0 ? string.Empty : "есть во всех языках";
            }

            if (present.Count == 0)
            {
                return "ключа нет нигде";
            }

            return "есть: " + string.Join(" ", present) + " | нет: " + string.Join(" ", missing);
        }

        // --- нижняя панель ------------------------------------------------------

        private void DrawTools()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _keyPrefix = EditorGUILayout.TextField(
                    new GUIContent("Префикс ключей", "Подставляется в имена новых строк."), _keyPrefix);

                var index = Mathf.Max(0, _languages.IndexOf(_language));

                if (_languages.Count > 0)
                {
                    var newIndex = EditorGUILayout.Popup(index, _languages.ToArray(), GUILayout.Width(90f));

                    if (newIndex != index)
                    {
                        _language = _languages[newIndex];
                    }
                }

                if (GUILayout.Button("Перечитать", GUILayout.Width(90f)))
                {
                    LoadTexts();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_editedTexts.Count == 0))
                {
                    if (GUILayout.Button($"Сохранить тексты в GameText_{_language}.json ({_editedTexts.Count})",
                            GUILayout.Height(26f)))
                    {
                        SaveTexts();
                    }
                }

                if (GUILayout.Button("Проверить", GUILayout.Height(26f), GUILayout.Width(100f)))
                {
                    var issues = _target.Validate((float)Length);

                    EditorUtility.DisplayDialog("Проверка",
                        issues.Count == 0
                            ? $"Всё в порядке. Строк: {_target.Subtitles.Count}."
                            : string.Join("\n", issues),
                        "Ок");
                }
            }
        }

        // --- действия -----------------------------------------------------------

        private void HandleHotkeys()
        {
            var e = Event.current;

            if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField || _target == null)
            {
                return;
            }

            switch (e.keyCode)
            {
                case KeyCode.Space:
                    TogglePlay();
                    break;
                case KeyCode.N:
                    AddLineAtCurrentTime();
                    break;
                case KeyCode.Q:
                    SetSelectedStart();
                    break;
                case KeyCode.W:
                    SetSelectedEnd();
                    break;
                case KeyCode.LeftArrow:
                    StepFrames(-1);
                    break;
                case KeyCode.RightArrow:
                    StepFrames(1);
                    break;
                default:
                    return;
            }

            e.Use();
        }

        private void AddLineAtCurrentTime()
        {
            Record("Добавление субтитра");

            var start = Round((float)CurrentTime);

            _target.Subtitles.Add(new SubtitleLine
            {
                Start = start,
                End = Round(start + 2f),
                LocKey = $"{_keyPrefix}{_target.Subtitles.Count + 1:000}"
            });

            _selected = _target.Subtitles.Count - 1;
        }

        private void SetSelectedStart()
        {
            if (_selected < 0 || _selected >= _target.Subtitles.Count)
            {
                return;
            }

            Record("Тайминг субтитра");
            _target.Subtitles[_selected].Start = Round((float)CurrentTime);
        }

        private void SetSelectedEnd()
        {
            if (_selected < 0 || _selected >= _target.Subtitles.Count)
            {
                return;
            }

            Record("Тайминг субтитра");
            _target.Subtitles[_selected].End = Round((float)CurrentTime);
        }

        private void TogglePlay()
        {
            if (_preview == null || !_preview.isPrepared)
            {
                return;
            }

            if (_preview.isPlaying)
            {
                _preview.Pause();
            }
            else
            {
                Play();
            }
        }

        private void Play()
        {
            if (_preview != null && _preview.isPrepared)
            {
                _preview.Play();
            }
        }

        private void Seek(double time)
        {
            if (_preview == null || !_preview.isPrepared)
            {
                return;
            }

            _preview.time = Math.Max(0d, Math.Min(time, Length));

            // Пары кадров цикла хватает, чтобы плеер отрисовал новую позицию.
            _pendingFrames = 2;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void StepFrames(int frames)
        {
            if (_preview == null || !_preview.isPrepared)
            {
                return;
            }

            var rate = _preview.frameRate > 0.0 ? _preview.frameRate : 30.0;

            _preview.Pause();
            Seek(CurrentTime + frames / rate);
        }

        // --- локализация --------------------------------------------------------

        private string LocalizationFolder => Path.Combine(Application.streamingAssetsPath, LocalizationGame);

        private string LocalizationPath(string language) =>
            Path.Combine(LocalizationFolder, $"GameText_{language}.json");

        private void LoadTexts()
        {
            _languages.Clear();
            _byLanguage.Clear();
            _editedTexts.Clear();
            _languageInfoCache.Clear();

            if (!Directory.Exists(LocalizationFolder))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(LocalizationFolder, "GameText_*.json"))
            {
                var language = Path.GetFileNameWithoutExtension(file).Replace("GameText_", string.Empty);
                var map = new Dictionary<string, string>();
                var content = File.ReadAllText(file, Encoding.UTF8);

                foreach (Match match in Regex.Matches(content,
                             "\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
                {
                    var key = Unescape(match.Groups[1].Value);

                    if (!map.ContainsKey(key))
                    {
                        map[key] = Unescape(match.Groups[2].Value);
                    }
                }

                _languages.Add(language);
                _byLanguage[language] = map;
            }

            _languages.Sort();

            if (!_byLanguage.ContainsKey(_language) && _languages.Count > 0)
            {
                _language = _languages.Contains(DefaultLanguage) ? DefaultLanguage : _languages[0];
            }
        }

        private void EnsureTextsLoaded()
        {
            if (_byLanguage.Count == 0)
            {
                LoadTexts();
            }
        }

        private string GetText(string key)
        {
            EnsureTextsLoaded();

            if (_editedTexts.TryGetValue(key, out var edited))
            {
                return edited;
            }

            if (_byLanguage.TryGetValue(_language, out var map) && map.TryGetValue(key, out var text))
            {
                return text;
            }

            return null;
        }

        /// <summary>Пары «ключ — фраза» текущего языка для поиска.</summary>
        internal IEnumerable<KeyValuePair<string, string>> CurrentLanguageTexts
        {
            get
            {
                EnsureTextsLoaded();

                return _byLanguage.TryGetValue(_language, out var map)
                    ? (IEnumerable<KeyValuePair<string, string>>)map
                    : Array.Empty<KeyValuePair<string, string>>();
            }
        }

        private void SaveTexts()
        {
            var path = LocalizationPath(_language);

            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Локализация", $"Файл не найден:\n{path}", "Ок");
                return;
            }

            var content = File.ReadAllText(path, Encoding.UTF8);

            foreach (var pair in _editedTexts)
            {
                var pattern = "\"" + Regex.Escape(pair.Key) + "\"\\s*:\\s*\"(?:[^\"\\\\]|\\\\.)*\"";
                var replacement = "\"" + pair.Key + "\": \"" + Escape(pair.Value) + "\"";

                if (Regex.IsMatch(content, pattern))
                {
                    content = Regex.Replace(content, pattern, replacement.Replace("$", "$$"));
                    continue;
                }

                // Ключа ещё нет — дописываем первым в объект Data, форматирование файла не трогаем.
                var anchor = Regex.Match(content, "\"Data\"\\s*:\\s*\\{");

                if (!anchor.Success)
                {
                    EditorUtility.DisplayDialog("Локализация",
                        "В файле не найден объект \"Data\" — добавь ключи вручную.", "Ок");
                    return;
                }

                content = content.Insert(anchor.Index + anchor.Length, "\n    " + replacement + ",");
            }

            File.WriteAllText(path, content, new UTF8Encoding(false));

            var count = _editedTexts.Count;

            LoadTexts();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Локализация",
                $"Сохранено строк: {count}\n{path}\n\nОстальные языки — через обычную таблицу локализации.", "Ок");
        }

        private static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", string.Empty);

        private static string Unescape(string value) =>
            value.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");

        private static string MakeDefaultPrefix(VideoCutsceneSO cutscene)
        {
            if (cutscene == null)
            {
                return "cut_";
            }

            var cleaned = Regex.Replace(cutscene.name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');

            return string.IsNullOrEmpty(cleaned) ? "cut_" : cleaned + "_";
        }

        // --- превью -------------------------------------------------------------

        private double CurrentTime => _preview == null ? 0d : _preview.time;

        private double Length => _preview == null || !_preview.isPrepared ? 0d : _preview.length;

        private void EnsurePreview()
        {
            var url = _target.GetVideoUrl();

            if (_preview != null && _preparedUrl == url)
            {
                return;
            }

            DestroyPreview();

            if (!File.Exists(url))
            {
                _previewError = $"Файл не найден:\n{url}";
                return;
            }

            _previewObject = EditorUtility.CreateGameObjectWithHideFlags(PreviewObjectName,
                HideFlags.HideAndDontSave, typeof(VideoPlayer), typeof(AudioSource));

            _previewTexture = new RenderTexture(1280, 720, 0);

            _previewObject.GetComponent<AudioSource>().playOnAwake = false;

            _primed = false;
            _primeFrames = 0;

            _preview = _previewObject.GetComponent<VideoPlayer>();
            _preview.source = VideoSource.Url;
            _preview.url = url;
            _preview.playOnAwake = false;
            _preview.isLooping = false;
            _preview.renderMode = VideoRenderMode.RenderTexture;
            _preview.targetTexture = _previewTexture;
            // Direct, а не AudioSource: в edit mode в открытой сцене может не быть
            // AudioListener, и тогда предпросмотр был бы немым.
            _preview.audioOutputMode = VideoAudioOutputMode.Direct;
            _preview.EnableAudioTrack(0, true);
            _preview.errorReceived += PreviewErrorHandler;
            _preview.Prepare();

            _preparedUrl = url;
            _prepareStartedAt = EditorApplication.timeSinceStartup;
            _previewError = null;
        }

        private void PreviewErrorHandler(VideoPlayer source, string message) => _previewError = message;

        private static void CleanupLeakedPreviews()
        {
            foreach (var player in Resources.FindObjectsOfTypeAll<VideoPlayer>())
            {
                if (player == null || player.gameObject == null)
                {
                    continue;
                }

                if (player.gameObject.name == PreviewObjectName
                    && player.gameObject.hideFlags == HideFlags.HideAndDontSave)
                {
                    DestroyImmediate(player.gameObject);
                }
            }
        }

        private void DestroyPreview()
        {
            if (_preview != null)
            {
                _preview.errorReceived -= PreviewErrorHandler;
            }

            if (_previewObject != null)
            {
                DestroyImmediate(_previewObject);
                _previewObject = null;
                _preview = null;
            }

            if (_previewTexture != null)
            {
                _previewTexture.Release();
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }

            _preparedUrl = null;
            _previewError = null;
            _primed = false;
            _primeFrames = 0;
        }

        private void Record(string action)
        {
            // Именно RegisterCompleteObjectUndo: RecordObject не восстанавливает
            // добавление и удаление элементов списка, и Ctrl+Z работал через раз.
            Undo.RegisterCompleteObjectUndo(_target, action);
            EditorUtility.SetDirty(_target);
        }

        private void UndoRedoHandler()
        {
            if (_target != null && _selected >= _target.Subtitles.Count)
            {
                _selected = -1;
            }

            Repaint();
        }

        /// <summary>
        /// Выпадашка выбора ключа с поиском по фразе и по ключу.
        /// Нативное окно Unity: всплывающие окна Odin падают на Unity 6000.0.x.
        /// </summary>
        private class KeyPickerPopup : PopupWindowContent
        {
            private readonly VideoCutsceneTimingWindow _owner;
            private readonly Action<string> _picked;

            private string _search = string.Empty;
            private Vector2 _scroll;
            private bool _focused;

            private string _cachedSearch;
            private List<KeyValuePair<string, string>> _cachedMatches;
            private int _total = -1;

            public KeyPickerPopup(VideoCutsceneTimingWindow owner, Action<string> picked)
            {
                _owner = owner;
                _picked = picked;
            }

            public override Vector2 GetWindowSize() => new(620f, 440f);

            public override void OnGUI(Rect rect)
            {
                const float rowHeight = 38f;
                const float padding = 8f;

                var searchRect = new Rect(padding, padding, rect.width - 2f * padding, 20f);

                GUI.SetNextControlName("cutscene_key_search");
                _search = EditorGUI.TextField(searchRect, _search);

                if (!_focused)
                {
                    EditorGUI.FocusTextInControl("cutscene_key_search");
                    _focused = true;
                }

                if (string.IsNullOrEmpty(_search))
                {
                    EditorGUI.LabelField(new Rect(searchRect.x + 4f, searchRect.y, searchRect.width, 20f),
                        "поиск по фразе или ключу", EditorStyles.centeredGreyMiniLabel);
                }

                // Фильтр и подсчёт кэшируются: OnGUI зовётся на каждое движение мыши,
                // а в локализации несколько тысяч строк.
                if (_cachedMatches == null || _cachedSearch != _search)
                {
                    _cachedMatches = Filter().Take(300).ToList();
                    _cachedSearch = _search;

                    if (_total < 0)
                    {
                        _total = _owner.CurrentLanguageTexts.Count();
                    }
                }

                var matches = _cachedMatches;
                var total = _total;

                EditorGUI.LabelField(new Rect(padding, searchRect.yMax + 2f, rect.width - 2f * padding, 14f),
                    total == 0
                        ? $"локализация не загружена — проверь StreamingAssets/RoadsOfDaVinci/GameText_{_owner._language}.json"
                        : $"найдено: {matches.Count} из {total}",
                    EditorStyles.miniLabel);

                var listRect = new Rect(padding, searchRect.yMax + 18f,
                    rect.width - 2f * padding, rect.height - searchRect.yMax - 18f - padding);
                var contentRect = new Rect(0f, 0f, listRect.width - 18f, matches.Count * rowHeight);

                _scroll = GUI.BeginScrollView(listRect, _scroll, contentRect, false, true);

                for (int i = 0; i < matches.Count; i++)
                {
                    var row = new Rect(0f, i * rowHeight, contentRect.width, rowHeight - 2f);

                    if (GUI.Button(row, GUIContent.none, EditorStyles.miniButton))
                    {
                        _picked(matches[i].Key);
                        editorWindow.Close();
                    }

                    // Рисуем поверх кнопки сами: фраза крупно, ключ мелким серым, обе строки слева.
                    var textRect = new Rect(row.x + 6f, row.y + 2f, row.width - 12f, 16f);
                    var keyRect = new Rect(row.x + 6f, row.y + 18f, row.width - 12f, 14f);

                    GUI.Label(textRect, Shorten(matches[i].Value, 90), LeftLabel);
                    GUI.Label(keyRect, matches[i].Key, EditorStyles.miniLabel);
                }

                GUI.EndScrollView();
            }

            private static GUIStyle _leftLabel;

            private static GUIStyle LeftLabel => _leftLabel ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };

            private static string Shorten(string value, int max) =>
                string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max) + "…";

            private IEnumerable<KeyValuePair<string, string>> Filter()
            {
                var texts = _owner.CurrentLanguageTexts;

                if (string.IsNullOrWhiteSpace(_search))
                {
                    return texts;
                }

                var needle = _search.Trim();

                return texts.Where(pair =>
                    pair.Key.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                    || pair.Value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }
    }
}
#endif
