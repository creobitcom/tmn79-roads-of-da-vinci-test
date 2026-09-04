using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.CollectionItems
{
    /// <summary>
    /// Одноразовость предмета коллекции: если предмет уже записан в профиль
    /// (SaveService.IsCollectionItemSaved по имени его ObjectDataSO), при старте уровня
    /// предмет прячется и становится неинтерактивным. Повторяет оригинал GG8, где
    /// getConditionChest() выключал коллекционную ячейку сундука для уже собранных предметов.
    ///
    /// Объект не удаляется, а гасится (рендереры/коллайдеры + SetActive(false)):
    /// на него могут целиться UltEvent'ы сундука (SetActive, set_sprite) — повторное
    /// включение пустого объекта безвредно, а удаление порвало бы persistent-вызовы.
    /// </summary>
    public class CollectionItemGate : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Вью предмета — по имени его ObjectDataSO проверяется, собран ли предмет.")]
        private ObjectView.ObjectView _objectView;

        private ISaveController _saveController;

        [Inject]
        private void Construct(ISaveController saveController)
        {
            _saveController = saveController;
        }

        private void Start()
        {
            if (_saveController == null || _objectView == null || _objectView.ObjectDataSO == null)
            {
                return;
            }

            if (!_saveController.Service.IsCollectionItemSaved(_objectView.ObjectDataSO.name))
            {
                return;
            }

            var root = _objectView.gameObject;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var c in root.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;

            root.SetActive(false);
        }
    }
}
