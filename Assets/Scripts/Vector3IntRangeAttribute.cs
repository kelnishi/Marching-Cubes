using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
/*
 https://gist.github.com/DGoodayle/69c9c06eb0a277d833c5
 
[Vector3IntRange(minX, maxX, minY, maxY, minZ, maxZ, clamped)]
public Vector3Int yourVector;
*/

public class Vector3IntRangeAttribute : PropertyAttribute
{
    public readonly Vector3Int min, max;
    public readonly bool bClamp;
    public Vector3IntRangeAttribute(
        int minX, int maxX,
        int minY, int maxY,
        int minZ, int maxZ,
        bool bClamp)
    {
        min = new Vector3Int(minX, minY, minZ);
        max = new Vector3Int(maxX, maxY, maxZ);
        this.bClamp = bClamp;
    }
}

[CustomPropertyDrawer(typeof(Vector3IntRangeAttribute))]
public class Vector3IntRangeAttributeDrawer : PropertyDrawer
{
    Vector3IntRangeAttribute RangeAttribute {  get { return (Vector3IntRangeAttribute)attribute;  } }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Color previous = GUI.color;
        GUI.color = !IsValid(property) ? Color.red : Color.white;
        EditorGUI.BeginChangeCheck();
        Rect textFieldPosition = position;
        textFieldPosition.width = position.width;
        textFieldPosition.height = position.height;
        
        Vector3Int val = EditorGUI.Vector3IntField(textFieldPosition, label, property.vector3IntValue);
        if (EditorGUI.EndChangeCheck())
        {
            if (RangeAttribute.bClamp)
            {
                val.x = math.clamp(val.x, RangeAttribute.min.x, RangeAttribute.max.x);
                val.y = math.clamp(val.y, RangeAttribute.min.y, RangeAttribute.max.y);
                val.y = math.clamp(val.y, RangeAttribute.min.z, RangeAttribute.max.z);
            }
            property.vector3IntValue = val;
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

        EditorGUI.HelpBox(position,$"Invalid Range X [{RangeAttribute.min.x}]-[{RangeAttribute.max.x}] Y [{RangeAttribute.min.y}]-[{RangeAttribute.max.y}] Z [{RangeAttribute.min.z}]-[{RangeAttribute.max.z}]", MessageType.Error);
    }
    bool IsValid(SerializedProperty prop)
    {
        Vector3Int vector = prop.vector3IntValue;
        return (vector.x >= RangeAttribute.min.x && 
                vector.x <= RangeAttribute.max.x &&
                vector.y >= RangeAttribute.min.y &&
                vector.y <= RangeAttribute.max.y &&
                vector.z >= RangeAttribute.min.z &&
                vector.z <= RangeAttribute.max.z);
    }
}