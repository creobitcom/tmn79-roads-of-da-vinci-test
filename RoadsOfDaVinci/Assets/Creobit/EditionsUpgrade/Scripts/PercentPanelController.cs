#if !PREMIUM
using Creobit.EditionsUpgrade;
using System;
using TMPro;
using UnityEngine;

public class PercentPanelController : MonoBehaviour
{
    [SerializeField] private TMP_Text _percentText;

    [SerializeField] private UnlockLevelWindow _unlockLevelWindow;

#if HUAWEI
     [SerializeField] private HuaweiIAPButton _fullPrice;
     [SerializeField] private HuaweiIAPButton _currentBuyButton;
#else
    [SerializeField] private CreobitIAPButton _fullPrice;
    [SerializeField] private CreobitIAPButton _currentBuyButton;
#endif

    private float _currentBuyPrice;
    private float _allOffersBuyPrice;

    private bool _textIsUpdated;

    private void OnEnable()
    {
        if (_unlockLevelWindow != null)
        {
            _currentBuyButton = _unlockLevelWindow.GetCurrentBuyButton().GetComponent<CreobitIAPButton>();

            _unlockLevelWindow.CurrentObjectSetChanged += OnObjectsSetChanged;
        }

        _textIsUpdated = false;
    }

    private void OnDisable()
    {
        if(_unlockLevelWindow != null)
        {
            _unlockLevelWindow.CurrentObjectSetChanged -= OnObjectsSetChanged;
        }
    }

    private void Update()
    {
        if(_textIsUpdated
        || _currentBuyButton.priceNumber == 0)
        {
            return;
        }

        UpdatePercentText();

        _textIsUpdated = true;
    }

    private void UpdatePercentText()
    {
#if !HUAWEI
        _currentBuyPrice = Convert.ToSingle(_currentBuyButton.priceNumber);
        _allOffersBuyPrice = Convert.ToSingle(_fullPrice.priceNumber);
#else
        _currentBuyPrice = Convert.ToSingle(Regex.Match(_currentBuyButton.priceNumber.ToString(), @"\d+").Value);
        _allOffersBuyPrice = Convert.ToSingle(Regex.Match(_fullPrice.priceNumber.ToString(), @"\d+").Value);
#endif

#if UNITY_EDITOR
        _allOffersBuyPrice = 10f;
        _currentBuyPrice = 10f;
#endif
        _percentText.text = (((_allOffersBuyPrice - _currentBuyPrice) / _allOffersBuyPrice) * 100f).ToString("N0") + '%';
    }
    
    private void OnObjectsSetChanged()
    {
        _textIsUpdated = false;

        UpdatePercentText();
    }
}
#endif