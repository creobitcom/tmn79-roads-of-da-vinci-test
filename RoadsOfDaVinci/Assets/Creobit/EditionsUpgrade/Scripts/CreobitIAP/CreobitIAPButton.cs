#if !PREMIUM
using System;
using Creobit.EditionsUpgrade;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
#if !HUAWEI
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif
using UnityEngine.UI;


[AddComponentMenu("In-App Purchasing/Creobit IAP Button")]
public class CreobitIAPButton : CreobitBaseIAPButton
{
    [HideInInspector] public string IapPrice = "0";

    [HideInInspector] public decimal priceNumber = 0;

    [SerializeField] private GameObject[] _additionalDisableObjects;

    public Product FetchedProduct { get; private set; } = null;
    
#if !HUAWEI
    // [SerializeField] private TMP_Text 
        /// <summary>
        /// Type of event fired after a successful fetching the product information from the store.
        /// </summary>
        [Serializable]
        public class OnProductFetchedEvent : UnityEvent<Product>
        {
        }

        /// <summary>
        /// Type of event fired after a successful purchase of a product.
        /// </summary>
        [Serializable]
        public class OnPurchaseCompletedEvent : UnityEvent<Product>
        {
        }

        /// <summary>
        /// Type of event fired after a failed purchase of a product.
        /// </summary>
        [Serializable]
        public class OnPurchaseFailedEvent : UnityEvent<Product, PurchaseFailureDescription>
        {
        }

        /// <summary>
        /// Type of event fired after a restore transactions was completed.
        /// </summary>
        [Serializable]
        public class OnTransactionsRestoredEvent : UnityEvent<bool, string>
        {
        }

        /// <summary>
        /// Which product identifier to represent. Note this is not a store-specific identifier.
        /// </summary>
        [HideInInspector]
        public string productId;

        /// <summary>
        /// The type of this button, can be either a purchase or a restore button.
        /// </summary>
        [Tooltip("The type of this button, can be either a purchase or a restore button.")]
        public CodelessButtonType buttonType = CodelessButtonType.Purchase;

        /// <summary>
        /// Consume the product immediately after a successful purchase.
        /// </summary>
        [Tooltip("Consume the product immediately after a successful purchase.")]
        public bool consumePurchase = true;

        /// <summary>
        /// Event fired after a restore transactions.
        /// </summary>
        [Tooltip("Event fired after a restore transactions.")]
        public OnTransactionsRestoredEvent onTransactionsRestored;

        /// <summary>
        /// Event fired after a successful purchase of this product.
        /// </summary>
        [Tooltip("Event fired after a successful purchase of this product.")]
        public OnPurchaseCompletedEvent onPurchaseComplete;

        /// <summary>
        /// Event fired after a failed purchase of this product.
        /// </summary>
        [Tooltip("Event fired after a failed purchase of this product.")]
        public OnPurchaseFailedEvent onPurchaseFailed;

        /// <summary>
        /// Event fired after a successful fetching the product information from the store.
        /// </summary>
        [Tooltip("Event fired after a successful fetching the product information from the store.")]
        public OnProductFetchedEvent onProductFetched;

        [Tooltip("Button that triggers purchase.")]
        public Button button;

        public override string GetProductId()
        {
            return productId;
        }

        public override bool IsAPurchaseButton()
        {
            return buttonType == CodelessButtonType.Purchase;
        }

        protected override bool IsARestoreButton()
        {
            return buttonType == CodelessButtonType.Restore;
        }

        protected override bool ShouldConsumePurchase()
        {
            return consumePurchase;
        }

        protected override void OnTransactionsRestored(bool success, string error)
        {
            onTransactionsRestored?.Invoke(success, error);
        }


        [HideInInspector] public override void OnPurchaseComplete(Product purchasedProduct)
        {
            onPurchaseComplete?.Invoke(purchasedProduct);
        }

        /// <summary>
        /// Invoked on a failed purchase of the product associated with this button
        /// </summary>
        /// <param name="product">The <typeparamref name="Product"/> which failed to purchase</param>
        /// <param name="failureDescription">Information to help developers recover from this failure</param>
        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            onPurchaseFailed?.Invoke(product, failureDescription);
        }

        protected override Button GetPurchaseButton()
        {
            return button;
        }

        protected override void AddButtonToCodelessListener()
        {
            // CodelessIAPStoreListener.Instance.AddButton(this);
        }

        protected override void RemoveButtonToCodelessListener()
        {
            // CodelessIAPStoreListener.Instance.RemoveButton(this);
        }

        internal override void OnInitCompleted()
        {
            var product = CodelessIAPStoreListener.Instance.GetProduct(productId);
            if (product != null)
            {
                IapPrice = product.metadata.localizedPriceString;
                priceNumber = product.metadata.localizedPrice;
                FetchedProduct = product;
                onProductFetched.Invoke(product);
                Debug.Log("Inited");
            }
        }

        /// <summary>
        /// Invoke to process a successful purchase of the product associated with this button.
        /// </summary>
        /// <param name="e">The successful <c>PurchaseEventArgs</c> for the purchase event. </param>
        /// <returns>The result of the successful purchase</returns>
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            return ProcessPurchaseInternal(args);
        }

        public void UpdateText()
        {
            GetComponent<TMP_Text>().text = IapPrice;
        }

        public void PurchaseAllGame()
        {
            Debug.Log("PurchaseAllGame method has called");
#if !PREMIUM
            IapCore.I.RegisterFullGamePurchase(productId);
#endif
        }

        public void DeleteIfGameBought()
        {
            if (PlayerPrefs.HasKey("AllLevelsBuyKey"))
            {
                gameObject.SetActive(PlayerPrefs.GetString("AllLevelsBuyKey") != "true");
                foreach (var additionalDisableObject in _additionalDisableObjects)
                {
                    additionalDisableObject.SetActive(PlayerPrefs.GetString("AllLevelsBuyKey") != "true");
                }
            }
        }
#endif
}
#endif