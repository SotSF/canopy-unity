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
        public Vector3 HSV;
    }
    public FilterParams filterParams;

    protected int kernelId = 0;
    public RenderTexture outputTexture;
    private ComputeBuffer paramsBuffer;

    private RenderTexture newRenderTexture()
    {
        RenderTexture tex = new RenderTexture(Constants.PIXELS_PER_STRIP + 1, Constants.NUM_STRIPS, 24);
        tex.enableRandomWrite = true;
        tex.Create();
        return tex;
    }

    // Use this for initialization
    void Start()
    {
        // Initialize the swapping rendertextures.
        textureBuffers = new RenderTexture[] { newRenderTexture(), newRenderTexture() };

        unsafe
        {
            paramsBuffer = new ComputeBuffer(1, sizeof(FilterParams));
        }
        paramsBuffer.SetData(new FilterParams[] { filterParams });

        // Set the input and output textures for each of the shaders, swapping each time.
        int count = filterShaders.Count;
        for (int i = 0; i < count; i++)
        {
            registerShader(filterShaders[i], i);
        }
        outputTexture = (filterShaders.Count - 1) % 2 == 0 ? textureBuffers[1] : textureBuffers[0];
    }

    void registerShader(ComputeShader shader)
    {
        filterShaders.Add(Instantiate(shader));
        registerShader(filterShaders[filterShaders.Count - 1]);
    }

    void registerShader(ComputeShader shader, int i)
    {
        int input = ((i % 2) == 0) ? 0 : 1;
        int output = ((i % 2) == 0) ? 1 : 0;

        filterShaders[i].SetTexture(kernelId, "InputTex", textureBuffers[input]);
        filterShaders[i].SetTexture(kernelId, "OutputTex", textureBuffers[output]);
        filterShaders[i].SetBuffer(kernelId, "Params", paramsBuffer);
    }

    public RenderTexture Apply(RenderTexture tex)
    {
        Graphics.Blit(tex, textureBuffers[0]);
        //filterShaders[0].SetTexture(0, "InputTex", textureBuffers[0]);
        RunShaders();
        return outputTexture;
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
