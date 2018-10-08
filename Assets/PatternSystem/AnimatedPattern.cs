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
        public RectTransform nextButton;

        VideoClip[] animatedTextures;
        int clipIndex = 0;

        protected override void Start()
        {
            base.Start();
            inputFrame = new RenderTexture(Constants.PIXELS_PER_STRIP, Constants.NUM_STRIPS, 24);
            inputFrame.enableRandomWrite = true;
            inputFrame.Create();
            animatedTextures = Resources.LoadAll<VideoClip>("AnimatedTextures");
            player = GetComponent<VideoPlayer>();
            Next();
            var texparam = parameters.Where((p) => p.name == "InputTex").First();
            texparam.defaultTexture = inputFrame;
        }

        public void Next()
        {
            clipIndex = (clipIndex + 1) % animatedTextures.Length;
            player.clip = animatedTextures[clipIndex];
            player.targetTexture = inputFrame;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.Play();
        }
    }
}
