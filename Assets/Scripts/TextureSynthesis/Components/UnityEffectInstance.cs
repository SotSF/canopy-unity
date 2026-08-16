using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Root component for plain GameObject-based visual prefabs (meshes, sprites, trails,
/// line renderers...) registered with VFXRegistry. Canvas-facing ports are the public
/// material properties of the listed child renderers plus any CanvasPortBinder
/// components in the hierarchy.
///
/// Property exposure per material: shaders with any "_LW_"-prefixed properties (the
/// ShaderGraph convention) expose only those, with the prefix stripped from the port
/// name; all other shaders expose exactly what the material inspector shows — every
/// property in the shader's Properties block not flagged HideInInspector /
/// PerRendererData / NonModifiableTextureData. Unity's internal plumbing (lightmaps,
/// shadow textures, blend-mode state, ...) is either global shader state that never
/// appears in the property table, or is flagged HideInInspector, so it stays out.
/// </summary>
public class UnityEffectInstance : CameraEffectInstance
{
    [Tooltip("Child renderers whose material properties become canvas ports. Empty = every renderer in the hierarchy.")]
    public List<Renderer> exposedRenderers = new List<Renderer>();

    private struct MaterialPort
    {
        public Material material;
        public string propName;
    }

    [NonSerialized] private readonly List<CanvasPort> materialPortList = new List<CanvasPort>();
    [NonSerialized] private readonly Dictionary<string, MaterialPort> materialPorts = new Dictionary<string, MaterialPort>();
    // renderer.materials instantiates private copies; we own them and must Destroy them
    [NonSerialized] private readonly List<Material> instancedMaterials = new List<Material>();

    protected override void OnInitialized()
    {
        BuildMaterialPorts();
    }

    protected override void CollectInstancePorts(List<CanvasPort> ports)
    {
        ports.AddRange(materialPortList);
    }

    private void BuildMaterialPorts()
    {
        materialPortList.Clear();
        materialPorts.Clear();
        IEnumerable<Renderer> renderers = exposedRenderers != null && exposedRenderers.Count > 0
            ? (IEnumerable<Renderer>)exposedRenderers
            : GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            var mats = renderer.materials;
            instancedMaterials.AddRange(mats);
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null || mats[m].shader == null) continue;
                string prefix = mats.Length > 1 ? $"{renderer.name}.{m}." : $"{renderer.name}.";
                AddMaterialPorts(mats[m], prefix);
            }
        }
    }

    private void AddMaterialPorts(Material mat, string prefix)
    {
        var shader = mat.shader;
        int propCount = shader.GetPropertyCount();
        bool lwConvention = false;
        for (int i = 0; i < propCount; i++)
        {
            if (shader.GetPropertyName(i).StartsWith(ShaderGraphInstance.ExposedPrefix))
            {
                lwConvention = true;
                break;
            }
        }
        for (int i = 0; i < propCount; i++)
        {
            string propName = shader.GetPropertyName(i);
            if (lwConvention)
            {
                if (!propName.StartsWith(ShaderGraphInstance.ExposedPrefix)) continue;
            }
            else
            {
                const ShaderPropertyFlags hidden = ShaderPropertyFlags.HideInInspector
                    | ShaderPropertyFlags.PerRendererData
                    | ShaderPropertyFlags.NonModifiableTextureData;
                if ((shader.GetPropertyFlags(i) & hidden) != 0) continue;
            }
            Type portType;
            switch (shader.GetPropertyType(i))
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    portType = typeof(float);
                    break;
                case ShaderPropertyType.Int:
                    portType = typeof(int);
                    break;
                case ShaderPropertyType.Color:
                    portType = typeof(Color);
                    break;
                case ShaderPropertyType.Vector:
                    portType = typeof(Vector4);
                    break;
                case ShaderPropertyType.Texture:
                    portType = typeof(Texture);
                    break;
                default:
                    continue;
            }
            string displayName = lwConvention
                ? propName.Substring(ShaderGraphInstance.ExposedPrefix.Length)
                : propName;
            // Same-named sibling renderers (or repeated props) get a numeric suffix
            string portName = prefix + displayName;
            int n = 1;
            while (materialPorts.ContainsKey(portName))
            {
                portName = $"{prefix}{displayName}#{++n}";
            }
            materialPorts[portName] = new MaterialPort { material = mat, propName = propName };
            materialPortList.Add(new CanvasPort { name = portName, type = portType });
        }
    }

    /// <summary>Routes a canvas value to the material property behind the port. False if the port isn't a material port.</summary>
    public bool TrySetMaterialPort(string portName, object value)
    {
        if (!materialPorts.TryGetValue(portName, out var port) || port.material == null)
        {
            return false;
        }
        switch (value)
        {
            case float f: port.material.SetFloat(port.propName, f); return true;
            case int i: port.material.SetInteger(port.propName, i); return true;
            case Color c: port.material.SetColor(port.propName, c); return true;
            case Vector4 v4: port.material.SetVector(port.propName, v4); return true;
            case Vector3 v3: port.material.SetVector(port.propName, v3); return true;
            case Vector2 v2: port.material.SetVector(port.propName, v2); return true;
            case Texture t: port.material.SetTexture(port.propName, t); return true;
            default: return false;
        }
    }

    /// <summary>Current float value behind a material port, for node GUI display.</summary>
    public bool TryGetMaterialFloat(string portName, out float value)
    {
        value = 0f;
        if (!materialPorts.TryGetValue(portName, out var port) || port.material == null
            || !port.material.HasFloat(port.propName))
        {
            return false;
        }
        value = port.material.GetFloat(port.propName);
        return true;
    }

    protected override void OnDestroy()
    {
        foreach (var mat in instancedMaterials)
        {
            if (mat != null) Destroy(mat);
        }
        instancedMaterials.Clear();
        base.OnDestroy();
    }
}
