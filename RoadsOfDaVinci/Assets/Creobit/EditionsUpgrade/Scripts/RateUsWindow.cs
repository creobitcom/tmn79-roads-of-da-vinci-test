using System;
#if HUAWEI
using HuaweiMobileServices.InAppComment;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/RateUsWindow")]
    public class RateUsWindow : MonoBehaviour
    {
        public const string RateUsPrefsKey = "RateUsSanded";
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private string _email = "support@8floor.net";
        [SerializeField] private string _AppstoreF2PAppID = "6806751974";
        [SerializeField] private string _AppstorePremiumAppID = "6806751967";
        [SerializeField] private string _uwpLink = "ms-windows-store://pdp/?ProductId=your_product_id";

        private string MyEscapeUrl(string url) => UnityWebRequest.EscapeURL(url).Replace("+", "%20");

        public void ShowRateUs()
        {
            gameObject.SetActive(!PlayerPrefs.HasKey(RateUsPrefsKey));
        }

        public void OpenStore()
        {
#if HUAWEI
            InAppComment.ShowInAppComment(null);
#elif UNITY_ANDROID
            Application.OpenURL($"https://play.google.com/store/apps/details?id={Application.identifier}");
#elif UNITY_IOS
            if (!Device.RequestStoreReview())
            {
#if PREMIUM
                Application.OpenURL($"itms-apps://itunes.apple.com/app/id{_AppstorePremiumAppID}");
#else
                Application.OpenURL($"itms-apps://itunes.apple.com/app/id{_AppstoreF2PAppID}");
#endif
            }
#elif UNITY_STANDALONE_OSX
#if PREMIUM
                Application.OpenURL($"macappstore://apps.apple.com/app/id{_AppstorePremiumAppID}");
#else
                Application.OpenURL($"macappstore://apps.apple.com/app/id{_AppstoreF2PAppID}");
#endif
#elif UNITY_STANDALONE_WIN
            Application.OpenURL(_uwpLink);
#endif
            Close();
        }

        public void SendEmail()
        {
            try
            {
                var email = _email;
                var subject = MyEscapeUrl(Application.productName);
                var body = MyEscapeUrl(_inputField.text);
                Application.OpenURL("mailto:" + email + "?subject=" + subject + "&body=" + body);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            Close();
        }

        private void Close()
        {
            PlayerPrefs.SetString(RateUsPrefsKey, "True");
            PlayerPrefs.Save();
            gameObject.SetActive(false);
        }
    }
}