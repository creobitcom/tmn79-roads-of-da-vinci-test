using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Fading
{
    public class FadeView : MonoBehaviour
    {

        [SerializeField] private Image _fadeImage;
        [SerializeField] private float _inDuration;
        [SerializeField] private float _outDuration;
        [SerializeField] private Ease _ease;

        public async UniTask FadeIn()
        {
            gameObject.SetActive(true);
            await PlayFadeTween(0f, 1f, _inDuration);
        }

        public async UniTask FadeOut()
        {
            await PlayFadeTween(1f, 0f, _outDuration);

            if (this == null)
            {
                return;
            }

            gameObject.SetActive(false);
        }

        private async UniTask PlayFadeTween(float startValue, float endValue, float duration)
        {
            await _fadeImage.DOFade(endValue, duration)
                .From(startValue)
                .SetEase(_ease)
                .AsyncWaitForCompletion();
        }

    }
}
