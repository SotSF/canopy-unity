using UnityEngine;

namespace Lasp
{
    // Precomputed triangular mel filterbank. Applies to a linear-frequency
    // spectrum (one value per FFT bin covering [0, sampleRate/2]) and produces
    // `bandCount` perceptually-spaced bands over [minHz, maxHz].
    internal sealed class MelFilterbank
    {
        private readonly int _bandCount;
        private readonly int[] _binStart;   // inclusive
        private readonly int[] _binEnd;     // exclusive
        private readonly float[] _weights;  // flat [band, bin-offset] layout

        public int BandCount => _bandCount;

        public MelFilterbank(int fftBins, int sampleRate, int bandCount, float minHz, float maxHz)
        {
            _bandCount = bandCount;
            _binStart = new int[bandCount];
            _binEnd   = new int[bandCount];

            float nyquist = sampleRate * 0.5f;
            maxHz = Mathf.Min(maxHz, nyquist);

            float melMin = HzToMel(minHz);
            float melMax = HzToMel(maxHz);

            // bandCount + 2 edges → bandCount triangles sharing edges.
            var edgeBins = new float[bandCount + 2];
            for (int i = 0; i < bandCount + 2; i++)
            {
                float mel = Mathf.Lerp(melMin, melMax, i / (float)(bandCount + 1));
                float hz = MelToHz(mel);
                edgeBins[i] = hz * fftBins * 2f / sampleRate; // bin index (float)
            }

            // Two-pass: find start/end bin ranges per band, then fill weights.
            int totalWeights = 0;
            for (int b = 0; b < bandCount; b++)
            {
                int s = Mathf.FloorToInt(edgeBins[b]);
                int e = Mathf.CeilToInt(edgeBins[b + 2]) + 1;
                s = Mathf.Clamp(s, 0, fftBins);
                e = Mathf.Clamp(e, s, fftBins);
                _binStart[b] = s;
                _binEnd[b]   = e;
                totalWeights += (e - s);
            }

            _weights = new float[totalWeights];
            int w = 0;
            for (int b = 0; b < bandCount; b++)
            {
                float left  = edgeBins[b];
                float peak  = edgeBins[b + 1];
                float right = edgeBins[b + 2];
                for (int j = _binStart[b]; j < _binEnd[b]; j++)
                {
                    float weight;
                    if (j <= peak)
                        weight = (j - left) / Mathf.Max(1e-6f, peak - left);
                    else
                        weight = (right - j) / Mathf.Max(1e-6f, right - peak);
                    _weights[w++] = Mathf.Clamp01(weight);
                }
            }
        }

        // Apply filterbank to `spectrum` (length >= fftBins used at construction).
        // Writes `bandCount` values to `output`.
        public void Apply(System.ReadOnlySpan<float> spectrum, float[] output)
        {
            int w = 0;
            for (int b = 0; b < _bandCount; b++)
            {
                float sum = 0f;
                float wsum = 0f;
                int s = _binStart[b];
                int e = _binEnd[b];
                for (int j = s; j < e; j++)
                {
                    float wj = _weights[w++];
                    sum  += wj * spectrum[j];
                    wsum += wj;
                }
                output[b] = wsum > 0f ? sum / wsum : 0f;
            }
        }

        private static float HzToMel(float hz) => 2595f * Mathf.Log10(1f + hz / 700f);
        private static float MelToHz(float mel) => 700f * (Mathf.Pow(10f, mel / 2595f) - 1f);
    }
}
