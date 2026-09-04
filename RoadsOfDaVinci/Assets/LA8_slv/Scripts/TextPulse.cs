using UnityEngine;
using UnityEngine.InputSystem;

public class TextPulse : MonoBehaviour
{
    public float speed = 2f;
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    private Vector3 originalScale;

    void Start()
    {
        originalScale = transform.localScale;
    }

    void Update()
    {
        float scale = Mathf.Lerp(
            minScale,
            maxScale,
            (Mathf.Sin(Time.time * speed) + 1f) / 2f
        );

        transform.localScale = originalScale * scale;

        // Клик мышкой
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            gameObject.SetActive(false);
        }
    }
}