#if UNITY_EDITOR
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.Attributes;
using UnityEditor;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils.Drawers
{
    [CustomPropertyDrawer(typeof(IntStepAttribute))]
    public class IntStepAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.LabelField(position, label.text, "Use attribute with int.");
                return;
            }

            IntStepAttribute stepAttribute = (IntStepAttribute)attribute;

            int currentValue = property.intValue;
            
            EditorGUI.BeginChangeCheck();
            
            currentValue = EditorGUI.IntField(position, label, currentValue);
 
            if (EditorGUI.EndChangeCheck())
            { 
                currentValue = (int)(Mathf.Round(currentValue / stepAttribute.Step) * stepAttribute.Step);
                
                property.intValue = currentValue;
            }
        }
    }
}

#endif