using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;

namespace sotsf.canopy.patterns
{
    public class AnimatedPattern : Pattern
    {
        private VideoPlayer player;
        private RenderTexture inputFrame;

        protected override void Start()
        {
            base.Start();
            inputFrame = new RenderTexture(Constants.PIXELS_PER_STRIP, Constants.NUM_STRIPS, 24);
            inputFrame.enableRandomWrite = true;
            inputFrame.Create();
            player = GetComponent<VideoPlayer>();
            player.targetTexture = inputFrame;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.Play();
            var texparam = parameters.Where((p) => p.name == "InputTex").First();
            texparam.defaultTexture = inputFrame;
        }

        protected override void UpdateRenderParams()
        {
            base.UpdateRenderParams();
            patternShader.SetInt("height", inputFrame.height);
            patternShader.SetInt("width", inputFrame.width);
            patternShader.SetTexture(kernelId, "InputTex", inputFrame);
        }
    }
}
