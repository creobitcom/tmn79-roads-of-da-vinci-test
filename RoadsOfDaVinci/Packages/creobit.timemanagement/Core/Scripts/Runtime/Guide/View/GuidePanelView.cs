using Creobit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Guide.View
{
    public class GuidePanelView : PanelData
    {
        [SerializeField] private Image _guideImage;

        public void SetGuide(Sprite guide)
        {
            _guideImage.sprite = guide;
        }
    }
    
    
}