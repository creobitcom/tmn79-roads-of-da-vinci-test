using System.Collections.Generic;
using System.IO;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Animations;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Stages;
using Creobit.UI.Utility;
using DG.Tweening;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Editor.Windows
{
    /// <summary>
    /// Создание туториальных окон и сборка тест-сцены, где окно видно на настоящем уровне
    /// с камерой и канвасом как в геймплее.
    /// </summary>
    public class TutorialAuthoringWindow : OdinEditorWindow
    {
        private const string TabCreate = "Создать";
        private const string TabPreview = "Проверить";
        private const string Tabs = "Вкладки";

        public enum AnimationPreset
        {
            [LabelText("Без анимации")] None = 0,
            [LabelText("Затухание")] Fade = 1,
            [LabelText("Всплытие")] Grow = 2,
            [LabelText("Снизу")] FromBottom = 3,
            [LabelText("Сверху")] FromTop = 4,
            [LabelText("С отскоком")] Bounce = 5,
        }

        #region Создать

        [TabGroup(Tabs, TabCreate)]
        [Title("Куда положить")]
        [FolderPath]
        [LabelText("Папка уровня")]
        [InfoBox("Все три ассета лягут в подпапку с именем туториала. " +
                 "Старая тулза раскидывала их по двум местам — здесь этого нет.")]
        [SerializeField]
        private string targetFolder;

        [TabGroup(Tabs, TabCreate)]
        [LabelText("Имя туториала")]
        [SerializeField]
        private string tutorialName = "Lvl01_Tutorial_1";

        [TabGroup(Tabs, TabCreate)]
        [LabelText("Сколько стадий")]
        [PropertyRange(1, 12)]
        [InfoBox("$" + nameof(StagesHint), InfoMessageType.None)]
        [SerializeField]
        private int stageCount = 1;

        [TabGroup(Tabs, TabCreate)]
        [Title("Настройки окна")]
        [LabelText("Анимация")]
        [EnumToggleButtons]
        [SerializeField]
        private AnimationPreset animationPreset = AnimationPreset.Grow;

        [TabGroup(Tabs, TabCreate)]
        [LabelText("Останавливать таймер уровня")]
        [SerializeField]
        private bool pauseLevelTimer;

        [TabGroup(Tabs, TabCreate)]
        [LabelText("Показывать даже при отключённых туториалах")]
        [SerializeField]
        private bool unskipTutorial;

        [TabGroup(Tabs, TabCreate)]
        [LabelText("Показывать поверх HUD")]
        [Tooltip("Иначе панели ресурсов и задач нарисуются поверх окна.")]
        [SerializeField]
        private bool showAboveHud;

        [TabGroup(Tabs, TabCreate)]
        [LabelText("Блокировать клики по HUD")]
        [Tooltip("Пока окно открыто, по интерфейсу кликнуть нельзя. " +
                 "Объекты на уровне остаются доступны.")]
        [SerializeField]
        private bool blockHudInput;

        [TabGroup(Tabs, TabCreate)]
        [InfoBox("Собираю пустой каркас с нуля: фон-заглушка, пустые стадии, кнопка «Ок» и " +
                 "служебные компоненты показа. Никакого чужого арта — сразу правишь под себя.")]
        [HideLabel]
        [DisplayAsString]
        [ShowInInspector]
        private string CreateBanner => string.Empty;

        private string StagesHint => stageCount <= 1
            ? "Одна стадия — обычное окно с кнопкой «Ок»."
            : $"Внутри будет {stageCount} пустых стадий. Каждая гасит предыдущую, " +
              "кнопка «Ок» листает их и на последней закрывает окно. " +
              "Тебе останется вписать текст и накидать арт в каждую.";

        #endregion

        #region Проверить

        // Порядок задан явно: без него Odin ставит свойства после полей и подсказка
        // «откуда взялся список» уезжает под кнопки.
        [TabGroup(Tabs, TabPreview)]
        [PropertyOrder(0)]
        [InfoBox("$" + nameof(PreviewHint), InfoMessageType.None)]
        [InfoBox("Здесь пусто. Открой префаб уровня — список соберётся из его папки.",
            InfoMessageType.Warning, nameof(NoTutorialsFound))]
        [LabelText("Окно туториала")]
        [ValueDropdown(nameof(GetLevelTutorials), AppendNextDrawer = true)]
        [SerializeField]
        private TutorialViewSo tutorialToPreview;

        [TabGroup(Tabs, TabPreview)]
        [PropertyOrder(1)]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("Что выбрано")]
        [ShowIf(nameof(HasSelection))]
        private string SelectedInfo
        {
            get
            {
                if (tutorialToPreview == null)
                {
                    return string.Empty;
                }

                var prefab = LoadWindowPrefab(tutorialToPreview);
                var prefabName = prefab != null ? prefab.name : "префаб не найден";
                var flags = new List<string>();

                if (tutorialToPreview.pauseLevelTimer)
                {
                    flags.Add("пауза таймера");
                }

                if (tutorialToPreview.unskipTutorial)
                {
                    flags.Add("не пропускается");
                }

                if (tutorialToPreview.HasUnskipStages)
                {
                    flags.Add($"непропускаемые стадии: {string.Join(", ", tutorialToPreview.unskipStages)}");
                }

                if (tutorialToPreview.isSequence)
                {
                    flags.Add("в цепочке");
                }

                if (tutorialToPreview.showAboveHud)
                {
                    flags.Add("поверх HUD");
                }

                if (tutorialToPreview.blockHudInput)
                {
                    flags.Add("HUD заблокирован");
                }

                var suffix = flags.Count > 0 ? "  ·  " + string.Join(", ", flags) : string.Empty;

                return $"id {tutorialToPreview.id}  ·  окно «{prefabName}»{suffix}";
            }
        }

        [TabGroup(Tabs, TabPreview)]
        [PropertyOrder(20)]
        [FoldoutGroup(Tabs + "/" + TabPreview + "/Настройки как в геймплей-сцене")]
        [LabelText("Ortho size камеры")]
        [InfoBox("Значения сняты с геймплей-сцены. Ortho size получается из фона: (1080 / 70 PPU) / 2. " +
                 "Канвас там — Scale With Screen Size, Expand. Трогай, только если поменяли в проекте.")]
        [SerializeField]
        private float cameraOrthoSize = 7.714286f;

        private bool HasSelection => tutorialToPreview != null;

        private bool NoTutorialsFound => GetTutorialsCached().Count == 0;

        private string _cachedRoot;
        private List<TutorialViewSo> _cachedTutorials;

        /// <summary>
        /// Список туториалов с кэшем: и подсказка, и проверка «пусто» опрашиваются на каждой
        /// перерисовке окна, а AssetDatabase.FindAssets по большому проекту стоит дорого.
        /// </summary>
        private List<TutorialViewSo> GetTutorialsCached()
        {
            var root = GetSearchRoot();

            if (_cachedTutorials == null || _cachedRoot != root)
            {
                _cachedRoot = root;
                _cachedTutorials = FindTutorials(root).ToList();
            }

            return _cachedTutorials;
        }

        private void InvalidateTutorialsCache() => _cachedTutorials = null;

        /// <summary>
        /// Откуда взялся список окон — иначе непонятно, почему в нём именно эти штуки.
        /// </summary>
        private string PreviewHint
        {
            get
            {
                var root = GetSearchRoot();
                var count = GetTutorialsCached().Count;

                return $"Список беру из папки {root} — найдено {count} шт. (с подпапками).\n" +
                       "Открой префаб уровня, чтобы список собрался по нему.";
            }
        }

        #endregion

        private TutorialView _previewView;
        private bool _previewIsShow;
        private float _previewTime;
        private float _previewDuration;
        private double _lastTickTime;
        private bool _isTicking;

        [MenuItem("Tools/Creobit/TMN/Туториалы")]
        private static void OpenWindow()
        {
            var window = GetWindow<TutorialAuthoringWindow>();

            window.titleContent = new GUIContent("Туториалы");
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(760, 560);
        }

        #region Кнопки: создание

        [TabGroup(Tabs, TabCreate)]
        [Button("Создать туториал", ButtonSizes.Large)]
        [GUIColor(0.5f, 0.9f, 0.5f)]
        private void CreateTutorial()
        {
            if (!ValidateCreation(out var folder))
            {
                return;
            }

            var prefabPath = CreateWindowPrefab(folder);

            if (string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            var prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);

            if (string.IsNullOrEmpty(prefabGuid))
            {
                Debug.LogError($"[Туториалы] Префаб {prefabPath} не попал в базу — ссылку не записать.");
                return;
            }

            // Сначала помечаем префаб Addressable, потом создаём на него AssetReference: инспектор
            // PanelReference показывает ссылку только если ассет уже адресуемый.
            RegisterInAddressables(prefabGuid);

            var panelReference = CreateAsset<PanelReference>($"{folder}/{tutorialName}Reference.asset");
            AssignPanelReference(panelReference, prefabGuid);

            // Считаем до создания ассета: иначе поиск найдёт сам создаваемый SO с id по умолчанию.
            var nextId = GetNextFreeId();

            var tutorialViewSo = CreateAsset<TutorialViewSo>($"{folder}/{tutorialName}SO.asset");
            tutorialViewSo.panelReference = panelReference;
            tutorialViewSo.id = nextId;
            tutorialViewSo.pauseLevelTimer = pauseLevelTimer;
            tutorialViewSo.unskipTutorial = unskipTutorial;
            tutorialViewSo.showAboveHud = showAboveHud;
            tutorialViewSo.blockHudInput = blockHudInput;

            EditorUtility.SetDirty(panelReference);
            EditorUtility.SetDirty(tutorialViewSo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            VerifyPanelReference(panelReference, prefabGuid);

            InvalidateTutorialsCache();
            tutorialToPreview = tutorialViewSo;

            Selection.activeObject = tutorialViewSo;
            EditorGUIUtility.PingObject(tutorialViewSo);

            Debug.Log($"[Туториалы] Создан «{tutorialName}» в {folder} (id {tutorialViewSo.id}).");
        }

        private bool ValidateCreation(out string folder)
        {
            folder = null;

            if (string.IsNullOrWhiteSpace(targetFolder) || !AssetDatabase.IsValidFolder(targetFolder.TrimEnd('/')))
            {
                EditorUtility.DisplayDialog("Туториалы", "Выбери существующую папку уровня.", "Ок");
                return false;
            }

            if (string.IsNullOrWhiteSpace(tutorialName))
            {
                EditorUtility.DisplayDialog("Туториалы", "Впиши имя туториала.", "Ок");
                return false;
            }

            var parent = targetFolder.TrimEnd('/');
            var candidate = $"{parent}/{tutorialName}";

            if (AssetDatabase.IsValidFolder(candidate))
            {
                EditorUtility.DisplayDialog("Туториалы",
                    $"Папка «{tutorialName}» уже есть. Выбери другое имя.", "Ок");
                return false;
            }

            var guid = AssetDatabase.CreateFolder(parent, tutorialName);

            if (string.IsNullOrEmpty(guid))
            {
                EditorUtility.DisplayDialog("Туториалы", "Не удалось создать папку.", "Ок");
                return false;
            }

            folder = AssetDatabase.GUIDToAssetPath(guid);
            return true;
        }

        /// <summary>
        /// Собирает пустой, но полностью рабочий каркас окна с нуля. Никакого чужого арта:
        /// нейтральные плашки-заглушки, пустые стадии, кнопка «Ок» и служебные компоненты показа.
        /// </summary>
        private string CreateWindowPrefab(string folder)
        {
            var prefabPath = $"{folder}/{tutorialName}.prefab";

            // Пока собираем, держим корень под временным канвасом. Иначе Unity считает его
            // КОРНЕВЫМ Overlay-канвасом, сам управляет его RectTransform, и в префаб уезжает
            // нулевой размер с pivot (0,0) — окно потом не видно.
            var host = new GameObject("__TutorialBuildHost", typeof(RectTransform), typeof(Canvas));
            host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var root = new GameObject(tutorialName,
                typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(host.transform, false);

            // Растягиваем по родителю — ровно как у рабочих окон проекта.
            Stretch((RectTransform)root.transform);

            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            try
            {
                var panelData = root.AddComponent<Creobit.UI.PanelData>();
                var view = root.AddComponent<TutorialView>();

                // Окно — то, что реально анимируется и гасится. Живёт под корнем.
                var window = NewChild(root.transform, "Window", 900, 540);
                var windowGroup = window.gameObject.AddComponent<CanvasGroup>();
                view.mainRect = window;

                var background = BuildBackground(window);
                BuildStages(background);
                BuildOkButton(background, root);

                var switches = BuildVisibilitySwitches(root, window, windowGroup);
                BindPanelAnimations(panelData, switches.Show, switches.Hide);

                var steps = GetPresetSteps();

                if (steps != null)
                {
                    view.ApplyPreset(steps);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

                // Форсируем импорт: без него AssetPathToGUID сразу после сохранения возвращает
                // пустую строку, и ссылка на префаб с пометкой Addressable не проставляются.
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);

                return prefabPath;
            }
            finally
            {
                DestroyImmediate(host);
            }
        }

        private static RectTransform BuildBackground(RectTransform window)
        {
            // Полупрозрачная плашка-заглушка: дизайнер заменит спрайтом.
            var background = NewChild(window, "Background", 0, 0);
            Stretch(background);

            var image = background.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.14f, 0.85f);
            image.raycastTarget = true;

            return background;
        }

        private void BuildStages(RectTransform background)
        {
            var stages = Mathf.Max(1, stageCount);

            for (var stage = 1; stage <= stages; stage++)
            {
                var block = NewChild(background, $"Stage {stage}", 0, 0);
                Stretch(block);

                block.gameObject.AddComponent<CanvasGroup>();
                block.gameObject.AddComponent<TutorialStageElement>()
                    .EditorSetup(stage, TutorialStageHideMode.NextStage);

                var label = NewChild(block, "Text", 720, 220);
                label.anchoredPosition = new Vector2(0, 40);

                var tmp = label.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
                tmp.text = stages > 1
                    ? $"Stage {stage} of {stages} — replace me"
                    : "Tutorial text";
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 18;
                tmp.fontSizeMax = 48;
                tmp.color = Color.white;

                var picture = NewChild(block, "Image (replace)", 260, 180);
                picture.anchoredPosition = new Vector2(0, -140);

                var pictureImage = picture.gameObject.AddComponent<Image>();
                pictureImage.color = new Color(1f, 1f, 1f, 0.15f);
            }
        }

        private void BuildOkButton(RectTransform background, GameObject root)
        {
            // Кнопка видна всегда (стадия 0) и одна на все шаги: листает стадии, на последней закрывает.
            var button = NewChild(background, "OkButton", 260, 90);
            button.anchorMin = new Vector2(0.5f, 0f);
            button.anchorMax = new Vector2(0.5f, 0f);
            button.pivot = new Vector2(0.5f, 0f);
            button.anchoredPosition = new Vector2(0, 36);

            button.gameObject.AddComponent<CanvasGroup>();
            button.gameObject.AddComponent<TutorialStageElement>()
                .EditorSetup(0, TutorialStageHideMode.Never);

            var image = button.gameObject.AddComponent<Image>();
            image.color = new Color(0.30f, 0.55f, 0.90f, 1f);

            var uiButton = button.gameObject.AddComponent<Button>();
            uiButton.targetGraphic = image;

            var label = NewChild(button, "Text", 0, 0);
            Stretch(label);

            var tmp = label.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "Ok";
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize = 34;
            tmp.color = Color.white;

            // Мост живёт в окне: кнопке нужен он, чтобы листать стадии и закрываться.
            var bridge = root.GetComponentInChildren<TutorialBridge>(true)
                         ?? NewChild(((RectTransform)root.transform), "Bridges", 0, 0)
                             .gameObject.AddComponent<TutorialBridge>();

            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
                uiButton.onClick, bridge.NextStageOrHide);
        }

        /// <summary>
        /// Два служебных «выключателя» видимости. Общий UIController показывает и прячет окно
        /// только через них: без записей в PanelData.PanelAnimations он и вовсе падает.
        /// Fade нулевой длительности + колбэк SetActive — окно появляется и уходит по команде.
        /// Вся видимая анимация (всплытие, отскок) живёт отдельно, в TutorialView.
        /// </summary>
        private static (DOTweenAnimation Show, DOTweenAnimation Hide) BuildVisibilitySwitches(
            GameObject root, RectTransform window, CanvasGroup windowGroup)
        {
            var holder = NewChild(window, "Internal", 0, 0);

            var show = BuildSwitch(holder, "ShowSwitch", root, windowGroup, true);
            var hide = BuildSwitch(holder, "HideSwitch", root, windowGroup, false);

            return (show, hide);
        }

        private static DOTweenAnimation BuildSwitch(RectTransform parent, string name,
            GameObject root, CanvasGroup windowGroup, bool isShow)
        {
            var go = NewChild(parent, name, 0, 0).gameObject;

            var anim = go.AddComponent<DOTweenAnimation>();

            anim.targetIsSelf = false;
            anim.targetGO = windowGroup.gameObject;
            anim.tweenTargetIsTargetGO = true;
            anim.target = windowGroup;
            anim.targetType = DOTweenAnimation.TargetType.CanvasGroup;
            anim.forcedTargetType = DOTweenAnimation.TargetType.CanvasGroup;
            anim.animationType = DOTweenAnimation.AnimationType.Fade;
            anim.endValueFloat = isShow ? 1f : 0f;
            anim.duration = 0f;
            anim.isFrom = false;
            // Твин создаёт сам UIController (CreateTween при показе), автогенерация не нужна.
            anim.autoGenerate = false;
            anim.autoPlay = false;
            anim.autoKill = true;

            if (isShow)
            {
                anim.hasOnStart = true;
                anim.onStart = new UnityEngine.Events.UnityEvent();

                // Корень включаем ПЕРВЫМ. UIController при загрузке панели гасит именно корень,
                // и если его не поднять, окно останется невидимым, сколько ни включай внутренности.
                UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(
                    anim.onStart, root.SetActive, true);
                UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(
                    anim.onStart, windowGroup.gameObject.SetActive, true);
            }
            else
            {
                anim.hasOnComplete = true;
                anim.onComplete = new UnityEngine.Events.UnityEvent();

                UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(
                    anim.onComplete, windowGroup.gameObject.SetActive, false);
                UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(
                    anim.onComplete, root.SetActive, false);
            }

            return anim;
        }

        private static void BindPanelAnimations(Creobit.UI.PanelData panelData,
            DOTweenAnimation show, DOTweenAnimation hide)
        {
            // У PanelAnimationData закрытые сеттеры — пишем через сериализованные backing-поля.
            var serialized = new SerializedObject(panelData);
            var array = serialized.FindProperty("PanelAnimations");

            array.arraySize = 2;

            AssignAnimation(array.GetArrayElementAtIndex(0), PanelState.Show, show);
            AssignAnimation(array.GetArrayElementAtIndex(1), PanelState.Hide, hide);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignAnimation(SerializedProperty element, PanelState state,
            DOTweenAnimation animation)
        {
            element.FindPropertyRelative("<PanelState>k__BackingField").enumValueIndex = (int)state;
            element.FindPropertyRelative("<AnimationPrefab>k__BackingField").objectReferenceValue = animation;

            var sequence = element.FindPropertyRelative("<IsSequence>k__BackingField");

            if (sequence != null)
            {
                sequence.boolValue = false;
            }
        }

        private static RectTransform NewChild(Transform parent, string name, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);

            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private TutorialAnimationStep[] GetPresetSteps() => animationPreset switch
        {
            AnimationPreset.Fade => TutorialAnimationSet.PresetFade(),
            AnimationPreset.Grow => TutorialAnimationSet.PresetGrow(),
            AnimationPreset.FromBottom => TutorialAnimationSet.PresetFrom(TutorialAnimationDirection.Down),
            AnimationPreset.FromTop => TutorialAnimationSet.PresetFrom(TutorialAnimationDirection.Up),
            AnimationPreset.Bounce => TutorialAnimationSet.PresetBounce(),
            _ => null,
        };

        /// <summary>
        /// Пишет guid префаба прямо в сериализованное поле AssetReference. Через свойство
        /// присваивание до диска не доезжало — поле оставалось пустым, и в инспекторе была «None».
        /// </summary>
        private static void AssignPanelReference(PanelReference panelReference, string prefabGuid)
        {
            var serialized = new SerializedObject(panelReference);
            var guidProperty = serialized.FindProperty("<UIPanelReference>k__BackingField.m_AssetGUID");

            if (guidProperty == null)
            {
                // Запасной путь, если Addressables поменяют внутреннее имя поля.
                panelReference.UIPanelReference = new AssetReference(prefabGuid);
                EditorUtility.SetDirty(panelReference);

                Debug.LogWarning("[Туториалы] Не нашёл поле m_AssetGUID — записал ссылку через свойство.");
                return;
            }

            guidProperty.stringValue = prefabGuid;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(panelReference);
        }

        /// <summary>
        /// Ссылка на префаб — самое хрупкое место: без неё окно не грузится, а понять это можно
        /// только запустив игру. Поэтому проверяем сразу и говорим вслух.
        /// </summary>
        private static void VerifyPanelReference(PanelReference panelReference, string expectedGuid)
        {
            var actual = panelReference != null && panelReference.UIPanelReference != null
                ? panelReference.UIPanelReference.AssetGUID
                : null;

            if (actual == expectedGuid)
            {
                return;
            }

            Debug.LogError($"[Туториалы] Ссылка на префаб не записалась в " +
                           $"{AssetDatabase.GetAssetPath(panelReference)}. Ожидал {expectedGuid}, " +
                           $"получил «{actual}». Пропиши поле UI Panel Reference руками.");
        }

        private static void RegisterInAddressables(string prefabGuid)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            var group = settings.FindGroup(RuntimeConstants.AddressablesGroupName)
                        ?? settings.CreateGroup(RuntimeConstants.AddressablesGroupName, false, false, true, null,
                            typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));

            var entry = settings.FindAssetEntry(prefabGuid) ?? settings.CreateOrMoveEntry(prefabGuid, group);

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        private int GetNextFreeId()
        {
            var searchRoot = string.IsNullOrWhiteSpace(targetFolder) ? "Assets" : targetFolder.TrimEnd('/');

            var maxId = -1;

            foreach (var so in FindTutorials(searchRoot))
            {
                maxId = Mathf.Max(maxId, so.id);
            }

            return maxId + 1;
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = CreateInstance<T>();

            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));

            return asset;
        }

        #endregion

        #region Кнопки: превью

        [PropertyOrder(6)]
        [TabGroup(Tabs, TabPreview)]
        [Button("Обновить список")]
        private void RefreshList()
        {
            InvalidateTutorialsCache();
        }

        [PropertyOrder(6.5f)]
        [TabGroup(Tabs, TabPreview)]
        [Button("Починить ссылку на префаб")]
        [EnableIf(nameof(HasSelection))]
        [InfoBox("Если в Reference стоит «None» — нажми: найдёт префаб рядом с SO, пометит его " +
                 "Addressable и пропишет ссылку.")]
        private void RepairPanelReference()
        {
            if (tutorialToPreview == null)
            {
                return;
            }

            var soPath = AssetDatabase.GetAssetPath(tutorialToPreview);
            var folder = Path.GetDirectoryName(soPath)?.Replace('\\', '/');

            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            var prefabGuid = AssetDatabase.FindAssets("t:Prefab", new[] { folder }).FirstOrDefault();

            if (string.IsNullOrEmpty(prefabGuid))
            {
                EditorUtility.DisplayDialog("Туториалы",
                    $"Рядом с {tutorialToPreview.name} нет префаба окна — чинить нечего.", "Ок");
                return;
            }

            if (tutorialToPreview.panelReference == null)
            {
                EditorUtility.DisplayDialog("Туториалы",
                    "У SO не заполнено поле panelReference — укажи Reference-ассет вручную.", "Ок");
                return;
            }

            RegisterInAddressables(prefabGuid);
            AssignPanelReference(tutorialToPreview.panelReference, prefabGuid);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            VerifyPanelReference(tutorialToPreview.panelReference, prefabGuid);

            Debug.Log($"[Туториалы] Ссылка починена: {AssetDatabase.GUIDToAssetPath(prefabGuid)}");
        }

        [PropertyOrder(7)]
        [TabGroup(Tabs, TabPreview)]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("Стадия")]
        [ShowIf(nameof(HasPreviewStages))]
        private string PreviewStageInfo =>
            PreviewView == null ? string.Empty : $"{PreviewView.CurrentStage} из {PreviewView.MaxStage}";

        [PropertyOrder(8)]
        [TabGroup(Tabs, TabPreview)]
        [Button("◀ Предыдущая стадия")]
        [EnableIf(nameof(HasPreviewStages))]
        private void PreviewPrevStage() => StepPreviewStage(-1);

        [PropertyOrder(9)]
        [TabGroup(Tabs, TabPreview)]
        [Button("Следующая стадия ▶")]
        [EnableIf(nameof(HasPreviewStages))]
        private void PreviewNextStage() => StepPreviewStage(1);

        private bool HasPreviewStages => PreviewView != null && PreviewView.MaxStage > 0;

        private void StepPreviewStage(int delta)
        {
            var view = PreviewView;

            if (view == null)
            {
                return;
            }

            view.ApplyStagePreview(view.CurrentStage + delta);

            SceneView.RepaintAll();
        }

        [PropertyOrder(3.5f)]
        [TabGroup(Tabs, TabPreview)]
        [Button("Сохранить окно в префаб", ButtonSizes.Large)]
        [GUIColor(0.95f, 0.8f, 0.4f)]
        [EnableIf(nameof(HasPreview))]
        [InfoBox("Подвинул окно в сцене — жми сюда. Запомнит положение и запишет правки в префаб.")]
        private void ApplyWindowToPrefab()
        {
            var view = PreviewView;

            if (view == null)
            {
                return;
            }

            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(view.gameObject);

            if (root == null)
            {
                EditorUtility.DisplayDialog("Туториалы",
                    "Окно в сцене не связано с префабом — сохранять некуда.", "Ок");
                return;
            }

            // Фиксируем положение: иначе SetPosition при показе вернёт окно на старое место.
            view.GetPosition();

            // Стадии гасят друг друга прозрачностью. Если применить как есть, в префаб запечётся
            // «видна только текущая стадия», и остальные окажутся невидимыми при открытии префаба.
            foreach (var element in root.GetComponentsInChildren<TutorialStageElement>(true))
            {
                element.SetAlpha(1f);
                element.SetInteractive(true);
            }

            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);

            // Возвращаем показ текущей стадии, чтобы дальше было видно то же, что и до сохранения.
            view.ApplyStagePreview(view.CurrentStage);

            Debug.Log($"[Туториалы] Правки записаны в префаб «{root.name}».");
        }

        [PropertyOrder(4)]
        [TabGroup(Tabs, TabPreview)]
        [Button("Проиграть появление")]
        [EnableIf(nameof(HasPreview))]
        private void PlayShowAnimation() => PlayAnimation(true);

        [PropertyOrder(5)]
        [TabGroup(Tabs, TabPreview)]
        [Button("Проиграть исчезновение")]
        [EnableIf(nameof(HasPreview))]
        private void PlayHideAnimation() => PlayAnimation(false);

        private bool HasPreview => PreviewView != null;

        /// <summary>
        /// Окно в открытой сцене. Не кэшируем намертво: обычное поле окна редактора теряется при
        /// каждой перекомпиляции, и кнопки внезапно гасли. Ищем в сцене по требованию.
        /// </summary>
        private TutorialView PreviewView
        {
            get
            {
                if (_previewView != null)
                {
                    return _previewView;
                }

                _previewView = FindFirstObjectByType<TutorialView>(FindObjectsInactive.Include);

                return _previewView;
            }
        }

        [PropertyOrder(2)]
        [TabGroup(Tabs, TabPreview)]
        [Title("Тест-сцена")]
        [LabelText("Префаб уровня")]
        [AssetsOnly]
        [InfoBox("Собирает сцену: настоящий уровень + камера и канвас как в геймплей-сцене + окно " +
                 "поверх. Смотришь в Game-view, Play жать не нужно — поэтому скрипты уровня не " +
                 "стартуют и не ругаются. Живого HUD (ресурсы, таски) не будет: он поднимается " +
                 "только полным запуском игры.")]
        [SerializeField]
        private GameObject levelPrefabForTest;

        [PropertyOrder(21)]
        [TabGroup(Tabs, TabPreview)]
        [FoldoutGroup(Tabs + "/" + TabPreview + "/Настройки как в геймплей-сцене")]
        [LabelText("Отдаление камеры (Z)")]
        [SerializeField]
        private float cameraDistance = 17f;

        [PropertyOrder(22)]
        [TabGroup(Tabs, TabPreview)]
        [FoldoutGroup(Tabs + "/" + TabPreview + "/Настройки как в геймплей-сцене")]
        [LabelText("Реф. разрешение канваса")]
        [Tooltip("Canvas Scaler в геймплей-сцене: Scale With Screen Size, Expand, 1920×1080.")]
        [SerializeField]
        private Vector2 canvasReferenceResolution = new(1920f, 1080f);

        [PropertyOrder(3)]
        [TabGroup(Tabs, TabPreview)]
        [Button("Открыть тест-сцену", ButtonSizes.Large)]
        [GUIColor(0.5f, 0.7f, 0.95f)]
        private void OpenTestScene()
        {
            if (tutorialToPreview == null)
            {
                EditorUtility.DisplayDialog("Туториалы", "Сначала выбери окно туториала выше.", "Ок");
                return;
            }

            var windowPrefab = LoadWindowPrefab(tutorialToPreview);

            if (windowPrefab == null)
            {
                EditorUtility.DisplayDialog("Туториалы",
                    "У этого туториала не найден префаб окна. Проверь Panel Reference в SO.", "Ок");
                return;
            }

            var levelPrefab = ResolveLevelPrefab();

            if (levelPrefab == null)
            {
                EditorUtility.DisplayDialog("Туториалы",
                    "Не понял, какой уровень показывать. Укажи префаб уровня в поле выше " +
                    "или открой префаб уровня перед нажатием.", "Ок");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // Превью в префаб-моде больше не нужно — сцена откроется поверх и стадия поедет туда.
            StopTicking();

            _previewView = TutorialPreviewSceneBuilder.BuildAndOpen(
                windowPrefab, levelPrefab, tutorialToPreview.name,
                cameraOrthoSize, canvasReferenceResolution, cameraDistance);
        }

        /// <summary>
        /// Уровень берём из поля, а если пусто — из открытого префаба уровня.
        /// </summary>
        private GameObject ResolveLevelPrefab()
        {
            if (levelPrefabForTest != null)
            {
                return levelPrefabForTest;
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            if (stage == null || string.IsNullOrEmpty(stage.assetPath))
            {
                return null;
            }

            levelPrefabForTest = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);

            return levelPrefabForTest;
        }

        #endregion

        #region Внутреннее: превью

        private static GameObject LoadWindowPrefab(TutorialViewSo tutorialViewSo)
        {
            var guid = tutorialViewSo.panelReference != null && tutorialViewSo.panelReference.UIPanelReference != null
                ? tutorialViewSo.panelReference.UIPanelReference.AssetGUID
                : null;

            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private void PlayAnimation(bool isShow)
        {
            var view = PreviewView;

            if (view == null)
            {
                return;
            }

            _previewIsShow = isShow;
            _previewTime = 0f;
            _previewDuration = view.GetAnimationDuration(isShow);

            view.PrepareAnimationPreview();

            if (_previewDuration <= 0f)
            {
                view.SampleAnimation(0f, isShow);
                SceneView.RepaintAll();
                return;
            }

            _lastTickTime = EditorApplication.timeSinceStartup;

            if (!_isTicking)
            {
                EditorApplication.update += TickPreview;
                _isTicking = true;
            }
        }

        private void TickPreview()
        {
            if (_previewView == null)
            {
                StopTicking();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            _previewTime += (float)(now - _lastTickTime);
            _lastTickTime = now;

            _previewView.SampleAnimation(Mathf.Min(_previewTime, _previewDuration), _previewIsShow);

            SceneView.RepaintAll();

            if (_previewTime >= _previewDuration)
            {
                StopTicking();
            }
        }

        private void StopTicking()
        {
            if (!_isTicking)
            {
                return;
            }

            EditorApplication.update -= TickPreview;
            _isTicking = false;
        }

        #endregion

        #region Поиск туториалов

        private IEnumerable<ValueDropdownItem<TutorialViewSo>> GetLevelTutorials()
        {
            // Слэш в подписи Odin превращает в раскрывающуюся группу — туториалы сами
            // разложатся по папкам, в которых лежат.
            return GetTutorialsCached()
                .OrderBy(so => so.name)
                .Select(so => new ValueDropdownItem<TutorialViewSo>(
                    $"{GetGroupFolder(so)}{so.name}   ·   id {so.id}", so));
        }

        /// <summary>
        /// Папка-группа, в которой лежит туториал: у каждого окна своя подпапка,
        /// поэтому берём на уровень выше — получается «LevelTutorial/», «LevelTutorial new/».
        /// </summary>
        private static string GetGroupFolder(TutorialViewSo tutorialViewSo)
        {
            var ownFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(tutorialViewSo));
            var groupFolder = string.IsNullOrEmpty(ownFolder) ? null : Path.GetDirectoryName(ownFolder);
            var name = string.IsNullOrEmpty(groupFolder) ? null : Path.GetFileName(groupFolder);

            return string.IsNullOrEmpty(name) ? string.Empty : name + "/";
        }

        /// <summary>
        /// Где искать туториалы: сначала папка открытого префаба уровня, иначе выбранная папка.
        /// </summary>
        private string GetSearchRoot()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            if (stage != null && !string.IsNullOrEmpty(stage.assetPath))
            {
                var directory = Path.GetDirectoryName(stage.assetPath);

                if (!string.IsNullOrEmpty(directory))
                {
                    return directory.Replace('\\', '/');
                }
            }

            return string.IsNullOrWhiteSpace(targetFolder) ? "Assets" : targetFolder.TrimEnd('/');
        }

        private static IEnumerable<TutorialViewSo> FindTutorials(string root)
        {
            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root))
            {
                root = "Assets";
            }

            return AssetDatabase.FindAssets($"t:{nameof(TutorialViewSo)}", new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TutorialViewSo>)
                .Where(so => so != null)
                .ToList();
        }

        #endregion
    }
}
