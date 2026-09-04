#if !PREMIUM
using System.Collections;
using System.Collections.Generic;
using Creobit.EditionsUpgrade;
using UnityEditor;
using UnityEngine;


namespace Creobit.EditionsUpgrade
{
    
    [CustomEditor(typeof(CreobitIAPButton))]
    [CanEditMultipleObjects]
    public class CreobitIapButtonEditor : CreobitAbstractIapButtonEditor
    {
        #if !HUAWEI && !PREMIUM
        /// <summary>
        /// Event trigger when <c>CodelessIAPButton</c> is enabled in the scene.
        /// </summary>
        public void OnEnable()
        {
            OnEnableInternal();
        }

        /// <summary>
        /// Event trigger when trying to draw the <c>CodelessIAPButton</c> in the inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            OnInspectorGuiInternal();
        }
    #endif
    }
}
#endif
