using NodeEditorFramework;
using SecretFire.TextureSynth;
using UnityEngine;
using NativeWebSocket;
using System;
using System.Collections.Generic;


[Node(false, "ShaderGraph/Test")]
public class ShaderGraphTestPatternNode : DynamicPatternNode
{
    public const string ID = "ShaderGraphTest";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "ShaderGraphTest"; } }
    //ShaderGraph graph;

    public Material graphMaterial;
    private Dictionary<string, Texture> lastTexInputs;
    //protected new CustomRenderTexture outputTex;
    public override void DoInit()
    {
        outputSize = new Vector2Int(256,256);
        Shader testShaderGraph = Shader.Find("Shader Graphs/TestShaderGraph");
        graphMaterial = new Material(testShaderGraph);
        InitializeRenderTexture();
        graphMaterial.SetTexture("_MainTexture", outputTex);
        inputPortNames = new List<string>();
        inputPortTypes = new List<Type>();
        lastTexInputs = new Dictionary<string, Texture>(); 
        var propTypes = new Dictionary<MaterialPropertyType, Type>(){
            { MaterialPropertyType.Float, typeof(float) },
            { MaterialPropertyType.Int, typeof(int) },
            { MaterialPropertyType.Texture, typeof(Texture) }
            //{ MaterialPropertyType.Vector, typeof(Vector4)
        };
        foreach (var (propType, cType) in propTypes)
        {
            var propNames = graphMaterial.GetPropertyNames(propType);
            foreach (var propName in propNames)
            {
                if (propName.StartsWith("_LW_"))
                {
                    inputPortNames.Add(propName);
                    inputPortTypes.Add(cType);

                    if (propType == MaterialPropertyType.Texture)
                    {
                        lastTexInputs[propName] = null;
                    }
                }
            }
        }
        SetSize();
    }
    protected new void InitializeRenderTexture()
    {
        if (outputTex != null)
        {
            outputTex.Release();
        }
        outputTex = new CustomRenderTexture(outputSize.x, outputSize.y);
        outputTex.enableRandomWrite = true;
        (outputTex as CustomRenderTexture).initializationMaterial = graphMaterial;
        (outputTex as CustomRenderTexture).initializationSource = CustomRenderTextureInitializationSource.Material;
        (outputTex as CustomRenderTexture).initializationMode = CustomRenderTextureUpdateMode.Realtime;
        outputTex.Create();
    }

    public override bool DoCalc()
    {
        for (int i = 0; i < dynamicConnectionPorts.Count; i++)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            var portType = port.valueType;
            if (portType == typeof(float))
            {
                float val = port.GetValue<float>();
                graphMaterial.SetFloat(inputPortNames[i], val);
            } 
            else if (portType == typeof(int))
            {
                int val = port.GetValue<int>();
                graphMaterial.SetInt(inputPortNames[i], val);
            }
            else if (portType == typeof(Texture))
            {
                Texture tex = port.GetValue<Texture>();
                if (lastTexInputs[inputPortNames[i]] != tex)
                {
                    graphMaterial.SetTexture(inputPortNames[i], tex);
                }
            }
        }
        graphMaterial.SetTexture("_MainTexture", outputTex);
        //Graphics.SetRenderTarget(outputTex);
        //GL.Clear(true, true, Color.clear);
        //RenderTexture.active = outputTex;
        //Graphics.Blit(null, outputTex, graphMaterial);
        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}
