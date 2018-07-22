using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameOfLifePattern : Pattern {

    public Texture tex;
    private RenderTexture renderTexture;
    public ComputeShader swapShader;

    protected override void Start()
    {
        base.Start();
        //renderTexture = new RenderTexture(tex);
        //RenderTexture.crea
        renderTexture = new RenderTexture(Constants.PIXELS_PER_STRIP, Constants.NUM_STRIPS, 24);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();

        Graphics.Blit(tex, renderTexture);

        patternShader.SetTexture(kernelId, "InputTex", renderTexture);
        patternShader.SetTexture(kernelId, "Frame", patternTexture);
        swapShader.SetTexture(kernelId, "InputTex", patternTexture);
        swapShader.SetTexture(kernelId, "Frame", renderTexture);
    }

    protected override void UpdateRenderParams()
    {
        base.UpdateRenderParams();
    }

    protected override void Update()
    {
        base.Update();
        int groupx_size = Constants.PIXELS_PER_STRIP + (8 - (Constants.PIXELS_PER_STRIP % 8));
        int groupy_size = Constants.NUM_STRIPS + (8 - (Constants.NUM_STRIPS % 8));
        swapShader.Dispatch(kernelId, groupx_size / 8, groupy_size / 8, 1);
    }
}
