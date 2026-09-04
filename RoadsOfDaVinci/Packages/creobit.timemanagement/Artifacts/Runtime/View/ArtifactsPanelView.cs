using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Artifacts.Runtime.Data;
using _8floor.TimeManagement.Artifacts.Runtime.Service;
using Creobit.AddressablesController;
using Creobit.Localization;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace _8floor.TimeManagement.Artifacts.Runtime.View
{
    public class ArtifactsPanelView : PanelData
    {
        private const float ButtonThrottleSeconds = 0.1f;

        private readonly List<AssetReference> _viewsPrefabReferences = new();

        [SerializeField] private string _receivedPartsLocalizationKey;

        [SerializeField] private TMP_Text _nameText;

        [SerializeField] private TMP_Text _descriptionText;

        [SerializeField] private Button _previousButton;

        [SerializeField] private Button _nextButton;

        [SerializeField] private TMP_Text _receivedPartsAmountText;

        [SerializeField] private RectTransform _artifactsParent;

        private int _currentArtifactIndex = -1;

        private bool _currentArtifactIsLoaded;

        private IObjectResolver _objectResolver;

        private IAddressablesController _addressablesController;

        private IArtifactsService _artifactsService;

        private Dictionary<int, ArtifactView> _artifacts = new();

        [Inject]
        private void Construct(IObjectResolver objectResolver, IArtifactsService artifactsService,
            IAddressablesController addressablesController)
        {
            _objectResolver = objectResolver;
            _addressablesController = addressablesController;
            _artifactsService = artifactsService;
        }

        public override UniTask Load()
        {
            Observable
                .EveryValueChanged(gameObject, x => x.activeSelf) // detect if panel open or close
                .Skip(1) // skip initialization
                .Subscribe(OnPanelActiveStateChanged)
                .AddTo(this);

            _previousButton
                .OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(ButtonThrottleSeconds))
                .Subscribe(_ => { SelectArtifactByIndex(_currentArtifactIndex - 1).Forget(); }).AddTo(this);

            _nextButton
                .OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(ButtonThrottleSeconds))
                .Subscribe(_ => { SelectArtifactByIndex(_currentArtifactIndex + 1).Forget(); }).AddTo(this);

            return UniTask.CompletedTask;
        }

        private void OnDestroy()
        {
            UnloadViewPrefabs();
        }

        private void UnloadViewPrefabs()
        {
            foreach (var prefab in _viewsPrefabReferences)
            {
                _addressablesController.UnloadAssetReference(prefab);
            }

            _viewsPrefabReferences.Clear();
        }

        private void UpdateReceivedPartsAmountText()
        {
            var artifact = _artifacts[_currentArtifactIndex];

            _receivedPartsAmountText.text = _receivedPartsLocalizationKey == string.Empty
                ? $"{artifact.ReceivedPartsAmount}/{artifact.PartsAmount}"
                : $"{LocalizationService.Instance.GetText(_receivedPartsLocalizationKey)} {artifact.ReceivedPartsAmount}/{artifact.PartsAmount}";
        }

        private void UpdateButtonsInteractable()
        {
            _previousButton.gameObject.SetActive(_currentArtifactIndex > 0);
            _nextButton.gameObject.SetActive(_currentArtifactIndex < _artifactsService.AvailableArtifacts.Count - 1);
        }

        private void UpdateNameAndDescriptionTexts()
        {
            var artifact = _artifacts[_currentArtifactIndex].Artifact;

            _nameText.text = LocalizationService.Instance.GetText(artifact.NameLocalizationKey);
            _descriptionText.text = LocalizationService.Instance.GetText(artifact.DescriptionLocalizationKey);
        }

        private void DestroyArtifacts()
        {
            foreach (var artifact in _artifacts.Values)
            {
                Destroy(artifact.gameObject);
            }

            _artifacts.Clear();
        }

        private void OnPanelActiveStateChanged(bool isActive)
        {
            if (!isActive)
            {
                DestroyArtifacts();
                UnloadViewPrefabs();
                return;
            }

            SelectArtifactByIndex(0).Forget();
        }

        private async UniTaskVoid SelectArtifactByIndex(int index)
        {
            if (!_currentArtifactIsLoaded && _currentArtifactIndex != -1)
            {
                return;
            }

            if (index > _artifactsService.AvailableArtifacts.Count - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            _currentArtifactIsLoaded = false;

            if (_artifacts.TryGetValue(_currentArtifactIndex, out var previousArtifact))
            {
                previousArtifact.gameObject.SetActive(false);
            }

            _currentArtifactIndex = index;

            if (_artifacts.TryGetValue(_currentArtifactIndex, out var currentArtifact))
            {
                currentArtifact.gameObject.SetActive(true);
            }
            else
            {
                _artifacts.Add(index, await CreateArtifactView(_artifactsService.AvailableArtifacts[index]));
            }

            UpdateButtonsInteractable();

            UpdateReceivedPartsAmountText();

            UpdateNameAndDescriptionTexts();

            _currentArtifactIsLoaded = true;
        }

        private async UniTask<ArtifactView> CreateArtifactView(ArtifactDataSO artifact)
        {
            var prefab = await _addressablesController.LoadAssetByReferenceAsync<GameObject>(artifact.Prefab);

            _viewsPrefabReferences.Add(artifact.Prefab);

            var result = _objectResolver.Instantiate(prefab, _artifactsParent).GetComponent<ArtifactView>();

            await result.Initialize(artifact);

            return result;
        }
    }
}