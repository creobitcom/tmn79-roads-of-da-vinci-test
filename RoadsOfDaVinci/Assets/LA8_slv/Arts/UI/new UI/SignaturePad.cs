using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SignaturePad : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] private RawImage image;

    [Header("Drawing")]
    [SerializeField] private int textureWidth = 512;
    [SerializeField] private int textureHeight = 256;
    [SerializeField] private int brushSize = 4;

    private Texture2D texture;
    private Vector2Int? previousPoint;

    private int drawnPixels;

    public bool HasSignature => drawnPixels > 100;

    private void Start()
    {
        texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);

        Clear();

        image.texture = texture;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        previousPoint = null;
        Draw(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Draw(eventData);
    }

    private void Draw(PointerEventData eventData)
    {
        RectTransform rect = image.rectTransform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        float x = (localPoint.x + rect.rect.width * 0.5f) / rect.rect.width;
        float y = (localPoint.y + rect.rect.height * 0.5f) / rect.rect.height;

        int px = Mathf.RoundToInt(x * texture.width);
        int py = Mathf.RoundToInt(y * texture.height);

        px = Mathf.Clamp(px, 0, texture.width - 1);
        py = Mathf.Clamp(py, 0, texture.height - 1);

        if (previousPoint.HasValue)
        {
            DrawLine(
                previousPoint.Value.x,
                previousPoint.Value.y,
                px,
                py);
        }

        DrawCircle(px, py, brushSize);

        previousPoint = new Vector2Int(px, py);

        texture.Apply();
    }

    private void DrawLine(int x0, int y0, int x1, int y1)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;

        int err = dx - dy;

        while (true)
        {
            DrawCircle(x0, y0, brushSize);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = err * 2;

            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void DrawCircle(int cx, int cy, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > radius * radius)
                    continue;

                int px = cx + x;
                int py = cy + y;

                if (px < 0 || px >= texture.width)
                    continue;

                if (py < 0 || py >= texture.height)
                    continue;

                texture.SetPixel(px, py, Color.black);
                drawnPixels++;
            }
        }
    }

    public void Clear()
    {
        Color[] pixels = new Color[textureWidth * textureHeight];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        drawnPixels = 0;
        previousPoint = null;
    }

    public void SubmitSignature()
    {
        if (!HasSignature)
        {
            Debug.Log("Подпись слишком короткая");
            return;
        }

        Debug.Log("Документы подписаны!");
    }
}