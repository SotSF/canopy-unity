using System.Collections.Generic;
using UnityEngine;

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

    // Not serialized: rebuilt from the managed children in OnEnable so nothing
    // is persisted into the saved scene.
    private readonly List<GameObject> mirroredCopies = new List<GameObject>();

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
