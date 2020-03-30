using UnityEngine;
using System.Collections;

using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.DSP;
using CSCore.SoundIn;
using CSCore.Streams;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics;

namespace TexSynth.Audio.WasapiAudio
{

    public enum ScalingStrategy
    {
        Decibel,
        Linear,
        Sqrt
    }

    public enum WasapiCaptureType
    {
        Loopback,
        Microphone
    }

    internal interface ISpectrumProvider
    {
        bool GetFftData(float[] fftBuffer, object context);
        int GetFftBandIndex(float frequency);
    }

    public class SpectrumSmoother
    {
        private long _frameCount;

        private readonly int _spectrumSize;
        private readonly int _smoothingIterations;
        private readonly float[] _smoothedSpectrum;
        private readonly List<float[]> _spectrumHistory = new List<float[]>();

        public SpectrumSmoother(int spectrumSize, int smoothingIterations)
        {
            _spectrumSize = spectrumSize;
            _smoothingIterations = smoothingIterations;

            _smoothedSpectrum = new float[_spectrumSize];

            for (int i = 0; i < _spectrumSize; i++)
            {
                _spectrumHistory.Add(new float[_smoothingIterations]);
            }
        }

        public void AdvanceFrame()
        {
            _frameCount++;
        }

        public float[] GetSpectrumData(float[] spectrum)
        {
            // Record and average last N frames
            for (var i = 0; i < _spectrumSize && i < spectrum.Length; i++)
            {
                var historyIndex = _frameCount % _smoothingIterations;

                var audioData = spectrum[i];
                _spectrumHistory[i][historyIndex] = audioData;

                _smoothedSpectrum[i] = _spectrumHistory[i].Average();
            }

            return _smoothedSpectrum;
        }
    }

    internal abstract class SpectrumBase
    {
        private const int ScaleFactorLinear = 9;
        protected const int ScaleFactorSqr = 2;
        protected const double MinDbValue = -90;
        protected const double MaxDbValue = 0;
        protected const double DbScale = (MaxDbValue - MinDbValue);

        private int _fftSize;
        private bool _isXLogScale;
        private int _maxFftIndex;
        private int _minFrequency;
        private int _minimumFrequencyIndex;
        private int _maxFrequency;
        private int _maximumFrequencyIndex;
        private int[] _spectrumIndexMax;
        private int[] _spectrumLogScaleIndexMax;
        private ISpectrumProvider _spectrumProvider;

        protected int SpectrumResolution;
        private bool _useAverage;

        [Browsable(false)]
        public ISpectrumProvider SpectrumProvider
        {
            get => _spectrumProvider;
            set => _spectrumProvider = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool IsXLogScale
        {
            get => _isXLogScale;
            set
            {
                _isXLogScale = value;
                UpdateFrequencyMapping();
            }
        }

        public ScalingStrategy ScalingStrategy { get; set; }

        public bool UseAverage
        {
            get => _useAverage;
            set => _useAverage = value;
        }

        [Browsable(false)]
        public FftSize FftSize
        {
            get => (FftSize)_fftSize;
            protected set
            {
                if ((int)Math.Log((int)value, 2) % 1 != 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _fftSize = (int)value;
                _maxFftIndex = _fftSize / 2 - 1;
            }
        }

        public SpectrumBase(int minFrequency, int maxFrequency)
        {
            _minFrequency = minFrequency;
            _maxFrequency = maxFrequency;
        }

        protected virtual void UpdateFrequencyMapping()
        {
            _minimumFrequencyIndex = Math.Min(_spectrumProvider.GetFftBandIndex(_minFrequency), _maxFftIndex);
            _maximumFrequencyIndex = Math.Min(_spectrumProvider.GetFftBandIndex(_maxFrequency) + 1, _maxFftIndex);

            int actualResolution = SpectrumResolution;

            int indexCount = _maximumFrequencyIndex - _minimumFrequencyIndex;
            double linearIndexBucketSize = Math.Round(indexCount / (double)actualResolution, 3);

            _spectrumIndexMax = _spectrumIndexMax.CheckBuffer(actualResolution, true);
            _spectrumLogScaleIndexMax = _spectrumLogScaleIndexMax.CheckBuffer(actualResolution, true);

            double maxLog = Math.Log(actualResolution, actualResolution);
            for (int i = 1; i < actualResolution; i++)
            {
                int logIndex =
                    (int)((maxLog - Math.Log((actualResolution + 1) - i, (actualResolution + 1))) * indexCount) +
                    _minimumFrequencyIndex;

                _spectrumIndexMax[i - 1] = _minimumFrequencyIndex + (int)(i * linearIndexBucketSize);
                _spectrumLogScaleIndexMax[i - 1] = logIndex;
            }

            if (actualResolution > 0)
            {
                _spectrumIndexMax[_spectrumIndexMax.Length - 1] =
                    _spectrumLogScaleIndexMax[_spectrumLogScaleIndexMax.Length - 1] = _maximumFrequencyIndex;
            }
        }

        protected virtual SpectrumPointData[] CalculateSpectrumPoints(double maxValue, float[] fftBuffer)
        {
            var dataPoints = new List<SpectrumPointData>();

            double value0 = 0, value = 0;
            double lastValue = 0;
            double actualMaxValue = maxValue;
            int spectrumPointIndex = 0;

            for (int i = _minimumFrequencyIndex; i <= _maximumFrequencyIndex; i++)
            {
                switch (ScalingStrategy)
                {
                    case ScalingStrategy.Decibel:
                        value0 = (((20 * Math.Log10(fftBuffer[i])) - MinDbValue) / DbScale) * actualMaxValue;
                        break;
                    case ScalingStrategy.Linear:
                        value0 = (fftBuffer[i] * ScaleFactorLinear) * actualMaxValue;
                        break;
                    case ScalingStrategy.Sqrt:
                        value0 = ((Math.Sqrt(fftBuffer[i])) * ScaleFactorSqr) * actualMaxValue;
                        break;
                }

                bool recalc = true;

                value = Math.Max(0, Math.Max(value0, value));

                while (spectrumPointIndex <= _spectrumIndexMax.Length - 1 &&
                       i ==
                       (IsXLogScale
                           ? _spectrumLogScaleIndexMax[spectrumPointIndex]
                           : _spectrumIndexMax[spectrumPointIndex]))
                {
                    if (!recalc)
                        value = lastValue;

                    if (value > maxValue)
                        value = maxValue;

                    if (_useAverage && spectrumPointIndex > 0)
                        value = (lastValue + value) / 2.0;

                    dataPoints.Add(new SpectrumPointData { SpectrumPointIndex = spectrumPointIndex, Value = value });

                    lastValue = value;
                    value = 0.0;
                    spectrumPointIndex++;
                    recalc = false;
                }
            }

            return dataPoints.ToArray();
        }

        [DebuggerDisplay("{" + nameof(Value) + "}")]
        protected struct SpectrumPointData
        {
            public int SpectrumPointIndex;
            public double Value;
        }
    }

    public class BasicSpectrumProvider : FftProvider, ISpectrumProvider
    {
        private readonly int _sampleRate;
        private readonly List<object> _contexts = new List<object>();

        public BasicSpectrumProvider(int channels, int sampleRate, FftSize fftSize)
            : base(channels, fftSize)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            _sampleRate = sampleRate;
        }

        public int GetFftBandIndex(float frequency)
        {
            int fftSize = (int)FftSize;
            double f = _sampleRate / 2.0;
            // ReSharper disable once PossibleLossOfFraction
            return (int)((frequency / f) * (fftSize / 2));
        }

        public bool GetFftData(float[] fftResultBuffer, object context)
        {
            if (_contexts.Contains(context))
                return false;

            _contexts.Add(context);
            GetFftData(fftResultBuffer);
            return true;
        }

        public override void Add(float[] samples, int count)
        {
            base.Add(samples, count);

            if (count > 0)
            {
                _contexts.Clear();
            }
        }

        public override void Add(float left, float right)
        {
            base.Add(left, right);
            _contexts.Clear();
        }
    }

    internal class LineSpectrum : SpectrumBase
    {
        public int BarCount
        {
            get => SpectrumResolution;
            set => SpectrumResolution = value;
        }

        public LineSpectrum(FftSize fftSize, int minFrequency, int maxFrequency)
        : base(minFrequency, maxFrequency)
        {
            FftSize = fftSize;
        }

        public float[] GetSpectrumData(double maxValue)
        {
            // Get spectrum data internal
            var fftBuffer = new float[(int)FftSize];

            UpdateFrequencyMapping();

            if (SpectrumProvider.GetFftData(fftBuffer, this))
            {
                SpectrumPointData[] spectrumPoints = CalculateSpectrumPoints(maxValue, fftBuffer);

                // Convert to float[]
                List<float> spectrumData = new List<float>();
                spectrumPoints.ToList().ForEach(point => spectrumData.Add((float)point.Value));
                return spectrumData.ToArray();
            }

            return null;
        }
    }

    public class WasapiAudio
    {
        private const FftSize CFftSize = FftSize.Fft4096;
        private const float MaxAudioValue = 1.0f;

        private readonly WasapiCaptureType _captureType;
        private readonly int _spectrumSize;
        private readonly ScalingStrategy _scalingStrategy;
        private readonly int _minFrequency;
        private readonly int _maxFrequency;

        private WasapiCapture _wasapiCapture;
        private SoundInSource _soundInSource;
        private IWaveSource _realtimeSource;
        private BasicSpectrumProvider _basicSpectrumProvider;
        private LineSpectrum _lineSpectrum;
        private SingleBlockNotificationStream _singleBlockNotificationStream;
        private Action<float[]> _receiveAudio;

        public WasapiAudio(WasapiCaptureType captureType, int spectrumSize, ScalingStrategy scalingStrategy, int minFrequency, int maxFrequency, Action<float[]> receiveAudio)
        {
            _captureType = captureType;
            _spectrumSize = spectrumSize;
            _scalingStrategy = scalingStrategy;
            _minFrequency = minFrequency;
            _maxFrequency = maxFrequency;
            _receiveAudio = receiveAudio;
        }

        public void StartListen()
        {
            switch (_captureType)
            {
                case WasapiCaptureType.Loopback:
                    _wasapiCapture = new WasapiLoopbackCapture();
                    break;
                case WasapiCaptureType.Microphone:
                    MMDevice defaultMicrophone;
                    using (var deviceEnumerator = new MMDeviceEnumerator())
                    {
                        defaultMicrophone = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    }
                    _wasapiCapture = new WasapiCapture();
                    _wasapiCapture.Device = defaultMicrophone;
                    break;
                default:
                    throw new InvalidOperationException("Unhandled WasapiCaptureType");
            }

            _wasapiCapture.Initialize();

            _soundInSource = new SoundInSource(_wasapiCapture);

            _basicSpectrumProvider = new BasicSpectrumProvider(_soundInSource.WaveFormat.Channels, _soundInSource.WaveFormat.SampleRate, CFftSize);

            _lineSpectrum = new LineSpectrum(CFftSize, _minFrequency, _maxFrequency)
            {
                SpectrumProvider = _basicSpectrumProvider,
                BarCount = _spectrumSize,
                UseAverage = true,
                IsXLogScale = true,
                ScalingStrategy = _scalingStrategy
            };

            _wasapiCapture.Start();

            _singleBlockNotificationStream = new SingleBlockNotificationStream(_soundInSource.ToSampleSource());
            _realtimeSource = _singleBlockNotificationStream.ToWaveSource();

            var buffer = new byte[_realtimeSource.WaveFormat.BytesPerSecond / 2];

            _soundInSource.DataAvailable += (s, ea) =>
            {
                while (_realtimeSource.Read(buffer, 0, buffer.Length) > 0)
                {
                    float[] spectrumData = _lineSpectrum.GetSpectrumData(MaxAudioValue);

                    if (spectrumData != null)
                    {
                        _receiveAudio?.Invoke(spectrumData);
                    }
                }
            };

            _singleBlockNotificationStream.SingleBlockRead += SingleBlockNotificationStream_SingleBlockRead;
        }

        public void StopListen()
        {
            _singleBlockNotificationStream.SingleBlockRead -= SingleBlockNotificationStream_SingleBlockRead;

            _soundInSource.Dispose();
            _realtimeSource.Dispose();
            _receiveAudio = null;
            _wasapiCapture.Stop();
            _wasapiCapture.Dispose();
        }

        private void SingleBlockNotificationStream_SingleBlockRead(object sender, SingleBlockReadEventArgs e)
        {
            _basicSpectrumProvider.Add(e.Left, e.Right);
        }
    }

}