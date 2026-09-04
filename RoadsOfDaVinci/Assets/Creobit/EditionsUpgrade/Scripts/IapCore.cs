#if !PREMIUM
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if !HUAWEI
using System;
using UnityEngine.Purchasing;
#endif
using UnityEngine.Serialization;
using Creobit.Bootstrap.Core.Scripts.Runtime.Analytics;

namespace Creobit.EditionsUpgrade
{
#if !HUAWEI
    public class IapCore : MonoBehaviour
        , IStoreListener
    {
        private static IapCore _instance;

        public static IapCore I
        {
            get { return _instance; }
        }

        public static bool initializationComplete;

        private const string kNoProduct = "com.eighthfloor.roadsofdavinci1.f2p";

        private readonly List<string> m_ValidIDs = new List<string>();

        // [SerializeField] private GameObject _unlockWindow;
        private static IStoreController _storeController;
        private static IExtensionProvider _storeExtensionProvider;
        public string _place = "unlock";
        [HideInInspector] public string IapPrice = "0";
        public UnityEvent OnUpdatePrice;
        public event Action OnRegisterPurchase;
        private CreobitBaseIAPButton _currentBaseIAPButton;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static IGooglePlayStoreExtensions _googlePlayStoreExtensions;
#endif
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(this.gameObject);

            if (PlayerPrefs.HasKey(UnlockLevelManager.AllLevelsBuyKey))
            {
                // _unlockWindow.SetActive(false);
                gameObject.SetActive(false);
                return;
            }

            LoadProductIdsFromCodelessCatalog();
            InitializePurchasing();
        }

        void LoadProductIdsFromCodelessCatalog()
        {
            var catalog = ProductCatalog.LoadDefaultCatalog();

            m_ValidIDs.Clear();
            m_ValidIDs.Add(kNoProduct);
            foreach (var product in catalog.allProducts)
            {
                m_ValidIDs.Add(product.id);
            }
        }

        private void InitializePurchasing()
        {
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

            foreach (var id in m_ValidIDs)
            {
                builder.AddProduct(id, ProductType.NonConsumable);
            }

            UnityPurchasing.Initialize(this, builder);

            foreach (var id in m_ValidIDs)
            {
                var product = CodelessIAPStoreListener.Instance.GetProduct(id);
                if (product != null)
                {
                    IapPrice = product.metadata.localizedPriceString;
                    OnUpdatePrice?.Invoke();
                }
                else
                {
                    Debug.Log($"product {id} = null");
                }
            }
            // #if UNITY_IOS
            //             foreach (var id in _iapIdAppStoreIos)
            //             {
            //                 builder.AddProduct(id, ProductType.NonConsumable);
            //             }
            //             // builder.AddProduct(_iapIdAppStoreIos, ProductType.NonConsumable);
            // #elif MAC_APPSTORE
            //             builder.AddProduct(_iapIdAppStoreOSX, ProductType.NonConsumable);
            // #else
            //             builder.AddProduct(_iapIdGooglePlay, ProductType.NonConsumable);
            // #endif

            UnityPurchasing.Initialize(this, builder);

            // #if UNITY_IOS
            //             foreach (var id in _iapIdAppStoreIos)
            //             {
            //                 var product = CodelessIAPStoreListener.Instance.GetProduct(id);
            //                 if (product != null)
            //                 {
            //                     IapPrice = product.metadata.localizedPriceString;
            //                     OnUpdatePrice?.Invoke();
            //                 }
            //                 else
            //                 {
            //                     Debug.Log($"product {id} = null");
            //                 }
            //             }
            //             // var product = CodelessIAPStoreListener.Instance.GetProduct(_iapIdAppStoreIos);
            // #elif MAC_APPSTORE
            //             var product = CodelessIAPStoreListener.Instance.GetProduct(_iapIdAppStoreOSX);
            // #else
            //             var product = CodelessIAPStoreListener.Instance.GetProduct(_iapIdGooglePlay);
            // #endif


            // if (product != null)
            // {
            //     IapPrice = product.metadata.localizedPriceString;
            //     OnUpdatePrice?.Invoke();
            // }
            // else
            // {
            //     Debug.Log("product = null");
            // }
        }

        public void RegisterFullGamePurchase(string productID)
        {

            PlayerPrefs.SetString(UnlockLevelManager.AllLevelsBuyKey, "true");
            PlayerPrefs.Save();
            // _unlockWindow.SetActive(false);

            AnalyticsRecorder.Instance?.InappPurchased(_place, PlayerPrefs.GetInt(UnlockLevelManager.LastUnlockedLevelIndexKey));

            CompletePurchase(productID);
        }

        private bool IsInitialized()
        {
            return _storeController != null && _storeExtensionProvider != null;
        }

        public void InitiatePurchase(string productID, CreobitBaseIAPButton baseIAPButton)
        {
            if (BuyProductID(productID))
                _currentBaseIAPButton = baseIAPButton;
        }

        private bool BuyProductID(string productId)
        {
            if (IsInitialized())
            {
                UnityEngine.Purchasing.Product product = _storeController.products.WithID(productId);

                if (product is { availableToPurchase: true })
                {
                    Debug.Log(string.Format("Purchasing product:" + product.definition.id.ToString()));
                    _storeController.InitiatePurchase(product);
                    return true;
                }
                else
                {
                    Debug.Log("BuyProductID: FAIL. Not purchasing product, either is not found or is not available for purchase");
                }
            }
            else
            {
                Debug.LogError("BuyProductID FAIL. Not initialized.");
            }

            return false;
        }

        public void RestorePurchases()
        {
            _storeExtensionProvider.GetExtension<IAppleExtensions>().RestoreTransactions((result, msg) =>
            {
                Debug.Log("Restore result " + result);
            });

        }

        private void CompletePurchase(string productId)
        {
            var product = _storeController.products.WithID(productId);

            if (product == null)
                Debug.LogError("Cannot complete purchase, product not initialized.");
            else
            {
                _storeController.ConfirmPendingPurchase(product);
                Debug.Log("Completed purchase with " + product.transactionID.ToString());
                OnRegisterPurchase?.Invoke();
            }
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            var product = args.purchasedProduct;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_googlePlayStoreExtensions.IsPurchasedProductDeferred(product))
            {
                //The purchase is Deferred.
                //Therefore, we do not unlock the content or complete the transaction.
                //ProcessPurchase will be called again once the purchase is Purchased.
                return PurchaseProcessingResult.Pending;
            }
#endif
            if (_currentBaseIAPButton != null)
            {
                _currentBaseIAPButton.OnPurchaseComplete(product);
            }
            else
            {
                RegisterFullGamePurchase(product.definition.id);
            }

            Debug.Log($"Purchase Complete - Product: {product.definition.id}");

            return PurchaseProcessingResult.Complete;
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.Log($"In-App Purchasing initialize failed: {error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            throw new System.NotImplementedException();
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Debug.Log($"Purchase failed - Product: '{product.definition.id}', PurchaseFailureReason: {failureReason}");
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            Debug.Log("In-App Purchasing successfully initialized");
            _storeController = controller;
            _storeExtensionProvider = extensions;
#if UNITY_ANDROID && !UNITY_EDITOR
            _googlePlayStoreExtensions = extensions.GetExtension<IGooglePlayStoreExtensions>();
#endif
        }
    }
#endif
}
#endif
