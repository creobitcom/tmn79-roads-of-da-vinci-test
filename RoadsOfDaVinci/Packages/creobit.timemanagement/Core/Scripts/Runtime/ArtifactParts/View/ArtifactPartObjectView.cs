using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using Creobit.AddressablesController;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.View
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ArtifactPartObjectView : MonoBehaviour, IReactToActions
    {
        [SerializeField]
        private SpriteRenderer _spriteRenderer;

        [SerializeField]
        private DOTweenAnimation _showAnimation;

        [SerializeField]
        private DOTweenAnimation _hideAnimation;

        private IAddressablesController _addressablesController;
        
        [field: SerializeField]
        public GameplayArtifactPartData Data { get; private set; }

        [field: SerializeField]
        public bool CanReactToPrimaryAction { get; private set; }

        public bool CanReactToSecondaryAction { get; private set; }

        public ObjectViewInteractionType InteractionType { get; private set; }

        public float InteractionTime { get; private set; }

        public bool AlwaysReactToPrimaryAction { get; set; }

        public MovableObjectTaskView CurrentMovableObjectTaskView { get; set; }

        public float InteractionSpeed { get; set; }
        public bool IsInactiveBlocked { get; set; } = false;

        public event Action Interacted;

        [Inject]
        private void Construct(IArtifactPartsController artifactPartsController, IAddressablesController addressablesController)
        {
            _addressablesController = addressablesController;
            artifactPartsController.Service.SetArtifactObject(this);
        }

        private void OnDestroy()
        {
            _addressablesController.UnloadAssetReference(Data.ArtifactPart.ObjectColoredSprite);
        }
        
        public async UniTaskVoid Initialize()
        {
            _spriteRenderer.sprite = await _addressablesController.LoadAssetByReferenceAsync<Sprite>(Data.ArtifactPart.ObjectColoredSprite);
        }

        public UniTask Interact(MovableObjectView unit = null)
        {
            return UniTask.CompletedTask;
        }

        public bool IsFree { get; } = true;

        public void OnPrimaryAction()
        {
            Interacted?.Invoke();
            return;
        }

        public void OnSecondaryActionEnd()
        {
            return;
        }

        public void OnSecondaryActionStart()
        {
            return;
        }

        public void Show(bool immediately)
        {
            CanReactToPrimaryAction = true;

            if (!immediately)
            {
                _showAnimation.CreateTween();
                return;
            }

            gameObject.SetActive(true);

            _spriteRenderer.color = new(_spriteRenderer.color.r, _spriteRenderer.color.g, _spriteRenderer.color.b, 1);
        }

        public void Hide(bool immediately)
        {
            CanReactToPrimaryAction = false;

            if (!immediately)
            {
                _hideAnimation.CreateTween();
                return;
            }

            gameObject.SetActive(false);

            _spriteRenderer.color = new(_spriteRenderer.color.r, _spriteRenderer.color.g, _spriteRenderer.color.b, 0);
        }
    }
}