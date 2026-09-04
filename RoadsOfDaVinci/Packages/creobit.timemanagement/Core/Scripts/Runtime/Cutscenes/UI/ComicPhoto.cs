using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.UI
{
    /// <summary>
    /// Одно фото комикс-сцены. Вешается на слот (тот же объект, где CanvasGroup),
    /// картинка (Image) обычно лежит в дочернем объекте. Позиция/поворот/размер
    /// настраиваются прямо на RectTransform в префабе, спрайт — в Image.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ComicPhoto : MonoBehaviour
    {
        [Tooltip("Картинка кадра (обычно дочерний ComicImage). Спрайт ставит ГД в префабе.")]
        [SerializeField] private Image _image;

        [Tooltip("Время плавного проявления (fade-in) в секундах")]
        public float FadeDuration = 0.2f;

        [Tooltip("Время анимации появления (pop-up, масштаб) в секундах. 0 = без pop-up")]
        public float PopDuration = 0.3f;

        [Tooltip("Сколько кадр висит после появления до перехода к следующему (или закрытия)")]
        public float DurationSeconds = 1f;

        [Tooltip("Звук кадра. Играется через общий SFX-источник игры — микшер в префабе назначать не нужно.")]
        public AudioClip Sound;

        [Tooltip("Задержка звука от начала появления кадра, сек. 0 = сразу вместе с кадром")]
        public float SoundDelay;

        private CanvasGroup _group;
        private Button _button;

        public CanvasGroup Group => _group != null ? _group : (_group = GetComponent<CanvasGroup>());
        public Button Button => _button != null ? _button : (_button = GetComponent<Button>());
        public Image Image => _image;
        public RectTransform Rect => (RectTransform)transform;

#if UNITY_EDITOR
        // Рисует контур целевого размера при открытии фото (зум) — видно в Scene View префаба при выделении.
        private void OnDrawGizmosSelected()
        {
            var scene = GetComponentInParent<ComicScene>();
            if (scene == null) return;

            var rt = Rect;
            float w = rt.sizeDelta.x;
            float h = rt.sizeDelta.y;
            if (w <= 0f || h <= 0f) return;

            float scale = scene.ZoomTargetWidth > 0f
                ? scene.ZoomTargetWidth / w
                : rt.localScale.x * scene.ZoomScale;

            float finalW = w * scale;
            float finalH = h * scale;

            var parent = rt.parent;
            if (parent == null) return;

            Vector3 c = rt.localPosition + new Vector3(scene.ZoomCenterOffset.x, scene.ZoomCenterOffset.y, 0f);
            Vector3 hw = new Vector3(finalW * 0.5f, finalH * 0.5f, 0f);

            Vector3 bl = parent.TransformPoint(c + new Vector3(-hw.x, -hw.y, 0f));
            Vector3 br = parent.TransformPoint(c + new Vector3(hw.x, -hw.y, 0f));
            Vector3 tr = parent.TransformPoint(c + new Vector3(hw.x, hw.y, 0f));
            Vector3 tl = parent.TransformPoint(c + new Vector3(-hw.x, hw.y, 0f));

            Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
#endif
    }
}
