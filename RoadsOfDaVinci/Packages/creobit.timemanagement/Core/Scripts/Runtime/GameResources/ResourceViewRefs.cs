using System;
using TMPro;
using UltEvents;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameResources
{
    public class ResourceViewRefs : MonoBehaviour
    {
        public ResourceBaseSO resource;
        public GameObject amountObject;
        public TMP_Text countText;
        public Image image;
        public bool isBlocked;
        public RectTransform mainRect;
        public RectTransform spriteRect;
        public UltEvent onMerged;
    }
}