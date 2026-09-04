using System;
using Creobit.Loading;
using Creobit.UI.Utility;
using UltEvents;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public interface IExtrasController : ILoadUnit<ExtrasControllerJson>
    {
        public void SaveBackground();
        public void SaveMusic(int index);
        public event Action<string> OnSaveBackground;
    }
    
    [Serializable]
    public class ExtrasControllerJson
    {
        public PanelReference ExtrasMusicPanel;
        public PanelReference ExtrasPanel;
            
        public ExtrasControllerJson(PanelReference extrasMusicPanel, PanelReference extrasPanel)
        {
            ExtrasMusicPanel = extrasMusicPanel;
            ExtrasPanel = extrasPanel;
        }
    }
}