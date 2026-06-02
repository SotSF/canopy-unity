using UnityEngine;

namespace SecretFire.TextureSynth
{
    public static class SparklineRenderer
    {
        const string ShaderPath = "NodeShaders/Sparkline";

        static readonly int _SamplesId      = Shader.PropertyToID("_Samples");
        static readonly int _HeadId         = Shader.PropertyToID("_Head");
        static readonly int _CountId        = Shader.PropertyToID("_Count");
        static readonly int _CapacityId     = Shader.PropertyToID("_Capacity");
        static readonly int _YRangeId       = Shader.PropertyToID("_YRange");
        static readonly int _LineColorId    = Shader.PropertyToID("_LineColor");
        static readonly int _BgColorId      = Shader.PropertyToID("_BgColor");
        static readonly int _ZeroLineColor  = Shader.PropertyToID("_ZeroLineColor");
        static readonly int _LineThickness  = Shader.PropertyToID("_LineThickness");
        static readonly int _TargetSize     = Shader.PropertyToID("_TargetSize");
        static readonly int _PixelScale     = Shader.PropertyToID("_PixelScale");

        public static Material CreateMaterial()
        {
            var shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"Sparkline shader not found at Resources/{ShaderPath}.shader");
                return null;
            }
            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public static void Render(
            Material mat,
            RenderTexture target,
            ComputeBuffer samples,
            int head,
            int count,
            int capacity,
            Vector2 yRange,
            Color lineColor,
            Color bgColor,
            Color zeroLineColor,
            float lineThickness = 1.5f,
            float pixelScale    = 1f)
        {
            if (mat == null || target == null || samples == null) return;

            mat.SetBuffer(_SamplesId, samples);
            mat.SetInt(_HeadId, head);
            mat.SetInt(_CountId, count);
            mat.SetInt(_CapacityId, capacity);
            mat.SetVector(_YRangeId, new Vector4(yRange.x, yRange.y, 0, 0));
            mat.SetColor(_LineColorId, lineColor);
            mat.SetColor(_BgColorId, bgColor);
            mat.SetColor(_ZeroLineColor, zeroLineColor);
            mat.SetFloat(_LineThickness, lineThickness);
            mat.SetFloat(_PixelScale, pixelScale);
            mat.SetVector(_TargetSize, new Vector4(target.width, target.height, 0, 0));

            Graphics.Blit(null, target, mat);
        }
    }
}
