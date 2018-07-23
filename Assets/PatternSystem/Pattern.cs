using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UI;

public class Pattern : MonoBehaviour
{
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

    protected const int FLOAT_BYTES = 4;
    protected const int VEC3_LENGTH = 3;

    private readonly System.Uri pixelEndpoint = new System.Uri("http://localhost:8080/api/renderbytes");

    public void SelectThisPattern()
    {
        manager.SelectPattern(this);
    }

    protected virtual void Start()
    {
        manager = GetComponentInParent<PatternManager>();

        patternTexture = new RenderTexture(Constants.PIXELS_PER_STRIP, Constants.NUM_STRIPS, 24);
        patternTexture.enableRandomWrite = true;
        patternTexture.Create();

        RawImage image = GetComponent<RawImage>();
        image.texture = patternTexture;

        kernelId = patternShader.FindKernel("CSMain");
        patternShader.SetTexture(kernelId, "Frame", patternTexture);
        dataBuffer = new ComputeBuffer(Constants.NUM_LEDS, FLOAT_BYTES * VEC3_LENGTH);
        colorData = new Vector3[Constants.NUM_LEDS];
        pixelBuffer = new byte[colorData.Length * 3];
        patternShader.SetBuffer(kernelId, "dataBuffer", dataBuffer);

    }

    protected void PresentPattern()
    {
        dataBuffer.GetData(colorData);

        if (manager.pusherConnected)
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

    protected virtual void UpdateRenderParams()
    {
        renderParams["timeSeconds"] = Time.time;
        renderParams["period"] = manager.period;
        renderParams["cycleCount"] = manager.cycles;
        renderParams["brightness"] = manager.brightness + manager.brightnessMod;
        renderParams["hue"] = manager.hue;
        renderParams["saturation"] = manager.saturation;
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        if (!manager.highPerformance || presenting)
        {
            UpdateRenderParams();
            foreach (string param in renderParams.Keys)
            {
                patternShader.SetFloat(param, renderParams[param]);
            }

            //Execute pattern shader
            int groupx_size = Constants.PIXELS_PER_STRIP + (8 - (Constants.PIXELS_PER_STRIP % 8));
            int groupy_size = Constants.NUM_STRIPS + (8 - (Constants.NUM_STRIPS % 8));
            patternShader.Dispatch(kernelId, groupx_size / 8, groupy_size / 8, 1);
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
