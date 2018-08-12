using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace sotsf.canopy.patterns
{
    public class GameOfLifePattern : Pattern
    {

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
            //25 and 16 are the thread group sizes, which evenly divide 75 and 96
            swapShader.Dispatch(kernelId, Constants.PIXELS_PER_STRIP / 25, Constants.NUM_STRIPS / 16, 1);
        }
    }
}
