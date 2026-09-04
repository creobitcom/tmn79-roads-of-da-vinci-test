using _8floor.TimeManagement.Core.Scripts.Runtime.Extras;
using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/VideoGuides/GuidesVideoBridge")]
    public class GuidesVideoBridge : MonoBehaviour, IGuideVideoTarget
    {
        [SerializeField] private VideoGuidesController _videoGuidesController;
        [SerializeField] private GameObject _playerVisual;

        private void Awake()
        {
            ResolveController();

            if (_videoGuidesController == null)
            {
                return;
            }

            _videoGuidesController.OnSuccess += ShowPlayer;
            _videoGuidesController.OnFail += HidePlayer;
            _videoGuidesController.OnError += HidePlayer;

            HidePlayer();
        }

        private void OnDestroy()
        {
            if (_videoGuidesController == null)
            {
                return;
            }

            _videoGuidesController.OnSuccess -= ShowPlayer;
            _videoGuidesController.OnFail -= HidePlayer;
            _videoGuidesController.OnError -= HidePlayer;
        }

        public void ShowLevelVideo(int levelNumber)
        {
            ResolveController();

            if (_videoGuidesController == null)
            {
                return;
            }

            HidePlayer();
            _videoGuidesController.LoadLevel(levelNumber);
        }

        private void ResolveController()
        {
            if (_videoGuidesController == null)
            {
                _videoGuidesController = GetComponentInChildren<VideoGuidesController>(true);
            }
        }

        private void ShowPlayer() => SetPlayerVisible(true);

        private void HidePlayer() => SetPlayerVisible(false);

        private void SetPlayerVisible(bool visible)
        {
            if (_playerVisual != null)
            {
                _playerVisual.SetActive(visible);
            }
        }
    }
}
