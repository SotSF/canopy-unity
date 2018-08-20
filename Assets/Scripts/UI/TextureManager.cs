using UnityEngine;
using System.Collections;

public class TextureManager : MonoBehaviour
{
    void Start()
    {
        var textures = Resources.LoadAll<Texture>("Textures");
        foreach (Texture tex in textures)
        {
            if (true)
            {
                
            }
        }
    }
}
