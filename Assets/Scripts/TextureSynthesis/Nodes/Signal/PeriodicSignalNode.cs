using System;
using System.Collections.Generic;

using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;


namespace SecretFire.TextureSynth.Signals
{
    [Node(false, "Signal/PeriodicSignal")]
    public class PeriodicSignalNode : TickingNode
    {
        public const string ID = "periodicSignal";
        public override string GetID { get { return ID; } }

        public override string Title { get { return "PeriodicSignal"; } }
        public override Vector2 DefaultSize { get { return new Vector2(230, 300); } }

        [ValueConnectionKnob("Period", Direction.In, typeof(float))]
        public ValueConnectionKnob periodInputKnob;

        [ValueConnectionKnob("Amplitude", Direction.In, typeof(float))]
        public ValueConnectionKnob amplInputKnob;

        [ValueConnectionKnob("Phase", Direction.In, typeof(float))]
        public ValueConnectionKnob phaseInputKnob;

        [ValueConnectionKnob("q", Direction.In, typeof(float))]
        public ValueConnectionKnob qInputKnob;

        [ValueConnectionKnob("Output", Direction.Out, typeof(float))]
        public ValueConnectionKnob outputKnob;

        public float period = 8;
        public float amplitude = 2;
        public float phase = 0;
        public float max = 2;
        public float min = -2;
        public RadioButtonSet signalType = new RadioButtonSet(0, "sine", "square", "saw", "reverse-saw", "triangle", "expspike", "hemi");
        public RadioButtonSet paramStyle = new RadioButtonSet(0, "amplitude", "min max");

        private float lastPeriod = 8;
        private float lastPhase = 0;
        private float lastAmplitude = 2;

        public delegate float SignalFunc(float x, float p, float a, float t, float q);
        private static Dictionary<string, SignalFunc> signalGenerators = new Dictionary<string, SignalFunc>();

        public void Awake()
        {
            signalGenerators["sine"] = CalcSine;
            signalGenerators["square"] = CalcSquare;
            signalGenerators["saw"] = CalcSaw;
            signalGenerators["reverse-saw"] = CalcRevSaw;
            signalGenerators["triangle"] = CalcTriangle;
            signalGenerators["expspike"] = CalcExpSpike;
            signalGenerators["hemi"] = CalcHemisphere;
            //signalGenerators.
        }

        public override void NodeGUI()
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            RadioButtonsVertical(signalType);

            GUILayout.BeginVertical();
            GUILayout.Label("Param config:");
            RadioButtons(paramStyle);
            outputKnob.DisplayLayout();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            if (paramStyle.SelectedOption() == "amplitude")
            {
                amplInputKnob.DisplayLayout();
                if (!amplInputKnob.connected())
                {
                    amplitude = RTEditorGUI.FloatField(amplitude);
                }
            } else if (paramStyle.SelectedOption() == "min max")
            {
                min = RTEditorGUI.FloatField("Min", min);
                max = RTEditorGUI.FloatField("Max", max);
            }
            FloatKnobOrSlider(ref period, 0.01f, 50, periodInputKnob);
            FloatKnobOrSlider(ref phase, -period, period, phaseInputKnob);
            GUILayout.Space(4);
            GUILayout.EndVertical();

            if (GUI.changed)
                NodeEditor.curNodeCanvas.OnNodeChange(this);
        }

        /* Parameters:
         * x: Time
         * p: Period
         * a: Amplitude
         * t: theta (phase)
         * */
        public static float CalcSine(float x, float p, float a, float t, float q = 0)
        {
            return Mathf.Sin((2 * Mathf.PI * (x - t)) / p) * a ;
        }

        public static float CalcSquare(float x, float p, float a, float t, float q = 0)
        {
            return ((x-t) % p < p / 2 ? a : -a);
        }

        public static float CalcSaw(float x, float p, float a, float t, float q = 0)
        {
            return 2* a * (((x-t) % p) / p - 0.5f);
        }

        public static float CalcRevSaw(float x, float p, float a, float t, float q = 0)
        {
            return 2* a * ((-((x-t) % p) / p -0.5f) + 1) ;
        }

        public static float CalcExpSpike(float x, float p, float a, float t, float q = 2)
        {
            // (x ^ (t%1) - 1) / (x-1)
            // x = 2^q for q in [0, 32] to control spikiness
            var b = Mathf.Pow(2, q);
            return (Mathf.Pow(b, x % 1) - 1) / (b - 1);
        }

        public static float CalcHemisphere(float x, float p, float a, float t, float q = 0)
        {
            // - root(1- (t%1)^2)+1
            return 0;
        }

        public static float CalcTriangle(float x, float p, float a, float t, float q = 0)
        {

            float halfPeriod = p / 2;
            float quarterPeriod = p / 4;
            // Offset time to match sin shape
            x -= quarterPeriod;
            return ((x-t) % p < halfPeriod ?
                                2* a * ((   ((x-t) % halfPeriod) / halfPeriod) - 0.5f) :
                                2* a * ((  -((x-t) % halfPeriod) / halfPeriod) + 0.5f));
        }




        float offset;
        public override bool Calculate()
        {
            float value = 0;
            float t = Time.time;

            amplitude = amplInputKnob.connected()  ? amplInputKnob.GetValue<float>()   : amplitude;
            period = periodInputKnob.connected()   ? periodInputKnob.GetValue<float>() : period;
            phase  = phaseInputKnob.connected()    ? phaseInputKnob.GetValue<float>()  : phase;

            offset = 0;
            if (paramStyle.SelectedOption() == "min max")
            {
                amplitude = (max - min) / 2;
                offset = min + amplitude;
            }

            var newParams = (period, amplitude, phase);
            var oldParams = (lastPeriod, lastAmplitude, lastPhase);
            if (newParams != oldParams)
            {
                if (period != lastPeriod)
                {
                    phase = (t - period / lastPeriod * (t - lastPhase)) % period;
                }
            }

            value = signalGenerators[signalType.SelectedOption()](t, period, amplitude, phase, 0);
            outputKnob.SetValue(value + offset);
            lastPeriod = period;
            lastPhase = phase;
            lastAmplitude = amplitude;
            return true;
        }
    }
}