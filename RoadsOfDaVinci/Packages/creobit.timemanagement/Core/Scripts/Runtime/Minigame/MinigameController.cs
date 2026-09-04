using System.Collections.Generic;
using Creobit.Loading;
using Cysharp.Threading.Tasks;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Minigame
{
    public class MinigameController
    {
        private readonly List<MinigameMainObjectView> _mainObjects = new();

        public void AddMainObject(MinigameMainObjectView mainObject)
        {
            _mainObjects.Add(mainObject);
            
            foreach (var target in mainObject.targets)
            {
                target.objectView.OnKeyChanged += () => CheckMainObjectState(mainObject);
                target.objectView.Init();
            }
        }

        private void CheckMainObjectState(MinigameMainObjectView mainObject)
        {
            if (mainObject.isUsed)
                return;
            
            var done = true;
            
            foreach (var target in mainObject.targets)
            {
                if (target.objectView.currentKey != target.targetKey)
                {
                    done = false;
                }
            }

            if (done)
            {
                mainObject.onMinigameComplete?.Invoke();
                mainObject.isUsed = true;
            }
        }
    }
}