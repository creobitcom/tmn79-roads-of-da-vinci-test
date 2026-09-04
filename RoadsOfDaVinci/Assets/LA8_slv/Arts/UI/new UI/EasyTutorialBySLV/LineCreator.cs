using UnityEngine;

public class LineCreator : MonoBehaviour
{
    public RectTransform pointA;
    public RectTransform pointB;

    public GameObject linePrefab;
    public Transform canvas;

    [Range(0f, 1f)]
    public float sagAmount = 0.3f;
    [Range(0f, 10f)]
    public float ropeThickness = 3f;
}