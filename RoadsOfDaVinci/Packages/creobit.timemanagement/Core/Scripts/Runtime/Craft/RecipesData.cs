using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    [CreateAssetMenu(menuName = "Create RecipesData", fileName = "RecipesData", order = 0)]
    public class RecipesData : ScriptableObject
    {
        public List<RecipeData> recipes;
    }
}