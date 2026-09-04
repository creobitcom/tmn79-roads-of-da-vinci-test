#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;

namespace Creobit.Localization
{
    public abstract class LocalizationWindowTitle
    {
        [DllImport("user32.dll", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(IntPtr windowPointer, string lpString);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        public static void Localize()
        {
#if COLLECTOR
            var windowName = LocalizationService.Instance.GetText("application_name_se"); 
#else
            var windowName = LocalizationService.Instance.GetText("application_name");
#endif

            var window = GetForegroundWindow();
            SetWindowText(window, windowName);
        }
    }
}
#endif