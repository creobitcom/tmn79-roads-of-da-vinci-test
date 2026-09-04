using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView
{
    public interface IObjectViewController : ILoadUnit, IDisposable
    {
        public void AddObjectView(ObjectView objectView);
        public bool TryCancelTask(ObjectView objectView);
        public event Action<ObjectView> OnObjectViewAdded;
        public StaticObjectController GetStaticObjectController();
        public MovableObjectController GetMovableObjectController();
        public ObjectSpecialTagController GetObjectSpecialTagController();
        public UnitBaseController GetUnitBaseController();
        public UniTask<bool> CanUse(ObjectView objectView);

        /// <summary>
        /// Пересобрать открытую панель выбора альтернативы под текущие данные (состав вариантов,
        /// проходимость пути, доступность ресурсов и юнитов). Если панель не открыта — ничего
        /// не делает. Нужен тем, кто меняет мир мимо ObjectView.OnInteractionStateChanged —
        /// например строителю COC.
        /// </summary>
        public void RefreshAlternativeSelection();

        /// <summary>
        /// Открылась панель выбора альтернативы. Тултип наведённого объекта на это время
        /// убирается: панель встаёт ровно над объектом, на место подсказки, и закрывает экран
        /// своим блокером — уйти курсором с объекта и погасить тултип штатным путём уже нельзя.
        ///
        /// Без аргумента сознательно: панель открывается для ObjectView, а целью наведения
        /// у тултипа в случае фабрики записан сам ComplexObject (ComplexObject.OnPrimaryAction
        /// делегирует клик в CurrentObjectView). Сверять их между собой не по чему — тултип
        /// гасит то, что показывает сам.
        /// </summary>
        public event Action OnAlternativeSelectionOpened;

        public event Action<ObjectView> OnAlternativeSelectionShown;

        public event Action<ObjectView> OnAlternativeSelectionClosed;

        public event Action<ObjectView> OnNotResourcesTooltip;
        public event Action<ObjectView> OnNotPathTooltip;
        public event Action<GameplayTagSO, Vector3> OnNotTagTooltip;
        public event Action<ObjectView> OnInitTooltip;
    }
}
