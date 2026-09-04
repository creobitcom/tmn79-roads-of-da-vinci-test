#if !PREMIUM
using System;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine;
#if !HUAWEI
using UnityEngine.Purchasing;
#endif

namespace Creobit.EditionsUpgrade
{
    public abstract class CreobitBaseIAPButton : MonoBehaviour
    {
#if !HUAWEI
        protected abstract bool ShouldConsumePurchase();

        protected abstract void OnTransactionsRestored(bool success, string error);
        [HideInInspector]public abstract void OnPurchaseComplete(Product purchasedProduct);

        internal abstract void OnInitCompleted();
        protected abstract void AddButtonToCodelessListener();
        protected abstract void RemoveButtonToCodelessListener();
        protected abstract Button GetPurchaseButton();
        [SerializeField] private UnityEvent OnPurchaseRegister;
        private void Awake()
        {
            var button = GetPurchaseButton();
            var productId = GetProductId();

            if (IsAPurchaseButton())
            {
                if (button)
                {
                    button.onClick.AddListener(PurchaseProduct);
                }
            }
            else if (IsARestoreButton())
            {
                if (button)
                {
                    button.onClick.AddListener(Restore);
                }
            }
            
            if (string.IsNullOrEmpty(productId))
            {
                Debug.LogError("IAPButton productId is empty");
            }
            else if (!CodelessIAPStoreListener.Instance.HasProductInCatalog(productId!))
            {
                Debug.LogWarning("The product catalog has no product with the ID \"" + productId + "\"");
            }
            else if (CodelessIAPStoreListener.initializationComplete)
            {
                OnInitCompleted();
            }
        }

        private void Start()
        {
            IapCore.I.OnRegisterPurchase += InvokeSuccessfulPurchaseAction;
        }

        private void InvokeSuccessfulPurchaseAction()
        {
            OnPurchaseRegister?.Invoke();
        }

        public abstract string GetProductId();
        public abstract bool IsAPurchaseButton();
        protected abstract bool IsARestoreButton();

        void OnEnable()
        {
            if (IsAPurchaseButton())
            {
                if (CodelessIAPStoreListener.initializationComplete)
                {
                    OnInitCompleted();
                }
            }
        }

        void OnDisable()
        {
            if (IsAPurchaseButton())
            {
                RemoveButtonToCodelessListener();
            }
        }

        private void OnDestroy()
        {
            IapCore.I.OnRegisterPurchase -= InvokeSuccessfulPurchaseAction;
        }

        void PurchaseProduct()
        {
            if (IsAPurchaseButton())
            {
                // CodelessIAPStoreListener.Instance.InitiatePurchase(GetProductId());
#if !PREMIUM
                IapCore.I.InitiatePurchase(GetProductId(), this);
#endif
            }
        }

        protected PurchaseProcessingResult ProcessPurchaseInternal(PurchaseEventArgs args)
        {
            OnPurchaseComplete(args.purchasedProduct);

            return ShouldConsumePurchase() ? PurchaseProcessingResult.Complete : PurchaseProcessingResult.Pending;
        }

        void Restore()
        {
            if (IsARestoreButton())
            {
                if (Application.platform == RuntimePlatform.WSAPlayerX86 ||
                    Application.platform == RuntimePlatform.WSAPlayerX64 ||
                    Application.platform == RuntimePlatform.WSAPlayerARM)
                {
                    // CodelessIAPStoreListener.Instance.GetStoreExtensions<IMicrosoftExtensions>()
                    //     .RestoreTransactions();
                }
                else if (Application.platform == RuntimePlatform.IPhonePlayer ||
                         Application.platform == RuntimePlatform.OSXPlayer ||
                         Application.platform == RuntimePlatform.tvOS)
                {
                    CodelessIAPStoreListener.Instance.GetStoreExtensions<IAppleExtensions>()
                        .RestoreTransactions(OnTransactionsRestored);
                }
                else if (Application.platform == RuntimePlatform.Android &&
                         StandardPurchasingModule.Instance().appStore == AppStore.GooglePlay)
                {
                    CodelessIAPStoreListener.Instance.GetStoreExtensions<IGooglePlayStoreExtensions>()
                        .RestoreTransactions(OnTransactionsRestored);
                }
                else
                {
                    Debug.LogWarning(Application.platform +
                        " is not a supported platform for the Codeless IAP restore button");
                }
            }
        }
#endif
    }
}
#endif
