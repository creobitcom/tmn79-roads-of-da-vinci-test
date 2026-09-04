using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Controller;
using Creobit.Audio;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.View
{
    public class AchievementNotificationView : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image _iconImage;

        [Header("Animations")]
        [SerializeField] private DOTweenAnimation _showAnimation;
        [SerializeField] private DOTweenAnimation _hideAnimation;
        
        [Header("Settings")]
        [SerializeField] private float _showDuration = 3f;
        [SerializeField] private AudioClip _achievementSound;

        private IAchievementsController _achievementsController;
        private IAudioService _audioService;
        
        private readonly Queue<AchievementBase> _achievementsQueue = new();
        private bool _isAnimating;

        [Inject]
        private void Construct(IAchievementsController achievementsController, IAudioService audioService)
        {
            _achievementsController = achievementsController;
            _audioService = audioService;
        }

        private void Start()
        {
            if (_achievementsController != null)
            {
                _achievementsController.OnCompleteAchievement += OnAchievementCompleted;
            }
        }

        private void OnDestroy()
        {
            if (_achievementsController != null)
            {
                _achievementsController.OnCompleteAchievement -= OnAchievementCompleted;
            }
        }

        private void OnAchievementCompleted(AchievementBase achievement)
        {
            _achievementsQueue.Enqueue(achievement);

            if (!_isAnimating)
            {
                ProcessQueue().Forget();
            }
        }

        private async UniTaskVoid ProcessQueue()
        {
            _isAnimating = true;

            while (_achievementsQueue.Count > 0)
            {
                var achievement = _achievementsQueue.Dequeue();
                var data = achievement.AchievementData;

                if (data.AchievementIcon != null)
                {
                    _iconImage.sprite = data.AchievementIcon;
                }

                if (_audioService != null && _achievementSound != null)
                {
                    _audioService.PlaySfx(_achievementSound);
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