using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using Creobit.Localization;
using Flexalon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip
{
    public class TooltipView : MonoBehaviour, IPrimaryTooltipView
    {
        [SerializeField]
        private FlexalonObject _rootFlexalon;

        [SerializeField]
        private TMP_Text _nameText;

        [SerializeField]
        private TMP_Text _descriptionText;

        [SerializeField]
        private Image _image;

        [SerializeField]
        private GameObject _input;

        [SerializeField]
        private TMP_Text _inputText;

        [SerializeField]
        private Transform _inputResourcesBox;

        [SerializeField]
        private GameObject _output;

        [SerializeField]
        private TMP_Text _outputText;

        [SerializeField]
        private Transform _outputResourcesBox;

        public Vector2 Size
        {
            get
            {
                var rect = (_rootFlexalon.transform as RectTransform).rect;

                return new(rect.width, rect.height);
            }
        }

        public GameObject CurrentGameObject => gameObject;

        public void SetTooltipData(TooltipSettings tooltipSettings,
            IReadOnlyCollection<TooltipResourceView> inputResources,
            IReadOnlyCollection<TooltipResourceView> outputResources)
        {
            if (inputResources.Count > 0)
            {
                SetResourcesParent(inputResources, _inputResourcesBox);
                _inputText.text = LocalizationService.Instance.GetText(tooltipSettings.TooltipData.TooltipInputText);
            }

            if (outputResources.Count > 0)
            {
                SetResourcesParent(outputResources, _outputResourcesBox);
                _outputText.text = LocalizationService.Instance.GetText(tooltipSettings.TooltipData.TooltipOutputText);
            }

            SetupShownInfo(tooltipSettings, inputResources, outputResources);

            if (tooltipSettings.ShowObjectImage)
            {
                _image.sprite = tooltipSettings.TooltipData.TooltipObjectIcon;
            }
        }

        private void SetResourcesParent(IEnumerable<TooltipResourceView> resources, Transform parent)
        {
            foreach (var tooltipResourceView in resources)
            {
                if (tooltipResourceView.transform.parent != parent)
                {
                    tooltipResourceView.transform.SetParent(parent);
                }
            }
        }

        private void SetupShownInfo(TooltipSettings tooltipSettings, IReadOnlyCollection<TooltipResourceView> inputResources, IReadOnlyCollection<TooltipResourceView> outputResources)
        {
            _nameText.transform.gameObject.SetActive(tooltipSettings.ShowObjectName);

            _image.transform.gameObject.SetActive(tooltipSettings.ShowObjectImage);

            _descriptionText.gameObject.SetActive(tooltipSettings.ShowDescription);

            _input.SetActive(inputResources.Count > 0 && !string.IsNullOrEmpty(tooltipSettings.TooltipData.TooltipInputText));

            _output.SetActive(outputResources.Count > 0 && !string.IsNullOrEmpty(tooltipSettings.TooltipData.TooltipOutputText));
        }

        public void SetPosition(Vector2 position)
        {
            transform.localPosition = position;
        }

        public void SetTooltipText(string objectName,
            string objectDescription)
        {
            _nameText.text = LocalizationService.Instance.GetText(objectName);

            _descriptionText.text = LocalizationService.Instance.GetText(objectDescription);
        }

        public void ForceUpdate()
        {
            _rootFlexalon.ForceUpdate();
        }
    }
}