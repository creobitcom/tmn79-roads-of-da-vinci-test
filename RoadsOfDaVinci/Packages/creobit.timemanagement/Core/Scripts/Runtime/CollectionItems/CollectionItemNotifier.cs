using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.CollectionItems
{
    /// <summary>
    /// Мостик от префаба предмета коллекции к плашке уведомления в сцене.
    /// Вешается на префаб предмета, вызов Notify() — на UltEvent SaveBridge._onSaveCollectionItem
    /// (т.е. строго после записи предмета в профиль).
    ///
    /// Иконка: явная _overrideIcon, иначе — из тултипа предмета
    /// (ObjectDataSO → TooltipSettings → TooltipData.TooltipObjectIcon). Благодаря этому при подмене
    /// _dataSO тулсой портирования (лут в сундуке) иконка меняется сама, без настройки.
    /// Если плашка в сцене не назначена (GameplaySceneReferences.CollectionNotificationView == null),
    /// вызов молча ничего не делает — части без этой механики ничего не замечают.
    /// </summary>
    public class CollectionItemNotifier : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Вью предмета — источник иконки через его ObjectDataSO. Обычно StaticObjectView этого же префаба.")]
        private ObjectView.ObjectView _objectView;

        [SerializeField]
        [Tooltip("Необязательная замена иконки. Пусто — берётся TooltipObjectIcon из тултипа предмета.")]
        private Sprite _overrideIcon;

        private GameplaySceneReferences _sceneReferences;

        [Inject]
        private void Construct(GameplaySceneReferences sceneReferences)
        {
            _sceneReferences = sceneReferences;
        }

        public void Notify()
        {
            var view = _sceneReferences != null ? _sceneReferences.CollectionNotificationView : null;
            if (view == null)
            {
                return;
            }

            var icon = _overrideIcon;
            if (icon == null)
            {
                icon = _objectView != null
                    ? _objectView.ObjectDataSO?.TooltipSettings?.TooltipData?.TooltipObjectIcon
                    : null;
            }

            view.Show(icon);
        }
    }
}
