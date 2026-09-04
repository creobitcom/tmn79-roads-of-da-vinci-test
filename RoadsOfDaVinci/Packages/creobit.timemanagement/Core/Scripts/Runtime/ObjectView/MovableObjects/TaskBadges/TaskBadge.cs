using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges
{
    [Serializable]
    public class TaskBadge
    {
        public ushort TaskId { get; set; }
            
        public Image BadgeImage { get; private set; }
        public GameObject Badge { get; private set; }
        public Image BackImage { get; private set; }
            
        public TextMeshProUGUI BadgeText { get; private set; }

        public TaskBadge(Image badgeImage, Image backImage, TextMeshProUGUI badgeText, GameObject badge)
        {
            BadgeImage = badgeImage;
            
            BackImage = backImage;

            BadgeText = badgeText;

            Badge = badge;
        }
    }
}