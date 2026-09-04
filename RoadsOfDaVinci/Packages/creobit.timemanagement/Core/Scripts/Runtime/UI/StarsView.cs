using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.Data;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.UI
{
    public class StarsView : MonoBehaviour
    {

        [SerializeField] private Star[] _stars;
        [SerializeField] private float _showDelay;

        private CancellationTokenSource _showCancellation;

        public void SetStars(LevelTimerResult levelTimerResult)
        {
            var count = levelTimerResult.NumberOfStars;
            float showDelay = 0;

            ResetShowCancellation();

            _showCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            for (var index = 0; index < _stars.Length; index++)
            {
                var star = _stars[index];
                star.HideStar();

                if (index >= count)
                    continue;

                ShowStar(star, showDelay, _showCancellation.Token).Forget();

                showDelay += _showDelay;
            }
        }

        private async UniTask ShowStar(Star star, float delay, CancellationToken cancellationToken)
        {
            await UniTask.WaitForSeconds(delay, cancellationToken: cancellationToken);

            star.ShowStar();
        }

        private void OnDestroy() => ResetShowCancellation();

        private void ResetShowCancellation()
        {
            if (_showCancellation == null)
            {
                return;
            }

            _showCancellation.Cancel();
            _showCancellation.Dispose();
            _showCancellation = null;
        }

    }
}
