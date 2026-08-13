using System.Collections.Generic;
using System.Linq;
using NodeEditorFramework;
using UnityEngine;

/// <summary>
/// Scene singleton that creates ShaderGraphInstances (material copy + per-instance
/// CustomRenderTexture) on demand — the ShaderGraph counterpart to VFXRegistry, minus
/// the GameObjects: shaders render straight to texture, so no grid layout is needed.
/// Shaders come from the inspector list plus any shader assets (including .shadergraph)
/// found in a Resources subfolder (default "ShaderGraphs").
/// </summary>
public class ShaderGraphRegistry : Singleton<ShaderGraphRegistry>
{
    [Tooltip("ShaderGraph shaders available for instantiation. Merged with shaders discovered in the Resources folder below.")]
    public List<Shader> shaderGraphs = new List<Shader>();

    [Tooltip("Resources subfolder scanned for shader assets.")]
    public string shaderResourcePath = "ShaderGraphs";

    [Tooltip("Render texture size allocated per instance unless the creator overrides it.")]
    public Vector2Int defaultTextureSize = new Vector2Int(256, 256);

    [Tooltip("On canvas load, release instances whose owning node is not part of the new canvas.")]
    public bool releaseOrphansOnCanvasLoad = true;

    private Dictionary<string, Shader> shadersByName;
    private readonly Dictionary<Node, ShaderGraphInstance> instancesByOwner = new Dictionary<Node, ShaderGraphInstance>();

    protected override void OnAwake()
    {
        RefreshShaders();
        NodeEditorCallbacks.OnLoadCanvas += HandleCanvasLoaded;
    }

    private void OnDestroy()
    {
        NodeEditorCallbacks.OnLoadCanvas -= HandleCanvasLoaded;
    }

    /// <summary>
    /// Rebuilds the name → shader lookup from the inspector list and the Resources folder.
    /// Names are the last segment of the shader path ("Shader Graphs/Foo" → "Foo");
    /// inspector entries win on collisions.
    /// </summary>
    public void RefreshShaders()
    {
        shadersByName = new Dictionary<string, Shader>();
        foreach (var shader in shaderGraphs)
        {
            if (shader != null)
            {
                shadersByName[ShortName(shader)] = shader;
            }
        }
        foreach (var shader in Resources.LoadAll<Shader>(shaderResourcePath))
        {
            if (!shadersByName.ContainsKey(ShortName(shader)))
            {
                shadersByName[ShortName(shader)] = shader;
            }
        }
    }

    private static string ShortName(Shader shader)
    {
        var name = shader.name;
        int slash = name.LastIndexOf('/');
        return slash >= 0 ? name.Substring(slash + 1) : name;
    }

    public string[] EffectNames
    {
        get
        {
            if (shadersByName == null) RefreshShaders();
            return shadersByName.Keys.OrderBy(n => n).ToArray();
        }
    }

    /// <summary>
    /// Creates a new instance of the named shader graph with its own material and
    /// render texture. If an owner node is given, the instance is tracked so it can
    /// be released when the node is deleted or its canvas is unloaded; a node
    /// re-binding replaces its old instance. Returns null if the name is unknown.
    /// </summary>
    public ShaderGraphInstance CreateInstance(Node owner, string shaderName, Vector2Int? textureSize = null)
    {
        if (shadersByName == null) RefreshShaders();
        if (!shadersByName.TryGetValue(shaderName, out var shader))
        {
            Debug.LogError($"ShaderGraphRegistry: no shader graph named '{shaderName}' is registered.");
            return null;
        }
        if (owner != null && instancesByOwner.ContainsKey(owner))
        {
            ReleaseInstance(owner);
        }
        var instance = new ShaderGraphInstance(shader, textureSize ?? defaultTextureSize);
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
        instance.Release();
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
}
