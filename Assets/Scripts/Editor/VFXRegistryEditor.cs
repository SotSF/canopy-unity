using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

/// <summary>
/// Inspector tools for VFXRegistry: scaffold a new VFX rig (VFXInstance root with
/// VFX + camera + spotlight + post volume children) on the next grid slot, and snap
/// existing child rigs onto the registry's grid layout.
/// </summary>
[CustomEditor(typeof(VFXRegistry))]
public class VFXRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var registry = (VFXRegistry)target;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("VFX Rig Tools", EditorStyles.boldLabel);

        if (registry.rigTemplate == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Rig Template above (e.g. the GoldenSpiralCam rig) and 'Create New VFX Rig' " +
                "will clone it with the VFX asset and render texture cleared. Without a template, a " +
                "minimal rig is built from scratch (camera/light/volume settings will need tuning).",
                MessageType.Info);
        }

        if (GUILayout.Button("Create New VFX Rig (next grid slot)"))
        {
            CreateRig(registry);
        }
        if (GUILayout.Button("Snap Child Rigs To Grid"))
        {
            SnapChildrenToGrid(registry);
        }
        if (GUILayout.Button("Disable All Child Rigs"))
        {
            SetAllChildrenActive(registry, false);
        }
        if (GUILayout.Button("Enable All Child Rigs"))
        {
            SetAllChildrenActive(registry, true);
        }
    }

    public static void SetAllChildrenActive(VFXRegistry registry, bool active)
    {
        int changed = 0;
        foreach (Transform child in registry.transform)
        {
            if (child.gameObject.activeSelf == active) continue;
            Undo.RecordObject(child.gameObject, active ? "Enable VFX Rigs" : "Disable VFX Rigs");
            child.gameObject.SetActive(active);
            changed++;
        }
        Debug.Log($"VFXRegistry: {(active ? "enabled" : "disabled")} {changed} child rig(s).");
    }

    public static GameObject CreateRig(VFXRegistry registry)
    {
        int slot = registry.transform.childCount;
        Vector3 position = registry.GetSlotWorldPosition(slot);

        GameObject rig;
        if (registry.rigTemplate != null)
        {
            // Plain clone (deliberately not prefab-linked): an independent rig to customize
            rig = Instantiate(registry.rigTemplate.gameObject, position, Quaternion.identity, registry.transform);
            // Blank the clone so it starts as an empty slate
            var effect = rig.GetComponentInChildren<VisualEffect>(true);
            if (effect != null) effect.visualEffectAsset = null;
            foreach (var cam in rig.GetComponentsInChildren<Camera>(true))
            {
                cam.targetTexture = null; // per-instance RTs are allocated at runtime
            }
        }
        else
        {
            rig = BuildMinimalRig(position, registry.transform);
        }
        rig.name = GameObjectUtility.GetUniqueNameForSibling(registry.transform, "NewVFXRig");
        Undo.RegisterCreatedObjectUndo(rig, "Create VFX Rig");
        Selection.activeGameObject = rig;
        EditorGUIUtility.PingObject(rig);
        return rig;
    }

    static GameObject BuildMinimalRig(Vector3 position, Transform parent)
    {
        var root = new GameObject("NewVFXRig", typeof(VFXInstance));
        root.transform.SetParent(parent);
        root.transform.position = position;

        var fxGo = new GameObject("VFX", typeof(VisualEffect));
        fxGo.transform.SetParent(root.transform, false);

        var camGo = new GameObject("Camera", typeof(Camera));
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -8f);
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;

        var lightGo = new GameObject("Spotlight", typeof(Light));
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 4f, -4f);
        lightGo.transform.LookAt(root.transform.position);
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Spot;
        light.range = 20f;
        light.spotAngle = 60f;

        var volumeGo = new GameObject("PostVolume", typeof(Volume));
        volumeGo.transform.SetParent(root.transform, false);
        volumeGo.GetComponent<Volume>().isGlobal = true; // assign a profile + volume mask per rig

        var instance = root.GetComponent<VFXInstance>();
        instance.cam = cam;
        instance.effect = fxGo.GetComponent<VisualEffect>();
        return root;
    }

    public static void SnapChildrenToGrid(VFXRegistry registry)
    {
        int slot = 0;
        int skipped = 0;
        foreach (Transform child in registry.transform)
        {
            // only rig-like children participate (anything with a camera or VFX in its subtree)
            bool isRig = child.GetComponentInChildren<Camera>(true) != null
                      || child.GetComponentInChildren<VisualEffect>(true) != null;
            if (!isRig)
            {
                skipped++;
                continue;
            }
            Undo.RecordObject(child, "Snap VFX Rigs To Grid");
            child.position = registry.GetSlotWorldPosition(slot);
            slot++;
        }
        Debug.Log($"VFXRegistry: snapped {slot} rig(s) to the grid" +
                  (skipped > 0 ? $", skipped {skipped} non-rig child(ren)." : "."));
    }
}
