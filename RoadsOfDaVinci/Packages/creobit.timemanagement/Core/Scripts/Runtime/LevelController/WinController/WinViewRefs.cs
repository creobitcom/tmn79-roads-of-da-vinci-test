using _8floor.TimeManagement.Core.Scripts.Runtime.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Loader
{
    public class WinViewRefs : MonoBehaviour
    {
        public List<WinResourceSliderRefs> resourceSliders;
        public StarsView starsView;
        
        public Transform achievementsParent;
        public GameObject achievementIconPrefab;

        public GameObject trophyRoot;
        public RectTransform starsTransform;
        public Vector2 starsPositionDefault;
        public Vector2 starsPositionWithTrophy;
        public TMP_Text trophyTitleText;
        public Image trophyIconImage;
        public TMP_Text trophyNameText;
        public Material trophyGrayscaleMaterial;
        public string trophyFoundKey;
        public string trophyNotFoundKey;
    }
}
