using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements
{
    public class AchievementsBridge : MonoBehaviour, IDisposable
    {
        private IAchievementsController _achievementsController;
        private ISaveController _saveController;
        private IPlayerProfilesController _playerProfileController;
        
        [SerializeField]
        private AchievementsData achievementsData;
        
        [SerializeField]
        private List<AchievementView> achievementViews = new();
        
        [Inject]
        public void Construct(
            IAchievementsController achievementsController,
            ISaveController saveController,
            IPlayerProfilesController playerProfileController)
        {
            _achievementsController = achievementsController;
            _saveController = saveController;
            _playerProfileController = playerProfileController;
            
            Load();

            _playerProfileController.Service.AddedProfile += OnProfileSelected;
            _playerProfileController.Service.SelectedProfile += OnProfileSelected;
            
            _achievementsController.OnCompleteAchievement += OnAchievementCompletedViewUpdate;
        }
        
        private void Load()
        {
            if (achievementsData == null)
                return;
            
            foreach (var achievement in achievementsData.achievements)
            {
                if (achievement == null)
                    continue;

                _achievementsController.LoadAchievement(achievement);
            }

            foreach (var achievementView in achievementViews)
            {
                if (achievementView == null)
                    continue;
  
                if (achievementView.achievementObject == null)
                {
                    continue; 
                } 
                
                var isSaved = _saveController.Service.TryGetAchievement(
                    achievementView.achievementObject.name, out var savedAch) 
                    && savedAch.AchievementStatus == AchievementStatus.Completed.ToString(); 

                achievementView.SetView(isSaved); 
            }
        }

        private void OnAchievementCompletedViewUpdate(AchievementBase achievement)
        {
            foreach (var achievementView in achievementViews)
            {
                if (achievementView != null && achievementView.achievementObject != null)
                {
                    if (achievement.name == achievementView.achievementObject.name)
                    {
                        achievementView.SetView(true);
                    }
                }
            }
        }

        public void AddAchievementProgress(AchievementBase achievement)
        {
            _achievementsController.ChangeAchievementProgress(achievement, 1);
        }

        private void OnProfileSelected(PlayerProfileData profile)
        {
            foreach (var achievementView in achievementViews)
            {
                if (achievementView == null) continue;

                achievementView.SetView(false);
            }
        }

        public void Dispose()
        {
            if (_playerProfileController != null)
            {
                _playerProfileController.Service.AddedProfile -= OnProfileSelected;
                _playerProfileController.Service.SelectedProfile -= OnProfileSelected;
            }
            
            if (_achievementsController != null)
            {
                _achievementsController.OnCompleteAchievement -= OnAchievementCompletedViewUpdate;
            }
        }
    }
}