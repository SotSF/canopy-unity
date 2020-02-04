using System;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;


namespace SecretFire.TextureSynth.Signals
{
    [Serializable]
    public struct Oscillator
    {
        public float period;
        public float amplitude;
        public float phase;

        public Oscillator(float period, float amplitude, float phase)
        {
            this.period = period;
            this.amplitude = amplitude;
            this.phase = phase;
        }
    }

    [Node(false, "Signal/PeriodicSignal")]
    public class PeriodicSignalNode : TickingNode
    {
        public const string ID = "periodicSignal";
        public override string GetID { get { return ID; } }

        public override string Title { get { return "PeriodicSignal"; } }
        public override Vector2 DefaultSize { get { return new Vector2(230, 200); } }

        [ValueConnectionKnob("Period", Direction.In, typeof(float))]
        public ValueConnectionKnob periodInputKnob;

        [ValueConnectionKnob("Amplitude", Direction.In, typeof(float))]
        public ValueConnectionKnob amplInputKnob;

        [ValueConnectionKnob("Phase", Direction.In, typeof(float))]
        public ValueConnectionKnob phaseInputKnob;

        [ValueConnectionKnob("Output", Direction.Out, typeof(float))]
        public ValueConnectionKnob outputKnob;

        public Oscillator oscParams;
        public float period = 8;
        public float amplitude = 2;
        public float phase = 0;
        public float max = 2;
        public float min = -2;
        public RadioButtonSet signalType = new RadioButtonSet(0, "sine", "square", "saw", "reverse-saw", "triangle");
        public RadioButtonSet paramStyle = new RadioButtonSet(0, "amplitude", "min max");

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
            FloatKnobOrSlider(ref phase, 0, 2 * Mathf.PI, phaseInputKnob);
            GUILayout.Space(4);
            GUILayout.EndVertical();

            if (GUI.changed)
                NodeEditor.curNodeCanvas.OnNodeChange(this);
        }

        public float CalcSine(float t, float newPeriod, float newAmpl, float newPhase)
        {
            float offset = 0;
            if (paramStyle.SelectedOption() == "min max")
            {
                newAmpl = (max - min) / 2;
                offset = min + newAmpl;
                // Calculate amplitude / offset
            }
            if (newPeriod != oscParams.period ||
                newAmpl != oscParams.amplitude ||
                newPhase != oscParams.phase)
            {
                if (newPeriod != oscParams.period)
                {
                    //Find a phase such that the oscillator won't instantaneously jump in amplitude
                    //If phase and freq change on the same Calculate()... we pick freq.
                    newPhase = 2 * Mathf.PI * t - (newPeriod / oscParams.period) * (2 * Mathf.PI * t - oscParams.phase);
                }
                period = newPeriod;
                amplitude = newAmpl;
                phase = newPhase;
                if (Application.isPlaying)
                {
                    oscParams = new Oscillator(period, amplitude, phase);
                }
            }
            return Mathf.Sin((2 * Mathf.PI * t - phase) / period) * amplitude + offset;
        }

        public float CalcSquare(float t, float newPeriod, float newAmpl, float newPhase)
        {
            float offset = 0;
            if (paramStyle.SelectedOption() == "min max")
            {
                amplitude = (max - min) / 2;
                offset = min + amplitude;
                // Calculate amplitude / offset
            }
            return (t % newPeriod < newPeriod / 2 ? amplitude : -amplitude) + offset;
        }

        public float CalcSaw(float t, float newPeriod, float newAmpl, float newPhase)
        {
            float offset = 0;
            if (paramStyle.SelectedOption() == "min max")
            {
                amplitude = (max - min) / 2;
                offset = min + amplitude;
                // Calculate amplitude / offset
            }
            return 2*amplitude * ((t % newPeriod) / newPeriod - 0.5f) + offset;
        }

        public float CalcRevSaw(float t, float newPeriod, float newAmpl, float newPhase)
        {
            float offset = 0;
            if (paramStyle.SelectedOption() == "min max")
            {
                amplitude = (max - min) / 2;
                offset = min + amplitude;
                // Calculate amplitude / offset
            }
            return 2*amplitude * ((-(t % newPeriod) / newPeriod -0.5f) + 1) + offset ;
        }

        public float CalcTriangle(float t, float newPeriod, float newAmpl, float newPhase)
        {
            float offset = 0;
            if (paramStyle.SelectedOption() == "min max")
            {
                amplitude = (max - min) / 2;
                offset = min + amplitude;
                // Calculate amplitude / offset
            }
            float halfPeriod = newPeriod / 2;
            float quarterPeriod = newPeriod / 4;
            // Offset time to match sin shape
            t -= quarterPeriod;
            return offset + (t % newPeriod < halfPeriod ?
                                2*amplitude * ((   ((t) % halfPeriod) / halfPeriod) - 0.5f) :
                                2*amplitude * ((  -((t) % halfPeriod) / halfPeriod) + 0.5f));
        }

        public override bool Calculate()
        {
            float value = 0;
            float t = Time.time;

            var newPeriod = periodInputKnob.connected() ? periodInputKnob.GetValue<float>() : period;
            var newAmpl   = amplInputKnob.connected()   ? amplInputKnob.GetValue<float>()   : amplitude;
            var newPhase  = phaseInputKnob.connected()  ? phaseInputKnob.GetValue<float>()  : phase;

            switch (signalType.SelectedOption())
            {
                case "sine":
                    value = CalcSine(t, newPeriod, newAmpl, newPhase);
                    break;
                case "square":
                    value = CalcSquare(t, newPeriod, newAmpl, newPhase);
                    break;
                case "saw":
                    value = CalcSaw(t, newPeriod, newAmpl, newPhase);
                    break;
                case "reverse-saw":
                    value = CalcRevSaw(t, newPeriod, newAmpl, newPhase);
                    break;
                case "triangle":
                    value = CalcTriangle(t, newPeriod, newAmpl, newPhase);
                    break;
            }
            outputKnob.SetValue(value);
            return true;
        }
    }
}