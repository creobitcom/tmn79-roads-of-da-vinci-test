using Creobit.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectHint
{
    public class ObjectHintView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Картинка добычи. Пусто — блок картинки прячется.")]
        private Image iconImage;

        [SerializeField]
        [Tooltip("Что выключать, когда картинки нет. Пусто — выключается сама картинка.")]
        private GameObject iconBlock;

        [SerializeField]
        [Tooltip("Текст названия. Пусто — блок названия прячется.")]
        private TMP_Text nameText;

        [SerializeField]
        [Tooltip("Что выключать, когда названия нет. Пусто — выключается сам текст.")]
        private GameObject nameBlock;

        public virtual void SetData(ObjectHintData data)
        {
            SetIcon(data.Icon);
            SetName(data.NameKey, data.NameSuffixKey);
        }

        protected virtual void SetIcon(Sprite icon)
        {
            var block = iconBlock != null ? iconBlock : iconImage != null ? iconImage.gameObject : null;

            if (block != null)
            {
                block.SetActive(icon != null);
            }

            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
            }
        }

        protected virtual void SetName(string nameKey, string nameSuffixKey)
        {
            var hasName = !string.IsNullOrEmpty(nameKey);
            var block = nameBlock != null ? nameBlock : nameText != null ? nameText.gameObject : null;

            if (block != null)
            {
                block.SetActive(hasName);
            }

            if (nameText == null || !hasName)
            {
                return;
            }

            nameText.text = Localize(nameKey, nameSuffixKey);
        }

        private static string Localize(string nameKey, string suffixKey)
        {
            var name = Localize(nameKey);

            return string.IsNullOrEmpty(suffixKey) ? name : $"{name} ({Localize(suffixKey)})";
        }

        private static string Localize(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            try
            {
                return LocalizationService.Instance?.GetText(key) ?? key;
            }
            catch
            {
                return key;
            }
        }
    }
}
