using sotsf.canopy.patterns;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class PropertyDrawerUtility
{
    //Code taken from SketchyVentures: http://sketchyventures.com/2015/08/07/unity-tip-getting-the-actual-object-from-a-custom-property-drawer/
    public static T GetActualObject<T>(FieldInfo fieldInfo, SerializedProperty property) where T : class
    {
        var obj = fieldInfo.GetValue(property.serializedObject.targetObject);
        if (obj == null) { return null; }

        T actualObject = null;
        if (obj.GetType().IsArray)
        {
            // Pulls the object index out of the propertypath. Black majik fuckery.
            var index = Convert.ToInt32(new string(property.propertyPath.Where(c => char.IsDigit(c)).ToArray()));
            try
            {
                actualObject = ((T[])obj)[index];
            } catch (IndexOutOfRangeException) {
                return null;
            }   
        }
        else
        {
            actualObject = obj as T;
        }
        return actualObject;
    }
}

[CustomPropertyDrawer(typeof(PatternParameter))]
public class PatternParameterDrawer : PropertyDrawer
{
    const int rowHeight = 16;
    const float rowSpacing = 20;

    private PatternParameter PropToParam(SerializedProperty prop)
    {
        return PropertyDrawerUtility.GetActualObject<PatternParameter>(fieldInfo, prop);
    }

    public bool IsNumeric(PatternParameter param)
    {
        if (param == null)
        {
            return false;
        }
        if (param.paramType == ParamType.FLOAT || param.paramType == ParamType.INT)
        {
            return true;
        }
        return false;
    }

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
    {
        PatternParameter param = PropToParam(prop);
        return base.GetPropertyHeight(prop, label) + GetRows(param) * rowHeight;
    }

    public int GetRows(PatternParameter param)
    {
        //1 row for name + type
        //1 row for controllable
        int rowcount = 2;
        if (param != null && param.controllable){
            rowcount++;
            if (IsNumeric(param))
            {
                //+1 row for "useRange"
                rowcount++;
                if (param.useRange)
                {
                    //+1 row for min/max
                    rowcount++;
                }
            }
        }
        return rowcount;
    }

    private float YOffset(int rows, float start){
        return rows * rowSpacing + start;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        PatternParameter param = PropToParam(property);
        int totalRows = GetRows(param);
        int rowsFilled = 0;
        var lineRect = new Rect(position.x, position.y+0.5f, position.width, 0.5f);
        EditorGUI.DrawRect(lineRect, Color.black);

        EditorGUI.BeginProperty(position, label, property);

        // Draw label
        //position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Name"));
        position = new Rect(position.x, position.y + 4.5f, position.width, position.height);
        float width = position.width;


        var nameRect = new Rect(position.x, YOffset(rowsFilled, position.y),
                                3* width / 4, rowHeight);
        
        var typeRect = new Rect(position.x + 3* width / 4, YOffset(rowsFilled, position.y),
                                width / 4, rowHeight);

        var nameProp = property.FindPropertyRelative("name");
        var typeProp = property.FindPropertyRelative("paramType");

        EditorGUI.PropertyField(nameRect, nameProp);
        EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);
        rowsFilled++;

        var controllableRect = new Rect(position.x, YOffset(rowsFilled, position.y),
                                        width, rowHeight);
        var controllableProp = property.FindPropertyRelative("controllable");
        EditorGUI.PropertyField(controllableRect, controllableProp);
        rowsFilled++;
        if (param != null && param.controllable){
            if (IsNumeric(param))
            {
                var rangeRect = new Rect(position.x, YOffset(rowsFilled, position.y), width, rowHeight);
                var rangeProp = property.FindPropertyRelative("useRange");
                EditorGUI.PropertyField(rangeRect, rangeProp);
                rowsFilled++;
                if (param.useRange)
                {
                    var minRect = new Rect(position.x, YOffset(rowsFilled, position.y), width / 2, rowHeight);
                    var maxRect = new Rect(position.x + width / 2, YOffset(rowsFilled, position.y), width / 2, rowHeight);
                    SerializedProperty minProp;
                    SerializedProperty maxProp;
                    if (param.paramType == ParamType.FLOAT)
                    {
                        minProp = property.FindPropertyRelative("minFloat");
                        maxProp = property.FindPropertyRelative("maxFloat");
                    } else
                    {
                        minProp = property.FindPropertyRelative("minInt");
                        maxProp = property.FindPropertyRelative("maxInt");
                    }
                    EditorGUI.PropertyField(minRect, minProp, new GUIContent("Min"));
                    EditorGUI.PropertyField(maxRect, maxProp, new GUIContent("Max"));
                    rowsFilled++;
                }
            }
            var defaultRect = new Rect(position.x, YOffset(rowsFilled, position.y), position.width, rowHeight);
            SerializedProperty defaultProp;
            if (param != null)
            {
                switch (param.paramType)
                {
                    case (ParamType.BOOL):
                        defaultProp = property.FindPropertyRelative("defaultBool");
                        break;
                    case (ParamType.INT):
                        defaultProp = property.FindPropertyRelative("defaultInt");
                        break;
                    case (ParamType.FLOAT4):
                        defaultProp = property.FindPropertyRelative("defaultVector");
                        break;
                    case (ParamType.TEXTURE):
                        defaultProp = property.FindPropertyRelative("defaultTexture");
                        break;
                    default:
                        defaultProp = property.FindPropertyRelative("defaultFloat");
                        break;
                }
                EditorGUI.PropertyField(defaultRect, defaultProp, new GUIContent("Default"));
            }
        }
        EditorGUI.EndProperty();
    }
}