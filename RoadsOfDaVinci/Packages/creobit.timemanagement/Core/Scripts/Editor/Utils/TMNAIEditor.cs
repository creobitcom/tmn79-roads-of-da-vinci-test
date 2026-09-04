using UnityEditor;

namespace Pathfinding
{
	[CustomEditor(typeof(AITMNPath), true)]
	[CanEditMultipleObjects]
    public class TMNAIEditor : AIBaseEditor
    {
        protected override void Inspector()
        {
            base.Inspector();
			FloatField("_maxDistance", min: 0f);
        }
    }
}