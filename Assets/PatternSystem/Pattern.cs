using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Collections.Generic;

public class Pattern : MonoBehaviour
{
    public ComputeShader patternShader;

    private Material patternMaterial;

    [HideInInspector]
    public RenderTexture patternTexture;
    [HideInInspector]
    public bool presenting;

    protected Dictionary<string, float> renderParams = new Dictionary<string, float>();

    private PatternManager manager;
    private ComputeBuffer dataBuffer;
    private Vector3[] colorData;

    private byte[] pixelBuffer;

    protected int kernelId;

    const int FLOAT_BYTES = 4;
    const int VEC3_LENGTH = 3;

    private readonly System.Uri pixelEndpoint = new System.Uri("http://localhost:8080/api/renderbytes");

    void Start()
    {
        manager = GetComponentInParent<PatternManager>();

        patternTexture = new RenderTexture(75, 96, 24);
        patternTexture.enableRandomWrite = true;
        patternTexture.Create();

        patternMaterial = new Material(Shader.Find("PatternDisplayShaderGraph"));

        foreach (string tex in patternMaterial.GetTexturePropertyNames())
        {
            patternMaterial.SetTexture(tex, patternTexture);
        }

        GetComponent<MeshRenderer>().sharedMaterial = patternMaterial;

        kernelId = patternShader.FindKernel("CSMain");
        patternShader.SetTexture(kernelId, "Frame", patternTexture);
        dataBuffer = new ComputeBuffer(75 * 96, FLOAT_BYTES * VEC3_LENGTH);
        colorData = new Vector3[75 * 96];
        pixelBuffer = new byte[colorData.Length * 3];
        patternShader.SetBuffer(kernelId, "dataBuffer", dataBuffer);

    }

    private void PresentPattern()
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

    protected virtual void UpdateRenderParams()
    {
        renderParams["timeSeconds"] = Time.time;
        renderParams["period"] = manager.period;
        renderParams["cycleCount"] = manager.cycles;
        renderParams["brightness"] = manager.brightness + manager.brightnessMod;
    }

    // Update is called once per frame
    void Update()
    {
        UpdateRenderParams();
        foreach (string param in renderParams.Keys)
        {
            patternShader.SetFloat(param, renderParams[param]);
        }

        //Execute pattern shader
        patternShader.Dispatch(kernelId, 75 / 8, 96 / 8, 1);
        if (presenting)
        {
             PresentPattern();
        }
    }

    private void OnDestroy()
    {
        dataBuffer.Release();
    }
}
