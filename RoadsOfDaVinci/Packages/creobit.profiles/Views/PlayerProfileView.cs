using System;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Views
{
    public class PlayerProfileView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Button _selectButton;
        [SerializeField] private GameObject _border;

        private Action<PlayerProfileView> _selected;

        public PlayerProfileData Data { get; private set; }

        private void OnDestroy()
        {
            _selectButton.onClick.RemoveListener(OnSelectButtonClicked);
        }

        public void Initialize(PlayerProfileData data, Action<PlayerProfileView> selected)
        {
            Data = data;

            _selected = selected;

            _nameText.text = data.Name;

            _selectButton.onClick.AddListener(OnSelectButtonClicked);
        }

        public void SetSelectedState(bool isSelected)
        {
            _selectButton.interactable = !isSelected;
            
            _nameText.fontStyle = (FontStyles)(isSelected ? FontStyle.Bold : FontStyle.Normal);

            _border.SetActive(isSelected);
        }

        private void OnSelectButtonClicked()
        {
            _selected?.Invoke(this);
        }
    }
}