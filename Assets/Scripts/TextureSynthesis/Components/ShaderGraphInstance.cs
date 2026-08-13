using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A ShaderGraph effect instance: a private material copy rendering into its own
/// CustomRenderTexture every frame (Realtime initialization mode), so multiple
/// instances of the same graph can run with independent parameters. No GameObject
/// or camera infrastructure required. Created and tracked by ShaderGraphRegistry.
/// Only shader properties following the "_LW_" prefix convention are exposed.
/// </summary>
public class ShaderGraphInstance
{
    public const string ExposedPrefix = "_LW_";

    public Material material;
    public CustomRenderTexture OutputTexture { get; private set; }

    public ShaderGraphInstance(Shader shader, Vector2Int size)
    {
        material = new Material(shader);
        OutputTexture = new CustomRenderTexture(size.x, size.y);
        OutputTexture.enableRandomWrite = true;
        OutputTexture.initializationMaterial = material;
        OutputTexture.initializationSource = CustomRenderTextureInitializationSource.Material;
        OutputTexture.initializationMode = CustomRenderTextureUpdateMode.Realtime;
        OutputTexture.Create();
    }

    /// <summary>
    /// Exposed (reference name, port type) pairs: shader properties prefixed "_LW_",
    /// mapped to canvas signal types. Vector properties are skipped — no Vector
    /// signal source exists on the canvas yet.
    /// </summary>
    public List<(string name, Type type)> GetExposedProperties()
    {
        var props = new List<(string, Type)>();
        if (material == null || material.shader == null) return props;
        var shader = material.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            var propName = shader.GetPropertyName(i);
            if (!propName.StartsWith(ExposedPrefix)) continue;
            switch (shader.GetPropertyType(i))
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    props.Add((propName, typeof(float)));
                    break;
                case ShaderPropertyType.Int:
                    props.Add((propName, typeof(int)));
                    break;
                case ShaderPropertyType.Color:
                    props.Add((propName, typeof(Color)));
                    break;
                case ShaderPropertyType.Texture:
                    props.Add((propName, typeof(Texture)));
                    break;
                default:
                    Debug.Log($"ShaderGraphInstance: skipping exposed property '{propName}' of unsupported type {shader.GetPropertyType(i)}.");
                    break;
            }
        }
        return props;
    }

    public void Release()
    {
        if (OutputTexture != null)
        {
            OutputTexture.Release();
            UnityEngine.Object.Destroy(OutputTexture);
            OutputTexture = null;
        }
        if (material != null)
        {
            UnityEngine.Object.Destroy(material);
            material = null;
        }
    }
}
