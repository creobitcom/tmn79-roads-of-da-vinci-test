using Creobit.AddressablesController;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Fading
{
    public class FadeController : IFadeController
    {
        private IAddressablesController _addressablesController;

        private FadeView _fadeView;

        [Inject]
        private void Construct(IAddressablesController addressablesController)
        {
            _addressablesController = addressablesController;
        }

        public async UniTask Init(GameObject fadeView)
        {
            _fadeView = Object.Instantiate(fadeView).GetComponent<FadeView>();
            _fadeView.gameObject.SetActive(false);

            Object.DontDestroyOnLoad(_fadeView);
        }

        public async UniTask FadeIn()
        {
            await _fadeView.FadeIn();
        }

        public async UniTask FadeOut()
        {
            await _fadeView.FadeOut();
        }

        public async UniTask FadeInOut()
        {
            await FadeIn();
            FadeOut().Forget();
        }
    }
}
