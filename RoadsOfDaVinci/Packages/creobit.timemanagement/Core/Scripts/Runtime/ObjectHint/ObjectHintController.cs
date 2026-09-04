using System;
using System.Collections.Generic;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using Creobit.Loading;
using Creobit.Logger;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectHint
{
    public class ObjectHintController : ILoadUnit, IReloadable, IDisposable
    {
        private readonly IReloadController _reloadController;
        private readonly GameplaySceneReferences _gameplaySceneReferences;
        private readonly IObjectViewController _objectViewController;
        private readonly IComplexObjectController _complexObjectController;
        private readonly ITutorialController _tutorialController;
        private readonly IUIController _uiController;
        private readonly IGameplayInputSystem _inputSystem;
        private readonly ICutsceneController _cutsceneController;
        private readonly ILevelController _levelController;

        private readonly List<ObjectView.ObjectView> _hoverSources = new();
        private readonly List<ComplexObject> _cocSources = new();
        private readonly HashSet<TutorialViewSo> _requestedHints = new();

        private TutorialViewSo _activeHint;
        private ObjectHintSource _activeSource;
        private Component _activeTarget;
        private CancellationTokenSource _autoHide;
        private int _requestId;

        [Inject]
        private ObjectHintController(IReloadController reloadController,
            GameplaySceneReferences gameplaySceneReferences,
            IObjectViewController objectViewController,
            IComplexObjectController complexObjectController,
            ITutorialController tutorialController,
            IUIController uiController,
            IGameplayInputSystem inputSystem,
            ICutsceneController cutsceneController,
            ILevelController levelController)
        {
            _reloadController = reloadController;
            _gameplaySceneReferences = gameplaySceneReferences;
            _objectViewController = objectViewController;
            _complexObjectController = complexObjectController;
            _tutorialController = tutorialController;
            _uiController = uiController;
            _inputSystem = inputSystem;
            _cutsceneController = cutsceneController;
            _levelController = levelController;
        }

        public UniTask Load()
        {
            _reloadController.AddReloadableObject(this);

            _objectViewController.OnObjectViewAdded -= HookHover;
            _objectViewController.OnObjectViewAdded += HookHover;
            _complexObjectController.ObjectViewAdded -= HookCoc;
            _complexObjectController.ObjectViewAdded += HookCoc;
            _objectViewController.OnAlternativeSelectionShown -= ShowForSelection;
            _objectViewController.OnAlternativeSelectionShown += ShowForSelection;
            _objectViewController.OnAlternativeSelectionClosed -= HideSelection;
            _objectViewController.OnAlternativeSelectionClosed += HideSelection;
            _objectViewController.OnNotPathTooltip -= ShowForBlockedClick;
            _objectViewController.OnNotPathTooltip += ShowForBlockedClick;
            _objectViewController.OnNotResourcesTooltip -= ShowForBlockedClick;
            _objectViewController.OnNotResourcesTooltip += ShowForBlockedClick;
            _inputSystem.OnMainButtonPressed -= HideOnNextClick;
            _inputSystem.OnMainButtonPressed += HideOnNextClick;
            _inputSystem.PrimaryActionPerformed -= OnPrimaryActionPerformed;
            _inputSystem.PrimaryActionPerformed += OnPrimaryActionPerformed;
            _cutsceneController.CutsceneStarted -= Hide;
            _cutsceneController.CutsceneStarted += Hide;
            _levelController.FinishScreenShowed -= Hide;
            _levelController.FinishScreenShowed += Hide;
            _levelController.WinViewShowing -= Hide;
            _levelController.WinViewShowing += Hide;

            return UniTask.CompletedTask;
        }

        public UniTask Reload()
        {
            Hide();
            UnhookHover();

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _objectViewController.OnObjectViewAdded -= HookHover;
            _complexObjectController.ObjectViewAdded -= HookCoc;
            _objectViewController.OnAlternativeSelectionShown -= ShowForSelection;
            _objectViewController.OnAlternativeSelectionClosed -= HideSelection;
            _objectViewController.OnNotPathTooltip -= ShowForBlockedClick;
            _objectViewController.OnNotResourcesTooltip -= ShowForBlockedClick;
            _inputSystem.OnMainButtonPressed -= HideOnNextClick;
            _inputSystem.PrimaryActionPerformed -= OnPrimaryActionPerformed;
            _cutsceneController.CutsceneStarted -= Hide;
            _levelController.FinishScreenShowed -= Hide;
            _levelController.WinViewShowing -= Hide;

            Hide();
            UnhookHover();
        }

        private void HookHover(ObjectView.ObjectView objectView)
        {
            if (objectView == null || !objectView.TryGetComponent<ObjectHintBinding>(out var binding)
                || !binding.ShowOnHover)
            {
                return;
            }

            objectView.OnShowTooltip -= ShowForHover;
            objectView.OnShowTooltip += ShowForHover;
            objectView.OnHideTooltip -= HideHover;
            objectView.OnHideTooltip += HideHover;

            if (!_hoverSources.Contains(objectView))
            {
                _hoverSources.Add(objectView);
            }
        }

        private void HookCoc(ComplexObject complexObject)
        {
            if (complexObject == null || !complexObject.TryGetComponent<ObjectHintBinding>(out var binding))
            {
                return;
            }

            if (binding.ShowOnHover)
            {
                complexObject.OnShowTooltip -= ShowForCocHover;
                complexObject.OnShowTooltip += ShowForCocHover;
                complexObject.OnHideTooltip -= HideCocHover;
                complexObject.OnHideTooltip += HideCocHover;
            }

            if (binding.ShowOnBlockedClick)
            {
                complexObject.OnNotResources -= ShowForCocBlockedClick;
                complexObject.OnNotResources += ShowForCocBlockedClick;
                complexObject.OnNotPath -= ShowForCocBlockedClick;
                complexObject.OnNotPath += ShowForCocBlockedClick;
            }

            if (!_cocSources.Contains(complexObject))
            {
                _cocSources.Add(complexObject);
            }
        }

        private void UnhookHover()
        {
            foreach (var objectView in _hoverSources)
            {
                if (objectView == null)
                {
                    continue;
                }

                objectView.OnShowTooltip -= ShowForHover;
                objectView.OnHideTooltip -= HideHover;
            }

            _hoverSources.Clear();

            foreach (var complexObject in _cocSources)
            {
                if (complexObject == null)
                {
                    continue;
                }

                complexObject.OnShowTooltip -= ShowForCocHover;
                complexObject.OnHideTooltip -= HideCocHover;
                complexObject.OnNotResources -= ShowForCocBlockedClick;
                complexObject.OnNotPath -= ShowForCocBlockedClick;
            }

            _cocSources.Clear();
        }

        private void ShowForHover(ObjectView.ObjectView objectView)
        {
            ShowForHover((Component)objectView);
        }

        private void ShowForCocHover(ComplexObject complexObject)
        {
            ShowForHover(complexObject);
        }

        private void ShowForHover(Component target)
        {
            if (!TryGetBinding(target, out var binding) || !binding.ShowOnHover)
            {
                return;
            }

            if (_activeTarget == target && (_activeSource == ObjectHintSource.BlockedClick || _activeSource == ObjectHintSource.SelectionPanel))
            {
                return;
            }

            Show(target, binding, ObjectHintSource.Hover);
        }

        private void HideHover(ObjectView.ObjectView objectView)
        {
            HideHover((Component)objectView);
        }

        private void HideCocHover(ComplexObject complexObject)
        {
            HideHover(complexObject);
        }

        private void HideHover(Component target)
        {
            if (_activeSource != ObjectHintSource.Hover || _activeTarget != target)
            {
                return;
            }

            Hide();
        }

        private void ShowForBlockedClick(ObjectView.ObjectView objectView)
        {
            ShowForBlockedClick((Component)objectView);
        }

        private void ShowForCocBlockedClick(ComplexObject complexObject)
        {
            ShowForBlockedClick(complexObject);
        }

        private void ShowForBlockedClick(Component target)
        {
            if (!TryGetBinding(target, out var binding) || !binding.ShowOnBlockedClick)
            {
                return;
            }

            Show(target, binding, ObjectHintSource.BlockedClick);
        }

        private void OnPrimaryActionPerformed(IReactToActions reactTo)
        {
            if (reactTo is not Component target)
            {
                return;
            }

            if (_activeSource == ObjectHintSource.BlockedClick && _activeTarget == target)
            {
                _requestId++;
            }

            if (!TryGetBinding(target, out var binding) || !binding.ShowOnNonInteractableClick)
            {
                return;
            }

            if (target is ObjectView.ObjectView objectView && objectView.CanInteract)
            {
                return;
            }

            Show(target, binding, ObjectHintSource.BlockedClick);
        }

        private void HideOnNextClick()
        {
            if (_activeSource != ObjectHintSource.BlockedClick || _activeHint == null)
            {
                return;
            }

            if (TryGetBinding(_activeTarget, out var binding) && !binding.BlockedClickCloseOnNextClick)
            {
                return;
            }

            CloseOnClick(_requestId).Forget();
        }

        private async UniTaskVoid CloseOnClick(int request)
        {
            await UniTask.NextFrame();

            if (_requestId != request)
            {
                return;
            }

            Hide();
        }

        private void ShowForSelection(ObjectView.ObjectView objectView)
        {
            if (!TryGetBinding(objectView, out var binding) || !binding.ShowOnSelectionPanel)
            {
                HideSelection(objectView);

                return;
            }

            var panel = _gameplaySceneReferences.ObjectAlternativeSelectionView;

            if (panel == null || !panel.IsShown)
            {
                return;
            }

            if (binding.SelectionMode == ObjectHintSelectionMode.OnlyWithSingleAlternative
                && panel.ShownAlternativesCount != 1)
            {
                return;
            }

            Show(objectView, binding, ObjectHintSource.SelectionPanel);
        }

        private void HideSelection(ObjectView.ObjectView objectView)
        {
            if (_activeSource != ObjectHintSource.SelectionPanel)
            {
                return;
            }

            Hide();
        }

        private void Show(Component target, ObjectHintBinding binding, ObjectHintSource source)
        {
            if (_cutsceneController.IsPlaying)
            {
                return;
            }

            var hintView = binding.HintView;

            if (hintView == null)
            {
                return;
            }

            CancelAutoHide();

            var request = ++_requestId;
            var alreadyShown = _activeHint == hintView && (_tutorialController.CurrentHint == hintView || _requestedHints.Contains(hintView));

            if (!alreadyShown)
            {
                HideActiveHint();
            }

            _activeTarget = target;
            _activeSource = source;
            _activeHint = hintView;
            _requestedHints.Add(hintView);

            if (!alreadyShown)
            {
                _tutorialController.ShowHint(hintView);
            }

            ApplyData(hintView, binding.BuildData(target), request).Forget();

            if (source != ObjectHintSource.BlockedClick || binding.BlockedClickDuration <= 0f)
            {
                return;
            }

            _autoHide = new CancellationTokenSource();

            HideAfterDelay(binding.BlockedClickDuration, request, _autoHide.Token).Forget();
        }

        private async UniTaskVoid ApplyData(TutorialViewSo hintView, ObjectHintData data, int request)
        {
            if (hintView.panelReference == null)
            {
                Log.Gameplay.Error($"Подсказка объекта: у окна '{hintView.name}' не задан panelReference.");

                return;
            }

            var panelData = await _uiController.GetPanel(hintView.panelReference);

            if (panelData == null || _requestId != request)
            {
                return;
            }

            var view = panelData.GetComponentInChildren<ObjectHintView>(true);

            if (view == null)
            {
                return;
            }

            view.SetData(data);
        }

        private async UniTaskVoid HideAfterDelay(float seconds, int request, CancellationToken token)
        {
            var cancelled = await UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true,
                cancellationToken: token).SuppressCancellationThrow();

            if (cancelled || _requestId != request)
            {
                return;
            }

            Hide();
        }

        private void Hide()
        {
            CancelAutoHide();

            _requestId++;

            HideActiveHint();
            ClearState();
        }

        private void HideActiveHint()
        {
            var currentHint = _tutorialController.CurrentHint;
            var isOurs = currentHint != null && _requestedHints.Contains(currentHint);

            _activeHint = null;
            _requestedHints.Clear();

            if (isOurs)
            {
                _tutorialController.HideHint();
            }
        }

        private void ClearState()
        {
            _activeHint = null;
            _activeTarget = null;
            _activeSource = ObjectHintSource.None;
        }

        private void CancelAutoHide()
        {
            if (_autoHide == null)
            {
                return;
            }

            _autoHide.Cancel();
            _autoHide.Dispose();
            _autoHide = null;
        }

        private static bool TryGetBinding(Component target, out ObjectHintBinding binding)
        {
            binding = null;

            return target != null && target.TryGetComponent(out binding) && binding.HintView != null;
        }
    }
}
