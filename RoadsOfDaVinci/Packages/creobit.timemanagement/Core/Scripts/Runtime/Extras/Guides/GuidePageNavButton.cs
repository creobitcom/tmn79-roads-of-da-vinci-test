using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    [RequireComponent(typeof(Button))]
    public class GuidePageNavButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _numberText;

        public event Action<int> Clicked;

        public int PageIndex { get; private set; }

        public void Initialize(int pageIndex)
        {
            PageIndex = pageIndex;

            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_numberText != null)
            {
                _numberText.text = (pageIndex + 1).ToString();
            }

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => Clicked?.Invoke(PageIndex));
        }

        public void SetSelected(bool selected)
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            _button.interactable = !selected;
        }
    }
}
