using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UDP
using UnityEngine.UDP;

namespace Creobit.EditionsUpgrade
{
    public class InitListener : IInitListener
    {
        public void OnInitialized(UserInfo userInfo)
        {
            Debug.Log("Initialization succeed");
        }

        public void OnInitializeFailed(string message)
        {
            Debug.Log("Initialization failed " + message);
        }
    }
}
#endif
