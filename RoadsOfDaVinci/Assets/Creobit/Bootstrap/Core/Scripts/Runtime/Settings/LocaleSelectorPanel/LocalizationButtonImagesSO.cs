using UnityEngine;


namespace Creobit.Bootstrap.Core.Scripts.Runtime.Settings
{
    [CreateAssetMenu(menuName = "Creobit/Bootstrap/Localization Button Images")]
    public class LocalizationButtonImagesSO : ScriptableObject
    {
        public LocalizationButtonData[] LocalizationButtonData;
    }
    
    [System.Serializable]
    public class LocalizationButtonData
    {
        public Sprite Image;
        public string Locale;
    }
}