using UnityEngine;

namespace Lasp
{
    // Captures one selected audio INPUT DEVICE through LASP's runtime API
    // (AudioSystem / InputStream), bypassing the edit-time-bound MonoBehaviour
    // wrappers (AudioLevelTracker / SpectrumAnalyzer), which refuse device changes
    // once their stream opens. LASP streams are on-demand: accessing the sample
    // data every Update is what keeps the device open, and dropping access lets
    // LASP close it — so there is no explicit stream teardown to manage.
    //
    // Lives inside the Lasp.Runtime assembly (via this folder's .asmref) so it can
    // feed LASP's internal FftBuffer, exactly like SystemAudioCapture. Output is
    // scale-compatible with SystemAudioCapture / AudioTrackNode: linear-frequency
    // FFT bins normalized to [0,1] over [floorDb, 0] dBFS, clamped (silence = 0),
    // smoothed per-bin with asymmetric attack/release.
    public class LaspDeviceCapture : MonoBehaviour
    {
        public const int SpectrumResolution = 512;

        public string DeviceId { get; private set; } = "";
        public string DeviceName { get; private set; } = "";
        public int SampleRate { get; private set; }
        public int ChannelCount { get; private set; }
        public bool IsRunning { get; private set; }
        /// <summary>True while bound but the device has vanished (e.g. unplugged).</summary>
        public bool DeviceLost { get; private set; }

        public float AttackTau = 0.04f;
        public float ReleaseTau = 0.25f;
        public float FloorDb = -60f;

        public float[] Spectrum => _spectrum;

        InputStream _stream;
        FftBuffer _fft;
        float[] _spectrum;

        /// <summary>Binds to a device by LASP ID (null/empty = system default input).
        /// Rebinding to a different device at runtime is fine — that's the point.</summary>
        public bool Bind(string deviceId)
        {
            Unbind();
            var desc = string.IsNullOrEmpty(deviceId)
                ? AudioSystem.DefaultDevice
                : AudioSystem.GetInputDevice(deviceId);
            if (!desc.IsValid)
            {
                Debug.LogError($"LaspDeviceCapture: input device '{deviceId}' not found.");
                return false;
            }
            _stream = AudioSystem.GetInputStream(desc);
            if (_stream == null || !_stream.IsValid)
            {
                Debug.LogError($"LaspDeviceCapture: couldn't open a stream on '{desc.Name}'.");
                return false;
            }
            DeviceId = desc.ID;
            DeviceName = desc.Name;
            SampleRate = _stream.SampleRate;
            ChannelCount = _stream.ChannelCount;
            _fft = new FftBuffer(SpectrumResolution * 2);
            _spectrum = new float[SpectrumResolution];
            IsRunning = true;
            DeviceLost = false;
            return true;
        }

        public void Unbind()
        {
            _fft?.Dispose();
            _fft = null;
            _stream = null; // weak wrapper — LASP closes the device once unused
            IsRunning = false;
            DeviceLost = false;
        }

        void OnDestroy()
        {
            Unbind();
        }

        void Update()
        {
            if (!IsRunning || _stream == null) return;
            if (!_stream.IsValid)
            {
                // Unplugged mid-run: keep state so the owning node can surface it and
                // offer a rescan, and recover automatically if the device returns.
                DeviceLost = true;
                return;
            }
            DeviceLost = false;
            SampleRate = _stream.SampleRate;

            // Channel 0 only: a spectrum gains little from a stereo downmix, and the
            // strided slice feeds the FFT without a copy. Accessing the data here is
            // what keeps LASP's on-demand stream open.
            var slice = _stream.GetChannelDataSlice(0);
            if (slice.Length > 0) _fft.Push(slice);
            _fft.Analyze(FloorDb, 0f);

            var src = _fft.Spectrum;
            int n = Mathf.Min(_spectrum.Length, src.Length);
            float dt = Time.deltaTime;
            float aAttack = AttackTau > 1e-4f ? 1f - Mathf.Exp(-dt / AttackTau) : 1f;
            float aRelease = ReleaseTau > 1e-4f ? 1f - Mathf.Exp(-dt / ReleaseTau) : 1f;
            for (int i = 0; i < n; i++)
            {
                // Clamped: LASP's dB mapping is unclamped and silence lands ≈ -3.3
                float target = Mathf.Clamp01(src[i]);
                float prev = _spectrum[i];
                float a = target > prev ? aAttack : aRelease;
                _spectrum[i] = prev + a * (target - prev);
            }
        }
    }
}
