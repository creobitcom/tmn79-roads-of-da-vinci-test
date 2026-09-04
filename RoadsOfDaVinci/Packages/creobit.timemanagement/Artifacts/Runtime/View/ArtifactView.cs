using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Artifacts.Runtime.Data;
using _8floor.TimeManagement.Artifacts.Runtime.Service;
using Creobit.AddressablesController;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Artifacts.Runtime.View
{
    public class ArtifactView : MonoBehaviour
    {
        private readonly List<AssetReferenceT<Sprite>> _partsSpriteReferences = new();
        
        [SerializeField]
        private Image[] _partImages;

        private IArtifactsService _artifactsService;
        
        private IAddressablesController _addressablesController;

        public int ReceivedPartsAmount { get; private set; }

        public int PartsAmount => Artifact.Parts.Count;

        public ArtifactDataSO Artifact { get; private set; }

        [Inject]
        private void Construct(IArtifactsService artifactsService, IAddressablesController addressablesController)
        {
            _artifactsService = artifactsService;
            _addressablesController = addressablesController;
        }

        private void OnDestroy()
        {
            foreach (var sprite in _partsSpriteReferences)
            {
                _addressablesController.UnloadAssetReference(sprite);
            }
            
            _partsSpriteReferences.Clear();
        }
        
        public async UniTask Initialize(ArtifactDataSO artifact)
        {
            Artifact = artifact;

            if (_partImages.Length < Artifact.Parts.Count)
            {
                throw new InvalidOperationException($"Not enough part images assigned. Required: {Artifact.Parts.Count}, current: {_partImages.Length}");
            }
            
            var loadPartSpriteTasks = new UniTask[Artifact.Parts.Count];

            for (int i = 0; i < Artifact.Parts.Count; i++)
            {
                var part = Artifact.Parts[i];
                var partImage = _partImages[i];

                if (!_artifactsService.PartIsReceived(part.Name))
                {
                    loadPartSpriteTasks[i] = LoadPartSprite(part.PanelUnColoredSprite, partImage);
                    continue;
                }

                ReceivedPartsAmount += 1;

                loadPartSpriteTasks[i] = LoadPartSprite(part.PanelColoredSprite, partImage);
            }
            
            await UniTask.WhenAll(loadPartSpriteTasks);
        }
        private async UniTask LoadPartSprite(AssetReferenceT<Sprite> reference, Image partImage)
        {
            partImage.sprite = await _addressablesController.LoadAssetByReferenceAsync<Sprite>(reference);
            
            _partsSpriteReferences.Add(reference);
        }
    }
}