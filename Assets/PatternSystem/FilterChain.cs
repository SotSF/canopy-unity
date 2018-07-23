using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class FilterChain : MonoBehaviour {

    public List<ComputeShader> filterShaders;
    public RenderTexture[] textureBuffers;

    [Serializable]
    public struct FilterParams
    {
        public Vector3 multiply;
    }
    public FilterParams filterParams;

    protected int kernelId = 0;
    public RenderTexture outputTexture;

    private RenderTexture newRenderTexture()
    {
        RenderTexture tex = new RenderTexture(Constants.PIXELS_PER_STRIP + 1, Constants.NUM_STRIPS, 24);
        tex.enableRandomWrite = true;
        tex.Create();
        return tex;
    }

	// Use this for initialization
	void Start () {
        // Initialize the swapping rendertextures.
        textureBuffers = new RenderTexture[] { newRenderTexture(), newRenderTexture() };

        ComputeBuffer paramsBuffer;
        unsafe
        {
            paramsBuffer = new ComputeBuffer(1, sizeof(FilterParams));
        }
        paramsBuffer.SetData(new FilterParams[] { filterParams });

        // Set the input and output textures for each of the shaders, swapping each time.
        for (int i = 0; i < filterShaders.Count; i++)
        {
            int input = ((i % 2) == 0) ? 0 : 1;
            int output = ((i % 2) == 0) ? 1 : 0;

            // Instantiate another instance of the passed in shader so the memory isn't shared.
            // https://forum.unity.com/threads/multiple-instances-of-same-compute-shader-is-it-possible.506961/
            filterShaders[i] = Instantiate(filterShaders[i]);

            filterShaders[i].SetTexture(kernelId, "InputTex", textureBuffers[input]);
            filterShaders[i].SetTexture(kernelId, "OutputTex", textureBuffers[output]);

            filterShaders[i].SetBuffer(kernelId, "Params", paramsBuffer);
        }

        // Set the pattern texture to the output texture of the last output texture.
        outputTexture = (filterShaders.Count - 1) % 2 == 0 ? textureBuffers[1] : textureBuffers[0];

        //// Apply the texture to a material and apply that material to the mesh renderer.
        //Material patternMaterial = new Material(Shader.Find("PatternDisplayShaderGraph"));
        //patternMaterial.name = gameObject.name + "_filterchain";
        //foreach (string tex in patternMaterial.GetTexturePropertyNames())
        //{
        //    patternMaterial.SetTexture(tex, patternTexture);
        //}
        //GetComponent<MeshRenderer>().sharedMaterial = patternMaterial;
	}

    public void Apply(RenderTexture tex)
    {
        Graphics.Blit(tex, textureBuffers[0]);
        //filterShaders[0].SetTexture(0, "InputTex", textureBuffers[0]);
        RunShaders();
    }

    // Update is called once per frame
    void RunShaders () {
        int groupx_size = Constants.PIXELS_PER_STRIP + (8 - (Constants.PIXELS_PER_STRIP % 8));
        int groupy_size = Constants.NUM_STRIPS + (8 - (Constants.NUM_STRIPS % 8));
        foreach (ComputeShader shader in filterShaders)
        {
            shader.Dispatch(kernelId, groupx_size / 8, groupy_size / 8, 1);
        }
	}
}
