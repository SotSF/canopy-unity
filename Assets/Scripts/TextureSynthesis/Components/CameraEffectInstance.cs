using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base component for prefab-rooted effects that render in isolation: a dedicated camera
/// under the prefab root renders the effect into a per-instance RenderTexture, so multiple
/// instances of the same effect can run with independent parameters. VFX graphs use
/// VFXInstance; plain GameObject/renderer rigs use UnityEffectInstance.
/// </summary>
public abstract class CameraEffectInstance : MonoBehaviour
{
    public struct CanvasPort
    {
        public string name;
        public Type type;
    }

    [NonSerialized] private Dictionary<string, CanvasPortBinder> binderPorts;

    [Tooltip("Camera rendering this effect. Auto-discovered in children if left unset.")]
    public Camera cam;

    [Tooltip("Render texture size allocated per instance unless the creator overrides it.")]
    public Vector2Int defaultTextureSize = new Vector2Int(256, 256);

    public RenderTexture OutputTexture { get; private set; }

    /// <summary>
    /// Binds components and allocates this instance's render target.
    /// Called by VFXRegistry after instantiation.
    /// </summary>
    public void Initialize(Vector2Int? textureSize = null)
    {
        if (cam == null)
        {
            cam = GetComponentInChildren<Camera>(true);
        }
        if (cam == null)
        {
            Debug.LogError($"CameraEffectInstance '{name}' has no Camera in its hierarchy.");
            return;
        }
        AllocateRenderTexture(textureSize ?? defaultTextureSize);
        cam.gameObject.SetActive(true);
        OnInitialized();
    }

    public RenderTexture AllocateRenderTexture(Vector2Int size)
    {
        ReleaseRenderTexture();
        OutputTexture = new RenderTexture(size.x, size.y, 24);
        OutputTexture.Create();
        if (cam != null)
        {
            cam.targetTexture = OutputTexture;
        }
        return OutputTexture;
    }

    protected void ReleaseRenderTexture()
    {
        if (OutputTexture == null) return;
        if (cam != null && cam.targetTexture == OutputTexture)
        {
            cam.targetTexture = null;
        }
        OutputTexture.Release();
        Destroy(OutputTexture);
        OutputTexture = null;
    }

    /// <summary>Effect-type-specific setup, called once camera and render target are ready.</summary>
    protected virtual void OnInitialized() { }

    /// <summary>
    /// All canvas-facing ports: the effect type's own ports (VFX exposed properties,
    /// material properties, ...) plus every CanvasPortBinder's declared ports (prefixed).
    /// Also rebuilds the binder routing table used by TrySetBinderPort, so call this
    /// when binding a node to the instance.
    /// </summary>
    public void GetCanvasPorts(List<CanvasPort> ports)
    {
        ports.Clear();
        if (binderPorts == null) binderPorts = new Dictionary<string, CanvasPortBinder>();
        binderPorts.Clear();

        CollectInstancePorts(ports);

        var defs = new List<CanvasPortBinder.PortDef>();
        foreach (var binder in GetComponentsInChildren<CanvasPortBinder>(true))
        {
            defs.Clear();
            binder.GetPorts(defs);
            foreach (var def in defs)
            {
                string fullName = binder.EffectivePrefix + def.name;
                binderPorts[fullName] = binder;
                ports.Add(new CanvasPort { name = fullName, type = def.type });
            }
        }
    }

    /// <summary>Appends the effect type's own ports; binder ports are appended after these.</summary>
    protected virtual void CollectInstancePorts(List<CanvasPort> ports) { }

    /// <summary>Routes a canvas value to the binder owning the port. False if no binder claims it.</summary>
    public bool TrySetBinderPort(string portName, object value)
    {
        if (binderPorts == null || !binderPorts.TryGetValue(portName, out var binder) || binder == null)
        {
            return false;
        }
        binder.SetPortValue(portName.Substring(binder.EffectivePrefix.Length), value);
        return true;
    }

    protected virtual void OnDestroy()
    {
        ReleaseRenderTexture();
    }
}
