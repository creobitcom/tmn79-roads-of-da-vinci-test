using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(LayoutElement))]
    public class CheatView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _titleText;

        [SerializeField]
        private Button _cheatExecuteButton;

        public ICheat Info { get; private set; }

        private void Awake()
        {
            ConfigureTitleText();
        }

        private void OnValidate()
        {
            if (_cheatExecuteButton == null)
            {
                _cheatExecuteButton = GetComponent<Button>();
            }

            _cheatExecuteButton.onClick.RemoveListener(OnCheatExecuteButtonClicked);
            ConfigureTitleText();
        }

        public void Initialize(ICheat info)
        {
            Info = info;

            _titleText.text = info.Title;
            ConfigureTitleText();
            UpdateButtonHeight();

            _cheatExecuteButton.onClick.AddListener(OnCheatExecuteButtonClicked);

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
            if (transform.parent is RectTransform parent)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            }
        }

        private void UpdateButtonHeight()
        {
            _titleText.ForceMeshUpdate();

            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minHeight = Mathf.Max(40f, _titleText.preferredHeight + 12f);
        }

        private void ConfigureTitleText()
        {
            if (_titleText == null)
            {
                return;
            }

            _titleText.enableWordWrapping = true;
            _titleText.overflowMode = TextOverflowModes.Overflow;
            _titleText.enableAutoSizing = true;
            _titleText.fontSizeMin = 11;
            _titleText.fontSizeMax = 14;
            _titleText.margin = new Vector4(8f, 4f, 8f, 4f);
        }

        private void OnCheatExecuteButtonClicked()
        {
            Info.Execute();
        }
    }
}
