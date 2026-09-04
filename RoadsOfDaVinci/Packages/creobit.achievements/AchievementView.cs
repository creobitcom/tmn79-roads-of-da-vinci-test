using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements
{
    public class AchievementView : MonoBehaviour
    {
        public Button achievementButton;
        public Image icon;
        public AchievementBase achievementObject;
        
        public Sprite enabledIcon;
        public Sprite disablesIcon;

        public void SetView(bool state)
        {
            icon.sprite = state ? enabledIcon : disablesIcon;
        }
    }
}