using TMPro;
using UnityEngine;
using UnityEngine.UI;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;

public class ResourceItemView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountText;

    public void Setup(ResourceBaseSO resource, int amount)
    {
        icon.sprite = resource.Image;
        amountText.text = amount.ToString();
    }
}