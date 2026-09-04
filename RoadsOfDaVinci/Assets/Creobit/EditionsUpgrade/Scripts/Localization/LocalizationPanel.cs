using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade")]
    public class LocalizationPanel : MonoBehaviour
    {
        private EditionsLocalizationManager _editionsLocalizationManager;

        private void Awake()
        {
#if !TK2D
            GetComponent<BoxCollider>().enabled = false;
#endif
            _editionsLocalizationManager = FindObjectOfType<EditionsLocalizationManager>();

            GenerateChangeLocaleButtons();
        }

        private void GenerateChangeLocaleButtons()
        {
            foreach (var localizationGroup in _editionsLocalizationManager.LocalizationGroups)
            {
                var obj = Instantiate(localizationGroup.LocalizationChangeButton, transform);
                obj.transform.localScale = Vector3.one;
                obj.SetDataOnInstantiate(localizationGroup.SystemLanguage);
            }
            
        }
    }
}