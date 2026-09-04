using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    /// <summary>
    /// Отображение одной пары "ресурс + количество" в карточке рецепта
    /// (используется как для входных ингредиентов, так и для результата крафта).
    /// Иконка и число подставляются кодом; красный круг включается при нехватке ресурса.
    /// </summary>
    public class RecipeResourceView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Иконка ресурса (спрайт ставит код из Resource.Image).")]
        private Image _icon;

        [SerializeField]
        [Tooltip("Число (amount). Шрифт Alkatra, цвет ставит код.")]
        private TMP_Text _amount;

        [SerializeField]
        [Tooltip("Красный круг 'не хватает'. Включается кодом, когда ресурса недостаточно.")]
        private GameObject _notEnoughCircle;

        [SerializeField]
        [Tooltip("Цвет числа, когда ресурса хватает (#698713).")]
        private Color _enoughColor = new Color(0.411765f, 0.529412f, 0.074510f, 1f);

        [SerializeField]
        [Tooltip("Цвет числа, когда ресурса не хватает (#ED3A3A).")]
        private Color _notEnoughColor = new Color(0.929412f, 0.227451f, 0.227451f, 1f);

        [SerializeField]
        [Tooltip("Цвет числа результата крафта (#FFFFFF).")]
        private Color _outputColor = Color.white;

        public void SetData(ResourceAmount resourceAmount, bool isEnough, bool isOutput)
        {
            if (resourceAmount?.Resource == null)
            {
                return;
            }

            if (_icon != null)
            {
                _icon.sprite = resourceAmount.Resource.Image;
                _icon.enabled = true;
            }

            if (_amount != null)
            {
                _amount.text = resourceAmount.Amount.ToString();
                _amount.color = isOutput ? _outputColor : (isEnough ? _enoughColor : _notEnoughColor);
            }

            if (_notEnoughCircle != null)
            {
                _notEnoughCircle.SetActive(!isOutput && !isEnough);
            }
        }
    }
}
