using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/Games8floorLink")]
    public class Games8floorLink : GoogleAndAppStoreOnly
    {
#if UNITY_ANDROID
        [SerializeField] private string _googlePlayLink = "https://play.google.com/store/apps/dev?id=6811631218194712418";
#elif UNITY_IOS || MAC_APPSTORE
        [SerializeField] private string _appStoreLink = "https://apps.apple.com/us/developer/8floor/id500140554";
#endif
        public void OpenOurGames()
        {
#if UNITY_ANDROID
            Application.OpenURL(_googlePlayLink);
#elif UNITY_IOS || MAC_APPSTORE
            Application.OpenURL(_appStoreLink);
#endif
        }
    }
}