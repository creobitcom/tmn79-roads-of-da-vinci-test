using System.Collections.Generic;
using System.Threading;
using Creobit.AddressablesController;
using Creobit.Localization;
using Creobit.Logger;
using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;
using VContainer.Unity;

namespace Creobit.UI
{
    public sealed class UIController : IUIController
    {
        /// <summary>
        /// Maintains a dictionary of all available panels by their unique identifiers (AssetGUID).
        /// Each panel is mapped to its associated <see cref="PanelData"/> instance,
        /// which holds the data and state necessary for the panel's lifecycle and operations.
        /// This variable is used internally to manage and cache panels for efficient reuse,
        /// and supports operations such as loading, caching, showing, hiding, and unloading panels.
        /// </summary>
        private readonly Dictionary<string, PanelData> _availablePanels = new();
        private readonly Dictionary<string, UniTask> _loadPanelsTasks = new();

        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private readonly Dictionary<string, DOTweenAnimation[]> _panelLastAnimations = new();
        private readonly Dictionary<string, Dictionary<PanelState, DOTweenAnimation[]>> _panelAnimations = new();
        private readonly Dictionary<string, int> _panelHideSession = new();
        private readonly Dictionary<TMP_Text, string> _panelTextKeys = new();

        private readonly Dictionary<PanelData, TMP_Text[]> _panelTexts = new();


        private IAddressablesController _addressablesController;
        private IObjectResolver _objectResolver;

        private GameObject _persistentUIObject;

        [Inject]
        private void Construct(IAddressablesController addressablesController,
            IObjectResolver objectResolver)
        {
            _addressablesController = addressablesController;
            _objectResolver = objectResolver;
        }

        public async UniTask LoadPanels(LoadablePanelReference[] loadablePanelReferences,
            bool state = false, bool parallel = false)
        {
            if (loadablePanelReferences == null || loadablePanelReferences.Length == 0)
            {
                return;
            }

            if (!parallel)
            {
                foreach (var panelReference in loadablePanelReferences)
                {
                    await LoadPanel(panelReference.Reference.UIPanelReference, panelReference.Parent, state);
                }

                return;
            }

            var tasks = new UniTask[loadablePanelReferences.Length];

            for (var i = 0; i < loadablePanelReferences.Length; i++)
            {
                var panelReference = loadablePanelReferences[i];
                tasks[i] = LoadPanel(panelReference.Reference.UIPanelReference, panelReference.Parent, state);
            }

            await UniTask.WhenAll(tasks);
        }

        public async UniTask PreservePanels(PreservedPanelReference[] preservedPanelReferences,
            bool parallel = false)
        {
            if (preservedPanelReferences == null || preservedPanelReferences.Length == 0)
            {
                return;
            }

            if (!parallel)
            {
                foreach (var panelReference in preservedPanelReferences)
                {
                    await PreservePanel(panelReference);
                }

                return;
            }

            var tasks = new UniTask[preservedPanelReferences.Length];

            for (var i = 0; i < preservedPanelReferences.Length; i++)
            {
                tasks[i] = PreservePanel(preservedPanelReferences[i]);
            }

            await UniTask.WhenAll(tasks);
        }

        private async UniTask PreservePanel(PreservedPanelReference panelReference)
        {
            if (panelReference.Layer >= _persistentUIObject.transform.childCount)
            {
                Log.Bootstrap.Warning(
                    $"Panel {panelReference.Reference.UIPanelReference} {nameof(panelReference.Layer)} out of range.");
                return;
            }

            await LoadPanel(panelReference.Reference.UIPanelReference,
                _persistentUIObject.transform.GetChild(panelReference.Layer));
        }

        public void CachePanels(ScenePanelReference[] panelReferences)
        {
            foreach (var panelReference in panelReferences)
            {
                DropCachedPanelIfDestroyed(panelReference.Reference.UIPanelReference);

                if (_availablePanels.ContainsKey(panelReference.Reference.UIPanelReference.AssetGUID))
                    continue;

                CachePanelTexts(panelReference.Data);
                CachePanelData(panelReference.Reference.UIPanelReference, panelReference.Data);
                CachePanelAnimations(panelReference.Reference.UIPanelReference, panelReference.Data);
                LocalizePanelTexts(panelReference.Data);

                _loadPanelsTasks[panelReference.Reference.UIPanelReference.AssetGUID] = UniTask.CompletedTask;
            }
        }

        public void ShowPanel(PanelReference panelReference, Transform parent)
        {
            ShowPanel(panelReference.UIPanelReference, parent);
        }

        public async UniTask<T> ShowPanel<T>(PanelReference panelReference, Transform parent) where T : PanelData
        {
            ShowPanel(panelReference.UIPanelReference, parent);

            return await GetPanel(panelReference) as T;
        }
        
        public void HidePanel(PanelReference panelReference, Transform parent)
        {
            HidePanel(panelReference.UIPanelReference, parent);
        }

        private void LocaleChangedHandler(string language)
        {
            foreach (var panelData in _availablePanels.Values)
            {
                LocalizePanelTexts(panelData);
            }
        }

        public async UniTask Load(GameObject persistentUICanvas)
        {
            LocalizationService.Instance.OnLanguageChanged += LocaleChangedHandler;
            
            _persistentUIObject = persistentUICanvas;

            Object.DontDestroyOnLoad(_persistentUIObject);
        }

        public void UnloadPanel(PanelReference panelReference)
        {
            if (!_availablePanels.TryGetValue(panelReference.UIPanelReference.AssetGUID, out _))
            {
                return;
            }
            _addressablesController.UnloadAssetReference(panelReference.UIPanelReference);

            var data = _availablePanels[panelReference.UIPanelReference.AssetGUID];

            foreach (var tmpText in _panelTexts[data])
            {
                _panelTextKeys.Remove(tmpText);
            }

            _panelTexts.Remove(data);
            _panelAnimations.Remove(panelReference.UIPanelReference.AssetGUID);
            _panelLastAnimations.Remove(panelReference.UIPanelReference.AssetGUID);
            _panelHideSession.Remove(panelReference.UIPanelReference.AssetGUID);
            _availablePanels.Remove(panelReference.UIPanelReference.AssetGUID);
            _loadPanelsTasks.Remove(panelReference.UIPanelReference.AssetGUID);
        }

        public void Dispose()
        {
            LocalizationService.Instance.OnLanguageChanged -= LocaleChangedHandler;

            _cancellationTokenSource.Cancel();

            _cancellationTokenSource.Dispose();

            foreach (var panel in _availablePanels.Values)
            {
                panel.Dispose();
            }
        }

        private void ShowPanel(AssetReference assetReference, Transform parent = null)
        {
            SetPanelState(assetReference, PanelState.Show, parent, true);
        }

        private void HidePanel(AssetReference assetReference, Transform parent = null)
        {
            SetPanelState(assetReference, PanelState.Hide, parent, false);
        }

        private void SetPanelState(AssetReference assetReference, PanelState state, Transform parent, bool visibility)
        {
            DropCachedPanelIfDestroyed(assetReference);

            if (!_availablePanels.ContainsKey(assetReference.AssetGUID))
            {
                LoadPanel(assetReference, parent, visibility).Forget();
                return;
            }

            DOTweenAnimation[] animations = _panelAnimations[assetReference.AssetGUID][state];

            // If there are any previous animations running for panel, 
            // stop them to prevent overlapping or parallel animations
            if (_panelLastAnimations.TryGetValue(assetReference.AssetGUID, out var activeAnimations))
            {
                foreach (var animation in activeAnimations)
                {
                    if (animation == null || !animation.tween.IsActive())
                    {
                        continue;
                    }

                    animation.tween.Kill();
                }
            }

            _panelLastAnimations[assetReference.AssetGUID] = animations;

            var panelData = _availablePanels[assetReference.AssetGUID];
            var assetGuid = assetReference.AssetGUID;

            if (visibility)
            {
                BumpPanelHideSession(assetGuid);
                panelData.gameObject.SetActive(true);
                panelData.transform.SetAsLastSibling();
                SetPanelRaycasts(panelData, true);
            }
            else
            {
                SetPanelRaycasts(panelData, false);
            }

            foreach (var doTweenAnimation in animations)
            {
                var safeArea = Screen.safeArea;

                if (panelData.Right)
                {
                    doTweenAnimation.endValueV3 += Vector3.left * (Screen.width - safeArea.xMax);
                }

                if (panelData.Left)
                {
                    doTweenAnimation.endValueV3 += Vector3.right * safeArea.xMin;
                }

                doTweenAnimation.CreateTween(true);
            }

            if (!visibility)
            {
                DeactivatePanelAfterHide(panelData, assetGuid, animations).Forget();
            }
        }

        private void BumpPanelHideSession(string assetGuid)
        {
            _panelHideSession.TryGetValue(assetGuid, out var session);
            _panelHideSession[assetGuid] = session + 1;
        }

        private async UniTaskVoid DeactivatePanelAfterHide(
            PanelData panelData, string assetGuid, DOTweenAnimation[] animations)
        {
            BumpPanelHideSession(assetGuid);
            var session = _panelHideSession[assetGuid];

            foreach (var animation in animations)
            {
                if (animation == null || animation.tween == null || !animation.tween.IsActive())
                {
                    continue;
                }

                await animation.tween.AsyncWaitForCompletion();
            }

            if (panelData == null)
            {
                return;
            }

            if (!_panelHideSession.TryGetValue(assetGuid, out var current) || current != session)
            {
                return;
            }

            panelData.gameObject.SetActive(false);
        }

        private static void SetPanelRaycasts(PanelData panelData, bool enabled)
        {
            if (panelData == null)
            {
                return;
            }

            var canvasGroups = panelData.GetComponentsInChildren<CanvasGroup>(true);

            for (var i = 0; i < canvasGroups.Length; i++)
            {
                canvasGroups[i].blocksRaycasts = enabled;
                canvasGroups[i].interactable = enabled;
            }
        }

        private async UniTask LoadPanel(AssetReference panelReference, Transform parent, bool state = false)
        {
            var assetGuid = panelReference.AssetGUID;

            if (_loadPanelsTasks.TryGetValue(assetGuid, out var pendingTask)
                && pendingTask.Status == UniTaskStatus.Pending)
            {
                await pendingTask;

                TryLoadExistingPanel(panelReference, parent, state);

                return;
            }

            var loadTask = LoadPanelInternal(panelReference, parent, state).Preserve();

            _loadPanelsTasks[assetGuid] = loadTask;

            await loadTask;
        }

        private async UniTask LoadPanelInternal(AssetReference panelReference, Transform parent, bool state)
        {
            if (TryLoadExistingPanel(panelReference, parent, state))
            {
                return;
            }

            var panelPrefab = await _addressablesController.LoadAssetByReferenceAsync<GameObject>(panelReference);

            if (panelPrefab == null)
            {
                Log.Bootstrap.Error($"[UIController] Panel prefab is null for {panelReference.AssetGUID}.");

                return;
            }

            if (_availablePanels.ContainsKey(panelReference.AssetGUID))
            {
                TryLoadExistingPanel(panelReference, parent, state);

                return;
            }

            var panelData = await InstantiatePanel(panelPrefab, parent);

            if (panelData == null || _availablePanels.ContainsKey(panelReference.AssetGUID))
            {
                if (panelData != null)
                {
                    Object.Destroy(panelData.gameObject);
                }

                return;
            }

            CachePanelTexts(panelData);
            CachePanelData(panelReference, panelData);
            CachePanelAnimations(panelReference, panelData);

            SetInitialWindowState(panelData, panelReference, parent, state);
            LocalizePanelTexts(panelData);
        }

        private void CachePanelTexts(PanelData panelData)
        {
            if (_panelTexts.ContainsKey(panelData))
                return;

            _panelTexts.Add(panelData, panelData.GetComponentsInChildren<TMP_Text>(true));

            foreach (var tmpText in _panelTexts[panelData])
            {
                if (!_panelTextKeys.ContainsKey(tmpText))
                {
                    _panelTextKeys.Add(tmpText, tmpText.text);
                }
            }
        }

        private void DropCachedPanelIfDestroyed(AssetReference assetReference)
        {
            var assetGuid = assetReference.AssetGUID;

            if (!_availablePanels.TryGetValue(assetGuid, out var panelData))
            {
                return;
            }

            if (panelData != null && !IsAnyPanelAnimationDestroyed(assetGuid))
            {
                return;
            }

            _addressablesController.UnloadAssetReference(assetReference);

            if (_panelTexts.TryGetValue(panelData, out var panelTexts))
            {
                foreach (var tmpText in panelTexts)
                {
                    _panelTextKeys.Remove(tmpText);
                }
            }

            _panelTexts.Remove(panelData);
            _panelAnimations.Remove(assetGuid);
            _panelLastAnimations.Remove(assetGuid);
            _panelHideSession.Remove(assetGuid);
            _availablePanels.Remove(assetGuid);
            _loadPanelsTasks.Remove(assetGuid);
        }

        private bool IsAnyPanelAnimationDestroyed(string assetGuid)
        {
            if (!_panelAnimations.TryGetValue(assetGuid, out var animationsByState))
            {
                return false;
            }

            foreach (var animations in animationsByState.Values)
            {
                foreach (var animation in animations)
                {
                    if (animation == null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryLoadExistingPanel(AssetReference panelReference, Transform parent, bool state)
        {
            DropCachedPanelIfDestroyed(panelReference);

            if (!_availablePanels.TryGetValue(panelReference.AssetGUID, out _))
            {
                return false;
            }

            if (state)
            {
                ShowPanel(panelReference, parent);
            }

            return true;
        }

        private async UniTask<PanelData> InstantiatePanel(GameObject panelPrefab, Transform parent)
        {
            var panelInstance = await panelPrefab.InstantiateAsync(parent, _cancellationTokenSource.Token);

            panelInstance.transform.localPosition = panelPrefab.transform.localPosition;

            return panelInstance.GetComponent<PanelData>();
        }

        private void CachePanelData(AssetReference panelReference, PanelData panelData)
        {
            _availablePanels.Add(panelReference.AssetGUID, panelData);

            _objectResolver.InjectGameObject(panelData.gameObject);

            panelData.Load().Forget();
        }

        private void CachePanelAnimations(AssetReference panelReference, PanelData panelData)
        {
            var animations = new Dictionary<PanelState, DOTweenAnimation[]>();

            foreach (var panelAnimationData in panelData.PanelAnimations)
            {
                animations.Add(panelAnimationData.PanelState,
                    panelAnimationData.AnimationPrefab.GetComponents<DOTweenAnimation>());
            }

            _panelAnimations.Add(panelReference.AssetGUID, animations);
        }

        public void ClearCache()
        {
            _panelTexts.Clear();
            _panelTextKeys.Clear();
            _availablePanels.Clear();
            _panelAnimations.Clear();
            _panelLastAnimations.Clear();
            _panelHideSession.Clear();
        }

        private void SetInitialWindowState(PanelData panelInstance, AssetReference panelReference, Transform parent,
            bool state)
        {
            if (state)
            {
                ShowPanel(panelReference, parent);

                return;
            }

            panelInstance.gameObject.SetActive(false);
        }

        private void LocalizePanelTexts(PanelData panelData)
        {
            foreach (var tmpText in _panelTexts[panelData])
            {
                tmpText.text = LocalizationService.Instance.GetText(_panelTextKeys[tmpText]);
            }
        }

        private void LocaleChangedHandler()
        {
            foreach (var panelData in _panelTexts.Keys) LocalizePanelTexts(panelData);
        }

        public async UniTask<PanelData> GetPanel(PanelReference panelReference)
        {
            DropCachedPanelIfDestroyed(panelReference.UIPanelReference);

            if (!_availablePanels.ContainsKey(panelReference.UIPanelReference.AssetGUID)
                && !_loadPanelsTasks.ContainsKey(panelReference.UIPanelReference.AssetGUID))
            {
                return null;
            }

            await UniTask.WaitUntil(() => _availablePanels.ContainsKey(panelReference.UIPanelReference.AssetGUID) ||
                _loadPanelsTasks[panelReference.UIPanelReference.AssetGUID].Status is not UniTaskStatus.Pending);

            return _availablePanels.GetValueOrDefault(panelReference.UIPanelReference.AssetGUID);
        }

        public DOTweenAnimation[] GetPanelAnimations(PanelReference panelReference, PanelState state)
        {
            return _panelAnimations[panelReference.UIPanelReference.AssetGUID][state];
        }
    }
}
