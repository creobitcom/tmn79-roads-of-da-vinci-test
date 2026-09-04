using System.Linq;
using TimeManagerEngine;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{    
    [AddComponentMenu("Creobit/EditionsUpgrade/OpenLocalizationPanelButton")]
    [RequireComponent(typeof(Button))]
    public class OpenLocalizationPanelButton : MonoBehaviour
    {
        [SerializeField] private Image _buttonImage;
        [SerializeField] private LocalizationPanel _localizationPanel;
            
        private Button _button;
        private EditionsLocalizationManager _editionsLocalizationManager;
        
        private void Awake()
        {
            _editionsLocalizationManager = FindObjectOfType<EditionsLocalizationManager>();
            if (_editionsLocalizationManager.LocalizationGroups.Count<=1)
            {
                gameObject.SetActive(false);
                return;
            }
            
            _editionsLocalizationManager.OnChangeLocale += UpdateLocaleImage;
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        private void OnEnable()
        {
            UpdateLocaleImage();
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(OnClick);
            _editionsLocalizationManager.OnChangeLocale -= UpdateLocaleImage;
        }

        private void OnClick()
        {
            _localizationPanel.gameObject.SetActive(true);
        }

        private void UpdateLocaleImage()
        {
            LocalizationChangeButton localizationChangeButton;
            
            if (PlayerPrefs.HasKey(EditionsLocalizationManager.SavedLocaleKey))
            {
                localizationChangeButton = Enumerable.FirstOrDefault(_editionsLocalizationManager.LocalizationGroups, x =>
                    x.LocalizationChangeButton.CurrentLocale ==
                    _editionsLocalizationManager.ActiveLocale)?.LocalizationChangeButton;
            }
            else
            {
                localizationChangeButton = Enumerable.FirstOrDefault
                (_editionsLocalizationManager.LocalizationGroups, x =>
                    x.SystemLanguage == Application.systemLanguage)?.LocalizationChangeButton;
            }
            
            if (localizationChangeButton == null)
            {
                return;
            }

            _buttonImage.sprite = localizationChangeButton.NormalSprite;
            _button.spriteState = localizationChangeButton.Button.spriteState;
        }
    }
}