using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UI;
using System;

namespace sotsf.canopy.patterns
{
    public enum ParamType
    {      
        FLOAT,
        FLOAT4,
        BOOL,
        INT,
        TEX
    }

    [Serializable]
    public class PatternParameter
    {
        public string name;
        public ParamType paramType;
        public bool input;
        public bool useRange;
        public float minFloat;
        public float maxFloat;
        public float minInt;
        public float maxInt;
        public float defaultFloat;
        public int defaultInt;
        public bool defaultBool;
        public Vector4 defaultVector;
        public Texture defaultTexture;
        private float currentFloat;
        private bool floatIsSet = false;
        private int currentInt;
        private bool currentBool;
        private bool boolIsSet = false;
        private Vector4 currentVector;
        private Texture currentTexture;

        public void SetTexture(Texture tex)
        {
            currentTexture = tex;
        }
        public void SetFloat(float fl)
        {
            currentFloat = fl;
            floatIsSet = true;
        }
        public void SetBool(bool val)
        {
            currentBool = val;
            boolIsSet = true;
        }

        public bool GetBool()
        {
            if (boolIsSet)
            {
                return currentBool;
            }
            else
            {
                return defaultBool;
            }
        }

        public Texture GetTexture()
        {
            if (currentTexture != null)
            {
                return currentTexture;
            } else
            {
                return defaultTexture;
            }
        }
        public float GetFloat()
        {
            if (name == "timeSeconds")
            {
                return Time.time;
            }
            if (floatIsSet)
            {
                return currentFloat;
            } else
            {
                return defaultFloat;
            }
        }
    }

    public class Pattern : MonoBehaviour
    {
        public PatternParameter[] parameters = new PatternParameter[] {
            new PatternParameter(){paramType = ParamType.FLOAT, name="Hue", useRange=true, minFloat=0,maxFloat=1},
            new PatternParameter(){paramType = ParamType.FLOAT, name="Saturation", useRange=true, minFloat=0,maxFloat=1},
            new PatternParameter(){paramType = ParamType.FLOAT, name="Brightness", useRange=true, minFloat=0,maxFloat=1},
        };

        public ComputeShader patternShader;
        private FilterChain filterChain;
        protected Material patternMaterial;

        [HideInInspector]
        public RenderTexture patternTexture;
        [HideInInspector]
        public bool presenting;

        protected Dictionary<string, float> renderParams = new Dictionary<string, float>();

        protected PatternManager manager;
        protected ComputeBuffer dataBuffer;
        protected Vector3[] colorData;

        protected byte[] pixelBuffer;

        protected int kernelId;

        private readonly System.Uri pixelEndpoint = new System.Uri("http://localhost:8080/api/renderbytes");

        public void SelectThisPattern()
        {
            manager.SelectPattern(this);
        }

        protected virtual void Start()
        {
            manager = GetComponentInParent<PatternManager>();

            filterChain = GetComponent<FilterChain>();

            if (filterChain != null)
            {
                patternTexture = filterChain.outputTexture;
            }
            else
            {
                patternTexture = new RenderTexture(Constants.PIXELS_PER_STRIP, Constants.NUM_STRIPS, 24);
                patternTexture.enableRandomWrite = true;
                patternTexture.Create();
            }

            RawImage image = GetComponent<RawImage>();

            image.texture = patternTexture;


            kernelId = patternShader.FindKernel("CSMain");
            patternShader.SetTexture(kernelId, "Frame", patternTexture);
            dataBuffer = new ComputeBuffer(Constants.NUM_LEDS, Constants.FLOAT_BYTES * Constants.VEC3_LENGTH);
            colorData = new Vector3[Constants.NUM_LEDS];
            pixelBuffer = new byte[colorData.Length * 3];
            patternShader.SetBuffer(kernelId, "dataBuffer", dataBuffer);
        }

        protected void PresentPattern()
        {
            dataBuffer.GetData(colorData);

            if (manager.pusherConnected && UIController.instance.sendToAPI)
            {
                for (int i = 0; i < colorData.Length*3; i += 3)
                {
                    pixelBuffer[i] = (byte)(colorData[i / 3].x * 255);
                    pixelBuffer[i + 1] = (byte)(colorData[i / 3].y * 255);
                    pixelBuffer[i + 2] = (byte)(colorData[i / 3].z * 255);
                }
                var request = new UnityWebRequest(pixelEndpoint, "POST");
                request.uploadHandler = new UploadHandlerRaw(pixelBuffer);
                request.SendWebRequest();
            }

            if (!manager.highPerformance)
            {
                int count = 0;
                Vector3 avg = Vector3.one;
                for (int i = 0; i < colorData.Length; i++)
                {
                    if (!(float.IsNaN(colorData[i].x) || float.IsNaN(colorData[i].y) || float.IsNaN(colorData[i].z)))
                    {
                        avg += colorData[i];
                        count++;
                    }
                    else
                    {
                        //Note the NaN?
                    }
                }
                avg /= count;
                manager.SetLightColor(new Color(avg.x, avg.y, avg.z));
            }
        }

        // Update is called once per frame
        protected virtual void Update()
        {
            if (!manager.highPerformance || presenting)
            {
                foreach (PatternParameter param in parameters)
                {
                    switch (param.paramType)
                    {
                        case ParamType.BOOL:
                            patternShader.SetBool(param.name, param.GetBool());
                            break;
                        case ParamType.FLOAT:
                            patternShader.SetFloat(param.name, param.GetFloat());
                            break;
                        case ParamType.FLOAT4:
                            // Not implemented
                            break;
                        case ParamType.INT:
                            // Not implemented
                            break;
                        case ParamType.TEX:
                            Texture texValue = param.GetTexture();
                            patternShader.SetInt("height", texValue.height);
                            patternShader.SetInt("width", texValue.width);
                            patternShader.SetTexture(kernelId, param.name, texValue);
                            // Not implemented
                            break;
                        default:
                            // EEK!
                            break;
                    }
                }

                //Execute pattern shader
                //25 and 16 are the thread group sizes, which evenly divide 75 and 96
                patternShader.Dispatch(kernelId, Constants.PIXELS_PER_STRIP / 25, Constants.NUM_STRIPS / 16, 1);
                if (presenting)
                {
                    PresentPattern();
                }
            }

            if (filterChain != null)
            {
                filterChain.Apply(patternTexture);
            }
        }

        private void OnDestroy()
        {
            dataBuffer.Release();
        }
    }
}
