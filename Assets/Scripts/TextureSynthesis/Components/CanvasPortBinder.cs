using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Exposes arbitrary MonoBehaviour state as canvas node input ports — the canvas-side
/// analogue of a VFX Property Binder. Attach an implementation anywhere in a VFX rig's
/// hierarchy and VFXInstanceNode will surface its declared ports alongside the
/// VisualEffect's exposed properties, routing values back through SetPortValue.
/// Generic by design: usable for any GameObject-based setup (SpaceshipGame etc.)
/// wherever a VFXInstance-style host enumerates binders.
/// </summary>
public abstract class CanvasPortBinder : MonoBehaviour
{
    public struct PortDef
    {
        public string name;
        public Type type;

        public PortDef(string name, Type type)
        {
            this.name = name;
            this.type = type;
        }
    }

    [Tooltip("Optional prefix for this binder's port names on the canvas node (avoids collisions " +
             "when multiple binders or VFX properties share names). Empty = no prefix.")]
    public string portPrefix = "";

    public string EffectivePrefix => string.IsNullOrEmpty(portPrefix) ? "" : portPrefix + ".";

    /// <summary>Appends this binder's port declarations (unprefixed names).</summary>
    public abstract void GetPorts(List<PortDef> ports);

    /// <summary>Receives a value from the canvas for the given (unprefixed) port name.</summary>
    public abstract void SetPortValue(string portName, object value);

    /// <summary>Optionally reports the current value for display (unprefixed name).</summary>
    public virtual bool TryGetPortValue(string portName, out object value)
    {
        value = null;
        return false;
    }
}
