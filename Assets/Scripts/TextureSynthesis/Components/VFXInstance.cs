using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Root component for VFX prefabs registered with VFXRegistry. Owns the VisualEffect,
/// its isolation camera, and a per-instance render texture.
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
}
