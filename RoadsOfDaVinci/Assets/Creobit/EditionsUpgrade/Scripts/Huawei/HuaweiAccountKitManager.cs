#if HUAWEI
using HmsPlugin;
#endif
using UnityEngine;

public class HuaweiAccountKitManager : MonoBehaviour
{
#if HUAWEI
    private void Start()
    {
        if (!HMSAccountKitManager.Instance.IsSignedIn)
            HMSAccountKitManager.Instance.SignIn();
#if HAS_AD
        if (!HMSIAPManager.Instance.isIapAvailable())
            HMSIAPManager.Instance.InitializeIAP();
#endif
        
    }
#endif
}
