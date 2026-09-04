using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    [Serializable]
    public class RecipeData
    {
        public List<ResourceAmount> inputResources;
        public List<ResourceAmount> outputResources;
    }
}