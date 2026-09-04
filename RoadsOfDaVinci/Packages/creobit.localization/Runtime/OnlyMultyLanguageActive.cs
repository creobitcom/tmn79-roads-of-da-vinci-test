using UnityEngine;

namespace Creobit.Localization
{
    public class OnlyMultyLanguageActive : MonoBehaviour
    {
        private void OnEnable()
        {
            if (LocalizationService.Instance.IsOneLanguageBuild())
            {
                gameObject.SetActive(false);
            }
        }
    }
}