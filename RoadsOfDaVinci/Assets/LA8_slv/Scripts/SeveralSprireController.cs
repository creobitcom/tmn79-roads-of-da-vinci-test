using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Контроллер для последовательного переключения спрайтов у SpriteRenderer.
/// </summary>
public class SeveralSpriteController : MonoBehaviour
{
    [Tooltip("Список спрайтов для переключения")]
    [SerializeField] private List<Sprite> sprites = new List<Sprite>();

    //private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteRenderer spriteRenderer;
    private int currentIndex = 0;

    private void Awake()
    {
        // Получаем компонент SpriteRenderer на этом же объекте
        //spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer не найден на объекте " + gameObject.name);
            return;
        }

        // Устанавливаем первый спрайт, если список не пуст
        if (sprites.Count > 0)
        {
            spriteRenderer.sprite = sprites[0];
        }
        else
        {
            Debug.LogWarning("Список спрайтов пуст!");
        }
    }

    /// <summary>
    /// Переключает на следующий спрайт (по кругу).
    /// </summary>
    public void NextSprite()
    {
        if (sprites.Count == 0) return;

        currentIndex = (currentIndex + 1) % sprites.Count;
        spriteRenderer.sprite = sprites[currentIndex];
    }

    /// <summary>
    /// Переключает на предыдущий спрайт (по кругу).
    /// </summary>
    public void PreviousSprite()
    {
        if (sprites.Count == 0) return;

        currentIndex--;
        if (currentIndex < 0) currentIndex = sprites.Count - 1;
        spriteRenderer.sprite = sprites[currentIndex];
    }

    /// <summary>
    /// Устанавливает конкретный спрайт по индексу.
    /// </summary>
    /// <param name="index">Индекс в списке (от 0 до Count-1)</param>
    public void SetSprite(int index)
    {
        if (sprites.Count == 0) return;

        if (index < 0 || index >= sprites.Count)
        {
            Debug.LogWarning($"Индекс {index} вне диапазона (0-{sprites.Count - 1})");
            return;
        }

        currentIndex = index;
        spriteRenderer.sprite = sprites[currentIndex];
    }

    /// <summary>
    /// Возвращает текущий индекс.
    /// </summary>
    public int CurrentIndex => currentIndex;

    /// <summary>
    /// Возвращает количество спрайтов в списке.
    /// </summary>
    public int Count => sprites.Count;
}