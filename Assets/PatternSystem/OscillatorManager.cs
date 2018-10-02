using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using NodeEditorFramework.Standard;

namespace Oscillators
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

    public class OscillatorManager : MonoBehaviour
    {
        public static OscillatorManager instance;


        private ComputeShader oscillatorShader;
        private ComputeBuffer oscillatorParamBuffer;
        private ComputeBuffer oscillatorValueBuffer;

        private Oscillator[] oscillatorParams;
        private float[] oscillatorValues;

        private Dictionary<OscillatorNode, int> indexMap;
        private int kernelId;
        private int count;

        private RTCanvasCalculator calc;

        float lastTick = 0;

        private void Awake()
        {
            instance = this;
            oscillatorShader = Resources.Load<ComputeShader>("Inputs/OscillatorShader");
            indexMap = new Dictionary<OscillatorNode, int>();
            kernelId = oscillatorShader.FindKernel("CSMain");
            oscillatorParams = new Oscillator[0];
            oscillatorValues = new float[0];
            calc = GetComponent<RTCanvasCalculator>();
        }

        void InitializeComputeBuffers()
        {
            if (oscillatorParamBuffer != null)
                oscillatorParamBuffer.Release();
            if (oscillatorValueBuffer != null)
                oscillatorValueBuffer.Release();
            oscillatorParamBuffer = new ComputeBuffer(oscillatorParams.Length, 12);
            oscillatorValueBuffer = new ComputeBuffer(oscillatorValues.Length, 4);
            oscillatorShader.SetBuffer(kernelId, "paramBuffer", oscillatorParamBuffer);
            oscillatorShader.SetBuffer(kernelId, "valueBuffer", oscillatorValueBuffer);
        }

        public void UpdateOscillators(bool force=false)
        {
            if (((Time.time - lastTick > 1.0f / 60) && oscillatorValues.Length > 0) || force)
            {
                lastTick = Time.time;
                oscillatorShader.SetFloat("time", Time.time);
                var threadGroups = Mathf.CeilToInt(oscillatorValues.Length / 32.0f);
                oscillatorShader.Dispatch(kernelId, threadGroups, 1, 1);
                oscillatorValueBuffer.GetData(oscillatorValues);
                foreach (var node in indexMap.Keys)
                {
                    node.ClearCalculation();
                    calc.ContinueCalculation(node);
                }
            }
        }

        // Update is called once per frame
        void Update()
        {
            UpdateOscillators();
        }

        public void Register(OscillatorNode node)
        {
            if (!indexMap.ContainsKey(node))
            {
                indexMap[node] = oscillatorParams.Length;

                var newParams = new Oscillator[oscillatorParams.Length + 1];
                oscillatorParams.CopyTo(newParams,0);
                newParams[newParams.Length-1] = node.oscParams;
                oscillatorParams = newParams;

                var newValues = new float[oscillatorValues.Length + 1];
                oscillatorValues.CopyTo(newValues, 0);
                oscillatorValues = newValues;
                InitializeComputeBuffers();
            } else
            {
                oscillatorParams[indexMap[node]] = node.oscParams;
            }
            oscillatorParamBuffer.SetData(oscillatorParams);
        }

        private void OnDestroy()
        {
            if (oscillatorParamBuffer != null)
                oscillatorParamBuffer.Release();
            if (oscillatorValueBuffer != null)
                oscillatorValueBuffer.Release();
        }

        public  float GetValue(OscillatorNode node)
        {
            if (!indexMap.ContainsKey(node))
            {
                UpdateOscillators(true);
            }
            return oscillatorValues[indexMap[node]];
        }
    }
}

