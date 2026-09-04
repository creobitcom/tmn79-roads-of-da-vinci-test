using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Loading;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.CraftController
{
    public interface ICraftController : ILoadUnit, IReloadable, IDisposable
    {
        void ShowCraftWindow(RecipesData recipesData, Transform parent, MovableObjectTaskView taskView);
        void HideCraftWindow();
    }
}