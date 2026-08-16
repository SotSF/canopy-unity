using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Root component for VFX prefabs registered with VFXRegistry. Owns the VisualEffect,
/// its isolation camera, and a per-instance render texture. Canvas-facing ports are the
/// VisualEffect's exposed properties plus any CanvasPortBinder components in the hierarchy.
/// </summary>
public class VFXInstance : CameraEffectInstance
{
    [Tooltip("Visual effect driven by this instance. Auto-discovered in children if left unset.")]
    public VisualEffect effect;

    public struct CanvasPort
    {
        public string name;
        public Type type;
    }

    [NonSerialized] private Dictionary<string, CanvasPortBinder> binderPorts;

    protected override void OnInitialized()
    {
        if (effect == null)
        {
            effect = GetComponentInChildren<VisualEffect>(true);
        }
        if (effect == null)
        {
            Debug.LogError($"VFXInstance '{name}' has no VisualEffect in its hierarchy.");
            return;
        }
        effect.gameObject.SetActive(true);
        effect.Play();
    }

    public List<VFXExposedProperty> GetExposedProperties()
    {
        var props = new List<VFXExposedProperty>();
        if (effect != null && effect.visualEffectAsset != null)
        {
            effect.visualEffectAsset.GetExposedProperties(props);
        }
        return props;
    }

    /// <summary>
    /// All canvas-facing ports: the VFX's exposed properties plus every CanvasPortBinder's
    /// declared ports (prefixed). Also rebuilds the binder routing table used by
    /// TrySetBinderPort, so call this when binding a node to the instance.
    /// </summary>
    public void GetCanvasPorts(List<CanvasPort> ports)
    {
        ports.Clear();
        if (binderPorts == null) binderPorts = new Dictionary<string, CanvasPortBinder>();
        binderPorts.Clear();

        foreach (var prop in GetExposedProperties())
        {
            ports.Add(new CanvasPort { name = prop.name, type = prop.type });
        }

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
}
