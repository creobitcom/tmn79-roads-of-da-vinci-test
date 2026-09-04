using System;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    [Serializable]
    public class GuideLocationData
    {
        public int Num;
        public string LocationName;
        public Sprite Icon;

        public GuideLocationData(int num, string locationName, Sprite icon)
        {
            Num = num;
            LocationName = locationName;
            Icon = icon;
        }
    }
}
