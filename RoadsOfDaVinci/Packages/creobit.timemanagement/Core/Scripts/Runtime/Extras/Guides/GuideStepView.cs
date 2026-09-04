using TMPro;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public class GuideStepView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _guideText;

        public void SetText(string guideText)
        {
            _guideText.text = guideText;
        }
    }
}