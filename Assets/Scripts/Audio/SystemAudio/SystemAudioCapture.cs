using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Lasp
{
    // Unity C# wrapper for the macOS SystemAudioCapture.bundle native plugin.
    //
    // Drop the built SystemAudioCapture.bundle into Assets/Plugins/macOS/.
    // Lives inside the Lasp.Runtime assembly (via .asmref) so it can feed
    // captured audio directly into Lasp's internal FftBuffer.
    public class SystemAudioCapture : MonoBehaviour
    {
        public static SystemAudioCapture Instance { get; private set; }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private const string DLL = "SystemAudioCapture";

        [DllImport(DLL)] private static extern int  SystemAudioCapture_Init(int sampleRate, int channels);
        [DllImport(DLL)] private static extern void SystemAudioCapture_Stop();
        [DllImport(DLL)] private static extern int  SystemAudioCapture_Read(
            [Out] float[] buffer, int frameCount);
        [DllImport(DLL)] private static extern void SystemAudioCapture_GetFormat(
            out int sampleRate, out int channels);
        [DllImport(DLL)] private static extern int  SystemAudioCapture_AvailableFrames();
#else
        private static int  SystemAudioCapture_Init(int sr, int ch) => -1;
        private static void SystemAudioCapture_Stop() {}
        private static int  SystemAudioCapture_Read(float[] b, int f) => 0;
        private static void SystemAudioCapture_GetFormat(out int sr, out int ch) { sr = 0; ch = 0; }
        private static int  SystemAudioCapture_AvailableFrames() => 0;
#endif

        [SerializeField] private int sampleRate = 48000;
        [SerializeField] private int channels = 2;

        [SerializeField, Range(-120, 0)] private float floorDb = -60;
        [SerializeField, Range(-120, 0)] private float headDb  = 0;
        [SerializeField] private int spectrumResolution = 512;

        [Header("Mel filterbank")]
        [SerializeField] private int melBands = 64;
        [SerializeField] private float melMinHz = 40f;
        [SerializeField] private float melMaxHz = 8000f;

        [Header("Smoothing (seconds)")]
        [SerializeField, Range(0f, 1f)] private float attackTau  = 0.04f;
        [SerializeField, Range(0f, 2f)] private float releaseTau = 0.25f;

        private bool _running;
        public bool IsRunning => _running;

        private FftBuffer _fft;
        private MelFilterbank _mel;
        private float[] _interleaved;
        private NativeArray<float> _mono;
        private float[] _spectrum;
        private float[] _melRaw;

        // Mel-binned spectrum, length == melBands. Values are normalized to
        // [0, 1] against the [floorDb, headDb] range (inherited from the
        // underlying FFT postprocess).
        public float[] Spectrum => _spectrum;

        public bool StartCapture()
        {
            Debug.Log("Beginning system audio capture.");
            if (_running) return true;
            int rc = SystemAudioCapture_Init(sampleRate, channels);
            if (rc != 0)
            {
                Debug.LogError($"[SystemAudioCapture] Init failed: {rc}");
                return false;
            }
            _fft = new FftBuffer(spectrumResolution * 2);
            _mel = new MelFilterbank(spectrumResolution, sampleRate, melBands, melMinHz, melMaxHz);
            _mono = new NativeArray<float>(4096, Allocator.Persistent);
            _interleaved = new float[4096 * Mathf.Max(1, channels)];
            _melRaw = new float[melBands];
            _spectrum = new float[melBands];
            _running = true;
            return true;
        }

        public void StopCapture()
        {
            if (!_running) return;
            SystemAudioCapture_Stop();
            _fft?.Dispose();
            _fft = null;
            if (_mono.IsCreated) _mono.Dispose();
            _running = false;
        }

        public int Read(float[] buffer, int frameCount)
            => _running ? SystemAudioCapture_Read(buffer, frameCount) : 0;

        public int AvailableFrames => _running ? SystemAudioCapture_AvailableFrames() : 0;

        private void OnEnable()  { Instance = this; StartCapture(); }
        private void OnDisable() { StopCapture(); if (Instance == this) Instance = null; }

        private void TimedDebug(string msg, float interval)
        {
            var boundary = Time.time - (Time.time % interval);
            if (Time.time < boundary + Time.deltaTime)
                Debug.Log(msg);
        }

        private void Update()
        {
            if (!_running) return;

            int avail = SystemAudioCapture_AvailableFrames();
            //TimedDebug($"Avail frames: {avail}", 2);
            if (avail <= 0) return;
            int maxFrames = _mono.Length;
            int frames = Mathf.Min(avail, maxFrames);

            int needed = frames * channels;
            if (_interleaved.Length < needed) _interleaved = new float[needed];

            int got = SystemAudioCapture_Read(_interleaved, frames);
            if (got <= 0) return;

            // Downmix interleaved channels to mono.
            if (channels <= 1)
            {
                for (int i = 0; i < got; i++) _mono[i] = _interleaved[i];
            }
            else
            {
                float invCh = 1f / channels;
                for (int i = 0; i < got; i++)
                {
                    float sum = 0f;
                    int baseIdx = i * channels;
                    for (int c = 0; c < channels; c++) sum += _interleaved[baseIdx + c];
                    _mono[i] = sum * invCh;
                }
            }

            _fft.Push(_mono.Slice(0, got));
            _fft.Analyze(floorDb, headDb);

            _mel.Apply(_fft.Spectrum.GetReadOnlySpan(), _melRaw);

            // Per-bin asymmetric exponential smoothing. Fast attack, slow
            // release — behaves like a peak-follower so transients punch
            // while the decay is visually calm.
            float dt = Time.deltaTime;
            float aAttack  = attackTau  > 0f ? 1f - Mathf.Exp(-dt / attackTau)  : 1f;
            float aRelease = releaseTau > 0f ? 1f - Mathf.Exp(-dt / releaseTau) : 1f;
            for (int i = 0; i < _spectrum.Length; i++)
            {
                float target = _melRaw[i];
                float prev   = _spectrum[i];
                float a = target > prev ? aAttack : aRelease;
                _spectrum[i] = prev + a * (target - prev);
            }
        }
    }
}
