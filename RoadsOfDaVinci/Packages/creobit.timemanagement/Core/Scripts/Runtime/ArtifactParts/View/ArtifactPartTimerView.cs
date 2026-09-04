using R3;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using VContainer;
using UnityEngine.UI;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.Controller;
using Creobit.AddressablesController;
using Creobit.Localization;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.View
{
    public class ArtifactPartTimerView : MonoBehaviour
    {
        [SerializeField]
        private float _partMoveAnimationDuration;

        [SerializeField]
        private string _remainingLocalizationKey;

        [SerializeField]
        private TMP_Text _timerText;

        [SerializeField]
        private Image _partImage;

        [SerializeField]
        private RectTransform _partImageTransform;

        [SerializeField]
        private DOTweenAnimation _hideAnimation;

        [SerializeField]
        private DOTweenAnimation _showAnimation;

        [SerializeField]
        private DOTweenAnimation[] _partCollectedAnimations;

        private IArtifactPartsController _artifactPartsController;

        // Эта вьюшка живёт только в геймплейной сцене (см. GameplaySceneReferences в Construct),
        // поэтому Service здесь всегда фактически ArtifactPartsService (не Meta-вариант).
        // PartHidden/PartShowed/ActivePartRemainingSeconds — геймплейные, их нет в общей базе.
        private ArtifactPartsService GameplayService => (ArtifactPartsService)_artifactPartsController.Service;

        private IAddressablesController _addressablesController;

        private Camera _sceneCamera;

        [Inject]
        private void Construct(IArtifactPartsController artifactPartsController, IAddressablesController addressablesController, GameplaySceneReferences gameplaySceneReferences)
        {
            _artifactPartsController = artifactPartsController;
            _addressablesController = addressablesController;

            _sceneCamera = gameplaySceneReferences.MainCamera;
        }

        private void OnDestroy()
        {
            GameplayService.PartHidden -= OnPartHidden;

            GameplayService.PartShowed -= OnPartShowed;

            _artifactPartsController.Service.PartCollected -= OnPartCollected;
            try
            {
                _addressablesController.UnloadAssetReference(_artifactPartsController.Service.ArtifactObject.Data.ArtifactPart.ObjectUnColoredSprite);
                
                _addressablesController.UnloadAssetReference(_artifactPartsController.Service.ArtifactObject.Data.ArtifactPart.ObjectColoredSprite);
            }
            catch
            {
                //ignore
            }
        }

        private async void Start()
        {
            await UniTask.WaitUntil(() => _artifactPartsController.Service != null);

            GameplayService.PartHidden += OnPartHidden;

            GameplayService.PartShowed += OnPartShowed;

            _artifactPartsController.Service.PartCollected += OnPartCollected;

            GameplayService.ActivePartRemainingSeconds
                .Subscribe(OnIntervalTicked)
                .AddTo(this);
        }

        private void OnPartHidden()
        {
            _hideAnimation.CreateTween(true);
        }

        private void OnPartShowed()
        {
            UniTask.Void(async () =>
            {
                _partImage.sprite = await _addressablesController.LoadAssetByReferenceAsync<Sprite>(_artifactPartsController.Service.ArtifactObject.Data.ArtifactPart.ObjectUnColoredSprite);
                
                _showAnimation.CreateTween(true);
            });
        }

        private void OnPartCollected()
        {
            Sequence sequence = DOTween.Sequence();

            // Calculate the world position of the center of the part image rectangle relative to the UI.
            // This is used to move the artifact sprite from its current position to the target position on the timer panel.
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                        _partImageTransform,
                        _partImageTransform.position,
                        _sceneCamera,
                        out var targetPosition);

            // Animate the movement of the artifact object's transform to the calculated target position on the timer panel.
            sequence.Append(_artifactPartsController.Service.ArtifactObject.transform.DOMove(targetPosition, _partMoveAnimationDuration));

            sequence.AppendCallback(async () =>
            {
                _artifactPartsController.Service.ArtifactObject.Hide(true);
                
                _partImage.sprite = await _addressablesController.LoadAssetByReferenceAsync<Sprite>(_artifactPartsController.Service.ArtifactObject.Data.ArtifactPart.ObjectColoredSprite);
            });

            foreach (var animation in _partCollectedAnimations)
            {
                sequence.Append(animation.tween);
            }

            sequence.Play();
        }

        private void OnIntervalTicked(float remainingSeconds)
        {
            _timerText.text = $"{LocalizationService.Instance.GetText(_remainingLocalizationKey)} {remainingSeconds}";
        }
    }
}