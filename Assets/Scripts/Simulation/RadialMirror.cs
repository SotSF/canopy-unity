using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Spawns radially symmetric copies of <see cref="rootObject"/> around this
/// transform's origin and keeps them in sync as the root moves. Each copy is
/// the root's local transform rotated about <see cref="axis"/> by an even
/// fraction of a full turn, so both position and orientation stay mirrored.
///
/// Runs in the editor as well as in play mode. Generated copies are marked
/// DontSave so they never get written into the scene, and are re-adopted after
/// domain reloads / recompiles to avoid spawning duplicates.
/// </summary>
[ExecuteAlways]
public class RadialMirror : MonoBehaviour
{
    [Tooltip("The child object to mirror. Its transform, relative to this object, defines slot 0.")]
    public GameObject rootObject;

    [Tooltip("Total number of radial slots including the root. e.g. 6 = the root plus 5 copies.")]
    [Min(1)]
    public int symmetryCount = 1;

    [Tooltip("Axis, in this object's local space, that copies are rotated around.")]
    public Vector3 axis = Vector3.forward;

    [Tooltip("Mirror the root's VisualEffect exposed-property values onto each copy's effect " +
             "every frame, so canvas-driven parameter changes reach all symmetric copies.")]
    public bool syncVfxProperties = true;

    // Not serialized: rebuilt from the managed children in OnEnable so nothing
    // is persisted into the saved scene.
    private readonly List<GameObject> mirroredCopies = new List<GameObject>();

    // VFX property sync caches, rebuilt when the copy pool or the VFX asset changes
    private VisualEffect rootEffect;
    private readonly List<VisualEffect> copyEffects = new List<VisualEffect>();
    private readonly List<VFXExposedProperty> exposedProps = new List<VFXExposedProperty>();
    private VisualEffectAsset cachedAsset;

    void OnEnable()
    {
        AdoptExistingCopies();
    }

    void OnDestroy()
    {
        // Only tear down when this component is genuinely destroyed, not on the
        // OnDisable/OnEnable cycle that fires around editor recompiles.
        DestroyAllCopies();
    }

    void Update()
    {
        if (rootObject == null)
            return;

        // Guard against cloning ourselves into oblivion.
        if (rootObject == gameObject)
        {
            Debug.LogWarning($"{nameof(RadialMirror)}: rootObject must not be this GameObject.", this);
            return;
        }

        ReconcileCopyCount();
        SyncCopies();
        if (syncVfxProperties)
            SyncVfxProperties();
    }

    /// <summary>
    /// Copies every exposed property value from the root's VisualEffect to each copy's,
    /// so parameter changes (e.g. from canvas node inputs) affect all symmetric copies.
    /// </summary>
    void SyncVfxProperties()
    {
        if (rootEffect == null)
        {
            rootEffect = rootObject.GetComponentInChildren<VisualEffect>(true);
            if (rootEffect == null)
                return;
        }
        var asset = rootEffect.visualEffectAsset;
        if (asset == null)
            return;
        if (cachedAsset != asset)
        {
            cachedAsset = asset;
            exposedProps.Clear();
            asset.GetExposedProperties(exposedProps);
        }
        if (copyEffects.Count != mirroredCopies.Count)
        {
            copyEffects.Clear();
            foreach (var copy in mirroredCopies)
                copyEffects.Add(copy != null ? copy.GetComponentInChildren<VisualEffect>(true) : null);
        }

        foreach (var target in copyEffects)
        {
            if (target == null)
                continue;
            foreach (var prop in exposedProps)
            {
                var type = prop.type;
                string name = prop.name;
                if (type == typeof(float))
                {
                    if (rootEffect.HasFloat(name)) target.SetFloat(name, rootEffect.GetFloat(name));
                }
                else if (type == typeof(int))
                {
                    if (rootEffect.HasInt(name)) target.SetInt(name, rootEffect.GetInt(name));
                }
                else if (type == typeof(uint))
                {
                    if (rootEffect.HasUInt(name)) target.SetUInt(name, rootEffect.GetUInt(name));
                }
                else if (type == typeof(bool))
                {
                    if (rootEffect.HasBool(name)) target.SetBool(name, rootEffect.GetBool(name));
                }
                else if (type == typeof(Vector2))
                {
                    if (rootEffect.HasVector2(name)) target.SetVector2(name, rootEffect.GetVector2(name));
                }
                else if (type == typeof(Vector3))
                {
                    if (rootEffect.HasVector3(name)) target.SetVector3(name, rootEffect.GetVector3(name));
                }
                else if (type == typeof(Vector4))
                {
                    if (rootEffect.HasVector4(name)) target.SetVector4(name, rootEffect.GetVector4(name));
                }
                else if (typeof(Texture).IsAssignableFrom(type))
                {
                    if (rootEffect.HasTexture(name)) target.SetTexture(name, rootEffect.GetTexture(name));
                }
                // Gradients/curves/meshes are left alone: rarely canvas-driven, expensive to compare
            }
        }
    }

    /// <summary>Re-collects previously generated copies after a reload so we reuse them.</summary>
    void AdoptExistingCopies()
    {
        mirroredCopies.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i).gameObject;
            if (child == rootObject)
                continue;
            // Our copies are the DontSave children; anything else is left alone.
            if ((child.hideFlags & HideFlags.DontSave) != 0)
                mirroredCopies.Add(child);
        }
    }

    /// <summary>Grows or shrinks the copy pool to match symmetryCount - 1.</summary>
    void ReconcileCopyCount()
    {
        int desired = Mathf.Max(0, symmetryCount - 1);

        while (mirroredCopies.Count < desired)
        {
            var copy = Instantiate(rootObject, transform);
            copy.name = $"{rootObject.name} (Mirror {mirroredCopies.Count + 1})";
            copy.hideFlags = HideFlags.DontSave;
            mirroredCopies.Add(copy);
        }

        while (mirroredCopies.Count > desired)
        {
            int last = mirroredCopies.Count - 1;
            DestroyCopy(mirroredCopies[last]);
            mirroredCopies.RemoveAt(last);
        }
    }

    /// <summary>Positions each copy at its radial slot, matching the root's local transform.</summary>
    void SyncCopies()
    {
        Vector3 rotationAxis = axis.sqrMagnitude > Mathf.Epsilon ? axis.normalized : Vector3.forward;
        float step = 360f / symmetryCount;

        Vector3 rootPos = rootObject.transform.localPosition;
        Quaternion rootRot = rootObject.transform.localRotation;
        Vector3 rootScale = rootObject.transform.localScale;
        bool rootActive = rootObject.activeSelf;

        for (int i = 0; i < mirroredCopies.Count; i++)
        {
            var copy = mirroredCopies[i];
            if (copy == null)
                continue;

            // Slot 0 is the root itself, so copies start at slot 1.
            Quaternion turn = Quaternion.AngleAxis(step * (i + 1), rotationAxis);

            var t = copy.transform;
            t.SetLocalPositionAndRotation(turn * rootPos, turn * rootRot);
            t.localScale = rootScale;

            if (copy.activeSelf != rootActive)
                copy.SetActive(rootActive);
        }
    }

    void DestroyAllCopies()
    {
        foreach (var copy in mirroredCopies)
            DestroyCopy(copy);
        mirroredCopies.Clear();
    }

    void DestroyCopy(GameObject copy)
    {
        if (copy == null)
            return;

        if (Application.isPlaying)
            Destroy(copy);
        else
            DestroyImmediate(copy);
    }
}
