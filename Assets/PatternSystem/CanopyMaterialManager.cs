using UnityEngine;
using System.Collections;
using System.Linq;

public class CanopyMaterialManager : MonoBehaviour
{
    
    public ComputeShader pattern;

    public Material canopyMaterial;

    public Light light;

    private Vector3[] data;

    private Vector3 baseData = new Vector3(.5f, .5f, .5f);
    private int kernelId;

    const int FLOAT_BYTES = 4;
    const int VEC3_LENGTH = 3;

    private ComputeBuffer buff;
    private RenderTexture canopyTex;

    private Texture2D tex;

    // Use this for initialization
    void Start()
    {
        buff = new ComputeBuffer(1, FLOAT_BYTES * VEC3_LENGTH);
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
            avg += data[i];
            if (data[i] != Vector3.zero)
            {
                count++;
            }
        }
            
        avg /= count;
        Color avgColor = new Color(avg.x, avg.y, avg.z);
        if (light != null)
        {
            if (elapsed > 2)
            {
                elapsed = 0;
                Debug.Log(avgColor);
                Debug.LogFormat("Sampling of data: {0}, {1}, {2}, {3}", data[0], data[1], data[2], data[3]);
                Debug.LogFormat("Count of nonzero values: {0}", count);
            }
            light.color = Color.Lerp(light.color, avgColor, 0.25f);
        }
        elapsed += Time.deltaTime;
        //Send render texture to backend!
    }
}
