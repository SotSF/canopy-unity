using NodeEditorFramework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a [CanvasResourcePath] string as its Resources path plus a drop field: dropping a
/// canvas asset that lives in a Resources folder records its path ("CanvasSaves/Foo"). The
/// referenced canvas is never loaded to draw the entry — in play mode that would deserialize
/// the asset and start its nodes, which is what storing paths is meant to avoid.
/// </summary>
[CustomPropertyDrawer(typeof(CanvasResourcePathAttribute))]
public class CanvasResourcePathDrawer : PropertyDrawer
{
    const float DropFieldWidth = 150f;
    const string ResourcesFolder = "/Resources/";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "[CanvasResourcePath] needs a string");
            return;
        }
        EditorGUI.BeginProperty(position, label, property);
        Rect pathRect = new Rect(position.x, position.y, position.width - DropFieldWidth - 4f, position.height);
        Rect dropRect = new Rect(pathRect.xMax + 4f, position.y, DropFieldWidth, position.height);

        property.stringValue = EditorGUI.TextField(pathRect, label, property.stringValue);

        EditorGUI.BeginChangeCheck();
        var dropped = (NodeCanvas)EditorGUI.ObjectField(dropRect, GUIContent.none, null, typeof(NodeCanvas), false);
        if (EditorGUI.EndChangeCheck() && dropped != null)
        {
            string resourcePath = ToResourcePath(AssetDatabase.GetAssetPath(dropped));
            if (resourcePath == null)
            {
                Debug.LogWarning($"'{dropped.name}' isn't inside a Resources folder, so the built app can't load it by name. " +
                                 "Move it under a Resources folder (e.g. Assets/TextureSynthesis/Resources/CanvasSaves).");
            }
            else
            {
                property.stringValue = resourcePath;
            }
        }
        EditorGUI.EndProperty();
    }

    /// <summary>"Assets/.../Resources/CanvasSaves/Foo.asset" → "CanvasSaves/Foo"; null if not under Resources.</summary>
    static string ToResourcePath(string assetPath)
    {
        int idx = assetPath.LastIndexOf(ResourcesFolder, System.StringComparison.Ordinal);
        if (idx < 0) return null;
        string relative = assetPath.Substring(idx + ResourcesFolder.Length);
        int dot = relative.LastIndexOf('.');
        return dot > 0 ? relative.Substring(0, dot) : relative;
    }
}
