using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.CraftController;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    public class CraftBridge : MonoBehaviour
    {
        private ICraftController _craftController;

        [Inject]
        public void Construct(ICraftController craftController)
        {
            _craftController = craftController;
        }
        
        public void ShowCraftWindow(RecipesData recipesData, Transform parent, MovableObjectTaskView taskView)
        {
            _craftController.ShowCraftWindow(recipesData, parent, taskView);
        }

        public void HideCraftWindow()
        {
            _craftController.HideCraftWindow();
        }
    }
}