using System;
using System.Collections.Generic;
using Creobit.Audio;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.CollectionItems
{
    /// <summary>
    /// Плашка «получен предмет коллекции» — визуально как уведомление об ачивке (иконка выехала/уехала),
    /// но полностью автономная: ни на какие контроллеры не подписана, показывает только то, что ей
    /// передали через <see cref="Show"/>. Вызов приходит с предмета (CollectionItemNotifier на UltEvent'е
    /// SaveBridge._onSaveCollectionItem). Ссылка на инстанс в сцене — через GameplaySceneReferences.
    /// </summary>
    public class CollectionNotificationView : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image _iconImage;

        [Header("Animations")]
        [SerializeField] private DOTweenAnimation _showAnimation;
        [SerializeField] private DOTweenAnimation _hideAnimation;

        [Header("Settings")]
        [SerializeField] private float _showDuration = 3f;
        [SerializeField] private AudioClip _sound;

        private IAudioService _audioService;

        private readonly Queue<Sprite> _queue = new();
        private bool _isAnimating;

        [Inject]
        private void Construct(IAudioService audioService)
        {
            _audioService = audioService;
        }

        /// <summary>Ставит иконку в очередь показа. null молча игнорируется.</summary>
        public void Show(Sprite icon)
        {
            if (icon == null)
            {
                return;
            }

            _queue.Enqueue(icon);

            if (!_isAnimating)
            {
                ProcessQueue().Forget();
            }
        }

        private async UniTaskVoid ProcessQueue()
        {
            _isAnimating = true;

            while (_queue.Count > 0)
            {
                _iconImage.sprite = _queue.Dequeue();

                if (_audioService != null && _sound != null)
                {
                    _audioService.PlaySfx(_sound);
                }

                _showAnimation.CreateTween(true);
                await UniTask.Delay(TimeSpan.FromSeconds(_showAnimation.duration));

                await UniTask.Delay(TimeSpan.FromSeconds(_showDuration));

                _hideAnimation.CreateTween(true);
                await UniTask.Delay(TimeSpan.FromSeconds(_hideAnimation.duration));

                await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
            }

            _isAnimating = false;
        }
    }
}
