using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Cards;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Loading;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Controller
{
    public interface ITooltipController : ILoadUnit, IDisposable, IReloadable
    {
        public IReactToActions CurrentPrimaryTooltipObject { get; set; }
        
        public void ShowTooltip(IReactToActions target, 
            TooltipSettings tooltipSettings, 
            ResourceAmount[] inputResources,
            ResourceAmount[] outputResources,
            Vector3 position,
            ResourceAmount[] haveResources = null);

        public void ShowExchangeTooltip(IReactToActions target, 
            TooltipSettings tooltipSettings,
            ResourceAmount[] inputResources,
            ResourceAmount[] outputResources,
            Vector3 position,
            ResourceAmount[] haveResources = null);

        public void ShowResourceNotEnoughTooltip(TooltipSettings tooltipSettings, 
            Vector3 position, 
            ResourceAmount[] inputResources, 
            ResourceAmount[] haveResources);

        public void ShowCantReachAnotherObjectTooltip(GameplayTagSO tooltipSettings, Vector3 position);

        /// <summary>
        /// Показать карточку тултипа под UI-элементом. В отличие от остальных Show* здесь нет
        /// ни объекта уровня, ни мировой позиции: и карточки, и UI-панели живут на одном канвасе,
        /// поэтому достаточно RectTransform, к которому надо прижаться.
        ///
        /// Данные карточки собирает вызывающий — контроллер про его предметную область не знает.
        /// Прячется общим <see cref="HidePrimaryTooltip"/>: карточка лежит в списке первичных.
        /// </summary>
        /// <param name="offset">X — сдвиг вбок, Y — зазор между низом элемента и верхом карточки.</param>
        /// <returns>False, если шаблон не задан или его карточка не загружена.</returns>
        public bool ShowCardAtRect(TooltipTemplate template, TooltipCardData data, RectTransform anchor,
            Vector2 offset);

        public void HidePrimaryTooltip();
        public void ShowNoPath(ObjectView.ObjectView objectView);
        public void ShowNoPath(ComplexObject coc);
        public void ShowResourceNotEnough(ObjectView.ObjectView objectView);
        public void ShowResourceNotEnough(ComplexObject coc);
        public void InitTooltip(ObjectView.ObjectView objectView);
        public void InitTooltip(ComplexObject coc);
        public void ShowTooltip(ObjectView.ObjectView objectView);
    }
}