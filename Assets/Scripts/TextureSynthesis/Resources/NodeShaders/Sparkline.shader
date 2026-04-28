Shader "Hidden/Sparkline"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            StructuredBuffer<float> _Samples;
            int   _Head;
            int   _Count;
            int   _Capacity;
            float4 _YRange;        // x=min, y=max
            float4 _LineColor;
            float4 _BgColor;
            float4 _ZeroLineColor;
            float  _LineThickness; // base thickness, in screen pixels
            float  _PixelScale;    // canvas zoom: >1 zoomed out, <1 zoomed in
            float4 _TargetSize;    // x=width, y=height

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Map "ith oldest sample" (i in [0, _Count-1]) to its ring buffer slot.
            int ringSlot(int i)
            {
                return (_Head - _Count + i + _Capacity) % _Capacity;
            }

            float4 frag(v2f i) : SV_Target
            {
                if (_Count < 2) return _BgColor;

                float2 ts = _TargetSize.xy;
                float2 p  = i.uv * ts;
                float yMin = _YRange.x;
                float yMax = _YRange.y;
                float yRange = max(yMax - yMin, 1e-6);

                // Find which segment(s) this pixel x falls in. Sweep three
                // adjacent segments so sharp bends at sample boundaries don't
                // leave gaps.
                float idxF  = i.uv.x * (_Count - 1);
                int   center = (int)floor(idxF);
                int   left   = max(0, center - 1);
                int   right  = min(_Count - 2, center + 1);

                float minDist = 1e10;
                [unroll(3)]
                for (int k = left; k <= right; k++)
                {
                    float v0 = _Samples[ringSlot(k)];
                    float v1 = _Samples[ringSlot(k + 1)];
                    float x0 = (float)k       / (float)(_Count - 1);
                    float x1 = (float)(k + 1) / (float)(_Count - 1);
                    float y0 = (v0 - yMin) / yRange;
                    float y1 = (v1 - yMin) / yRange;
                    float2 a = float2(x0, y0) * ts;
                    float2 b = float2(x1, y1) * ts;
                    float2 pa = p - a, ba = b - a;
                    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
                    float d = length(pa - ba * h);
                    minDist = min(minDist, d);
                }

                // Convert thickness/AA from screen pixels to RT pixels via _PixelScale.
                // Floors keep the line visible/sharp at extreme zooms.
                float halfThickness = max(0.5, _LineThickness * 0.5 * _PixelScale);
                float aaWidth       = max(0.25, _PixelScale);
                float lineAlpha = saturate((halfThickness + aaWidth * 0.5 - minDist) / aaWidth);

                float4 col = _BgColor;

                if (yMin < 0 && yMax > 0)
                {
                    float zeroY = -yMin / yRange;
                    float zeroDist = abs(i.uv.y - zeroY) * ts.y;
                    float zeroAlpha = saturate(1.0 - zeroDist) * 0.4;
                    col = lerp(col, _ZeroLineColor, zeroAlpha);
                }

                col = lerp(col, _LineColor, lineAlpha);
                col.a = 1.0;
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
