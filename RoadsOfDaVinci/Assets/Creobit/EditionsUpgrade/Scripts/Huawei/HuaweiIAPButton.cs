using System;
using Creobit.EditionsUpgrade;
#if HUAWEI
using HmsPlugin;
using HuaweiMobileServices.AuthService;
using HuaweiMobileServices.IAP;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class HuaweiIAPButton : MonoBehaviour
{
    [SerializeField] private string _productID;
    [SerializeField] private Button _purchaseButton;
    
    public UnityEvent OnUpdatePrice;
    public UnityEvent OnPurchaseSuccess;
    
    [HideInInspector] public string IapPrice = "0";
    public string priceNumber => IapPrice;
    
#if HUAWEI
    
    private void PurchaseProduct()
    {
        HMSIAPManager.Instance.PurchaseProduct(_productID);
    }
    

    private void Start()
    {
        if (!HMSIAPManager.Instance.isIapAvailable())
            HMSIAPManager.Instance.InitializeIAP();
        UpdatePriceString();
    }

    private void UpdatePriceString()
    {
        IapPrice = HMSIAPManager.Instance.GetProductInfo(_productID)?.Price;
        OnUpdatePrice.Invoke();
    }

    private void FixedUpdate()
    {
        if (HMSIAPManager.Instance.GetProductInfo(_productID)?.Price != IapPrice)
            UpdatePriceString();
    }

    private void OnEnable()
    {
        if (_purchaseButton != null)
            _purchaseButton.onClick.AddListener(PurchaseProduct);
        HMSIAPManager.Instance.OnBuyProductSuccess += OnBuyProductSuccess;
    }

    private void OnDisable()
    {
        CancelInvoke();
        HMSIAPManager.Instance.OnBuyProductSuccess -= OnBuyProductSuccess;
        if (_purchaseButton == null)
            return;
        _purchaseButton.onClick.RemoveListener(PurchaseProduct);
    }

    private void OnBuyProductSuccess(PurchaseResultInfo obj)
    {
        OnPurchaseSuccess?.Invoke();
    }
    
#endif
    
    // ---------   DELETE FOR UNIVERSAL VERSION    ---------
    public void UpdateText()
    {
        GetComponent<TMP_Text>().text = IapPrice;
    }

    public void RegisterFullGamePurchaseForEditionUpgradeOnly()
    {
        PlayerPrefs.SetString(UnlockLevelManager.AllLevelsBuyKey, "true");
        PlayerPrefs.Save();
    }

    public void CheckIfFullGamePurchased()
    {
        if (PlayerPrefs.GetString(UnlockLevelManager.AllLevelsBuyKey) == "true")
            gameObject.SetActive(false);
    }
        
    // ---------   DELETE FOR UNIVERSAL VERSION    ---------
}
