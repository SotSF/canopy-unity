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

        /// <summary>Consecutive frames of empty or exactly-zero input while running.
        /// CoreAudio delivers pure digital zeros to a process without mic permission
        /// (and a badly-opened stream delivers nothing), whereas a live device always
        /// carries a noise floor — so a long streak means the stream is effectively
        /// dead even though it reports valid.</summary>
        public int ZeroStreakFrames { get; private set; }
        public bool LooksSilent => ZeroStreakFrames >= 90;

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
            ZeroStreakFrames = 0;
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

        // Re-acquire the InputStream whenever the cached one is missing or invalid,
        // mirroring LASP's own SpectrumAnalyzer/AudioLevelTracker. LASP's device list
        // (and the handles our stream wraps) can be rebuilt out from under us — a
        // rescan, a hot-plug event, or the fast-enter-playmode static reset all do
        // this — which invalidates the cached wrapper without our GameObject changing.
        // Fetching a fresh wrapper for the same device ID recovers transparently.
        InputStream ResolveStream()
        {
            if (_stream != null && _stream.IsValid) return _stream;
            var desc = string.IsNullOrEmpty(DeviceId)
                ? AudioSystem.DefaultDevice
                : AudioSystem.GetInputDevice(DeviceId);
            _stream = desc.IsValid ? AudioSystem.GetInputStream(desc) : null;
            return (_stream != null && _stream.IsValid) ? _stream : null;
        }

        void Update()
        {
            if (!IsRunning) return;
            var stream = ResolveStream();
            if (stream == null)
            {
                // Device gone (e.g. unplugged): keep state so the owning node can
                // surface it and offer a rescan, and recover automatically — via
                // ResolveStream — if the device returns.
                DeviceLost = true;
                return;
            }
            DeviceLost = false;
            // 0 while the stream is failing to open (LASP retry cooldown) — keep the
            // last known rate rather than publishing a transient 0 downstream.
            int sr = stream.SampleRate;
            if (sr > 0) SampleRate = sr;

            // Channel 0 only: a spectrum gains little from a stereo downmix, and the
            // strided slice feeds the FFT without a copy. Accessing the data here is
            // what keeps LASP's on-demand stream open.
            var slice = stream.GetChannelDataSlice(0);
            bool anySignal = false;
            for (int s = 0; s < slice.Length; s++)
            {
                // Exact-zero comparison is intentional: a live device's noise floor is
                // dithered (never exactly 0), while a dead/unauthorized stream is.
                if (slice[s] != 0f) { anySignal = true; break; }
            }
            ZeroStreakFrames = anySignal ? 0 : ZeroStreakFrames + 1;
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
