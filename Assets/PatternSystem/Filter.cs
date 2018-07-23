using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class Filter : MonoBehaviour {

    public List<ComputeShader> filterShaders;
    public ComputeShader outputShader;
    public RenderTexture[] textureBuffers;

    public bool yes;
    public Vector3 rgb;

    protected int kernelId = 0;

    private RenderTexture newRenderTexture()
    {
        RenderTexture tex = new RenderTexture(Constants.PIXELS_PER_STRIP + 1, Constants.NUM_STRIPS, 24);
        tex.enableRandomWrite = true;
        tex.Create();
        return tex;
    }

	// Use this for initialization
	void Start () {
        textureBuffers = new RenderTexture[] { newRenderTexture(), newRenderTexture() };

        for (int i = 0; i < filterShaders.Count; i++)
        {
            int input = i % 2 == 0 ? 0 : 1;
            int output = i % 2 == 0 ? 1 : 0;
            filterShaders[i].SetTexture(kernelId, "InputTex", textureBuffers[input]);
            filterShaders[i].SetTexture(kernelId, "OutputTex", textureBuffers[output]);
        }
	}
	
	// Update is called once per frame
	void Update () {
        int groupx_size = Constants.PIXELS_PER_STRIP + (8 - (Constants.PIXELS_PER_STRIP % 8));
        int groupy_size = Constants.NUM_STRIPS + (8 - (Constants.NUM_STRIPS % 8));
        foreach (ComputeShader shader in filterShaders)
        {
            shader.Dispatch(kernelId, groupx_size / 8, groupy_size / 8, 1);
        }
        if (outputShader != null)
        {
            outputShader.Dispatch(kernelId, groupx_size / 8, groupy_size / 8, 1);
        }
	}
}
