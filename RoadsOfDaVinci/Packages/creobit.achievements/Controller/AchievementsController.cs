using System;
using System.Collections.Generic;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Controller
{
    public class AchievementsController : IAchievementsController
    {
        private IObjectResolver _objectResolver;
        private ISaveController _saveController;

        private readonly Dictionary<AchievementBase, AchievementObject> _achievements = new ();
        private readonly List<AchievementBase> _earnedThisSession = new();
        
        public IReadOnlyList<AchievementBase> EarnedThisSession => _earnedThisSession;
        
        public event Action<AchievementBase> OnCompleteAchievement;
        
        public IReadOnlyDictionary<AchievementBase, AchievementObject> Achievements => _achievements;
        
        [Inject]
        public void Construct(IObjectResolver objectResolver, ISaveController saveController)
        {
            _objectResolver = objectResolver;
            _saveController = saveController;
        }

        public UniTask Load()
        {
            return UniTask.CompletedTask;
        }

        public void ChangeAchievementProgress(AchievementBase achievementToChange, short amount)
        {
            if (_achievements.TryGetValue(achievementToChange, out var achievement)) 
            {
                achievement.ChangeProgress(amount);
            }            
        }

        private void AchievementCompleted(AchievementBase achievement)
        {
            if (_achievements.ContainsKey(achievement) == false)
                return;

            if (!_earnedThisSession.Contains(achievement))
            {
                _earnedThisSession.Add(achievement);
            }
            
            var parseAchievement = new AchievementSaveData(achievement.name, _achievements[achievement].AchievementStatus.ToString());
            _saveController.Service.SaveAchievement(parseAchievement);
            
            Log.Gameplay.Error("Complete " + achievement.name + " achievement");
            
            OnCompleteAchievement?.Invoke(achievement);
        }

        public void LoadAchievement(AchievementBase achievement)
        {
            if (_achievements.ContainsKey(achievement) == false) return;

            if (_achievements[achievement] != null)
            {
                _achievements[achievement].Dispose();
            }

            var achievementObject = achievement.GetAchievement();
            _objectResolver.Inject(achievementObject);
            achievementObject.AddAchievementCompleted(() => AchievementCompleted(achievement));

            if (_saveController.Service.TryGetAchievement(achievement.name, out var savedAchievement))
            {
                var parsedAchievement = new Creobit.Bootstrap.Core.Scripts.Runtime.Achievements.AchievementSaveData(savedAchievement.AchievementName, savedAchievement.AchievementStatus);
        
                achievementObject.AchievementStatus = parsedAchievement.AchievementStatus;
            }
            
            achievementObject.Setup();

            _achievements[achievement] = achievementObject;
        }

        public UniTask Load(AchievementsData achievementsData)
        {
            foreach (var oldAchievementObject in _achievements.Values)
            {
                oldAchievementObject?.Dispose();
            }
            
            _achievements.Clear();
            _earnedThisSession.Clear(); 
            
            foreach (var achievement in achievementsData.achievements)
            {
                _achievements.TryAdd(achievement, null);
                LoadAchievement(achievement);
            }
    
            return UniTask.CompletedTask;
        }
    }
}