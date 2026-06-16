using UnityEngine;

public class MaterialCleanup : MonoBehaviour
{
    private Material[] materials;
    void Start()
    {
        materials = GetComponent<Renderer>().materials;
    }

    void OnDestroy()
    {
        foreach(var material in materials)
        {
            Destroy(material);
        }
    }
}
