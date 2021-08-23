using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
/*
[Vector2MinMax(minVal, maxVal)]
public Vector2 rangePair;
*/

public class Vector2MinMaxAttribute : PropertyAttribute
{
    public readonly float minVal, maxVal;

    public Vector2MinMaxAttribute(float minVal, float maxVal)
    {
        this.minVal = minVal;
        this.maxVal = maxVal;
    }
}

[CustomPropertyDrawer(typeof(Vector2MinMaxAttribute))]
public class Vector2MinMaxAttributeDrawer : PropertyDrawer
{
    Vector2MinMaxAttribute RangeAttribute {  get { return (Vector2MinMaxAttribute)attribute;  } }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Color previous = GUI.color;
        GUI.color = !IsValid(property) ? Color.red : Color.white;
        EditorGUI.BeginChangeCheck();

        Vector2 xy = property.vector2Value;
        float x = xy.x;
        float y = xy.y;
        Rect labelRect = position;
        labelRect.width = EditorGUIUtility.labelWidth + 60f - EditorGUIUtility.standardVerticalSpacing;
        x = EditorGUI.FloatField(labelRect, label, x);
        position.x += labelRect.width + EditorGUIUtility.standardVerticalSpacing;
        position.width -= labelRect.width;
        Rect sliderRect = position;
        sliderRect.width -= 60f;
        EditorGUI.MinMaxSlider(sliderRect, "",
            ref x, ref y,
            RangeAttribute.minVal, RangeAttribute.maxVal);
        Rect maxRect = position;
        maxRect.x += sliderRect.width + EditorGUIUtility.standardVerticalSpacing;
        maxRect.width = 60f - EditorGUIUtility.standardVerticalSpacing;
        y = EditorGUI.FloatField(maxRect, "", y);
        
        if (EditorGUI.EndChangeCheck())
        {
            property.vector2Value = new Vector2(x,y);
        }
        Rect helpPosition = position;
        helpPosition.y += 16;
        helpPosition.height = 16;
        DrawHelpBox(helpPosition, property);
        GUI.color = previous;
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!IsValid(property))
        {
            return 32;
        }
        return base.GetPropertyHeight(property, label);
    }
    void DrawHelpBox(Rect position, SerializedProperty prop)
    {
        // No need for a help box if the pattern is valid.
        if (IsValid(prop))
            return;

        EditorGUI.HelpBox(position,$"Invalid Range [{RangeAttribute.minVal}]-[{RangeAttribute.maxVal}]", MessageType.Error);
    }
    bool IsValid(SerializedProperty prop)
    {
        Vector2 range = prop.vector2Value;
        return (range.x >= RangeAttribute.minVal &&
                range.y <= RangeAttribute.maxVal &&
                range.x <= range.y);
    }
}