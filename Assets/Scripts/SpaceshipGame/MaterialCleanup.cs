using UnityEngine;

public class MaterialCleanup : MonoBehaviour
{
    void OnDestroy()
    {
        foreach(var material in GetComponent<Renderer>().materials)
        {
            Destroy(material);
        }
    }
}
