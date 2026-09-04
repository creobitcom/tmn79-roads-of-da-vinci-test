using TMPro;
using UnityEngine;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;

public class TooltipView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Transform inputRoot;
    [SerializeField] private Transform outputRoot;
    [SerializeField] private ResourceItemView itemPrefab;

    public void SetName(string text)
    {
        nameText.text = text;
    }

    public void Clear()
    {
        foreach (Transform child in inputRoot)
            Destroy(child.gameObject);
    }

    public void SetResources(ResourceBaseSO[] resources, int[] amounts)
    {
        Clear();

        if (resources == null || amounts == null)
            return;

        int count = Mathf.Min(resources.Length, amounts.Length);

        for (int i = 0; i < count; i++)
        {
            var item = Instantiate(itemPrefab, inputRoot);
            item.Setup(resources[i], amounts[i]);
        }
    }


    public void SetOutputResources(ResourceBaseSO[] resources, int[] amounts)
    {
        foreach (Transform child in outputRoot)
            Destroy(child.gameObject);

        if (resources == null || amounts == null)
            return;

        int count = Mathf.Min(resources.Length, amounts.Length);

        for (int i = 0; i < count; i++)
        {
            var item = Instantiate(itemPrefab, outputRoot);
            item.Setup(resources[i], amounts[i]);
        }
    }

    public void ClearInput()
    {
        foreach (Transform child in inputRoot)
            Destroy(child.gameObject);
    }

    public void ClearOutput()
    {
        foreach (Transform child in outputRoot)
            Destroy(child.gameObject);
    }
}