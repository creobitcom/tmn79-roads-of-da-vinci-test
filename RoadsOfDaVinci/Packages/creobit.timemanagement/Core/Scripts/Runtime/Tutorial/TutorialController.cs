using System;
using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Localization;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial
{
    public class TutorialController : ITutorialController, IReloadable
    {
        private IUIController _uiController;
        private GameplaySceneReferences _gameplaySceneReferences;
        private IGameplayInputSystem _inputSystem;
        private IReloadController _reloadController;
        private ILevelController _levelController;
        private ILevelLoader _levelLoader;
        private ISaveController _saveController;
        private IObjectResolver _objectResolver;
        private GameplaySceneReferences _sceneReferences;
        private ILevelTimer _levelTimer;

        public List<TutorialEvent> onTutorialShowEvents { get; set; } = new();
        public List<TutorialEvent> onTutorialHideEvents { get; set; } = new();
        public List<TutorialViewLevelData> tutorialViewLevelData { get; set; } = new();
        private Dictionary<TutorialViewSo, HashSet<int>> ActionsToOpen { get; set; } = new();
        private Dictionary<TutorialViewSo, HashSet<int>> ActionsToClose { get; set; } = new();
        private readonly Dictionary<int, HashSet<int>> _stageOpenKeys = new();
        private readonly Dictionary<int, HashSet<int>> _stageCloseKeys = new();
        public ReactiveProperty<bool> isEnable { get; set; } = new(true);
        public ReactiveProperty<bool> isShowing { get; } = new(false);

        private TutorialViewSo _currentTutorial;
        private TutorialView _currentTutorialPanel;
        private bool _isActive;
        private bool _currentIsHint;
        private bool _suppressTutorialShows;
        private bool _tutorialsBlockedForCurrentLevel;

        // ShowHint is fire-and-forget; a second click can start another show while GetPanel
        // is still awaiting and leave an orphan panel the controller no longer tracks.
        private int _tutorialShowGeneration;
        private bool _hintShowInFlight;
        private TutorialViewSo _hintShowInFlightSo;

        private TutorialViewSo _parkedTutorialSo;
        private TutorialView _parkedTutorialPanel;
        private bool _hasParkedTutorial;

        private readonly List<TutorialArrow> _arrows = new();
        private ObjectPool<TutorialArrow> _arrowPool;
        private Canvas _tutorialCanvas;
        private Canvas _topUiLayerCanvas;

        private readonly HashSet<TutorialViewSo> _shownTutorials = new();
        private readonly HashSet<TutorialViewSo> _retainedHintPanels = new();
        private readonly HashSet<TutorialStageEvent> _processedStageEvents = new();

        // Какие объекты текущей стадии уже нажали. Нужен, когда стадия ждёт несколько объектов.
        private readonly HashSet<int> _clickedStageHighlights = new();
        private readonly Dictionary<int, IDisposable> _arrowMoveSubscriptions = new();

        private GameObject _viewEnableButton;
        private GameObject _viewDisableButton;

        // HUD: кто просит блокировку и что мы для этого погасили.
        private readonly HashSet<TutorialViewSo> _hudBlockSources = new();
        private readonly List<GraphicRaycaster> _disabledHudRaycasters = new();
        private readonly List<TutorialHudRaycaster> _filteringHudRaycasters = new();

        // Исходная сортировка окна — чтобы вернуть её после показа поверх HUD.
        private Canvas _overlayCanvas;
        private bool _overlayPreviousOverride;
        private int _overlayPreviousOrder;

        private bool TutorialToggleAppliesNextLevel =>
            _gameplaySceneReferences != null
            && _gameplaySceneReferences.GameplaySettings != null
            && _gameplaySceneReferences.GameplaySettings.TutorialToggleAppliesNextLevel;

        [Inject]
        public void Construct(IUIController uiController,
            GameplaySceneReferences gameplaySceneReferences,
            IObjectResolver resolver,
            IGameplayInputSystem inputSystem,
            IReloadController reloadController,
            ILevelController levelController,
            IObjectResolver objectResolver,
            GameplaySceneReferences sceneReferences,
            ISaveController saveController,
            ILevelLoader levelLoader,
            ILevelTimer levelTimer)
        {
            _levelTimer = levelTimer;
            _gameplaySceneReferences = gameplaySceneReferences;
            _inputSystem = inputSystem;
            _uiController = uiController;
            _reloadController = reloadController;
            _levelController = levelController;
            _saveController = saveController;
            _objectResolver = objectResolver;
            _sceneReferences = sceneReferences;
            _levelLoader = levelLoader;

            resolver.Inject(_uiController); // todo resolve
        }
        

        public UniTask Load()
        {
            // Слушаем клики централизованно: так «нажал на объект → следующая стадия» настраивается
            // одной галочкой в мосте, без проводки событий на каждом объекте уровня.
            _inputSystem.PrimaryActionPerformed -= PrimaryActionPerformedHandler;
            _inputSystem.PrimaryActionPerformed += PrimaryActionPerformedHandler;

            _reloadController.AddReloadableObject(this, 0);
            _tutorialCanvas = _gameplaySceneReferences.GameplayCanvasLayers[1];
            _topUiLayerCanvas = _gameplaySceneReferences.GameplayCanvasLayers.Count > 3
                ? _gameplaySceneReferences.GameplayCanvasLayers[3]
                : null;

            _arrowPool = new ObjectPool<TutorialArrow>(CreateTutorialArrow);
            SetEnable(_saveController.CurrentSaveData.ProfileData.TutorialActive);

            _levelLoader.LevelBaseSO.Skip(1).Subscribe(OnLevelStarted);

            return UniTask.CompletedTask;
        }

        private void OnLevelStarted(LevelBaseSO levelBaseSO)
        {
            if (TutorialToggleAppliesNextLevel || levelBaseSO == null || levelBaseSO.EnableTutorial)
            {
                return;
            }

            if (_viewDisableButton != null)
            {
                _viewDisableButton.SetActive(false);
            }

            if (_viewEnableButton != null)
            {
                _viewEnableButton.SetActive(false);
            }
        }

        public void ShowTutorial(TutorialViewSo tutorialViewSO)
        {
            ShowTutorialPreemptingHint(tutorialViewSO, 1).Forget();
        }

        public void ShowTutorialAtStage(TutorialViewSo tutorialViewSO, int stage)
        {
            ShowTutorialPreemptingHint(tutorialViewSO, stage).Forget();
        }

        public TutorialViewSo CurrentHint => _isActive && _currentIsHint ? _currentTutorial : null;

        public void ShowHint(TutorialViewSo tutorialViewSO, float durationSeconds = 0f)
        {
            ShowHintInternal(tutorialViewSO, durationSeconds).Forget();
        }

        private async UniTaskVoid ShowTutorialPreemptingHint(TutorialViewSo tutorialViewSO, int stage)
        {
            if (tutorialViewSO == null)
            {
                return;
            }

            var generation = ++_tutorialShowGeneration;

            await DismissActiveHintAsync(restoreParked: false);
            ClearParkedTutorial();

            if (generation != _tutorialShowGeneration)
            {
                return;
            }

            await ShowTutorialInternal(tutorialViewSO, stage, false, generation);
        }

        private async UniTask DismissActiveHintAsync(bool restoreParked = true)
        {
            _hintShowInFlight = false;
            _hintShowInFlightSo = null;

            if (!_isActive || !_currentIsHint)
            {
                if (restoreParked)
                {
                    RestoreParkedTutorial();
                }

                return;
            }

            var hintSo = _currentTutorial;
            var panel = _currentTutorialPanel;
            var hiddenStage = panel != null ? panel.CurrentStage : 0;

            SetActive(false);
            _currentIsHint = false;
            _currentTutorialPanel = null;
            _currentTutorial = null;

            if (panel != null)
            {
                panel.StageChanged -= StageChangedHandler;
                panel.StageHidden -= StageHiddenHandler;
            }

            _levelTimer.ResumeLevelTimer(this);
            RestoreHudOverlay();

            if (hintSo != null)
            {
                SetHudBlocked(hintSo, false);
                _shownTutorials.Remove(hintSo);
                _inputSystem.IsActionAvailable = true;

                if (hiddenStage > 0)
                {
                    ProcessStageEvents(hintSo, hiddenStage);
                }

                ProcessTutorialEvents(onTutorialHideEvents, hintSo);
            }
            else
            {
                _inputSystem.IsActionAvailable = true;
            }

            if (panel != null && panel.gameObject.activeInHierarchy && !panel.IsHidden)
            {
                await panel.PlayHide();
            }

            if (IsPanelOwnedByCurrentShow(hintSo, panel))
            {
                return;
            }

            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }

            if (hintSo != null)
            {
                SoftHideHintPanel(hintSo);
            }

            if (restoreParked)
            {
                RestoreParkedTutorial();
            }
        }

        private void ParkCurrentTutorialForHint()
        {
            if (!_isActive || _currentIsHint || _currentTutorial == null)
            {
                return;
            }

            _parkedTutorialSo = _currentTutorial;
            _parkedTutorialPanel = _currentTutorialPanel;
            _hasParkedTutorial = true;

            if (_currentTutorialPanel != null)
            {
                _currentTutorialPanel.StageChanged -= StageChangedHandler;
                _currentTutorialPanel.StageHidden -= StageHiddenHandler;
            }

            SetActive(false);
            _currentIsHint = false;
            _currentTutorial = null;
            _currentTutorialPanel = null;
            _inputSystem.IsActionAvailable = true;
        }

        private void RestoreParkedTutorial()
        {
            if (!_hasParkedTutorial)
            {
                return;
            }

            _currentTutorial = _parkedTutorialSo;
            _currentTutorialPanel = _parkedTutorialPanel;
            _currentIsHint = false;
            _isActive = true;
            isShowing.Value = _currentTutorialPanel != null && !_currentTutorialPanel.IsHidden;

            _parkedTutorialSo = null;
            _parkedTutorialPanel = null;
            _hasParkedTutorial = false;

            if (_currentTutorialPanel != null)
            {
                _currentTutorialPanel.StageChanged -= StageChangedHandler;
                _currentTutorialPanel.StageChanged += StageChangedHandler;
                _currentTutorialPanel.StageHidden -= StageHiddenHandler;
                _currentTutorialPanel.StageHidden += StageHiddenHandler;
            }
        }

        private void ClearParkedTutorial()
        {
            if (!_hasParkedTutorial)
            {
                return;
            }

            var parkedSo = _parkedTutorialSo;
            var parkedPanel = _parkedTutorialPanel;

            _parkedTutorialSo = null;
            _parkedTutorialPanel = null;
            _hasParkedTutorial = false;

            if (parkedPanel != null)
            {
                parkedPanel.gameObject.SetActive(false);
            }

            if (parkedSo != null)
            {
                _uiController.HidePanel(parkedSo.panelReference, _tutorialCanvas.transform);
                _uiController.UnloadPanel(parkedSo.panelReference);
            }
        }

        private async UniTaskVoid ShowHintInternal(TutorialViewSo tutorialViewSO, float durationSeconds)
        {
            if (tutorialViewSO == null)
            {
                return;
            }

            // Same hint already open or mid-open — ignore re-clicks (don't hide/show again).
            if (_isActive && _currentIsHint && _currentTutorial == tutorialViewSO)
            {
                return;
            }

            if (_hintShowInFlight && _hintShowInFlightSo == tutorialViewSO)
            {
                return;
            }

            var generation = ++_tutorialShowGeneration;
            _hintShowInFlight = true;
            _hintShowInFlightSo = tutorialViewSO;

            try
            {
                if (_isActive && !_currentIsHint)
                {
                    // HideWindow: паркуем тутор, не закрывая сессию — GoNextStage потом вернёт его.
                    if (_currentTutorialPanel != null && _currentTutorialPanel.IsHidden)
                    {
                        ParkCurrentTutorialForHint();
                    }
                    else
                    {
                        return;
                    }
                }
                else if (_isActive && _currentIsHint)
                {
                    _suppressTutorialShows = true;

                    try
                    {
                        await HideTutorialInternal(_currentTutorial, true);
                    }
                    finally
                    {
                        _suppressTutorialShows = false;
                    }

                    if (generation != _tutorialShowGeneration)
                    {
                        return;
                    }
                }

                var shown = await ShowTutorialInternal(tutorialViewSO, 1, true, generation);

                if (generation != _tutorialShowGeneration || !shown || durationSeconds <= 0f)
                {
                    return;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(durationSeconds), ignoreTimeScale: true);

                if (generation != _tutorialShowGeneration)
                {
                    return;
                }

                if (_isActive && _currentTutorial == tutorialViewSO)
                {
                    HideHint();
                }
            }
            finally
            {
                if (generation == _tutorialShowGeneration)
                {
                    _hintShowInFlight = false;
                    _hintShowInFlightSo = null;
                }
            }
        }

        public void HideHint()
        {
            if (!_isActive || !_currentIsHint)
            {
                return;
            }

            DismissActiveHintAsync(restoreParked: true).Forget();
        }

        private bool IsTutorialShowBlocked(TutorialViewSo tutorialViewSO, int stage, bool isHint)
        {
            if (tutorialViewSO.unskipTutorial || tutorialViewSO.IsStageUnskippable(stage))
            {
                return false;
            }

            if (!TutorialToggleAppliesNextLevel)
            {
                return !isEnable.Value;
            }

            return !isHint && (!isEnable.Value || _tutorialsBlockedForCurrentLevel);
        }

        private bool AreTutorialsSkipped() =>
            !isEnable.Value || (TutorialToggleAppliesNextLevel && _tutorialsBlockedForCurrentLevel);

        private async UniTask<bool> ShowTutorialInternal(
            TutorialViewSo tutorialViewSO,
            int initialStage,
            bool isHint,
            int? expectedGeneration = null)
        {
            if (_suppressTutorialShows || _isActive || tutorialViewSO == null
                || _shownTutorials.Contains(tutorialViewSO))
            {
                return false;
            }

            // Окна с unskipTutorial показываются даже когда игрок отключил туториалы в настройках.
            if (IsTutorialShowBlocked(tutorialViewSO, initialStage, isHint))
            {
                return false;
            }

            ActionsToOpen.TryAdd(tutorialViewSO, new HashSet<int>());
            if (!ActionsToOpen[tutorialViewSO].SetEquals(tutorialViewSO.actionsToOpen.ToHashSet()))
                return false;

            var showGeneration = expectedGeneration ?? ++_tutorialShowGeneration;

            ActionsToOpen.Clear();
            _stageOpenKeys.Clear();
            _stageCloseKeys.Clear();
            _clickedStageHighlights.Clear();
            SetActive(true);
            _currentIsHint = isHint;
            _currentTutorial = tutorialViewSO;

            _shownTutorials.Add(tutorialViewSO);
            _retainedHintPanels.Remove(tutorialViewSO);

            if (tutorialViewSO.pauseLevelTimer)
            {
                _levelTimer.PauseLevelTimer(this);
            }

            _uiController.ShowPanel(tutorialViewSO.panelReference, _tutorialCanvas.transform);

            var panelData = await _uiController.GetPanel(tutorialViewSO.panelReference);
            var panel = panelData != null ? panelData.GetComponent<TutorialView>() : null;

            if (isHint && panel != null)
            {
                panel.SetWindowRaycastsEnabled(false);
            }

            // Hide / a newer show may have finished while we awaited panel load.
            if (panel == null
                || showGeneration != _tutorialShowGeneration
                || !_isActive
                || _currentTutorial != tutorialViewSO)
            {
                ReleaseStaleShow(tutorialViewSO, panel, showGeneration);
                return false;
            }

            // Hide may have SetActive(false); DOTween ShowSwitch is often isValid=0 and won't turn it back on.
            panel.gameObject.SetActive(true);
            panel.SetPosition();
            _currentTutorialPanel = panel;
            isShowing.Value = !isHint;

            // Подписка до PlayShow: тот сбрасывает стадию на первую и это уже смена стадии.
            panel.StageChanged -= StageChangedHandler;
            panel.StageChanged += StageChangedHandler;
            panel.StageHidden -= StageHiddenHandler;
            panel.StageHidden += StageHiddenHandler;

            panel.PlayShowAtStage(initialStage).Forget();

            ApplyStageBehaviour(tutorialViewSO, panel);

            UpdateHighlightedObservableObject();

            ActivateDeactivatePrimaryAction(tutorialViewLevelData, true, tutorialViewSO);
            ProcessTutorialEvents(onTutorialShowEvents, tutorialViewSO);

            return true;
        }

        private void ReleaseStaleShow(TutorialViewSo tutorialViewSO, TutorialView panel, int showGeneration)
        {
            if (_currentTutorial != tutorialViewSO)
            {
                _shownTutorials.Remove(tutorialViewSO);
            }
            else if (_isActive && showGeneration == _tutorialShowGeneration)
            {
                SetActive(false);
                _currentIsHint = false;
                _currentTutorial = null;
                _currentTutorialPanel = null;
                _shownTutorials.Remove(tutorialViewSO);
                _levelTimer.ResumeLevelTimer(this);
                _inputSystem.IsActionAvailable = true;
            }

            DiscardStaleTutorialPanel(tutorialViewSO, panel);
        }

        private bool IsPanelOwnedByCurrentShow(TutorialViewSo tutorialViewSO, TutorialView panel)
        {
            if (panel != null && _currentTutorialPanel == panel)
            {
                return true;
            }

            if (!_isActive || _currentTutorial == null || tutorialViewSO == null)
            {
                return false;
            }

            if (_currentTutorial == tutorialViewSO)
            {
                return true;
            }

            return _currentTutorial.panelReference?.UIPanelReference != null
                   && tutorialViewSO.panelReference?.UIPanelReference != null
                   && _currentTutorial.panelReference.UIPanelReference.AssetGUID
                      == tutorialViewSO.panelReference.UIPanelReference.AssetGUID;
        }

        private void DiscardStaleTutorialPanel(TutorialViewSo tutorialViewSO, TutorialView panel)
        {
            if (tutorialViewSO == null || panel == null)
            {
                return;
            }

            // Another live show already owns this panel instance — leave it alone.
            if (IsPanelOwnedByCurrentShow(tutorialViewSO, panel))
            {
                return;
            }

            panel.SetWindowRaycastsEnabled(false);
            panel.gameObject.SetActive(false);

            SoftHideHintPanel(tutorialViewSO);
        }

        public void HideTutorial(TutorialViewSo tutorialViewSO = null, bool force = false)
        {
            HideTutorialInternal(tutorialViewSO, force).Forget();
        }

        private async UniTask<bool> HideTutorialInternal(TutorialViewSo tutorialViewSO = null, bool force = false)
        {
            if (!_isActive)
                return false;

            if (tutorialViewSO == null)
            {
                tutorialViewSO = _currentTutorial;
            }

            if (tutorialViewSO != _currentTutorial)
            {
                return false;
            }

            if (!force)
            {
                ActionsToClose.TryAdd(tutorialViewSO, new HashSet<int>());

                if (!ActionsToClose[tutorialViewSO].SetEquals(tutorialViewSO.actionsToClose.ToHashSet()))
                    return false;

                if (!CanHideCurrentStage(_currentTutorialPanel))
                    return false;
            }

            var wasHint = _currentIsHint;

            SetActive(false);
            _currentIsHint = false;
            var hiddenStage = _currentTutorialPanel != null ? _currentTutorialPanel.CurrentStage : 0;

            // Стадия может переопределить pauseLevelTimer из SO, поэтому снимаем свой источник
            // безусловно. Если паузу держит другой источник, LevelTimer её сохранит.
            _levelTimer.ResumeLevelTimer(this);

            if (_currentTutorialPanel != null)
            {
                _currentTutorialPanel.StageChanged -= StageChangedHandler;
                _currentTutorialPanel.StageHidden -= StageHiddenHandler;
            }

            SetHudBlocked(tutorialViewSO, false);
            RestoreHudOverlay();

            var hidePanelTask = HidePanelAfterAnimation(tutorialViewSO, _currentTutorialPanel, unload: !wasHint);

            UpdateHighlightedObservableObject();

            if (wasHint)
            {
                _inputSystem.IsActionAvailable = true;
            }
            else
            {
                ActivateDeactivatePrimaryAction(tutorialViewLevelData, false, tutorialViewSO);
            }

            if (hiddenStage > 0)
            {
                ProcessStageEvents(tutorialViewSO, hiddenStage);
            }

            ProcessTutorialEvents(onTutorialHideEvents, tutorialViewSO);

            if (!wasHint)
            {
                _stageOpenKeys.Clear();
            _stageCloseKeys.Clear();
            }

            if (wasHint)
            {
                _shownTutorials.Remove(tutorialViewSO);
            }

            await hidePanelTask;

            return true;
        }

        // Прятать панель через UI-контроллер можно только ПОСЛЕ нашей анимации. В шаблонах туториалов
        // штатная hide-анимация панели стоит на duration 0, а её onComplete делает SetActive(false)
        // корню окна — позови её раньше, и анимировать будет уже нечего: окно погаснет мгновенно.
        // Выгрузка ассета тоже ждёт, иначе панель исчезнет из памяти посреди анимации.
        // Если анимации не заданы, ожидание завершается синхронно и порядок остаётся прежним.
        private async UniTask HidePanelAfterAnimation(TutorialViewSo tutorialViewSO, TutorialView panel,
            bool unload = true)
        {
            // Окно уже спрятано временно — второй раз анимировать нечего, иначе оно моргнёт.
            if (panel != null && !panel.IsHidden)
            {
                await panel.PlayHide();
            }

            // За время анимации окно могли показать заново — тогда прятать нечего.
            if (_isActive && _currentTutorial == tutorialViewSO)
            {
                return;
            }

            // HideSwitch DOTween often has isValid=0, so its onComplete SetActive(false) never runs.
            // UnloadPanel also does not Destroy — an invisible active panel keeps eating world clicks.
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }

            if (unload)
            {
                _uiController.HidePanel(tutorialViewSO.panelReference, _tutorialCanvas.transform);
                _uiController.UnloadPanel(tutorialViewSO.panelReference);
                _retainedHintPanels.Remove(tutorialViewSO);
            }
            else
            {
                SoftHideHintPanel(tutorialViewSO);
            }
        }

        private void SoftHideHintPanel(TutorialViewSo hintSo)
        {
            if (hintSo?.panelReference == null || _tutorialCanvas == null)
            {
                return;
            }

            _uiController.HidePanel(hintSo.panelReference, _tutorialCanvas.transform);
            _retainedHintPanels.Add(hintSo);
        }

        private void UnloadRetainedHintPanels()
        {
            if (_retainedHintPanels.Count == 0)
            {
                return;
            }

            foreach (var hintSo in _retainedHintPanels)
            {
                if (hintSo?.panelReference == null)
                {
                    continue;
                }

                if (_tutorialCanvas != null)
                {
                    _uiController.HidePanel(hintSo.panelReference, _tutorialCanvas.transform);
                }

                _uiController.UnloadPanel(hintSo.panelReference);
            }

            _retainedHintPanels.Clear();
        }

        public void AddTutorialOpenKey(TutorialViewSo tutorialViewSO, int id)
        {
            if (tutorialViewSO == null)
                return;

            ActionsToOpen.TryAdd(tutorialViewSO, new HashSet<int>());
            ActionsToOpen[tutorialViewSO].Add(id);
        }

        public void AddTutorialCloseKey(TutorialViewSo tutorialViewSO, int id)
        {
            if (tutorialViewSO == null || tutorialViewSO != _currentTutorial)
                return;

            ActionsToClose.TryAdd(tutorialViewSO, new HashSet<int>());
            ActionsToClose[tutorialViewSO].Add(id);
        }

        public void AddStageOpenKey(TutorialViewSo tutorialViewSO, int stage, int id)
        {
            if (tutorialViewSO == null || stage <= 0 || !IsTutorialSession(tutorialViewSO))
            {
                return;
            }

            if (!_stageOpenKeys.TryGetValue(stage, out var keys))
            {
                keys = new HashSet<int>();
                _stageOpenKeys[stage] = keys;
            }

            keys.Add(id);
            TryEnterKeyedStage();
        }

        public void AddStageCloseKey(TutorialViewSo tutorialViewSO, int stage, int id)
        {
            if (tutorialViewSO == null || stage <= 0 || !IsTutorialSession(tutorialViewSO))
            {
                return;
            }

            if (!_stageCloseKeys.TryGetValue(stage, out var keys))
            {
                keys = new HashSet<int>();
                _stageCloseKeys[stage] = keys;
            }

            keys.Add(id);
            TryHideKeyedWindow();
        }

        public void HighlightObservableObject(TutorialViewSo tutorialViewSO, int id)
        {
            if (!_isActive || tutorialViewSO != _currentTutorial)
                return;

            if (TryGetHighlightObject(_currentTutorial, id, out var highlightedObject))
            {
                HighlightObservableObject(highlightedObject, id);
            }
        }

        public void DisableHighlightObservableObject(TutorialViewSo tutorialViewSO, int id)
        {
            HideArrow(id);

            if (TryGetHighlightObject(tutorialViewSO, id, out var highlightObject))
            {
                SetInteractable(highlightObject, false);
            }
        }

        // Стрелка и разрешение на клик развязаны: раньше оба действия жили в одном методе, поэтому
        // нельзя было сделать кликабельный объект без стрелки или показать стрелку без клика.
        public void SetObjectInteractable(TutorialViewSo tutorialViewSO, int id, bool interactable)
        {
            if (TryGetHighlightObject(tutorialViewSO, id, out var highlightObject))
            {
                SetInteractable(highlightObject, interactable);
            }
        }

        public void SetAlternativePanelAutoHide(bool value)
        {
            var selectionView = _gameplaySceneReferences.ObjectAlternativeSelectionView;

            if (selectionView != null)
            {
                selectionView.SetAutoHideEnabled(value);
            }
        }

        public void SetAllowedAlternative(int index)
        {
            var selectionView = _gameplaySceneReferences.ObjectAlternativeSelectionView;

            if (selectionView != null)
            {
                selectionView.SetAllowedAlternative(index);
            }
        }

        public void SetArrowVisible(TutorialViewSo tutorialViewSO, int id, bool visible)
        {
            if (!visible)
            {
                HideArrow(id);
                return;
            }

            if (TryGetHighlightObject(tutorialViewSO, id, out var highlightObject))
            {
                ShowArrow(highlightObject, id);
            }
        }

        private void HighlightObservableObject(TutorialHighlightObject highlightObject, int id)
        {
            SetInteractable(highlightObject, true);
            ShowArrow(highlightObject, id);
        }

        #region Переход по клику

        /// <summary>
        /// Игрок нажал на объект уровня. Если это подсветка текущей стадии с галочкой
        /// «следующая стадия по клику» — листаем окно дальше.
        /// </summary>
        private void PrimaryActionPerformedHandler(IReactToActions reactToActions)
        {
            if (!_isActive || _currentTutorial == null || _currentTutorialPanel == null
                || reactToActions == null)
            {
                return;
            }

            var panelData = tutorialViewLevelData
                .FirstOrDefault(data => data.TutorialViewData == _currentTutorial);

            if (panelData == null)
            {
                return;
            }

            var stage = _currentTutorialPanel.CurrentStage;
            var required = 0;
            var matched = false;

            foreach (var highlightObject in panelData.HighlightObjects)
            {
                if (!highlightObject.nextStageOnClick || highlightObject.stage != stage)
                {
                    continue;
                }

                required++;

                if (highlightObject.reactToActions != null
                    && highlightObject.reactToActions.TryGetComponent<IReactToActions>(out var target)
                    && ReferenceEquals(target, reactToActions))
                {
                    _clickedStageHighlights.Add(highlightObject.id);
                    matched = true;
                }
            }

            if (!matched || required == 0)
            {
                return;
            }

            // Стадия ждёт ВСЕ объекты, отмеченные «клик → след. стадия». Один объект — перейдём
            // сразу, два — только когда нажмут оба, в любом порядке.
            if (_clickedStageHighlights.Count < required)
            {
                return;
            }

            GoNextStage();
        }

        #endregion

        #region Поведение по стадиям

        private void StageChangedHandler()
        {
            // Новая стадия — счётчик нажатых объектов начинается заново.
            _clickedStageHighlights.Clear();

            if (!_isActive || _currentTutorial == null || _currentTutorialPanel == null)
            {
                return;
            }

            if (_currentTutorialPanel.IsHidden)
            {
                RestoreWindowIfHidden();
            }
            else
            {
                ApplyStageBehaviour(_currentTutorial, _currentTutorialPanel);
            }

            if (!_currentIsHint)
            {
                TryEnterKeyedStage();
            }
        }

        private void StageHiddenHandler(int stage)
        {
            if (!_isActive || _currentTutorial == null || stage <= 0)
            {
                return;
            }

            ProcessStageEvents(_currentTutorial, stage);

            TryCloseSkippedStage();
        }

        private void TryCloseSkippedStage()
        {
            if (_currentIsHint || !_isActive || _currentTutorial == null || _currentTutorialPanel == null)
            {
                return;
            }

            if (_currentTutorial.unskipTutorial || !_currentTutorial.HasUnskipStages || !AreTutorialsSkipped())
            {
                return;
            }

            if (_currentTutorialPanel.IsStageUnskippable(_currentTutorialPanel.CurrentStage))
            {
                return;
            }

            HideTutorial(force: true);
        }

        private void ProcessStageEvents(TutorialViewSo tutorialViewSO, int stage)
        {
            var panelData = tutorialViewLevelData
                .FirstOrDefault(data => data.TutorialViewData == tutorialViewSO);

            if (panelData?.StageEvents == null)
            {
                return;
            }

            foreach (var stageEvent in panelData.StageEvents)
            {
                if (stageEvent == null || stageEvent.stage != stage || !_processedStageEvents.Add(stageEvent))
                {
                    continue;
                }

                stageEvent.onStageHidden?.Invoke();
            }
        }

        /// <summary>
        /// Пересчитывает флаги окна с учётом текущей стадии: стадия может переопределить то,
        /// что задано в SO. Вызывается на показе и на каждой смене стадии.
        /// </summary>
        private void ApplyStageBehaviour(TutorialViewSo tutorialViewSO, TutorialView panel)
        {
            if (panel.ResolvePauseLevelTimer(tutorialViewSO.pauseLevelTimer))
            {
                _levelTimer.PauseLevelTimer(this);
            }
            else
            {
                _levelTimer.ResumeLevelTimer(this);
            }

            ApplyHudOverlay(panel, panel.ResolveShowAboveHud(tutorialViewSO.showAboveHud));

            if (_currentIsHint)
            {
                SetHudBlocked(tutorialViewSO, false);
                panel.SetWindowRaycastsEnabled(false);
            }
            else
            {
                SetHudBlocked(tutorialViewSO, panel.ResolveBlockHudInput(tutorialViewSO.blockHudInput));
                // Окно по умолчанию интерактивно: его кнопки продолжают работать. Только явно отмеченная стадия
                // отключает raycast всего окна и пропускает клик к игровому миру под ним.
                panel.SetWindowRaycastsEnabled(!panel.ShouldPassClicksThroughWindow());
            }

            // HideWindow временно открывает весь ввод. При возврате окна или смене стадии
            // восстанавливаем штатную блокировку туториала до включения разрешённых объектов.
            ActivateDeactivatePrimaryAction(tutorialViewLevelData, true, tutorialViewSO);
            ApplyStageHighlights(tutorialViewSO, panel.CurrentStage);
        }

        /// <summary>
        /// Включает подсветки текущей стадии и гасит чужие. Подсветки со стадией 0 не трогает —
        /// они не привязаны к стадиям и живут по старым правилам.
        /// </summary>
        private void ApplyStageHighlights(TutorialViewSo tutorialViewSO, int stage)
        {
            var panelData = tutorialViewLevelData
                .FirstOrDefault(data => data.TutorialViewData == tutorialViewSO);

            if (panelData == null)
            {
                return;
            }

            foreach (var highlightObject in panelData.HighlightObjects)
            {
                if (highlightObject.stage <= 0)
                {
                    continue;
                }

                // Сначала снимаем: HideArrow убирает стрелку по id, поэтому повторных не появится.
                DisableHighlightObservableObject(tutorialViewSO, highlightObject.id);

                if (highlightObject.stage == stage)
                {
                    HighlightObservableObject(highlightObject, highlightObject.id);
                }
            }
        }

        #endregion

        #region HUD

        /// <summary>
        /// Поднимает окно над интерфейсом. Слои канваса геймплея жёстко упорядочены (HUD лежит выше
        /// слоя туториалов), поэтому просто просим канвас самого окна сортироваться поверх всех.
        /// Общую структуру слоёв не трогаем — она общая для всех проектов на модуле.
        /// </summary>
        private void ApplyHudOverlay(TutorialView panel, bool aboveHud)
        {
            RestoreHudOverlay();

            if (!aboveHud || panel == null)
            {
                return;
            }

            if (!panel.TryGetComponent<Canvas>(out var canvas))
            {
                Debug.LogWarning($"[Tutorial] У окна {panel.name} нет Canvas — " +
                                 "показать поверх HUD не получится.");
                return;
            }

            _overlayCanvas = canvas;
            _overlayPreviousOverride = canvas.overrideSorting;
            _overlayPreviousOrder = canvas.sortingOrder;

            canvas.overrideSorting = true;
            canvas.sortingOrder = GetTopCanvasOrder() + 1;
        }

        private void RestoreHudOverlay()
        {
            if (_overlayCanvas == null)
            {
                return;
            }

            _overlayCanvas.overrideSorting = _overlayPreviousOverride;
            _overlayCanvas.sortingOrder = _overlayPreviousOrder;
            _overlayCanvas = null;
        }

        private int GetTopCanvasOrder()
        {
            var top = 0;

            foreach (var layer in _gameplaySceneReferences.GameplayCanvasLayers)
            {
                if (layer != null)
                {
                    top = Mathf.Max(top, layer.sortingOrder);
                }
            }

            return top;
        }

        /// <summary>
        /// Считаем источники блокировки, а не держим один флаг: два окна подряд с блокировкой
        /// не должны разблокировать HUD, когда закроется только первое.
        /// </summary>
        private void SetHudBlocked(TutorialViewSo tutorialViewSO, bool blocked)
        {
            if (blocked)
            {
                _hudBlockSources.Add(tutorialViewSO);
            }
            else
            {
                _hudBlockSources.Remove(tutorialViewSO);
            }

            ApplyHudBlock(_hudBlockSources.Count > 0);
        }

        private void ApplyHudBlock(bool blocked)
        {
            if (!blocked)
            {
                foreach (var raycaster in _disabledHudRaycasters)
                {
                    if (raycaster != null)
                    {
                        raycaster.enabled = true;
                    }
                }

                _disabledHudRaycasters.Clear();

                foreach (var raycaster in _filteringHudRaycasters)
                {
                    if (raycaster != null)
                    {
                        raycaster.IsFiltering = false;
                    }
                }

                _filteringHudRaycasters.Clear();
                return;
            }

            if (_disabledHudRaycasters.Count > 0 || _filteringHudRaycasters.Count > 0)
            {
                return;
            }

            // Гасим только UI-рейкастеры. Клики по объектам уровня идут через GameplayInputSystem
            // и остаются доступными — иначе сломался бы сам сценарий «нажми на этот объект».
            foreach (var layer in _gameplaySceneReferences.GameplayCanvasLayers)
            {
                if (layer == null || layer == _tutorialCanvas || layer == _topUiLayerCanvas)
                {
                    continue;
                }

                if (!layer.TryGetComponent<GraphicRaycaster>(out var raycaster) || !raycaster.enabled)
                {
                    continue;
                }

                if (raycaster is TutorialHudRaycaster filteringRaycaster)
                {
                    filteringRaycaster.IsFiltering = true;
                    _filteringHudRaycasters.Add(filteringRaycaster);
                    continue;
                }

                raycaster.enabled = false;
                _disabledHudRaycasters.Add(raycaster);
            }
        }

        #endregion

        #region Стадии

        public bool GoNextStage()
        {
            if (_isActive && _currentIsHint)
            {
                var canAdvance = _hasParkedTutorial && _parkedTutorialPanel != null
                    && CanEnterStage(_parkedTutorialPanel, _parkedTutorialPanel.CurrentStage + 1);
                GoNextStageAfterHintAsync().Forget();
                return canAdvance;
            }

            return TryGoNextStageGuarded(_currentTutorialPanel);
        }

        private async UniTaskVoid GoNextStageAfterHintAsync()
        {
            await DismissActiveHintAsync(restoreParked: true);
            TryGoNextStageGuarded(_currentTutorialPanel);
        }

        public bool GoNextStage(TutorialViewSo tutorialViewSO)
        {
            if (_isActive && _currentIsHint)
            {
                if (!_hasParkedTutorial || tutorialViewSO == null || tutorialViewSO != _parkedTutorialSo)
                {
                    return false;
                }

                var canAdvance = CanEnterStage(_parkedTutorialPanel, _parkedTutorialPanel.CurrentStage + 1);
                GoNextStageAfterHintAsync().Forget();
                return canAdvance;
            }

            if (!_isActive || tutorialViewSO == null || tutorialViewSO != _currentTutorial)
            {
                return false;
            }

            return TryGoNextStageGuarded(_currentTutorialPanel);
        }

        public void GoPrevStage()
        {
            if (_isActive && _currentIsHint)
            {
                GoPrevStageAfterHintAsync().Forget();
                return;
            }

            _currentTutorialPanel?.GoPrevStage();

            RestoreWindowIfHidden();
        }

        private async UniTaskVoid GoPrevStageAfterHintAsync()
        {
            await DismissActiveHintAsync(restoreParked: true);
            _currentTutorialPanel?.GoPrevStage();
            RestoreWindowIfHidden();
        }

        public void GoToStage(int stage)
        {
            if (_isActive && _currentIsHint)
            {
                if (TryOpenUnskipStage(null, stage))
                {
                    return;
                }

                if (!CanEnterStage(_parkedTutorialPanel, stage))
                {
                    return;
                }

                GoToStageAfterHintAsync(stage).Forget();
                return;
            }

            if (TryOpenUnskipStage(null, stage))
            {
                return;
            }

            TryGoToStageGuarded(_currentTutorialPanel, stage);
        }

        private async UniTaskVoid GoToStageAfterHintAsync(int stage)
        {
            await DismissActiveHintAsync(restoreParked: true);
            TryGoToStageGuarded(_currentTutorialPanel, stage);
        }

        public void GoToStage(TutorialViewSo tutorialViewSO, int stage)
        {
            if (_isActive && _currentIsHint)
            {
                if (!_hasParkedTutorial || tutorialViewSO == null || tutorialViewSO != _parkedTutorialSo)
                {
                    TryOpenUnskipStage(tutorialViewSO, stage);
                    return;
                }

                if (!CanEnterStage(_parkedTutorialPanel, stage))
                {
                    return;
                }

                GoToStageAfterHintAsync(stage).Forget();
                return;
            }

            if (!_isActive || tutorialViewSO == null || tutorialViewSO != _currentTutorial)
            {
                TryOpenUnskipStage(tutorialViewSO, stage);
                return;
            }

            TryGoToStageGuarded(_currentTutorialPanel, stage);
        }

        private bool TryOpenUnskipStage(TutorialViewSo tutorialViewSO, int stage)
        {
            if (stage <= 0 || !AreTutorialsSkipped() || !CanOpenUnskipStageNow())
            {
                return false;
            }

            var target = tutorialViewSO != null ? tutorialViewSO : FindUnskipStageOwner(stage);

            if (target == null || !target.IsStageUnskippable(stage))
            {
                return false;
            }

            ShowTutorialAtStage(target, stage);

            return true;
        }

        private bool CanOpenUnskipStageNow() => !_isActive || (_currentIsHint && !_hasParkedTutorial);

        private TutorialViewSo FindUnskipStageOwner(int stage)
        {
            TutorialViewSo owner = null;

            foreach (var data in tutorialViewLevelData)
            {
                var candidate = data?.TutorialViewData;

                if (candidate == null || !candidate.IsStageUnskippable(stage) || candidate == owner)
                {
                    continue;
                }

                if (owner != null)
                {
                    Debug.LogWarning($"[Tutorial] Стадию {stage} пометили непропускаемой сразу несколько окон " +
                                     "уровня — открывать нечего, зови ShowTutorialAtStage с нужным окном.");
                    return null;
                }

                owner = candidate;
            }

            return owner;
        }

        public void GoToStageIfCurrent(int currentStage, int targetStage)
        {
            var tutorialViewSO = _isActive && _currentIsHint && _hasParkedTutorial
                ? _parkedTutorialSo
                : _currentTutorial;

            GoToStageIfCurrent(tutorialViewSO, currentStage, targetStage);
        }

        public void GoToStageIfCurrent(TutorialViewSo tutorialViewSO, int currentStage, int targetStage)
        {
            var panel = GetSessionPanel(tutorialViewSO);

            if (panel == null || panel.CurrentStage != currentStage)
            {
                return;
            }

            GoToStage(tutorialViewSO, targetStage);
        }

        private bool IsTutorialSession(TutorialViewSo tutorialViewSO)
        {
            if (!_isActive || tutorialViewSO == null)
            {
                return false;
            }

            if (_currentIsHint)
            {
                return _hasParkedTutorial && tutorialViewSO == _parkedTutorialSo;
            }

            return tutorialViewSO == _currentTutorial;
        }

        private TutorialView GetSessionPanel(TutorialViewSo tutorialViewSO)
        {
            if (!IsTutorialSession(tutorialViewSO))
            {
                return null;
            }

            return _currentIsHint ? _parkedTutorialPanel : _currentTutorialPanel;
        }

        private void TryEnterKeyedStage()
        {
            var tutorialViewSO = _isActive && _currentIsHint && _hasParkedTutorial
                ? _parkedTutorialSo
                : _currentTutorial;
            var panel = GetSessionPanel(tutorialViewSO);

            if (panel == null)
            {
                return;
            }

            var current = panel.CurrentStage;
            var max = panel.MaxStage;

            for (var stage = current + 1; stage <= max; stage++)
            {
                var settings = panel.GetStageSettings(stage);

                if (settings == null || !settings.HasEnterKeys)
                {
                    continue;
                }

                if (!_stageOpenKeys.TryGetValue(stage, out var collected)
                    || !collected.SetEquals(settings.KeysToEnter))
                {
                    continue;
                }

                if (settings.EnterFromStage > 0 && current != settings.EnterFromStage)
                {
                    continue;
                }

                if (!CanEnterStage(panel, stage))
                {
                    continue;
                }

                GoToStage(tutorialViewSO, stage);
                return;
            }
        }

        private bool CanEnterStage(TutorialView panel, int targetStage)
        {
            if (panel == null)
            {
                return false;
            }

            var current = panel.CurrentStage;

            if (targetStage <= current)
            {
                return true;
            }

            for (var stage = current + 1; stage <= targetStage; stage++)
            {
                var settings = panel.GetStageSettings(stage);

                if (settings == null || !settings.HasEnterKeys)
                {
                    continue;
                }

                if (settings.EnterFromStage > 0 && current != settings.EnterFromStage)
                {
                    return false;
                }

                if (!_stageOpenKeys.TryGetValue(stage, out var collected)
                    || !collected.SetEquals(settings.KeysToEnter))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryGoNextStageGuarded(TutorialView panel)
        {
            if (panel == null || !CanEnterStage(panel, panel.CurrentStage + 1))
            {
                return false;
            }

            if (!panel.TryGoNextStage())
            {
                return false;
            }

            RestoreWindowIfHidden();
            return true;
        }

        private bool TryGoToStageGuarded(TutorialView panel, int stage)
        {
            if (panel == null || !CanEnterStage(panel, stage))
            {
                return false;
            }

            panel.GoToStage(stage);
            RestoreWindowIfHidden();
            return true;
        }

        public void NextStageOrHide()
        {
            if (_isActive && _currentIsHint)
            {
                NextStageOrHideAfterHintAsync().Forget();
                return;
            }

            TryNextStageOrHide(_currentTutorialPanel);
        }

        private async UniTaskVoid NextStageOrHideAfterHintAsync()
        {
            await DismissActiveHintAsync(restoreParked: true);
            TryNextStageOrHide(_currentTutorialPanel);
        }

        private void TryNextStageOrHide(TutorialView panel)
        {
            if (panel == null)
            {
                HideTutorial();
                return;
            }

            if (panel.CurrentStage < panel.MaxStage && !CanEnterStage(panel, panel.CurrentStage + 1))
            {
                return;
            }

            if (TryGoNextStageGuarded(panel))
            {
                return;
            }

            HideTutorial();
        }

        #endregion

        #region Временное скрытие окна

        public void HideWindow()
        {
            if (_isActive && _currentIsHint)
            {
                if (!CanHideCurrentStage(_parkedTutorialPanel))
                {
                    return;
                }

                HideWindowAfterHintAsync().Forget();
                return;
            }

            TryHideWindow(_currentTutorialPanel);
        }

        private async UniTaskVoid HideWindowAfterHintAsync()
        {
            await DismissActiveHintAsync(restoreParked: true);
            TryHideWindow(_currentTutorialPanel);
        }

        private void TryHideWindow(TutorialView panel)
        {
            if (!_isActive || panel == null || !CanHideCurrentStage(panel))
            {
                return;
            }

            SuspendStageBehaviour();
            panel.PlayHideTemporary().Forget();
        }

        private bool CanHideCurrentStage(TutorialView panel)
        {
            if (panel == null)
            {
                return true;
            }

            var settings = panel.GetStageSettings(panel.CurrentStage);

            if (settings == null || !settings.HasCloseKeys)
            {
                return true;
            }

            return _stageCloseKeys.TryGetValue(panel.CurrentStage, out var collected)
                && collected.SetEquals(settings.KeysToClose);
        }

        private void TryHideKeyedWindow()
        {
            var tutorialViewSO = _isActive && _currentIsHint && _hasParkedTutorial
                ? _parkedTutorialSo
                : _currentTutorial;
            var panel = GetSessionPanel(tutorialViewSO);

            if (panel == null || !CanHideCurrentStage(panel))
            {
                return;
            }

            var settings = panel.GetStageSettings(panel.CurrentStage);

            if (settings == null || !settings.HasCloseKeys)
            {
                return;
            }

            HideWindow();
        }

        /// <summary>
        /// Пока окно временно скрыто, туториал не должен ограничивать игрока. Сам туториал,
        /// текущая стадия и её настройки сохраняются и будут применены снова при смене стадии.
        /// </summary>
        private void SuspendStageBehaviour()
        {
            _levelTimer.ResumeLevelTimer(this);

            if (_currentTutorial != null)
            {
                SetHudBlocked(_currentTutorial, false);
            }

            RestoreHudOverlay();
            _inputSystem.IsActionAvailable = true;
            isShowing.Value = false;
        }

        /// <summary>
        /// Стадию пролистали — если окно было спрятано, показываем его обратно на новой стадии.
        /// Сначала меняется стадия, потом играет появление, чтобы старый шаг не мелькнул.
        /// </summary>
        private void RestoreWindowIfHidden()
        {
            if (!_isActive || _currentTutorialPanel == null || !_currentTutorialPanel.IsHidden)
            {
                return;
            }

            // GoToStage может получить номер уже текущей стадии и не вызвать StageChanged.
            // В этом случае правила всё равно нужно вернуть перед показом окна.
            if (_currentTutorial != null)
            {
                ApplyStageBehaviour(_currentTutorial, _currentTutorialPanel);
            }

            isShowing.Value = true;
            _currentTutorialPanel.PlayShowKeepStage().Forget();
        }

        #endregion

        private bool TryGetHighlightObject(TutorialViewSo tutorialViewSO, int id,
            out TutorialHighlightObject highlightObject)
        {
            highlightObject = default;

            var panelData = tutorialViewLevelData
                .FirstOrDefault(data => data.TutorialViewData == tutorialViewSO);

            if (panelData == null)
            {
                return false;
            }

            foreach (var candidate in panelData.HighlightObjects)
            {
                if (candidate.id != id)
                {
                    continue;
                }

                highlightObject = candidate;
                return true;
            }

            return false;
        }

        private void SetInteractable(TutorialHighlightObject highlightObject, bool interactable)
        {
            if (highlightObject.reactToActions == null)
            {
                return;
            }

            if (highlightObject.reactToActions.TryGetComponent<IReactToActions>(out var reactToActions))
            {
                reactToActions.AlwaysReactToPrimaryAction = interactable;
            }
        }

        private void ShowArrow(TutorialHighlightObject highlightObject, int id)
        {
            if (highlightObject.hideArrow || highlightObject.target == null)
            {
                return;
            }

            var arrow = GetFreeArrow();

            var observableObject = highlightObject.target;

            Vector2 arrowPosition = _currentTutorial.IsUI
                ? observableObject.GetComponent<RectTransform>().position
                : UIHelper.ConvertWorldToLocalCanvasPosition(observableObject.position,
                    _gameplaySceneReferences.MainCamera, _gameplaySceneReferences.MainCanvas);

            if(_currentTutorial.IsUI)
            {
                arrow.rectTransform.position = arrowPosition;
            }
            else
            {
                if (highlightObject.isHighlightMovable)
                {
                    _arrowMoveSubscriptions[id] = SubscribeMovableArrow(arrow, highlightObject);
                }
                else
                {
                    arrow.rectTransform.anchoredPosition = arrowPosition;
                }
            }
            arrow.rectTransform.rotation = observableObject.transform.rotation;
            arrow.highlightId = id;
            _arrows.Add(arrow);
        }

        private void HideArrow(int id)
        {
            for (var index = 0; index < _arrows.Count; index++)
            {
                var arrow = _arrows[index];

                if (arrow.highlightId != id)
                {
                    continue;
                }

                ReleaseArrow(arrow);

                if (_arrowMoveSubscriptions.TryGetValue(id, out var sub))
                {
                    sub.Dispose();
                    _arrowMoveSubscriptions.Remove(id);
                }

                break;
            }
        }

        private void UpdateHighlightedObservableObject()
        {
            var panelData = tutorialViewLevelData.FirstOrDefault(data => data.TutorialViewData == _currentTutorial);

            if (panelData != null)
            {
                foreach (var availableObject in panelData.HighlightObjects)
                {
                    if (!_isActive)
                    {
                        DisableHighlightObservableObject(_currentTutorial, availableObject.id);
                    }
                    else if (availableObject.stage > 0)
                    {
                        // Привязанные к стадии включает ApplyStageHighlights, когда стадия совпадёт.
                        continue;
                    }
                    else if (availableObject.isHighlightOnStart)
                    {
                        HighlightObservableObject(availableObject, availableObject.id);
                    }
                }
            }
        }

        private IDisposable SubscribeMovableArrow(TutorialArrow arrow, TutorialHighlightObject highlightObject)
        {
            return Observable.EveryUpdate()
                .Where(_ => _isActive && arrow != null && arrow.gameObject.activeSelf)
                .Subscribe(_ =>
                {
                    if (arrow == null || highlightObject.target == null)
                        return;

                    Vector2 pos = UIHelper.ConvertWorldToLocalCanvasPosition(
                        highlightObject.target.position,
                        _gameplaySceneReferences.MainCamera,
                        _gameplaySceneReferences.MainCanvas);

                    arrow.rectTransform.anchoredPosition = pos;
                    arrow.rectTransform.rotation = highlightObject.target.transform.rotation;
                });
        }

        private TutorialArrow GetFreeArrow()
        {
            var arrow = _arrowPool.Get();
            arrow.gameObject.SetActive(true);
            return arrow;
        }

        private void ReleaseArrow(TutorialArrow arrow)
        {
            arrow.gameObject.SetActive(false);
            _arrows.Remove(arrow);
            _arrowPool.Release(arrow);
        }

        public void SetEnable(bool enable)
        {
            isEnable.Value = enable;
            _saveController.Service.SetTutorialActive(enable);

            var keepCurrentTutorial = TutorialToggleAppliesNextLevel && IsCurrentTutorialIndependentOfToggle();

            if (!keepCurrentTutorial)
            {
                ForgetTutorialClosedByToggle(enable);

                HideTutorial(force: true);

                if (!isEnable.Value)
                {
                    _inputSystem.IsActionAvailable = true;
                }
            }

            if (!enable && TutorialToggleAppliesNextLevel)
            {
                _tutorialsBlockedForCurrentLevel = true;

                if (!keepCurrentTutorial)
                {
                    ReleaseTutorialRestrictions();
                }
            }

            UpdateTutorialButtons(enable);
        }

        private bool IsCurrentTutorialIndependentOfToggle()
        {
            if (!_isActive || _currentTutorial == null)
            {
                return false;
            }

            return _currentIsHint || ResolveUnskipForCurrentStage();
        }

        private bool ResolveUnskipForCurrentStage() =>
            _currentTutorialPanel != null
                ? _currentTutorialPanel.ResolveUnskipTutorial(_currentTutorial.unskipTutorial)
                : _currentTutorial.unskipTutorial;

        private void ForgetTutorialClosedByToggle(bool enable)
        {
            if (enable || !_isActive || _currentIsHint || _currentTutorial == null
                || !_currentTutorial.HasUnskipStages)
            {
                return;
            }

            _shownTutorials.Remove(_currentTutorial);
        }

        private void UpdateTutorialButtons(bool enable)
        {
            UniTask.Void(async () =>
            {
                var view = (await _uiController.GetPanel(_sceneReferences.GameplayTopPanel))
                    .GetComponent<GameplayTopPanelView>();
                
                view.tutorialEnableButton.gameObject.SetActive(!enable);
                view.tutorialDisableButton.gameObject.SetActive(enable);
                
                _viewEnableButton = view.tutorialEnableButton.gameObject;
                _viewDisableButton = view.tutorialDisableButton.gameObject;
            });
        }

        private void ReleaseTutorialRestrictions()
        {
            _hudBlockSources.Clear();
            ApplyHudBlock(false);
            RestoreHudOverlay();
            SetAlternativePanelAutoHide(true);
            SetAllowedAlternative(-1);

            for (var index = _arrows.Count - 1; index >= 0; index--)
            {
                HideArrow(_arrows[index].highlightId);
            }

            foreach (var subscription in _arrowMoveSubscriptions.Values)
            {
                subscription?.Dispose();
            }

            _arrowMoveSubscriptions.Clear();

            _inputSystem.IsActionAvailable = true;
        }

        public async UniTaskVoid ShowConfirmTutorial(bool enable)
        {
            _uiController.ShowPanel(_gameplaySceneReferences.ConfirmTutorialView,_tutorialCanvas.transform);

            var view = (await _uiController.GetPanel(_gameplaySceneReferences.ConfirmTutorialView))
                .GetComponent<ConfirmTutorialView>();

            _inputSystem.IsActionAvailable = false;

            view.acceptButton.onClick.RemoveListener(EnableTutorial);
            view.acceptButton.onClick.RemoveListener(DisableTutorial);

            if (enable)
            {
                view.acceptButton.onClick.AddListener(DisableTutorial);
                if (view.headLabel)
                    view.headLabel.text = LocalizationService.Instance.GetText("tutorial_txt_disable_tutorial");
                view.mainLabel.text = LocalizationService.Instance.GetText("tutorial_dlg_disable_tutorial");
            }
            else
            {
                view.acceptButton.onClick.AddListener(EnableTutorial);
                if (view.headLabel)
                    view.headLabel.text = LocalizationService.Instance.GetText("tutorial_txt_enable_tutorial");
                view.mainLabel.text = LocalizationService.Instance.GetText("tutorial_dlg_enable_tutorial");
            }
        }

        public void HideConfirmTutorial()
        {
            _uiController.HidePanel(_gameplaySceneReferences.ConfirmTutorialView, _tutorialCanvas.transform);

            var windowHidden = _currentTutorialPanel != null && _currentTutorialPanel.IsHidden;

            if (TutorialToggleAppliesNextLevel && _isActive && _currentTutorial != null && !windowHidden)
            {
                ActivateDeactivatePrimaryAction(tutorialViewLevelData, true, _currentTutorial);
                return;
            }

            _inputSystem.IsActionAvailable = true;
        }

        private void EnableTutorial()
        {
            SetEnable(true);

            if (TutorialToggleAppliesNextLevel)
            {
                return;
            }

            _levelController.ReloadLevel();
        }

        private void DisableTutorial()
        {
            SetEnable(false);
            _levelController.Resume();
        }

        private void ProcessTutorialEvents(List<TutorialEvent> events, TutorialViewSo tutorialViewSo = null)
        {
            if (tutorialViewSo == null)
                tutorialViewSo = _currentTutorial;

            for (var index = events.Count - 1; index >= 0; index--)
            {
                var tutorialEvent = events[index];
                
                if (tutorialEvent.TutorialViewData.id != tutorialViewSo.id)
                {
                    continue;
                }

                tutorialEvent.AffectedTutorialEvent?.Invoke();
                events.RemoveAt(index);
            }
        }

        private void ActivateDeactivatePrimaryAction(List<TutorialViewLevelData> actionToCloses, bool isActivate,
            TutorialViewSo tutorialViewSo = null)
        {
            if (tutorialViewSo == null)
                tutorialViewSo = _currentTutorial;

            if (_currentIsHint)
            {
                _inputSystem.IsActionAvailable = true;
                return;
            }

            if (tutorialViewSo.isSequence)
            {
                _inputSystem.IsActionAvailable = false;
            }
            else
            {
                _inputSystem.IsActionAvailable = !isActivate;
            }
        }

        private TutorialArrow CreateTutorialArrow()
        {
            var instance = _objectResolver.Instantiate(_sceneReferences.TutorialArrow,
                _tutorialCanvas.transform).GetComponent<TutorialArrow>();

            instance.gameObject.SetActive(false);

            return instance;
        }

        public UniTask Reload()
        {
            if (_currentTutorialPanel != null)
            {
                _currentTutorialPanel.StageChanged -= StageChangedHandler;
                _currentTutorialPanel.StageHidden -= StageHiddenHandler;
            }

            _levelTimer.ResumeLevelTimer(this);

            // Без этого перезапуск уровня посреди открытого окна оставил бы HUD навсегда мёртвым.
            _hudBlockSources.Clear();
            ApplyHudBlock(false);
            RestoreHudOverlay();

            SetAlternativePanelAutoHide(true);
            SetAllowedAlternative(-1);

            _inputSystem.IsActionAvailable = true;
            onTutorialShowEvents.Clear();
            onTutorialHideEvents.Clear();
            tutorialViewLevelData.Clear();
            _shownTutorials.Clear();
            UnloadRetainedHintPanels();
            _currentIsHint = false;
            _tutorialShowGeneration++;
            _hintShowInFlight = false;
            _hintShowInFlightSo = null;
            _suppressTutorialShows = false;
            _tutorialsBlockedForCurrentLevel = false;
            _processedStageEvents.Clear();
            _clickedStageHighlights.Clear();
            _arrowMoveSubscriptions.Clear();

            for (var index = _arrows.Count - 1; index >= 0; index--)
            {
                var arrow = _arrows[index];
                ReleaseArrow(arrow);
            }

            if (_isActive && _currentTutorial != null && _tutorialCanvas != null)
            {
                if (_currentTutorialPanel != null)
                {
                    _currentTutorialPanel.gameObject.SetActive(false);
                }

                _uiController.HidePanel(_currentTutorial.panelReference, _tutorialCanvas.transform);

                if (_currentIsHint)
                {
                    _uiController.UnloadPanel(_currentTutorial.panelReference);
                }
            }

            _currentTutorial = null;
            _currentTutorialPanel = null;
            SetActive(false);

            ClearParkedTutorial();

            ActionsToOpen.Clear();
            ActionsToClose.Clear();
            _stageOpenKeys.Clear();
            _stageCloseKeys.Clear();

            if (TutorialToggleAppliesNextLevel)
            {
                UpdateTutorialButtons(isEnable.Value);
            }

            return UniTask.CompletedTask;
        }

        private void SetActive(bool active)
        {
            _isActive = active;

            if (!active)
            {
                isShowing.Value = false;
            }
        }
    }
}
