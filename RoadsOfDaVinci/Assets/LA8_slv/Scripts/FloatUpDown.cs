using UnityEngine;

public class FloatUpDown : MonoBehaviour
{
    public float amplitude = 1f; // высота движения
    public float speed = 2f;     // скорость

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * speed) * amplitude;
        transform.position = startPosition + Vector3.up * y;
    }
}