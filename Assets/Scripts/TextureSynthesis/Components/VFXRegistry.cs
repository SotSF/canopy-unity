using System.Collections.Generic;
using System.Linq;
using NodeEditorFramework;
using UnityEngine;

/// <summary>
/// Scene singleton that instantiates registered effect prefabs (rooted in a
/// CameraEffectInstance) on demand, laying them out on a growing 2D grid so each
/// instance renders in isolation with its own camera and render texture.
/// Prefabs come from the inspector list plus any prefabs found in a Resources
/// subfolder (default "VFXPrefabs").
/// </summary>
public class VFXRegistry : Singleton<VFXRegistry>
{
    [Tooltip("Effect prefabs available for instantiation. Merged with prefabs discovered in the Resources folder below.")]
    public List<CameraEffectInstance> effectPrefabs = new List<CameraEffectInstance>();

    [Tooltip("Resources subfolder scanned for effect prefabs (root GameObject must have a CameraEffectInstance).")]
    public string prefabResourcePath = "VFXPrefabs";

    [Tooltip("World-space size of each effect's grid cell, in meters.")]
    public float cellSize = 20f;

    [Tooltip("World position of grid cell (0,0). Keep it away from other scene content.")]
    public Vector3 gridOrigin = new Vector3(0, 1000, 0);

    [Tooltip("On canvas load, release instances whose owning node is not part of the new canvas.")]
    public bool releaseOrphansOnCanvasLoad = true;

    private Dictionary<string, CameraEffectInstance> prefabsByName;
    private readonly Dictionary<Node, CameraEffectInstance> instancesByOwner = new Dictionary<Node, CameraEffectInstance>();
    private readonly Dictionary<CameraEffectInstance, int> slotsByInstance = new Dictionary<CameraEffectInstance, int>();
    private readonly Stack<int> freeSlots = new Stack<int>();
    private int nextSlot = 0;

    protected override void OnAwake()
    {
        RefreshPrefabs();
        NodeEditorCallbacks.OnLoadCanvas += HandleCanvasLoaded;
    }

    private void OnDestroy()
    {
        NodeEditorCallbacks.OnLoadCanvas -= HandleCanvasLoaded;
    }

    /// <summary>
    /// Rebuilds the name → prefab lookup from the inspector list and the Resources folder.
    /// Inspector entries win on name collisions.
    /// </summary>
    public void RefreshPrefabs()
    {
        prefabsByName = new Dictionary<string, CameraEffectInstance>();
        foreach (var prefab in effectPrefabs)
        {
            if (prefab != null)
            {
                prefabsByName[prefab.name] = prefab;
            }
        }
        foreach (var go in Resources.LoadAll<GameObject>(prefabResourcePath))
        {
            var effectInstance = go.GetComponent<CameraEffectInstance>();
            if (effectInstance != null && !prefabsByName.ContainsKey(go.name))
            {
                prefabsByName[go.name] = effectInstance;
            }
        }
    }

    public string[] EffectNames
    {
        get
        {
            if (prefabsByName == null) RefreshPrefabs();
            return prefabsByName.Keys.OrderBy(n => n).ToArray();
        }
    }

    /// <summary>
    /// Instantiates the named effect prefab in the next free grid cell and initializes it.
    /// If an owner node is given, the instance is tracked so it can be released when the
    /// node is deleted or its canvas is unloaded; a node re-binding replaces its old instance.
    /// Returns null if no prefab with that name is registered.
    /// </summary>
    public CameraEffectInstance CreateInstance(Node owner, string effectName, Vector2Int? textureSize = null)
    {
        if (prefabsByName == null) RefreshPrefabs();
        if (!prefabsByName.TryGetValue(effectName, out var prefab))
        {
            Debug.LogError($"VFXRegistry: no effect prefab named '{effectName}' is registered.");
            return null;
        }
        if (owner != null && instancesByOwner.ContainsKey(owner))
        {
            ReleaseInstance(owner);
        }
        int slot = freeSlots.Count > 0 ? freeSlots.Pop() : nextSlot++;
        var cell = SlotCoordinates(slot);
        var position = gridOrigin + new Vector3(cell.x, cell.y, 0) * cellSize;
        var instance = Instantiate(prefab, position, Quaternion.identity, transform);
        instance.name = $"{effectName} [slot {slot}]";
        instance.Initialize(textureSize);
        slotsByInstance[instance] = slot;
        if (owner != null)
        {
            instancesByOwner[owner] = instance;
        }
        return instance;
    }

    public void ReleaseInstance(Node owner)
    {
        if (owner == null || !instancesByOwner.TryGetValue(owner, out var instance)) return;
        instancesByOwner.Remove(owner);
        ReleaseInstance(instance);
    }

    public void ReleaseInstance(CameraEffectInstance instance)
    {
        if (instance == null) return;
        if (slotsByInstance.TryGetValue(instance, out int slot))
        {
            freeSlots.Push(slot);
            slotsByInstance.Remove(instance);
        }
        Destroy(instance.gameObject);
    }

    // When a canvas is loaded, nodes from the previous canvas are replaced wholesale and
    // will never release their instances themselves, so reclaim any instance whose owner
    // is not part of the newly loaded canvas.
    private void HandleCanvasLoaded(NodeCanvas canvas)
    {
        if (!releaseOrphansOnCanvasLoad || canvas == null) return;
        var orphans = instancesByOwner.Keys
            .Where(node => node == null || !canvas.nodes.Contains(node))
            .ToList();
        foreach (var node in orphans)
        {
            ReleaseInstance(node);
        }
    }

    // Maps a slot index to 2D grid coordinates in expanding square shells
    // ((0,0), (1,0), (1,1), (0,1), (2,0), ...) so the occupied area grows
    // uniformly in X and Y rather than marching off along one axis.
    private static Vector2Int SlotCoordinates(int index)
    {
        int shell = Mathf.FloorToInt(Mathf.Sqrt(index));
        int rem = index - shell * shell;
        return rem <= shell ? new Vector2Int(shell, rem) : new Vector2Int(2 * shell - rem, shell);
    }
}
