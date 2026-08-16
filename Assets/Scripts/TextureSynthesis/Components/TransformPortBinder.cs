using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Exposes a Transform's position/rotation/scale as Vector3 canvas ports, so node
/// signals (e.g. from VectorCombine) can drive object placement — for example the
/// MirrorRingRoot, whose transform RadialMirror then propagates to symmetric copies.
/// </summary>
public class TransformPortBinder : CanvasPortBinder
{
    [Tooltip("Transform to drive. Empty = this object's transform.")]
    public Transform target;

    public bool exposePosition = true;
    public bool exposeRotation = false;
    public bool exposeScale = false;

    [Tooltip("Apply position/rotation in local space (relative to the parent) rather than world space.")]
    public bool useLocalSpace = true;

    Transform Target => target != null ? target : transform;

    public override void GetPorts(List<PortDef> ports)
    {
        if (exposePosition) ports.Add(new PortDef("position", typeof(Vector3)));
        if (exposeRotation) ports.Add(new PortDef("rotation", typeof(Vector3)));
        if (exposeScale) ports.Add(new PortDef("scale", typeof(Vector3)));
    }

    public override void SetPortValue(string portName, object value)
    {
        if (!(value is Vector3 v)) return;
        var t = Target;
        switch (portName)
        {
            case "position":
                if (useLocalSpace) t.localPosition = v;
                else t.position = v;
                break;
            case "rotation":
                if (useLocalSpace) t.localEulerAngles = v;
                else t.eulerAngles = v;
                break;
            case "scale":
                t.localScale = v;
                break;
        }
    }

    public override bool TryGetPortValue(string portName, out object value)
    {
        var t = Target;
        switch (portName)
        {
            case "position": value = useLocalSpace ? t.localPosition : t.position; return true;
            case "rotation": value = useLocalSpace ? t.localEulerAngles : t.eulerAngles; return true;
            case "scale": value = t.localScale; return true;
        }
        value = null;
        return false;
    }
}
