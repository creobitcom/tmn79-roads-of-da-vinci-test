using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data
{
    /// <summary>
    /// Шаблон тултипа: какой префаб карточки показывать для объекта.
    ///
    /// Сознательно ScriptableObject, а не enum: добавление нового вида тултипа не должно
    /// требовать правки базового модуля (требование ГД). Новый вид = новый ассет шаблона
    /// + свой префаб; код модуля не меняется, пока хватает существующих вьюх.
    ///
    /// Назначается на объекте: ObjectDataSO → TooltipSettings → Template.
    /// Пусто — объект показывается СТАРОЙ системой тултипов (обратная совместимость).
    /// </summary>
    [CreateAssetMenu(fileName = "TooltipTemplate", menuName = "8floor/TimeManager/Tooltip/Template")]
    public class TooltipTemplate : ScriptableObject
    {
        [field: SerializeField]
        [field: Tooltip("Префаб карточки. На корне должен висеть компонент с ITooltipCardView " +
                        "(TooltipCardView или TooltipFactoryCardView). Должен быть в Addressables.")]
        public AssetReference ViewPrefab { get; private set; }

        [field: SerializeField]
        [field: TextArea]
        [field: Tooltip("Заметка для ГД: для чего этот шаблон. На игру не влияет.")]
        public string Note { get; private set; }
    }
}
