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

    /// <summary>The VFX's exposed properties, surfaced as canvas ports.</summary>
    protected override void CollectInstancePorts(List<CanvasPort> ports)
    {
        foreach (var prop in GetExposedProperties())
        {
            ports.Add(new CanvasPort { name = prop.name, type = prop.type });
        }
    }
}
