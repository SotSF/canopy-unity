using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace sotsf.canopy.patterns
{
    public class TexturePattern : ControllerPattern
    {

        public Texture tex;

        protected override void UpdateRenderParams()
        {
            base.UpdateRenderParams();
            patternShader.SetInt("height", tex.height);
            patternShader.SetInt("width", tex.width);
            patternShader.SetTexture(kernelId, "InputTex", tex);
        }
    }
}