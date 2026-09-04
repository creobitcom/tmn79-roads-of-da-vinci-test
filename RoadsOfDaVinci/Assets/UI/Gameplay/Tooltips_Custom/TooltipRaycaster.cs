using UnityEngine;
using UnityEngine.InputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;

public class MouseRaycast : MonoBehaviour
{
    [SerializeField] private GameObject inputTooltipPrefab;
    [SerializeField] private GameObject outputTooltipPrefab;
    [SerializeField] private GameObject bothTooltipPrefab;

    [SerializeField] private float hoverDelay = 0.3f;
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 1.5f, 0);

    private RectTransform layer2;

    private RectTransform tooltipRect;
    private GameObject currentTooltip;
    private GameObject currentTarget;
    private TooltipView tooltipView;

    private float hoverTime = 0f;

    void Awake()
    {
        GameObject layerObj = GameObject.Find("Layer2");

        if (layerObj != null)
            layer2 = layerObj.GetComponent<RectTransform>();
        else
            Debug.LogError("Layer2 not found in scene!");
    }

    void Start()
    {
        if (layer2 == null) return;
    }

    void Update()
    {
        if (layer2 == null)
            return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            GameObject hitObj = hit.collider.gameObject;
            var staticView = hit.collider.GetComponentInParent<StaticObjectView>();

            // смена объекта
            if (hitObj != currentTarget)
            {
                currentTarget = hitObj;
                hoverTime = 0f;

                if (currentTooltip != null)
                    Destroy(currentTooltip);
            }

            hoverTime += Time.deltaTime;

            if (hoverTime >= hoverDelay && currentTooltip == null)
            {
                if (staticView != null)
                {
                    var data = staticView.ObjectDataSO;

                    bool hasInput =
                        data.InteractionNeedInputResources &&
                        data.InputResources != null &&
                        data.InputResources.Length > 0;

                    bool hasOutput =
                        data.InteractionHaveOutputResources &&
                        data.OutputResources != null &&
                        data.OutputResources.Length > 0;

                    GameObject prefabToUse = null;

                    if (hasInput && hasOutput)
                        prefabToUse = bothTooltipPrefab;
                    else if (hasInput)
                        prefabToUse = inputTooltipPrefab;
                    else if (hasOutput)
                        prefabToUse = outputTooltipPrefab;

                    if (prefabToUse == null)
                        return;

                    // SPAWN TOOLTIP
                    currentTooltip = Instantiate(prefabToUse, layer2);
                    tooltipRect = currentTooltip.GetComponent<RectTransform>();
                    tooltipView = currentTooltip.GetComponent<TooltipView>();

                    // NAME
                    tooltipView.SetName(
                        data.TooltipSettings.TooltipData.TooltipObjectName
                    );

                    // INPUT
                    if (hasInput)
                    {
                        var res = data.InputResources;

                        ResourceBaseSO[] resources = new ResourceBaseSO[res.Length];
                        int[] amounts = new int[res.Length];

                        for (int i = 0; i < res.Length; i++)
                        {
                            resources[i] = res[i].Resource;
                            amounts[i] = res[i].Amount;
                        }

                        tooltipView.SetResources(resources, amounts);
                    }

                    // OUTPUT
                    if (hasOutput)
                    {
                        var res = data.OutputResources;

                        ResourceBaseSO[] resources = new ResourceBaseSO[res.Length];
                        int[] amounts = new int[res.Length];

                        for (int i = 0; i < res.Length; i++)
                        {
                            resources[i] = res[i].Resource;
                            amounts[i] = res[i].Amount;
                        }

                        tooltipView.SetOutputResources(resources, amounts);
                    }
                }
            }

            // POSITION
            if (currentTooltip != null)
            {
                Vector3 worldPos = hitObj.transform.position + worldOffset;
                Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    layer2,
                    screenPos,
                    null,
                    out Vector2 localPos
                );

                tooltipRect.anchoredPosition =
                    Vector2.Lerp(tooltipRect.anchoredPosition, localPos, Time.deltaTime * 12f);
            }
        }
        else
        {
            currentTarget = null;
            hoverTime = 0f;

            if (currentTooltip != null)
                Destroy(currentTooltip);

            currentTooltip = null;
        }
    }
}