using UnityEngine;
using System.Collections;

public class Pattern : MonoBehaviour
{
    public ComputeShader patternShader;
    public Material patternMaterial;

    [HideInInspector]
    public RenderTexture patternTexture;
    [HideInInspector]
    public bool presenting;

    private PatternManager manager;

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

    Color AverageColor()
    {
        manager.dataBuffer.GetData(manager.colorData);
        //Average result colors for setting light property

        int count = 0;
        Vector3 avg = Vector3.zero;
        for (int i = 0; i < manager.colorData.Length; i++)
        {
            if (!(float.IsNaN(manager.colorData[i].x) || float.IsNaN(manager.colorData[i].y) || float.IsNaN(manager.colorData[i].z)))
            {
                avg += manager.colorData[i];
                count++;
            }
            else
            {
                //Note the NaN?
            }
        }
        avg /= count;
        return new Color(avg.x, avg.y, avg.z);
        
    }

    // Update is called once per frame
    void Update()
    {
        patternShader.SetBuffer(kernelId, "dataBuffer", manager.dataBuffer);

        patternShader.SetFloat("timeSeconds", Time.time);
        patternShader.SetFloat("period", manager.period);
        patternShader.SetFloat("cycleCount", manager.cycles);

        //Execute pattern shader
        patternShader.Dispatch(kernelId, 75 / 8, 96 / 8, 1);
        if (presenting)
        {
            manager.SetLightColor(AverageColor());
            //Now send it to the backend!
        }
    }
}
