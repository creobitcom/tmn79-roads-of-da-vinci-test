using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public class GuidesPanelView : MonoBehaviour
    {
        public Button closeButton;
        public Button nextButton;
        public Button prevButton;
        public Image background;
        public Image pageImage;
        public RawImage pageRawImage;
        public TMP_Text pageText;
        public Transform navButtonsRoot;
        public GuidePageNavButton navButtonTemplate;
    }
}
