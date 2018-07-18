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
    public Material patternMaterial;

    [HideInInspector]
    public RenderTexture patternTexture;
    [HideInInspector]
    public bool presenting;

    private PatternManager manager;

    protected Dictionary<string, float> renderParams = new Dictionary<string, float>();

    int kernelId;

    void Start()
    {
        manager = GetComponentInParent<PatternManager>();

        patternTexture = new RenderTexture(75, 96, 24);
        patternTexture.enableRandomWrite = true;
        patternTexture.Create();

        foreach (string tex in patternMaterial.GetTexturePropertyNames())
        {
            patternMaterial.SetTexture(tex, patternTexture);
        }

        kernelId = patternShader.FindKernel("CSMain");
        patternShader.SetTexture(kernelId, "Frame", patternTexture);
    }

    Color PresentPattern()
    {
        var uri = new System.Uri("http://localhost:8080/api/renderbytes");
        manager.dataBuffer.GetData(manager.colorData);
        //byte[] bytes = new byte[manager.colorData.Length * 3];
        byte[] bytes = new byte[144 * 3];
        for (int i = 0; i < 144*3; i += 3)
        {
            bytes[i] = (byte)(manager.colorData[i / 3].x * 255);
            bytes[i + 1] = (byte)(manager.colorData[i / 3].y * 255);
            bytes[i + 2] = (byte)(manager.colorData[i / 3].z * 255);
        }
        var request = new UnityWebRequest(uri, "POST");
        request.uploadHandler = new UploadHandlerRaw(bytes);
        request.SendWebRequest();
        return Color.white;

        //int count = 0;
        //Vector3 avg = Vector3.zero;
        //for (int i = 0; i < manager.colorData.Length; i++)
        //{
        //    if (!(float.IsNaN(manager.colorData[i].x) || float.IsNaN(manager.colorData[i].y) || float.IsNaN(manager.colorData[i].z)))
        //    {
        //        avg += manager.colorData[i];
        //        count++;
        //    }
        //    else
        //    {
        //        //Note the NaN?
        //    }
        //}
        //avg /= count;
        //return new Color(avg.x, avg.y, avg.z);
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
        patternShader.SetBuffer(kernelId, "dataBuffer", manager.dataBuffer);
        UpdateRenderParams();
        foreach (string param in renderParams.Keys)
        {
            patternShader.SetFloat(param, renderParams[param]);
        }
        //patternShader.SetFloat("timeSeconds", Time.time);
        //patternShader.SetFloat("period", manager.period);
        //patternShader.SetFloat("cycleCount", manager.cycles);
        //patternShader.SetFloat("brightness", manager.brightness + manager.brightnessMod);

        //Execute pattern shader
        patternShader.Dispatch(kernelId, 75 / 8, 96 / 8, 1);
        if (presenting)
        {
            //PresentPattern();
            manager.SetLightColor(PresentPattern());
            //Now send it to the backend!
        }
    }
}
