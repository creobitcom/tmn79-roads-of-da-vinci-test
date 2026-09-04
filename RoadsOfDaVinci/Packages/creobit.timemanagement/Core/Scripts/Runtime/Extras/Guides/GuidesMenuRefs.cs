using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public class GuidesMenuRefs : MonoBehaviour
    {
        public TMP_Text titleText;
        public List<GuideLocationRefs> locations;
        public Transform locationsRoot;
        public GuideLocationRefs locationTemplate;
    }
}
