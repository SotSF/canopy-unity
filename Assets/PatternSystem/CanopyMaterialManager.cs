using UnityEngine;
using System.Collections;
using System.Linq;

public class CanopyMaterialManager : MonoBehaviour
{
    
    public ComputeShader pattern;

    public Material canopyMaterial;

    public Light lightCaster;

    private Vector3[] data;

    private int kernelId;

    const int FLOAT_BYTES = 4;
    const int VEC3_LENGTH = 3;

    private ComputeBuffer buff;
    private RenderTexture canopyTex;

    private Texture2D tex;

    // Use this for initialization
    void Start()
    {
        buff = new ComputeBuffer(75*96, FLOAT_BYTES * VEC3_LENGTH);
        canopyTex = new RenderTexture(128, 128, 24);
        canopyTex.enableRandomWrite = true;
        canopyTex.Create();
        kernelId = pattern.FindKernel("CSMain");
        pattern.SetTexture(kernelId, "Result", canopyTex);
        var textureIDs = canopyMaterial.GetTexturePropertyNameIDs();
        Debug.Log("Canopy material texture ids length:" + textureIDs.Length);
        foreach (int index in canopyMaterial.GetTexturePropertyNameIDs())
        {
            canopyMaterial.SetTexture(index, canopyTex);
        }
        data = new Vector3[75*96];
    }

    // Update is called once per frame
    float elapsed = 0;
    void Update()
    {
        buff.SetData(data);
        pattern.SetBuffer(kernelId, "dataBuffer", buff);
        pattern.SetFloat("timeSeconds", Time.time);
        pattern.Dispatch(kernelId, 128/8, 128/8, 1);
        buff.GetData(data);
        int count = 0;
        Vector3 avg = Vector3.zero;
        for (int i = 0; i < data.Length; i++)
        {
            if (!(float.IsNaN(data[i].x) || float.IsNaN(data[i].y) || float.IsNaN(data[i].z)))
            {
                avg += data[i];
                count++;
            }
        }     
        avg /= count;
        Color avgColor = new Color(avg.x, avg.y, avg.z);
        if (lightCaster != null)
        {
            if (elapsed > 2)
            {
                elapsed = 0;
                Debug.Log(avgColor);
                Debug.LogFormat("Sampling of data: {0}, {1}, {2}, {3}", data[0], data[1], data[2], data[3]);
                Debug.LogFormat("Non-NaN count: {0}", count);
            }
            lightCaster.color = Color.Lerp(lightCaster.color, avgColor, 0.25f);
        }
        elapsed += Time.deltaTime;
        //Send render texture to backend!
    }
}
