using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using Creobit.Bootstrap.Core.Scripts.Runtime.DTO;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Cysharp.Threading.Tasks;
using System;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using VContainer;
using System.Collections.Generic;
using UnityEngine;
using Creobit.Bootstrap.Core.Scripts.Runtime.Operation;
using _8floor.TimeManagement.Core.Scripts.Runtime.Comics.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Comics;
using _8floor.TimeManagement.Core.Scripts.Runtime.Extras;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Fading;
using Creobit.Loading;
using Creobit.Logger;
using Creobit.UI;
using Creobit.UI.Utility;
using DG.Tweening;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Meta
{
    public class MetaController : IMetaController
    {
        private MetaSceneReferences _sceneRefs;
        private RuntimeData _runtimeData;

        private ILoadingController _loadingController;
        private IMapController _mapController;
        private IUIController _uiController;
        private IPlayerProfilesController _playerProfilesController;
        private IPlayerPrefsSaveProvider _saveController;
        private IComicsController _comicsController;
        private IVideoCutsceneController _videoCutsceneController;
        private PanelReference _createProfilePanel;
        private PanelReference _alertPanel;
        private IFadeController _fadeController;
        private IGuidesController _guidesController;

        public MetaSceneReferences SceneReferences => _sceneRefs;

        public RuntimeData RuntimeData => _runtimeData;

        public IGuidesController GuidesController => _guidesController;

        public Stack<IMetaState> CurrentStates { get; private set; } = new Stack<IMetaState>();

        public bool LockState { get; set; }

        public event Action OnStateChanged;

        [Inject]
        private void Construct(MetaSceneReferences sceneRefs,
            RuntimeData runtimeData,
            ILoadingController loadingController,
            IMapController mapController,
            IUIController uiController,
            IPlayerProfilesController playerProfilesController,
            IPlayerPrefsSaveProvider saveProvider,
            IComicsController comicsController,
            IVideoCutsceneController videoCutsceneController,
            IFadeController fadeController,
            IGuidesController guidesController)
        {
            _guidesController = guidesController;
            _videoCutsceneController = videoCutsceneController;
            _sceneRefs = sceneRefs;
            _runtimeData = runtimeData;
            _loadingController = loadingController;
            _mapController = mapController;
            _uiController = uiController;
            _playerProfilesController = playerProfilesController;
            _saveController = saveProvider;
            _comicsController = comicsController;
            _fadeController = fadeController;
        }

        public UniTask Load()
        {
            CurrentStates.Push(MetaStates.Gameplay);

            _loadingController.LoadingFinished += LoadingFinishedHandler;
            _comicsController.OnComicsCompleted += ComicsCompletedHandler;
            _videoCutsceneController.OnCutsceneStarted += VideoCutsceneStartedHandler;
            _videoCutsceneController.OnCutsceneCompleted += VideoCutsceneCompletedHandler;
            _mapController.OnBackRequested += MapBackRequestedHandler;

            return UniTask.CompletedTask;
        }

        private void MapBackRequestedHandler()
        {
            if (LockState || CurrentStates.Peek() != MetaStates.Map)
            {
                return;
            }

            ChangeState(MetaStates.Menu);
        }

        private void LoadingFinishedHandler()
        {
            if(_runtimeData.MetaState == null || _runtimeData.MetaState == MetaStates.Menu)
            {
                CurrentStates.Pop();
                CurrentStates.Push(MetaStates.Menu);
                CurrentStates.Peek().StartState(this);
                return;
            }

            ChangeState(_runtimeData.MetaState);
        }

        public void ChangeState(IMetaState state)
        {
            var currentState = CurrentStates.Peek();

#if !PREMIUM
            if (state is GameplayState && !IsCurrentLevelAvailable())
            {
                return;
            }
#endif

            if (state.GetType() == typeof(ComicsState) || state.GetType() == typeof(VideoCutsceneState))
            {
                _fadeController.FadeIn();
            }
            
            currentState.ChangeState(this, state);
        }

        public async UniTask HandleStateChange(IMetaState currentState, IMetaState newState,
            bool async = true, bool startState = true, bool endState = true)
        {
            CurrentStates.Push(newState);

            Log.Meta.Info("LockState: " + LockState + ", CurrentState: " + currentState + ", NewState: " + newState);

            if (LockState)
                return;

            if (endState)
                await currentState.EndState(this, async);

            if (startState)
                newState.StartState(this);

            StateChanged();
        }

        public void ReturnLastState()
        {
            CurrentStates.Pop();
            ChangeState(CurrentStates.Peek());
        }

        private void ReturnFromOverlay()
        {
            var desiredState = CurrentStates.Pop();

            if (desiredState == MetaStates.Comics || desiredState == MetaStates.VideoCutscene)
            {
                desiredState = CurrentStates.Pop();
            }

            if (CurrentStates.Count == 0)
            {
                CurrentStates.Push(MetaStates.Comics);
            }
            ChangeState(desiredState);
        }

        public void StateChanged()
        {
            OnStateChanged?.Invoke();
        }

        public void ShowPanel(PanelReference panelReference, Transform parent)
        {
            _uiController.ShowPanel(panelReference, parent);
        }

        public async UniTask<T> ShowPanel<T>(PanelReference panelReference, Transform parent) where T : PanelData
        {
            var panel = await _uiController.ShowPanel<T>(panelReference, parent);
            return panel;
        }

        public void HidePanel(PanelReference panelReference, Transform parent)
        {
            _uiController.HidePanel(panelReference, parent);
        }

        public DOTweenAnimation[] GetPanelAnimations(PanelReference panelReference, PanelState state)
        {
            return _uiController.GetPanelAnimations(panelReference, state);
        }

        public void ShowMap()
        {
            _mapController.ShowMap();
        }

        public void HideMap()
        {
            _mapController.HideMap();
        }

        public void RefreshMapAfterUnlockCheat(bool switchToOtherPage = true)
        {
            HideMap();
            ShowMap();

            if (switchToOtherPage && _mapController is MapController mapController)
            {
                mapController.ChangeMapPage();
            }

            TryShowMapChangeButton();
        }
        
        public void TryShowMapChangeButton()
        {
            _mapController.TryShowMapChangeButton();
        }

        public OperationResult AddProfile(string profileName)
        {
            return _playerProfilesController.Service.AddProfile(profileName);
        }

        public void RemoveProfile(string profileName)
        {
            _playerProfilesController.Service.RemoveProfile(profileName);
        }

        public void Save(string key, string value)
        {
            _saveController.Save(key, value);
        }

        public string TryGetSaveValue(string key)
        {
            return _saveController.TryGetValue(key);
        }

        public async void ShowComics(ComicsData comicsData)
        {
            await _comicsController.ShowComics(comicsData);
            await _fadeController.FadeOut();
        }

        public ComicsData PendingComics { get; private set; }

        public void ForceShowComics(ComicsData comicsData)
        {
            PendingComics = comicsData;
            ChangeState(MetaStates.Comics);
        }

        public void ClearPendingComics()
        {
            PendingComics = null;
        }

        public bool IsComicsReady(out ComicsData comicsData)
        {
            if(_comicsController.IsComicsReady(out ComicsData comics))
            {
                comicsData = comics;
                return true;
            }

            comicsData = null;
            return false;
        }

        private void ComicsCompletedHandler()
        {
            LockState = false;
            ReturnFromOverlay();
        }

        /// <summary>Катсцена, которую покажет VideoCutsceneState после перехода в него.</summary>
        public VideoCutsceneSO PendingCutscene { get; private set; }

        /// <summary>
        /// Если под условие есть непросмотренный ролик — уводит мету в оверлей катсцены.
        /// Вернёт false, если ролика нет: вызывающий код в этом случае идёт по старому пути с комиксом.
        /// </summary>
        public bool TryShowVideoCutscene(params VideoCutsceneTrigger[] triggers)
        {
            // Второй запуск поверх играющего ролика оставил бы мету в LockState навсегда:
            // контроллер молча выйдет, а событие завершения придёт только от первой катсцены.
            if (_videoCutsceneController == null || _videoCutsceneController.IsPlaying)
            {
                return false;
            }

            if (!_videoCutsceneController.TryGetReady(out var cutscene, triggers))
            {
                return false;
            }

            PendingCutscene = cutscene;

            ChangeState(MetaStates.VideoCutscene);
            LockState = true;

            return true;
        }

        /// <summary>Принудительный показ конкретной катсцены (читы, ручной вызов).</summary>
        public void ShowVideoCutscene(VideoCutsceneSO cutscene)
        {
            if (cutscene == null || _videoCutsceneController == null || _videoCutsceneController.IsPlaying)
            {
                return;
            }

            PendingCutscene = cutscene;

            ChangeState(MetaStates.VideoCutscene);
            LockState = true;
        }

        public void PlayPendingVideoCutscene()
        {
            if (PendingCutscene == null)
            {
                Log.Meta.Warning("VideoCutsceneState: нечего играть.");
                VideoCutsceneCompletedHandler();
                return;
            }

            PlayVideoCutsceneAsync(PendingCutscene).Forget();
        }

        private async UniTaskVoid PlayVideoCutsceneAsync(VideoCutsceneSO cutscene)
        {
            await _videoCutsceneController.Play(cutscene);
        }

        private void VideoCutsceneStartedHandler()
        {
            // Первый кадр готов — снимаем затемнение перехода.
            _fadeController.FadeOut().Forget();
        }

        private void VideoCutsceneCompletedHandler()
        {
            var played = PendingCutscene;

            PendingCutscene = null;
            LockState = false;

            // Ролик мог быть настроен так, чтобы сразу за ним шёл старый комикс.
            if (played != null && played.ShowComicsAfter && IsComicsReady(out _))
            {
                MetaStates.Comics.StartState(this);
                return;
            }

            _fadeController.FadeOut().Forget();

            ReturnFromOverlay();
        }

        public async void LoadGameplay()
        {
            _loadingController.ScheduleSceneLoading(Utility.RuntimeConstants.SceneIndex.GameplayScene);
           await _loadingController.LoadScheduledScene();
        }

#if !PREMIUM
        private bool IsCurrentLevelAvailable()
        {
            var currentLevel = _sceneRefs?.CurrentLevel;
            var allLevels = _sceneRefs?.allLevels;

            if (currentLevel?.CurrentLevel == null || allLevels?.AllLevels == null)
            {
                return true;
            }

            var targetGuid = currentLevel.CurrentLevel.AssetGUID;

            if (string.IsNullOrEmpty(targetGuid))
            {
                return true;
            }

            var levelNum = 0;

            foreach (var levelReference in allLevels.AllLevels)
            {
                if (levelReference.level != null && levelReference.level.AssetGUID == targetGuid)
                {
                    levelNum = levelReference.levelNum;
                    break;
                }
            }

            if (levelNum <= 0)
            {
                return true;
            }

            var unlockLevelManager = UnityEngine.Object
                .FindFirstObjectByType<Creobit.EditionsUpgrade.UnlockLevelManager>();

            if (unlockLevelManager == null || unlockLevelManager.UnlockLevels == null)
            {
                return true;
            }

            Creobit.EditionsUpgrade.UnlockLevelItem targetLevel = null;

            foreach (var unlockLevel in unlockLevelManager.UnlockLevels)
            {
                if (unlockLevel != null && unlockLevel.Index == levelNum)
                {
                    targetLevel = unlockLevel;
                    break;
                }
            }

            if (targetLevel == null || unlockLevelManager.LevelCondition(targetLevel))
            {
                return true;
            }

            Log.Meta.Info($"MetaController: level {levelNum} is not purchased, showing unlock window.");

            unlockLevelManager.ShowUnlockWindow(targetLevel);

            return false;
        }
#endif

        public void Dispose()
        {
            _loadingController.LoadingFinished -= LoadingFinishedHandler;
            _comicsController.OnComicsCompleted -= ComicsCompletedHandler;
            _videoCutsceneController.OnCutsceneStarted -= VideoCutsceneStartedHandler;
            _videoCutsceneController.OnCutsceneCompleted -= VideoCutsceneCompletedHandler;
            _mapController.OnBackRequested -= MapBackRequestedHandler;
        }
    }
}