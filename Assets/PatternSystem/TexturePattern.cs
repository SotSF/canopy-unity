using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TexturePattern : ControllerPattern {

    public Texture tex;
    
    protected override void UpdateRenderParams()
    {
        base.UpdateRenderParams();
        patternShader.SetTexture(kernelId, "InputTex", tex);
    }
}
