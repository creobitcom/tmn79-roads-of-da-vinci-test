using UnityEngine;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Fading
{
    public class FadeBridge : MonoBehaviour
    {
        private IFadeController _fadeController;

        [Inject]
        private void Construct(IFadeController fadeController)
        {
            _fadeController = fadeController;
        }

        public void FadeIn()
        {
            _fadeController.FadeIn();
        }

        public void FadeOut()
        {
            _fadeController.FadeOut();
        }

        public void FadeInOut()
        {
            _fadeController.FadeInOut();
        }
    }
}